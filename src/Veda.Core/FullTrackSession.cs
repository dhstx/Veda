namespace Veda.Core;

/// <summary>One control thread owns this session (WinUI dispatcher or offline driver).
/// Loading runs in the background; no library audio is read by the render callback.</summary>
public sealed class FullTrackSession : IDisposable
{
    private readonly TrackSelector _selector = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly Func<LibraryTrack, Task<PreparedTrack>> _prepare;
    private int _revision;
    private bool _loading, _disposed, _quietScheduled;
    private long? _quietUntil;
    private long _seenStarts;
    private long _retryAfterFrame;
    public TrackLibrary Library { get; private set; }
    public FullTrackPlayer Player { get; } = new();
    public string Genre { get; private set; }
    public string SituationId { get; private set; } = GameState.CalmSpace.ToString();
    // Compatibility for the original six-state manual prototype.
    public GameState State => Enum.TryParse<GameState>(SituationId, out var state) ? state : GameState.CalmSpace;
    private double? _crossfadeSeconds;
    public double CrossfadeSeconds
    {
        get => _crossfadeSeconds ?? 8;
        set
        {
            if (!double.IsFinite(value) || value is < 0 or > 20) throw new ArgumentException("Crossfade must be 0–20 seconds.");
            if (_crossfadeSeconds == value) return;
            _crossfadeSeconds = value; ++_revision; _loading = false; _quietScheduled = false;
            Player.Submit(new(TrackCommandKind.CancelScheduled, FinishAtFileEnd: value == 0));
        }
    }
    public TrackEntry Entry { get; set; } = TrackEntry.Automatic;
    public bool AllowQuietBreaks { get; set; } = true;
    private double _quietSeconds = 20;
    public double QuietSeconds
    {
        get => _quietSeconds;
        set
        {
            if (!double.IsFinite(value) || value < 0 || value > 120) throw new ArgumentException("Quiet duration must be between zero and 120 seconds.");
            _quietSeconds = value;
        }
    }
    public string Status { get; private set; } = "Select a situation, then press Play.";

    public FullTrackSession(TrackLibrary library, Func<LibraryTrack, Task<PreparedTrack>>? prepare = null)
    {
        Library = library;
        Genre = library.Genres[0];
        _prepare = prepare ?? (track => Task.Run(() => PreparedTrack.Load(track), _lifetime.Token));
        var first = library.Tracks.FirstOrDefault(t => t.Enabled && t.Genre == Genre);
        if (!HasSituation(SituationId) && first is not null) SituationId = first.SituationId;
    }
    public bool HasState(GameState state) => HasSituation(state.ToString());
    public bool HasSituation(string id) => Library.For(Genre, id).Length > 0;
    public async Task UpdateLibraryAsync(TrackLibrary library)
    {
        Library = library;
        if (!library.Genres.Contains(Genre, StringComparer.OrdinalIgnoreCase)) Genre = library.Genres[0];
        var current = Player.Snapshot().Track;
        if (current is not null && library.For(Genre, SituationId).Any(t => t.Id == current.Id) ||
            HasSituation(SituationId) && (_quietUntil is not null || _quietScheduled))
        { ++_revision; _loading = false; _quietScheduled = false; Player.Submit(new(TrackCommandKind.CancelScheduled)); return; }
        await SelectAvailableAsync();
    }
    private async Task SelectAvailableAsync()
    {
        if (!HasSituation(SituationId))
        {
            var first = Library.Tracks.FirstOrDefault(t => t.Enabled && t.Genre.Equals(Genre, StringComparison.OrdinalIgnoreCase));
            if (first is not null) SituationId = first.SituationId;
        }
        if (HasSituation(SituationId)) await RequestSituationAsync(SituationId, true);
        else
        {
            ++_revision; _loading = false; _quietScheduled = false; _quietUntil = null;
            Player.Submit(new(TrackCommandKind.Silence, FadeSeconds: 1));
            Status = "No enabled songs in this genre. Add songs or enable a music source.";
        }
    }
    public async Task SetGenreAsync(string genre)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Library.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Unknown genre.");
        if (string.Equals(Genre, genre, StringComparison.OrdinalIgnoreCase)) return;
        Genre = Library.Genres.First(g => g.Equals(genre, StringComparison.OrdinalIgnoreCase));
        await SelectAvailableAsync();
    }
    public Task RequestAsync(GameState state, bool force = false)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        return RequestSituationAsync(state.ToString(), force);
    }
    public async Task RequestSituationAsync(string situationId, bool force = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!HasSituation(situationId)) { Status = $"No {situationId} tracks in {Genre}. Current music is preserved."; return; }
        var same = SituationId == situationId;
        SituationId = situationId;
        if (same && !force && (Player.Snapshot().Track is not null || _loading || Player.Snapshot().Scheduled)) return;
        if (same && !force && (_quietScheduled || _quietUntil is not null || Player.Snapshot().Transitioning)) return;
        var revision = ++_revision;
        _retryAfterFrame = 0;
        _quietUntil = null; _quietScheduled = false;
        Player.Submit(new(TrackCommandKind.CancelScheduled));
        await PrepareAsync(revision, automatic: false);
    }
    public async Task TickAsync()
    {
        if (_disposed) return;
        var snapshot = Player.Snapshot();
        if (snapshot.Starts > _seenStarts)
        {
            _seenStarts = snapshot.Starts;
            if (snapshot.Track is not null) _selector.MarkPlayed(snapshot.Track);
        }
        if (!HasSituation(SituationId)) return;
        if (_loading || snapshot.Scheduled || snapshot.Transitioning || snapshot.Frames < _retryAfterFrame) return;
        if (snapshot.Track is null)
        {
            if (_quietScheduled)
            {
                _quietScheduled = false;
                _quietUntil = snapshot.Frames + (long)(Math.Clamp(QuietSeconds, 0, 120) * Player.SampleRate);
                Status = "Intentional quiet break. A different situation or Next track can interrupt it.";
            }
            if (_quietUntil is not null && snapshot.Frames < _quietUntil.Value && AllowQuietBreaks) return;
            _quietUntil = null;
            await PrepareAsync(++_revision, automatic: true);
            return;
        }
        if (snapshot.RemainingSeconds > Math.Max(20, CrossfadeSeconds + 5)) return;
        if (AllowQuietBreaks && SituationId is "CalmSpace" or "Relief")
        {
            _quietScheduled = true;
            var fade = Math.Min(CrossfadeSeconds, snapshot.RemainingSeconds);
            var remaining = CrossfadeSeconds == 0 ? snapshot.Track.DurationSeconds - snapshot.PositionSeconds : snapshot.RemainingSeconds;
            Player.Submit(new(TrackCommandKind.Silence, FadeSeconds: fade, DelaySeconds: Math.Max(0, remaining - fade), FinishAtFileEnd: CrossfadeSeconds == 0));
            Status = "Finishing this track, then leaving a quiet break.";
        }
        else await PrepareAsync(++_revision, automatic: true);
    }
    private async Task PrepareAsync(int revision, bool automatic)
    {
        _loading = true;
        var state = State; var situationId = SituationId; var genre = Genre; var entry = Entry;
        try
        {
            var intensity = state switch { GameState.Combat => .9f, GameState.Escape => .8f,
                GameState.Uncertain => .4f, GameState.Travel => .5f, _ => .2f };
            var track = _selector.Choose(Library.For(genre, situationId), entry, intensity);
            if (CrossfadeSeconds == 0) track = track with { EndSeconds = track.DurationSeconds };
            Status = $"Preparing {track.Title}…";
            PreparedTrack prepared;
            await _loadGate.WaitAsync(_lifetime.Token);
            try
            {
                if (_disposed || revision != _revision) return;
                prepared = await _prepare(track);
            }
            finally { _loadGate.Release(); }
            if (_disposed || revision != _revision) return;
            var snapshot = Player.Snapshot();
            var urgent = state is GameState.Combat or GameState.Escape;
            var cue = track.EntrySeconds(entry, intensity);
            var available = prepared.Info.EndSeconds - cue;
            var fade = Math.Min(_crossfadeSeconds ?? (urgent ? 2d : 6d), available);
            var wait = 0d;
            if (automatic && snapshot.Track is not null)
            {
                fade = Math.Min(CrossfadeSeconds, Math.Min(snapshot.RemainingSeconds, available));
                wait = Math.Max(0, snapshot.RemainingSeconds - fade);
            }
            else if (!urgent && snapshot.Track is not null)
                wait = Math.Max(0, snapshot.Track.FindExit(snapshot.PositionSeconds, 6) - snapshot.PositionSeconds);
            if (CrossfadeSeconds == 0)
            {
                fade = 0;
                if (snapshot.Track is not null) wait = Math.Max(0, snapshot.Track.DurationSeconds - snapshot.PositionSeconds);
            }
            Player.Submit(new(TrackCommandKind.Play, prepared, cue, fade, wait, FinishAtFileEnd: CrossfadeSeconds == 0));
            Status = $"{track.Title} · entry {cue:0.0}s · {wait:0.0}s wait · {fade:0.0}s fade. Cue/recency rules, no AI detection.";
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            if (!_disposed && revision == _revision)
            {
                Status = $"Could not prepare track: {error.Message}";
                _retryAfterFrame = Player.Snapshot().Frames + Player.SampleRate * 5;
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        finally { if (revision == _revision) _loading = false; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_revision; _lifetime.Cancel(); _lifetime.Dispose();
        Player.Submit(new(TrackCommandKind.CancelScheduled));
    }
}
