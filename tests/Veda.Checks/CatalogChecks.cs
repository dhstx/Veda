using System.Text;
using System.Text.Json;
using Veda.Core;

internal static class CatalogChecks
{
    public static void Run(Action<string, Action> check, string temp)
    {
        var root = Path.Combine(temp, "managed-library");
        var original = Path.Combine(root, "ambient synth", "calm space", "original.wav");
        Tone(original, 16);
        var originalBytes = File.ReadAllBytes(original);
        var catalog = MusicCatalog.Open(root);
        var defaultSong = catalog.Data.Songs.Single();
        var genre = catalog.AddGenre("Industrial / heavy: ice");
        var situation = catalog.AddSituation("Blue star", "Whenever it is evident that I am near a blue star.");
        var custom = catalog.ImportSong(original, "ambient", "CalmSpace");
        var customScene = catalog.ImportSong(original, genre.Id, situation.Id);
        check("Defaults are protected, migration preserves original audio and default cue overrides", () =>
        {
            Assert(catalog.Data.Genres.Count(g => g.IsDefault) == 4 && catalog.Data.Situations.Count(s => s.IsDefault) == 6, "Missing defaults.");
            Throws<InvalidOperationException>(() => catalog.RemoveSong(defaultSong.Id));
            Throws<InvalidOperationException>(() => catalog.RemoveGenre("ambient"));
            Throws<InvalidOperationException>(() => catalog.RemoveSituation("CalmSpace"));
            Assert(File.ReadAllBytes(original).AsSpan().SequenceEqual(originalBytes), "Original changed.");
            Assert(defaultSong.RelativePath.Contains("/Default/") && custom.RelativePath.Contains("/Custom/"), "Origin folders not standardized.");
            var cueRoot = Path.Combine(temp, "managed-cues");
            Tone(Path.Combine(cueRoot, "ambient synth", "combat", "cue.wav"), 16);
            File.WriteAllText(Path.Combine(cueRoot, "track-cues.json"), JsonSerializer.Serialize(new Dictionary<string, TrackCues> { ["ambient synth/combat/cue.wav"] = new(2, 6, 15) }));
            var reopened = MusicCatalog.Open(cueRoot); reopened = MusicCatalog.Open(cueRoot);
            Assert(reopened.BuildLibrary().Tracks.Single().StrongSeconds == 6, "Migration lost authored cue on reopening.");
            File.WriteAllText(Path.Combine(cueRoot, "track-cues.json"), JsonSerializer.Serialize(new Dictionary<string, TrackCues> { ["ambient synth/combat/Default/cue.wav"] = new(1, 5, 15) }));
            Assert(MusicCatalog.Open(cueRoot).BuildLibrary().Tracks.Single().StrongSeconds == 5, "Reload did not apply a new cue override.");
        });
        check("Default and Custom group switches, individual choices and crossfade survive reopening", () =>
        {
            catalog.SetSongEnabled(defaultSong.Id, false); catalog.SetSources(true, false); catalog.SetCrossfade(0);
            var saved = MusicCatalog.Open(root); var library = saved.BuildLibrary();
            Assert(!saved.Data.Songs.First(s => s.Id == defaultSong.Id).Enabled && saved.Data.CrossfadeSeconds == 0, "Settings not retained.");
            Assert(library.Tracks.All(t => !t.Enabled), "Disabled sources still selectable.");
            saved.SetSources(false, true);
            Assert(saved.BuildLibrary().Tracks.Where(t => t.Enabled).All(t => !t.IsDefault), "Default source leaked.");
            saved.SetSources(true, true); saved.SetSongEnabled(defaultSong.Id, true);
            Assert(saved.BuildLibrary().For("ambient synth", GameState.CalmSpace).Length == 2, "Sources did not combine.");
            catalog = saved;
        });
        check("Custom imports accept exactly four minutes and reject longer, non-WAV and malformed audio", () =>
        {
            var exact = Path.Combine(temp, "exact.wav"); var over = Path.Combine(temp, "over.wav");
            Tone(exact, 240); Tone(over, 240, extraFrames: 1);
            var accepted = catalog.ImportSong(exact, genre.Id, situation.Id);
            Assert(catalog.BuildLibrary().Tracks.First(t => t.Id == accepted.Id).DurationSeconds == 240, "Four-minute boundary rejected.");
            var count = catalog.Data.Songs.Length;
            Throws<InvalidDataException>(() => catalog.ImportSong(over, genre.Id, situation.Id));
            var fake = Path.Combine(temp, "fake.mp3"); File.Copy(original, fake);
            Throws<InvalidDataException>(() => catalog.ImportSong(fake, genre.Id, situation.Id));
            var bad = Path.Combine(temp, "bad.wav"); File.WriteAllText(bad, "not audio");
            Throws<InvalidDataException>(() => catalog.ImportSong(bad, genre.Id, situation.Id));
            Assert(catalog.Data.Songs.Length == count, "Rejected import entered the catalog.");
            var shortFile = Path.Combine(temp, "short.wav"); Tone(shortFile, 1);
            var shortSong = catalog.ImportSong(shortFile, genre.Id, situation.Id);
            Assert(catalog.BuildLibrary().Tracks.First(t => t.Id == shortSong.Id).DurationSeconds == 1, "A valid short custom WAV was rejected.");
            catalog.RemoveSong(shortSong.Id);
        });
        check("Custom names and described situations use safe paths and reject duplicate or blank names", () =>
        {
            Assert(!genre.Folder.Contains(':') && !genre.Folder.Contains('/'), "User name became an unsafe folder.");
            Assert(catalog.Data.Situations.First(s => s.Id == situation.Id).Description.Contains("blue star"), "Description lost.");
            Throws<ArgumentException>(() => catalog.AddGenre("Industrial / heavy: ice"));
            Throws<ArgumentException>(() => catalog.AddSituation("Combat", "Duplicate standard name"));
            Throws<ArgumentException>(() => catalog.AddSituation("Cave", " "));
            Throws<ArgumentException>(() => catalog.AddGenre(" "));
        });
        check("Removing a custom song deletes its managed copy and preserves the device original", () =>
        {
            var imported = catalog.ImportSong(original, "ambient", "CalmSpace");
            var copy = Path.Combine(root, imported.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            catalog.RemoveSong(imported.Id);
            Assert(!File.Exists(copy) && File.ReadAllBytes(original).AsSpan().SequenceEqual(originalBytes), "Removal touched original or left active copy.");
            Assert(!MusicCatalog.Open(root).Data.Songs.Any(s => s.Id == imported.Id), "Removed song came back.");
        });
        check("Custom situation playback rotates independently; disabling all music settles to silence", () =>
        {
            var library = catalog.BuildLibrary();
            using var s = new FullTrackSession(library, t => Task.FromResult(PreparedTrack.Load(t))) { CrossfadeSeconds = 2, AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            s.SetGenreAsync(genre.Name).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 3);
            Assert(s.SituationId == situation.Id && s.Player.Snapshot().Track?.SituationId == situation.Id, "Custom situation became a default state.");
            s.TickAsync().GetAwaiter().GetResult();
            var starts = s.Player.Snapshot().Starts;
            catalog.SetSources(false, false); s.UpdateLibraryAsync(catalog.BuildLibrary()).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 3);
            s.TickAsync().GetAwaiter().GetResult(); Advance(s.Player, 48000);
            Assert(s.Player.Snapshot().Track is null && s.Player.Snapshot().Starts == starts, "Disabled music restarted.");
            catalog.SetSources(true, true); s.UpdateLibraryAsync(catalog.BuildLibrary()).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 3);
            Assert(s.Player.Snapshot().Track is not null, "Re-enabled music did not resume.");
        });
        check("Zero crossfade finishes the full file and starts the next exactly at EOF; positive values overlap", () =>
        {
            var tracks = catalog.BuildLibrary().For("ambient synth", GameState.CalmSpace);
            var library = new TrackLibrary(tracks, []);
            using var zero = new FullTrackSession(library, t => Task.FromResult(PreparedTrack.Load(t))) { CrossfadeSeconds = 0, AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            zero.RequestAsync(GameState.CalmSpace, true).GetAwaiter().GetResult(); Advance(zero.Player, 48000);
            zero.TickAsync().GetAwaiter().GetResult();
            Advance(zero.Player, 48000 * 15 - 1);
            Assert(zero.Player.Snapshot().Starts == 1 && !zero.Player.Snapshot().Transitioning && zero.Player.Snapshot().Track is not null, "Zero overlap truncated the file.");
            Advance(zero.Player, 2);
            Assert(zero.Player.Snapshot().Starts == 2 && zero.Player.Snapshot().PositionSeconds < .001 && !zero.Player.Snapshot().Transitioning, "Next file did not begin at EOF without overlap.");
            using var positive = new FullTrackSession(library, t => Task.FromResult(PreparedTrack.Load(t))) { CrossfadeSeconds = 4, AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            positive.RequestAsync(GameState.CalmSpace, true).GetAwaiter().GetResult(); Advance(positive.Player, 48000 * 5);
            positive.TickAsync().GetAwaiter().GetResult(); Advance(positive.Player, 48000 * 8);
            Assert(positive.Player.Snapshot().Starts == 2 && positive.Player.Snapshot().Transitioning && positive.Player.Snapshot().PositionSeconds is > .9 and < 1.1, "Crossfade did not start four seconds before EOF.");
            Throws<ArgumentException>(() => positive.CrossfadeSeconds = double.NaN);
            using var changed = new FullTrackSession(new TrackLibrary(tracks.Select(t => t with { EndSeconds = 12 }).ToArray(), []), t => Task.FromResult(PreparedTrack.Load(t))) { CrossfadeSeconds = 4, AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            changed.RequestAsync(GameState.CalmSpace, true).GetAwaiter().GetResult(); Advance(changed.Player, 48000 * 5);
            changed.CrossfadeSeconds = 0; Advance(changed.Player, 48000); changed.TickAsync().GetAwaiter().GetResult();
            Advance(changed.Player, 48000 * 10 - 1);
            Assert(changed.Player.Snapshot().Starts == 1, "Changing to zero did not restore the full file ending.");
            Advance(changed.Player, 2); Assert(changed.Player.Snapshot().Starts == 2, "Full-tail handoff failed.");
        });
        check("Custom category removal cascades only its custom copies and leaves protected defaults", () =>
        {
            var ice = catalog.AddSituation("Ice cave", "Whenever I am in a dark cave with ice.");
            var song = catalog.ImportSong(original, "ambient", ice.Id);
            catalog.RemoveSituation(ice.Id);
            Assert(!catalog.Data.Songs.Any(s => s.Id == song.Id) && !catalog.Data.Situations.Any(s => s.Id == ice.Id), "Situation cascade failed.");
            catalog.RemoveGenre(genre.Id);
            Assert(catalog.Data.Songs.All(s => s.GenreId != genre.Id) && catalog.Data.Songs.Any(s => s.Id == defaultSong.Id), "Genre removal damaged defaults.");
            Assert(File.ReadAllBytes(original).AsSpan().SequenceEqual(originalBytes), "Cascade deleted device original.");
        });
        check("Unsafe or modified protected catalog metadata is rejected before file mutation", () =>
        {
            var clean = File.ReadAllText(Path.Combine(root, "veda-library.json"));
            var changed = catalog.Data with { Genres = catalog.Data.Genres.Where(g => g.Id != "ambient").ToArray() };
            File.WriteAllText(Path.Combine(root, "veda-library.json"), JsonSerializer.Serialize(changed));
            Throws<InvalidDataException>(() => MusicCatalog.Open(root));
            changed = catalog.Data with { Songs = catalog.Data.Songs.Select(s => s.Id == custom.Id ? s with { RelativePath = "../outside.wav" } : s).ToArray() };
            File.WriteAllText(Path.Combine(root, "veda-library.json"), JsonSerializer.Serialize(changed));
            Throws<InvalidDataException>(() => MusicCatalog.Open(root));
            File.WriteAllText(Path.Combine(root, "veda-library.json"), clean);
        });
    }
    private static void Tone(string path, int seconds, int extraFrames = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var frames = checked(48000 * seconds + extraFrames); var bytes = frames * 4;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(48000); writer.Write(192000); writer.Write((ushort)4); writer.Write((ushort)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
        var chunk = new byte[65536];
        for (var i = 0; i < chunk.Length; i += 4) { chunk[i] = 0x99; chunk[i + 1] = 0x09; chunk[i + 2] = 0x33; chunk[i + 3] = 0x03; }
        while (bytes > 0) { var count = Math.Min(bytes, chunk.Length); writer.Write(chunk, 0, count); bytes -= count; }
    }
    private static void Advance(FullTrackPlayer player, int frames)
    {
        var buffer = new float[2048];
        while (frames > 0) { var count = Math.Min(frames, 1024); player.Render(buffer.AsSpan(0, count * 2)); frames -= count; }
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
}
