namespace Veda.Core;

public sealed class PreparedTrack
{
    public LibraryTrack Info { get; }
    internal float[] Samples { get; }
    private PreparedTrack(LibraryTrack info, float[] samples) { Info = info; Samples = samples; }
    public static PreparedTrack Load(LibraryTrack track)
    {
        var decoded = WavFile.Read(track.Path, 48000 * TrackLibraryLoader.MaximumTrackSeconds);
        if (Math.Abs(decoded.Samples.Length / 96000d - track.DurationSeconds) > 1d / 48000)
            throw new InvalidDataException("The audio length changed after import. Load the library again.");
        return new(track, decoded.Samples);
    }
}
public enum TrackCommandKind { Play, Silence, CancelScheduled }
public sealed record TrackPlaybackCommand(TrackCommandKind Kind, PreparedTrack? Track = null,
    double EntrySeconds = 0, double FadeSeconds = 4, double DelaySeconds = 0, bool FinishAtFileEnd = false);
public sealed record FullTrackSnapshot(long Frames, LibraryTrack? Track, double PositionSeconds,
    double RemainingSeconds, bool Transitioning, bool Scheduled, long Starts, long ClampedSamples);

/// <summary>Two preloaded voices, one render owner, bounded latest-wins commands.
/// Requests during a fade preserve both voices. Urgent requests shorten that fade smoothly.</summary>
public sealed class FullTrackPlayer : IAudioRenderer
{
    private PreparedTrack? _current, _outgoing;
    private int _position, _outPosition, _end, _outEnd;
    private double _currentGain = 1, _outGain, _fadeFrom, _incomingFrom;
    private long _frames, _fadeStart, _fadeLength, _scheduledAt, _starts, _clamped;
    private bool _fading;
    private TrackPlaybackCommand? _pending, _scheduled;
    private float _volume, _targetVolume = .5f;
    private LibraryTrack? _publishedTrack;
    private long _publishedFrames, _publishedStarts;
    private int _publishedPosition, _publishedRemaining, _publishedFading, _publishedScheduled;
    public int SampleRate => 48000;
    public int Channels => 2;
    public void SetVolume(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentException("Volume must be finite.");
        Volatile.Write(ref _targetVolume, Math.Clamp(value, 0, 1));
    }
    public void Submit(TrackPlaybackCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Kind) || !double.IsFinite(command.EntrySeconds) || command.EntrySeconds < 0 ||
            !double.IsFinite(command.FadeSeconds) || command.FadeSeconds < 0 || command.FadeSeconds > 30 ||
            !double.IsFinite(command.DelaySeconds) || command.DelaySeconds < 0 || command.DelaySeconds > 600)
            throw new ArgumentException("Invalid playback command.");
        if (command.Kind == TrackCommandKind.Play)
        {
            var track = command.Track ?? throw new ArgumentException("Play requires prepared audio.");
            if (track.Samples.Length == 0 || track.Samples.Length % 2 != 0 ||
                !float.IsFinite(track.Info.Gain) || track.Info.Gain <= 0 || track.Info.Gain > 1 ||
                !double.IsFinite(track.Info.EndSeconds) || track.Info.EndSeconds <= 0 ||
                track.Info.EndSeconds * SampleRate > track.Samples.Length / 2d + 1 ||
                command.EntrySeconds >= track.Info.EndSeconds - .05)
                throw new ArgumentException("Invalid prepared audio or entry/end cue.");
        }
        Interlocked.Exchange(ref _pending, command);
    }
    public int Render(Span<float> destination)
    {
        if (destination.Length % 2 != 0) throw new ArgumentException("Render complete stereo frames.");
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is not null)
        {
            if (pending.FinishAtFileEnd && _current is not null) _end = _current.Samples.Length / 2;
            _scheduled = pending.Kind == TrackCommandKind.CancelScheduled ? null : pending;
            _scheduledAt = _frames + (long)Math.Round(pending.DelaySeconds * SampleRate);
            if (_fading && pending.Kind == TrackCommandKind.Play && pending.DelaySeconds == 0 && pending.FadeSeconds <= 2)
            {
                _fadeFrom = _outGain; _incomingFrom = _currentGain;
                _fadeLength = Math.Max(1, Math.Min(_fadeLength - (_frames - _fadeStart), SampleRate * 15 / 100));
                _fadeStart = _frames;
            }
        }
        var volumeTarget = Volatile.Read(ref _targetVolume);
        var volumeStep = 1f / (SampleRate * .05f);
        long clamped = 0;
        for (var offset = 0; offset < destination.Length; offset += 2, _frames++)
        {
            if (_fading)
            {
                var t = Math.Clamp((_frames - _fadeStart) / (double)_fadeLength, 0, 1);
                var blend = .5 - .5 * Math.Cos(Math.PI * t);
                _outGain = _fadeFrom * (1 - blend);
                _currentGain = _incomingFrom + (1 - _incomingFrom) * blend;
                if (t >= 1) { _fading = false; _outgoing = null; _outGain = 0; }
            }
            if (_scheduled is not null && _frames >= _scheduledAt && !_fading)
            {
                _outgoing = _current; _outPosition = _position; _outEnd = _end;
                _fadeFrom = _current is null ? 0 : _currentGain;
                _current = _scheduled.Kind == TrackCommandKind.Play ? _scheduled.Track : null;
                _position = (int)Math.Round(_scheduled.EntrySeconds * SampleRate);
                _end = _current is null ? 0 : (int)Math.Min(_current.Samples.Length / 2,
                    Math.Round(_current.Info.EndSeconds * SampleRate));
                _incomingFrom = 0; _currentGain = _scheduled.FadeSeconds == 0 ? 1 : 0; _outGain = _fadeFrom;
                _fadeStart = _frames; _fadeLength = Math.Max(1, (long)Math.Round(_scheduled.FadeSeconds * SampleRate));
                _fading = _scheduled.FadeSeconds > 0;
                if (!_fading) { _outgoing = null; _outGain = 0; }
                if (_current is not null) _starts++;
                _scheduled = null;
            }
            _volume += Math.Clamp(volumeTarget - _volume, -volumeStep, volumeStep);
            for (var channel = 0; channel < 2; channel++)
            {
                double sample = 0;
                if (_current is not null && _position < _end)
                    sample += _current.Samples[_position * 2 + channel] * _currentGain * _current.Info.Gain * EndEnvelope(_position, _end);
                if (_outgoing is not null && _outPosition < _outEnd)
                    sample += _outgoing.Samples[_outPosition * 2 + channel] * _outGain * _outgoing.Info.Gain * EndEnvelope(_outPosition, _outEnd);
                sample *= _volume;
                if (Math.Abs(sample) > .98) clamped++;
                destination[offset + channel] = (float)Math.Clamp(sample, -.98, .98);
            }
            if (_current is not null && ++_position >= _end) _current = null;
            if (_outgoing is not null && ++_outPosition >= _outEnd) _outgoing = null;
        }
        Volatile.Write(ref _publishedFrames, _frames);
        Volatile.Write(ref _publishedTrack, _current?.Info);
        Volatile.Write(ref _publishedPosition, _position);
        Volatile.Write(ref _publishedRemaining, Math.Max(0, _end - _position));
        Volatile.Write(ref _publishedFading, _fading ? 1 : 0);
        Volatile.Write(ref _publishedScheduled, _scheduled is not null ? 1 : 0);
        Volatile.Write(ref _publishedStarts, _starts);
        Interlocked.Add(ref _clamped, clamped);
        return destination.Length;
    }
    // Safety envelope also protects EOF when a background preload is late.
    private static double EndEnvelope(int position, int end) => Math.Min(1, Math.Max(0, (end - position - 1) / 2400d));
    public FullTrackSnapshot Snapshot() => new(Volatile.Read(ref _publishedFrames), Volatile.Read(ref _publishedTrack),
        Volatile.Read(ref _publishedPosition) / (double)SampleRate,
        Volatile.Read(ref _publishedRemaining) / (double)SampleRate,
        Volatile.Read(ref _publishedFading) != 0,
        Volatile.Read(ref _publishedScheduled) != 0 || Volatile.Read(ref _pending) is not null,
        Volatile.Read(ref _publishedStarts), Interlocked.Read(ref _clamped));
}
