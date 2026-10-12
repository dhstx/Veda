namespace Veda.Core;

public sealed record MixerSnapshot(long RenderedFrames, GameState ActiveState, GameState RequestedState,
    bool Transitioning, float PeakBeforeClamp, long ClampedSamples);

/// <summary>
/// One rendering thread owns the clock and all gain envelopes. Producers replace one pending plan.
/// Render performs no file IO, inference, locks, or per-block managed allocations.
/// </summary>
public sealed class AdaptiveMixer : IAudioRenderer
{
    private readonly LoadedSoundtrack _soundtrack;
    private readonly float[] _current, _from, _to;
    private MixPlan? _pending, _scheduled;
    private long _frame, _scheduledFrame, _fadeStart, _fadeLength;
    private bool _fading;
    private float _master, _targetMaster = 0.5f;
    private long _publishedFrames, _clamped;
    private int _activeState, _requestedState, _transitioning;
    private float _peak;

    public int SampleRate => _soundtrack.Definition.SampleRate;
    public int Channels => _soundtrack.Definition.Channels;

    public AdaptiveMixer(LoadedSoundtrack soundtrack)
    {
        _soundtrack = soundtrack;
        _current = new float[soundtrack.Layers.Count];
        _from = new float[_current.Length]; _to = new float[_current.Length];
        Submit(new MusicDirector(soundtrack).Plan(GameState.CalmSpace) with { Boundary = TransitionBoundary.Immediate });
    }

    public void SetVolume(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentException("Volume must be finite.");
        Volatile.Write(ref _targetMaster, Math.Clamp(value, 0, 1));
    }

    public void Submit(MixPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(plan.State) || !Enum.IsDefined(plan.Boundary) || !double.IsFinite(plan.FadeBeats) ||
            plan.FadeBeats <= 0 || plan.FadeBeats > 128 || plan.Gains is null || plan.Gains.Count != _current.Length)
            throw new ArgumentException("Invalid mix plan.");
        foreach (var layer in _soundtrack.Layers)
            if (!plan.Gains.TryGetValue(layer.Id, out var gain) || !float.IsFinite(gain) || gain < 0 || gain > 1)
                throw new ArgumentException("Invalid layer gain.");
        Volatile.Write(ref _requestedState, (int)plan.State);
        Interlocked.Exchange(ref _pending, plan);
    }

    public int Render(Span<float> destination)
    {
        if (destination.Length % Channels != 0) throw new ArgumentException("Render complete audio frames.");
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is not null)
        {
            _scheduled = pending;
            var grid = SampleRate * 60d / _soundtrack.Definition.TempoBpm;
            if (pending.Boundary == TransitionBoundary.NextBar) grid *= _soundtrack.Definition.BeatsPerBar;
            _scheduledFrame = pending.Boundary == TransitionBoundary.Immediate ? _frame
                : (long)Math.Round(Math.Ceiling(_frame / grid) * grid, MidpointRounding.AwayFromZero);
        }
        var targetVolume = Volatile.Read(ref _targetMaster);
        var volumeStep = 1f / (SampleRate * 0.05f);
        float peak = 0;
        long clamped = 0;
        for (var offset = 0; offset < destination.Length; offset += Channels, _frame++)
        {
            if (_scheduled is not null && _frame >= _scheduledFrame)
            {
                Array.Copy(_current, _from, _current.Length);
                for (var i = 0; i < _to.Length; i++) _to[i] = _scheduled.Gains[_soundtrack.Layers[i].Id];
                _fadeStart = _frame;
                _fadeLength = Math.Max(1, (long)Math.Round(_scheduled.FadeBeats * SampleRate * 60d / _soundtrack.Definition.TempoBpm));
                Volatile.Write(ref _activeState, (int)_scheduled.State);
                _scheduled = null; _fading = true;
            }
            if (_fading)
            {
                var t = Math.Clamp((_frame - _fadeStart) / (double)_fadeLength, 0, 1);
                var blend = (float)(0.5 - 0.5 * Math.Cos(Math.PI * t));
                for (var i = 0; i < _current.Length; i++) _current[i] = _from[i] + (_to[i] - _from[i]) * blend;
                if (t >= 1) _fading = false;
            }
            _master += Math.Clamp(targetVolume - _master, -volumeStep, volumeStep);
            var sampleIndex = (int)(_frame % _soundtrack.FrameCount) * Channels;
            for (var channel = 0; channel < Channels; channel++)
            {
                float mixed = 0;
                for (var i = 0; i < _current.Length; i++)
                    mixed += _soundtrack.Layers[i].Samples[sampleIndex + channel] * _current[i];
                mixed *= _master;
                peak = Math.Max(peak, Math.Abs(mixed));
                if (Math.Abs(mixed) > 0.98f) clamped++;
                destination[offset + channel] = Math.Clamp(mixed, -0.98f, 0.98f);
            }
        }
        Volatile.Write(ref _publishedFrames, _frame);
        Volatile.Write(ref _transitioning, _fading || _scheduled is not null || Volatile.Read(ref _pending) is not null ? 1 : 0);
        Volatile.Write(ref _peak, peak);
        Interlocked.Add(ref _clamped, clamped);
        return destination.Length;
    }

    public MixerSnapshot Snapshot() => new(Volatile.Read(ref _publishedFrames),
        (GameState)Volatile.Read(ref _activeState), (GameState)Volatile.Read(ref _requestedState),
        Volatile.Read(ref _transitioning) != 0, Volatile.Read(ref _peak), Interlocked.Read(ref _clamped));
}
