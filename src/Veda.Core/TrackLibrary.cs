using System.Text.Json;

namespace Veda.Core;

public enum TrackEntry { Automatic, Beginning, Quiet, Strong }
public sealed record TrackCues(double? QuietSeconds = null, double? StrongSeconds = null,
    double? EndSeconds = null);

public sealed record LibraryTrack(string Id, string Path, string Genre, GameState State,
    double DurationSeconds, double EndSeconds, double QuietSeconds, double StrongSeconds,
    float Gain, float[] Energy)
{
    public string SituationId { get; init; } = State.ToString();
    public bool IsDefault { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public string? DisplayTitle { get; init; }
    public string Title => DisplayTitle ?? System.IO.Path.GetFileNameWithoutExtension(Path);
    public double EntrySeconds(TrackEntry entry, float intensity) => entry switch
    {
        TrackEntry.Beginning => 0,
        TrackEntry.Quiet => QuietSeconds,
        TrackEntry.Strong => StrongSeconds,
        _ => intensity >= .7f ? StrongSeconds : intensity <= .3f ? QuietSeconds : 0
    };

    // Energy valleys are approximate edit opportunities, not verified beat boundaries.
    public double FindExit(double position, double maximumWait)
    {
        var first = Math.Max(0, (int)Math.Ceiling(position));
        var last = Math.Min(Energy.Length - 1, (int)Math.Floor(Math.Min(EndSeconds - 2, position + maximumWait)));
        if (last < first) return position;
        var best = first;
        for (var i = first + 1; i <= last; i++) if (Energy[i] < Energy[best]) best = i;
        return best;
    }
}

public sealed record TrackLibrary(IReadOnlyList<LibraryTrack> Tracks, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string>? DefinedGenres { get; init; }
    public string[] Genres => (DefinedGenres ?? Tracks.Select(t => t.Genre).ToArray()).Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public LibraryTrack[] For(string genre, GameState state) => For(genre, state.ToString());
    public LibraryTrack[] For(string genre, string situationId) => Tracks.Where(t => t.Enabled && t.SituationId == situationId &&
        string.Equals(t.Genre, genre, StringComparison.OrdinalIgnoreCase)).ToArray();
}

public static class TrackLibraryLoader
{
    public const int MaximumTrackSeconds = 600;
    public static TrackLibrary Load(string folder, CancellationToken cancellationToken = default)
    {
        folder = System.IO.Path.GetFullPath(folder);
        var tracks = new List<LibraryTrack>();
        var warnings = new List<string>();
        var cuePath = System.IO.Path.Combine(folder, "track-cues.json");
        var cues = File.Exists(cuePath)
            ? JsonSerializer.Deserialize<Dictionary<string, TrackCues>>(File.ReadAllText(cuePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
                ?? throw new InvalidDataException("track-cues.json must contain a cue dictionary.")
            : new Dictionary<string, TrackCues>();
        var options = new EnumerationOptions { RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var file in Directory.EnumerateFiles(folder, "*", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = System.IO.Path.GetExtension(file);
            if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                if (new[] { ".mp3", ".flac", ".m4a", ".ogg" }.Contains(extension.ToLowerInvariant()))
                    warnings.Add($"Skipped {System.IO.Path.GetRelativePath(folder, file)}: use stereo 48 kHz PCM16 or float32 WAV.");
                continue;
            }
            var relative = System.IO.Path.GetRelativePath(folder, file).Replace('\\', '/');
            var parts = relative.Split('/');
            if (parts.Length < 3 || !TryState(parts[1], out var state))
            {
                warnings.Add($"Skipped {relative}: expected genre/situation/song.wav, with a recognized situation folder directly inside the genre.");
                continue;
            }
            var genre = parts[0];
            try
            {
                tracks.Add(Analyze(file, relative, genre, state, cues.GetValueOrDefault(relative), cancellationToken: cancellationToken));
            }
            catch (Exception error) when (error is InvalidDataException or IOException or OverflowException)
            { warnings.Add($"Skipped {relative}: {error.Message}"); }
        }
        if (tracks.Count == 0) throw new InvalidDataException("No playable tracks found. Select the parent containing genre folders, each with situation folders and stereo 48 kHz PCM16/float32 WAV files. " + string.Join(" ", warnings.Take(3)));
        return new(tracks.AsReadOnly(), warnings.AsReadOnly());
    }

    public static LibraryTrack Analyze(string file, string relative, string genre, GameState state,
        TrackCues? authored = null, int maximumSeconds = MaximumTrackSeconds, double minimumSeconds = 12,
        CancellationToken cancellationToken = default)
    {
        var decoded = WavFile.Read(file, 48000 * maximumSeconds);
        cancellationToken.ThrowIfCancellationRequested();
        var duration = decoded.Samples.Length / 96000d;
        if (duration < minimumSeconds) throw new InvalidDataException("The track is too short for this library.");
        var energy = new float[(int)Math.Ceiling(duration)];
        float peak = 0;
        for (var second = 0; second < energy.Length; second++)
        {
            double sum = 0;
            var start = second * 96000;
            var end = Math.Min(start + 96000, decoded.Samples.Length);
            for (var i = start; i < end; i++)
            {
                var sample = decoded.Samples[i]; sum += sample * sample;
                peak = Math.Max(peak, Math.Abs(sample));
            }
            energy[second] = (float)Math.Sqrt(sum / (end - start));
        }
        if (peak < .001f) throw new InvalidDataException("The track is effectively silent.");
        var endSeconds = duration;
        var lastCue = Math.Max(0, (int)Math.Floor(endSeconds - Math.Min(45, endSeconds / 2)));
        var firstCue = Math.Min(8, lastCue / 3);
        var quiet = firstCue; var strong = firstCue;
        double quietEnergy = double.MaxValue, strongEnergy = -1;
        for (var i = firstCue; i <= lastCue; i++)
        {
            var average = energy.Skip(i).Take(4).Average();
            if (average > .003f && average < quietEnergy) { quietEnergy = average; quiet = i; }
            if (average > strongEnergy) { strongEnergy = average; strong = i; }
        }
        if (duration < 8) quiet = strong = 0;
        if (authored is not null)
        {
            endSeconds = authored.EndSeconds ?? endSeconds;
            if (!double.IsFinite(endSeconds) || endSeconds < 12 || endSeconds > duration)
                throw new InvalidDataException("Authored end cue is outside the track.");
            var q = authored.QuietSeconds ?? quiet; var s = authored.StrongSeconds ?? strong;
            if (!ValidCue(q, endSeconds) || !ValidCue(s, endSeconds))
                throw new InvalidDataException("Entry cues must be finite, nonnegative, and leave at least eight seconds.");
            return new(relative, file, genre, state, duration, endSeconds, q, s,
                Math.Min(1, .7f / peak), energy);
        }
        return new(relative, file, genre, state, duration, endSeconds, quiet, strong, Math.Min(1, .7f / peak), energy);
    }

    private static bool ValidCue(double value, double end) => double.IsFinite(value) && value >= 0 && value <= end - 8;
    public static bool TryState(string folder, out GameState state)
    {
        var normalized = new string(folder.Where(char.IsLetterOrDigit).ToArray());
        return Enum.TryParse(normalized, true, out state) && Enum.IsDefined(state) &&
            normalized.Equals(state.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Transparent cue/recency ranking; this is not an AI or a gameplay detector.</summary>
public sealed class TrackSelector
{
    private readonly Dictionary<string, int> _plays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _history = new(StringComparer.OrdinalIgnoreCase);
    public LibraryTrack Choose(IReadOnlyList<LibraryTrack> candidates, TrackEntry entry, float intensity)
    {
        if (candidates.Count == 0) throw new InvalidOperationException("This situation has no tracks in the selected genre.");
        if (!float.IsFinite(intensity)) throw new ArgumentException("Intensity must be finite.");
        var key = candidates[0].Genre + "/" + candidates[0].SituationId;
        var recent = _history.GetValueOrDefault(key) ?? [];
        var excluded = recent.TakeLast(Math.Min(2, candidates.Count - 1)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var available = candidates.Where(t => !excluded.Contains(t.Id)).ToArray();
        if (available.Length == 0) available = candidates.ToArray();
        return available.OrderBy(t => _plays.GetValueOrDefault(t.Id)).ThenBy(t =>
        {
            var second = Math.Min(t.Energy.Length - 1, (int)t.EntrySeconds(entry, intensity));
            var max = t.Energy.Max();
            return Math.Abs((max > 0 ? t.Energy[second] / max : 0) - intensity);
        }).ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase).First();
    }
    public void MarkPlayed(LibraryTrack track)
    {
        _plays[track.Id] = _plays.GetValueOrDefault(track.Id) + 1;
        var key = track.Genre + "/" + track.SituationId;
        if (!_history.TryGetValue(key, out var history)) _history[key] = history = [];
        history.Add(track.Id);
        if (history.Count > 2) history.RemoveAt(0);
    }
}
