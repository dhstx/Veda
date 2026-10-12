using System.Text.Json;

namespace Veda.Core;

public sealed record CatalogGenre(string Id, string Name, string Folder, bool IsDefault)
{
    public string Label => $"{Name} · {(IsDefault ? "Default" : "Custom")}";
}
public sealed record CatalogSituation(string Id, string Name, string Description, string Folder, bool IsDefault)
{
    public string Label => $"{Name} · {(IsDefault ? "Default" : "Custom")}";
}
public sealed record CatalogSong(string Id, string Title, string RelativePath, string GenreId,
    string SituationId, bool IsDefault, bool Enabled = true, TrackCues? Cues = null);
public sealed record CatalogData(int Version, CatalogGenre[] Genres, CatalogSituation[] Situations,
    CatalogSong[] Songs, bool IncludeDefault = true, bool IncludeCustom = true, double CrossfadeSeconds = 8);

/// <summary>One control owner. File work runs outside playback. Default identities are code-defined.</summary>
public sealed class MusicCatalog
{
    public const int MaximumCustomSeconds = 240;
    public const string ImportRequirements = "WAV only, no longer than 4:00. Use stereo 48 kHz, 16-bit PCM or 32-bit float WAV (ordinary RIFF). Originals stay untouched; Veda imports a copy.";
    public static readonly CatalogGenre[] DefaultGenres =
    [new("ambient", "ambient synth", "ambient synth", true), new("hiphop", "modern hiphop", "modern hiphop", true),
     new("rock", "synth hard rock", "synth hard rock", true), new("western", "western", "western", true)];
    public static readonly CatalogSituation[] DefaultSituations = Enum.GetValues<GameState>()
        .Select(s => new CatalogSituation(s.ToString(), GameStateLabels.Display(s),
            $"The standard {GameStateLabels.Display(s).ToLowerInvariant()} situation.", GameStateLabels.Display(s).ToLowerInvariant(), true)).ToArray();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private readonly Dictionary<string, (long Length, long Stamp, LibraryTrack Track)> _analysis = new();
    private readonly List<string> _warnings = [];
    public string Root { get; }
    public CatalogData Data { get; private set; }
    private string MetadataPath => Path.Combine(Root, "veda-library.json");

    private MusicCatalog(string root, CatalogData data) { Root = root; Data = data; }
    public static MusicCatalog Open(string root, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "veda-library.json");
        var data = File.Exists(path) ? JsonSerializer.Deserialize<CatalogData>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("The music catalog is empty.")
            : new CatalogData(1, DefaultGenres.ToArray(), DefaultSituations.ToArray(), []);
        var catalog = new MusicCatalog(root, data); catalog.Validate(data);
        catalog.RefreshDefaults(cancellationToken);
        return catalog;
    }

    // Existing genre/situation WAVs are adopted as defaults and copied into Default/.
    // New songs in those default folders are discovered on subsequent loads.
    public void RefreshDefaults(CancellationToken cancellationToken = default)
    {
        _warnings.Clear(); _analysis.Clear();
        var songs = Data.Songs.ToList();
        var cuesPath = Path.Combine(Root, "track-cues.json");
        var cues = File.Exists(cuesPath) ? JsonSerializer.Deserialize<Dictionary<string, TrackCues>>(File.ReadAllText(cuesPath), Json) ?? [] : [];
        foreach (var genreDir in Directory.EnumerateDirectories(Root).Where(d => !IsLink(d)))
        {
            var normalized = Normalize(Path.GetFileName(genreDir));
            var genre = DefaultGenres.FirstOrDefault(g => Normalize(g.Name) == normalized ||
                g.Id == "ambient" && normalized == "ambientspacesynth");
            if (genre is null) continue;
            foreach (var situationDir in Directory.EnumerateDirectories(genreDir).Where(d => !IsLink(d)))
            {
                if (!TrackLibraryLoader.TryState(Path.GetFileName(situationDir), out var state)) continue;
                var situation = DefaultSituations.First(s => s.Id == state.ToString());
                var sources = Directory.EnumerateFiles(situationDir).Where(IsWav).ToList();
                var defaultDir = Path.Combine(situationDir, "Default");
                if (Directory.Exists(defaultDir) && !IsLink(defaultDir)) sources.AddRange(Directory.EnumerateFiles(defaultDir).Where(IsWav));
                foreach (var source in sources.Where(f => !IsLink(f)).Order(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var target = Resolve($"{genre.Folder}/{situation.Folder}/Default/{Path.GetFileName(source)}");
                        var relative = Path.GetRelativePath(Root, target).Replace('\\', '/');
                        var id = "default/" + relative;
                        var originalRelative = Path.GetRelativePath(Root, source).Replace('\\', '/');
                        var existingIndex = songs.FindIndex(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                        if (existingIndex >= 0)
                        {
                            var authored = cues.GetValueOrDefault(relative) ?? cues.GetValueOrDefault(originalRelative);
                            if (authored is not null) songs[existingIndex] = songs[existingIndex] with { Cues = authored };
                            continue;
                        }
                        var actual = File.Exists(target) ? target : source;
                        var info = TrackLibraryLoader.Analyze(actual, id, genre.Name, state, cues.GetValueOrDefault(originalRelative), cancellationToken: cancellationToken);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        if (!File.Exists(target)) File.Copy(source, target);
                        // Cues follow the migrated copy through the stable song ID.
                        songs.Add(new(id, Path.GetFileNameWithoutExtension(source), relative, genre.Id, situation.Id, true, Cues: cues.GetValueOrDefault(originalRelative)));
                        Cache(target, info with { Path = target });
                    }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                    { _warnings.Add($"Skipped {Path.GetFileName(source)}: {e.Message}"); }
                }
            }
        }
        foreach (var genre in Data.Genres)
            foreach (var situation in Data.Situations)
                foreach (var origin in new[] { "Default", "Custom" }) Directory.CreateDirectory(Resolve($"{genre.Folder}/{situation.Folder}/{origin}"));
        Commit(Data with { Songs = songs.ToArray() });
    }

    public TrackLibrary BuildLibrary(CancellationToken cancellationToken = default)
    {
        var tracks = new List<LibraryTrack>(); var warnings = _warnings.ToList();
        foreach (var song in Data.Songs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var genre = Data.Genres.First(g => g.Id == song.GenreId);
                var situation = Data.Situations.First(s => s.Id == song.SituationId);
                var path = Resolve(song.RelativePath); var file = new FileInfo(path);
                LibraryTrack info;
                if (_analysis.TryGetValue(song.Id, out var cached) && file.Exists && cached.Length == file.Length && cached.Stamp == file.LastWriteTimeUtc.Ticks) info = cached.Track;
                else
                {
                    if (!IsWav(path)) throw new InvalidDataException("Only WAV songs are allowed.");
                    var state = Enum.TryParse<GameState>(situation.Id, out var known) ? known : GameState.CalmSpace;
                    info = TrackLibraryLoader.Analyze(path, song.Id, genre.Name, state, song.Cues,
                        maximumSeconds: song.IsDefault ? TrackLibraryLoader.MaximumTrackSeconds : MaximumCustomSeconds,
                        minimumSeconds: song.IsDefault ? 12 : .1, cancellationToken: cancellationToken);
                    Cache(path, info);
                }
                tracks.Add(info with { SituationId = situation.Id, IsDefault = song.IsDefault, DisplayTitle = song.Title,
                    Enabled = song.Enabled && (song.IsDefault ? Data.IncludeDefault : Data.IncludeCustom) });
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            { warnings.Add($"Unavailable: {song.Title}: {e.Message}"); }
        }
        return new(tracks.AsReadOnly(), warnings.AsReadOnly()) { DefinedGenres = Data.Genres.Select(g => g.Name).ToArray() };
    }

    public CatalogGenre AddGenre(string name)
    {
        name = Name(name);
        if (Data.Genres.Any(g => Normalize(g.Name) == Normalize(name))) throw new ArgumentException("That genre already exists.");
        var id = Guid.NewGuid().ToString("N"); var genre = new CatalogGenre(id, name, Folder(name, id), false);
        Commit(Data with { Genres = [.. Data.Genres, genre] }); return genre;
    }
    public CatalogSituation AddSituation(string name, string description)
    {
        name = Name(name); description = description.Trim();
        if (description.Length is < 1 or > 600) throw new ArgumentException("Describe the situation in 1–600 characters.");
        if (Data.Situations.Any(s => Normalize(s.Name) == Normalize(name))) throw new ArgumentException("That situation already exists.");
        var id = Guid.NewGuid().ToString("N"); var situation = new CatalogSituation(id, name, description, Folder(name, id), false);
        Commit(Data with { Situations = [.. Data.Situations, situation] }); return situation;
    }
    public CatalogSong ImportSong(string source, string genreId, string situationId)
    {
        if (!IsWav(source)) throw new InvalidDataException(ImportRequirements);
        var genre = Data.Genres.First(g => g.Id == genreId); var situation = Data.Situations.First(s => s.Id == situationId);
        // Validate the full source before copying it; no oversize decode is allocated.
        var info = TrackLibraryLoader.Analyze(source, "pending", genre.Name, GameState.CalmSpace,
            maximumSeconds: MaximumCustomSeconds, minimumSeconds: .1);
        var id = Guid.NewGuid().ToString("N");
        var relative = $"{genre.Folder}/{situation.Folder}/Custom/{id}.wav"; var target = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
        var song = new CatalogSong(id, Path.GetFileNameWithoutExtension(source), relative, genreId, situationId, false);
        try
        {
            info = TrackLibraryLoader.Analyze(target, id, genre.Name, GameState.CalmSpace,
                maximumSeconds: MaximumCustomSeconds, minimumSeconds: .1);
            Commit(Data with { Songs = [.. Data.Songs, song] });
        }
        catch { File.Delete(target); throw; }
        Cache(target, info with { Id = id, Path = target, SituationId = situationId, IsDefault = false });
        return song;
    }
    public void SetSongEnabled(string id, bool enabled)
    {
        if (!Data.Songs.Any(s => s.Id == id)) throw new ArgumentException("Unknown song.");
        Commit(Data with { Songs = Data.Songs.Select(s => s.Id == id ? s with { Enabled = enabled } : s).ToArray() });
    }
    public void SetSources(bool defaults, bool custom) => Commit(Data with { IncludeDefault = defaults, IncludeCustom = custom });
    public void SetCrossfade(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds is < 0 or > 20) throw new ArgumentException("Crossfade must be 0–20 seconds.");
        Commit(Data with { CrossfadeSeconds = seconds });
    }
    public void RemoveSong(string id)
    {
        var song = Data.Songs.First(s => s.Id == id);
        if (song.IsDefault) throw new InvalidOperationException("Default songs can be turned off, but cannot be removed.");
        Remove([song], Data with { Songs = Data.Songs.Where(s => s.Id != id).ToArray() });
    }
    public void RemoveGenre(string id)
    {
        var genre = Data.Genres.First(g => g.Id == id);
        if (genre.IsDefault) throw new InvalidOperationException("Default genres cannot be removed.");
        var songs = Data.Songs.Where(s => s.GenreId == id).ToArray();
        Remove(songs, Data with { Genres = Data.Genres.Where(g => g.Id != id).ToArray(), Songs = Data.Songs.Except(songs).ToArray() });
    }
    public void RemoveSituation(string id)
    {
        var situation = Data.Situations.First(s => s.Id == id);
        if (situation.IsDefault) throw new InvalidOperationException("Default situations cannot be removed.");
        var songs = Data.Songs.Where(s => s.SituationId == id).ToArray();
        Remove(songs, Data with { Situations = Data.Situations.Where(s => s.Id != id).ToArray(), Songs = Data.Songs.Except(songs).ToArray() });
    }
    private void Remove(CatalogSong[] songs, CatalogData next)
    {
        if (songs.Any(s => s.IsDefault)) throw new InvalidOperationException("This selection contains a protected default song.");
        Commit(next);
        foreach (var song in songs)
        {
            _analysis.Remove(song.Id);
            try { File.Delete(Resolve(song.RelativePath)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            { _warnings.Add($"Removed {song.Title} from the catalog; its imported copy could not be deleted."); }
        }
    }
    private void Cache(string path, LibraryTrack info)
    {
        var file = new FileInfo(path); _analysis[info.Id] = (file.Length, file.LastWriteTimeUtc.Ticks, info);
    }
    private void Commit(CatalogData next)
    {
        Validate(next);
        var temporary = MetadataPath + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(next, Json)); File.Move(temporary, MetadataPath, true); Data = next; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void Validate(CatalogData data)
    {
        if (data.Version != 1 || data.Genres is null || data.Situations is null || data.Songs is null ||
            !double.IsFinite(data.CrossfadeSeconds) || data.CrossfadeSeconds is < 0 or > 20) throw new InvalidDataException("Invalid music catalog.");
        foreach (var genre in DefaultGenres) if (!data.Genres.Contains(genre)) throw new InvalidDataException("A default genre was modified or removed.");
        foreach (var situation in DefaultSituations) if (!data.Situations.Contains(situation)) throw new InvalidDataException("A default situation was modified or removed.");
        if (data.Genres.Select(g => g.Id).Distinct().Count() != data.Genres.Length || data.Situations.Select(s => s.Id).Distinct().Count() != data.Situations.Length || data.Songs.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Songs.Length)
            throw new InvalidDataException("Duplicate catalog identity.");
        foreach (var genre in data.Genres) { Name(genre.Name); Resolve(genre.Folder); if (genre.IsDefault != DefaultGenres.Any(g => g.Id == genre.Id)) throw new InvalidDataException("Invalid genre type."); }
        foreach (var situation in data.Situations) { Name(situation.Name); Resolve(situation.Folder); if (situation.IsDefault != DefaultSituations.Any(s => s.Id == situation.Id)) throw new InvalidDataException("Invalid situation type."); }
        foreach (var song in data.Songs)
        {
            if (!data.Genres.Any(g => g.Id == song.GenreId) || !data.Situations.Any(s => s.Id == song.SituationId)) throw new InvalidDataException("Song references an unknown genre or situation.");
            Resolve(song.RelativePath);
            var parts = song.RelativePath.Replace('\\', '/').Split('/');
            var genre = data.Genres.First(g => g.Id == song.GenreId);
            var situation = data.Situations.First(s => s.Id == song.SituationId);
            if (parts.Length != 4 || parts[0] != genre.Folder || parts[1] != situation.Folder || parts[2] != (song.IsDefault ? "Default" : "Custom") || !IsWav(song.RelativePath) || song.IsDefault && (!genre.IsDefault || !situation.IsDefault)) throw new InvalidDataException("Song source and folder do not agree.");
        }
    }
    private string Resolve(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Replace('\\', '/').Split('/').Any(p => p is "." or ".." || p.Length == 0 || p.IndexOfAny([':', '*', '?', '"', '<', '>', '|']) >= 0)) throw new InvalidDataException("Invalid library path.");
        var path = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var current = Root;
        foreach (var part in Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar))
        { current = Path.Combine(current, part); if ((File.Exists(current) || Directory.Exists(current)) && IsLink(current)) throw new InvalidDataException("Library links are not followed."); }
        return path;
    }
    private static string Name(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80 || !name.Any(char.IsLetterOrDigit) || name.Any(char.IsControl)) throw new ArgumentException("Use a name of 1–80 characters with at least one letter or number.");
        return name;
    }
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string Folder(string name, string id) => "custom-" + Normalize(name)[..Math.Min(24, Normalize(name).Length)] + "-" + id[..8];
    private static bool IsWav(string path) => Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);
    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
