using System.Text.Json;
using Veda.Core;

internal static class FullTrackChecks
{
    public static void Run(Action<string, Action> check, string temp, string? uploadedFolder)
    {
        var root = Path.Combine(temp, "song-library");
        foreach (var (state, count) in new[] { ("calm space", 2), ("combat", 3) })
        {
            var dir = Path.Combine(root, "ambient synth", state); Directory.CreateDirectory(dir);
            for (var i = 0; i < count; i++)
            {
                var samples = new float[48000 * 20 * 2];
                for (var frame = 0; frame < samples.Length / 2; frame++)
                {
                    var amplitude = frame < 48000 * 8 ? .03f : .2f + i * .03f;
                    samples[frame * 2] = amplitude * (float)Math.Sin(frame * .01);
                    samples[frame * 2 + 1] = -samples[frame * 2] / 2;
                }
                WavFile.WritePcm16(Path.Combine(dir, $"{i + 1}.wav"), 48000, 2, samples);
            }
        }
        var library = TrackLibraryLoader.Load(root);
        check("Full-song import handles mixed counts, genre/situation folders, and energy cues", () =>
        {
            Assert(library.For("ambient synth", GameState.CalmSpace).Length == 2 && library.For("ambient synth", GameState.Combat).Length == 3, "Wrong counts.");
            Assert(library.Tracks.All(t => t.DurationSeconds == 20 && t.QuietSeconds < 8 && t.StrongSeconds >= 8), "Wrong cues.");
            Assert(TrackLibraryLoader.TryState("Calm_space", out var state) && state == GameState.CalmSpace, "Alias rejected.");
            Assert(!TrackLibraryLoader.TryState("999", out _), "Undefined state accepted.");
        });
        check("Track recency alternates two options and cycles three without recent repeats", () =>
        {
            foreach (var state in new[] { GameState.CalmSpace, GameState.Combat })
            {
                var candidates = library.For("ambient synth", state); var selector = new TrackSelector(); var ids = new List<string>();
                for (var i = 0; i < 8; i++)
                {
                    var track = selector.Choose(candidates, TrackEntry.Automatic, .5f);
                    Assert(!ids.TakeLast(candidates.Length - 1).Contains(track.Id), "Recent track repeated.");
                    ids.Add(track.Id); selector.MarkPlayed(track);
                }
            }
        });
        check("Full-song cue starts preserve stereo and avoid peak clamping", () =>
        {
            var track = PreparedTrack.Load(library.Tracks[0]); var p = new FullTrackPlayer();
            p.SetVolume(1); p.Submit(new(TrackCommandKind.Play, track, 8, .05)); Advance(p, 4800);
            var output = new float[960]; p.Render(output);
            Assert(Math.Abs(output[20] + output[21] * 2) < .0001, "Stereo changed.");
            Assert(p.Snapshot().PositionSeconds > 8 && output.Any(v => Math.Abs(v) > .1f), "Wrong entry cue.");
            Assert(p.Snapshot().ClampedSamples == 0, "Clipped.");
        });
        check("Full-song rendering is independent of callback size; latest request and cancellation win", () =>
        {
            var tracks = library.Tracks.Take(2).Select(PreparedTrack.Load).ToArray();
            float[] Render(int block)
            {
                var p = new FullTrackPlayer(); p.Submit(new(TrackCommandKind.Play, tracks[0], 0, .2));
                var output = new float[48000 * 3 * 2];
                for (var i = 0; i < output.Length; i += block * 2) p.Render(output.AsSpan(i, Math.Min(block * 2, output.Length - i)));
                return output;
            }
            Assert(Render(127).AsSpan().SequenceEqual(Render(2048)), "Block size changed playback.");
            var p = new FullTrackPlayer(); p.Submit(new(TrackCommandKind.Play, tracks[0], 0, .1));
            p.Submit(new(TrackCommandKind.Play, tracks[1], 0, .1)); Advance(p, 48000);
            Assert(p.Snapshot().Track?.Id == tracks[1].Info.Id, "Stale request played.");
            p.Submit(new(TrackCommandKind.Play, tracks[0], 0, .1, 2)); Advance(p, 100);
            p.Submit(new(TrackCommandKind.CancelScheduled)); Advance(p, 48000 * 3);
            Assert(p.Snapshot().Track?.Id == tracks[1].Info.Id, "Cancelled track played.");
        });
        check("Interrupted crossfades retain only the latest next track without discarding fading voices", () =>
        {
            var a = PreparedTrack.Load(library.Tracks[0]); var b = PreparedTrack.Load(library.Tracks[2]); var c = PreparedTrack.Load(library.Tracks[3]);
            var p = new FullTrackPlayer(); p.Submit(new(TrackCommandKind.Play, a, 0, .1)); Advance(p, 48000);
            p.Submit(new(TrackCommandKind.Play, b, 0, 2)); Advance(p, 4800);
            p.Submit(new(TrackCommandKind.Play, a, 0, .1)); p.Submit(new(TrackCommandKind.Play, c, 0, .1)); Advance(p, 48000 * 3);
            Assert(p.Snapshot().Track?.Id == c.Info.Id && p.Snapshot().Starts == 3, "Queued stale tracks.");
        });
        check("Full-song callback allocates no managed memory; intentional silence keeps clock running", () =>
        {
            var p = new FullTrackPlayer(); p.Submit(new(TrackCommandKind.Play, PreparedTrack.Load(library.Tracks[0]), 0, .1));
            var output = new float[960]; Advance(p, 48000); var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) p.Render(output);
            Assert(GC.GetAllocatedBytesForCurrentThread() == before, "Callback allocated.");
            p.Submit(new(TrackCommandKind.Silence, FadeSeconds: .1)); Advance(p, 48000);
            var clock = p.Snapshot().Frames; p.Render(output);
            Assert(output.All(v => v == 0) && p.Snapshot().Frames > clock, "Silence stopped clock.");
        });
        check("Quiet breaks resume, urgent changes interrupt silence, repeated states do not restart, missing states preserve music", () =>
        {
            using var s = new FullTrackSession(library, t => Task.FromResult(PreparedTrack.Load(t))) { QuietSeconds = 2 };
            s.RequestAsync(GameState.CalmSpace, true).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 7);
            s.TickAsync().GetAwaiter().GetResult(); var first = s.Player.Snapshot().Track!.Id; var starts = s.Player.Snapshot().Starts;
            s.RequestAsync(GameState.CalmSpace).GetAwaiter().GetResult(); Advance(s.Player, 48000);
            Assert(s.Player.Snapshot().Starts == starts, "Repeated state restarted song.");
            s.RequestAsync(GameState.Travel).GetAwaiter().GetResult();
            Assert(s.State == GameState.CalmSpace && s.Status.Contains("No Travel"), "Missing state changed state/music.");
            Advance(s.Player, 48000 * 7);
            s.RequestAsync(GameState.CalmSpace).GetAwaiter().GetResult();
            Advance(s.Player, 48000 * 13); s.TickAsync().GetAwaiter().GetResult(); Advance(s.Player, 48000);
            s.TickAsync().GetAwaiter().GetResult(); Assert(s.Player.Snapshot().Track is null, "Quiet break not held.");
            Advance(s.Player, 48000 * 2); s.TickAsync().GetAwaiter().GetResult(); Advance(s.Player, 48000);
            Assert(s.Player.Snapshot().Track is { } track && track.Id != first, "Did not resume another calm track.");
            s.Player.Submit(new(TrackCommandKind.Silence, FadeSeconds: .1)); Advance(s.Player, 48000);
            s.RequestAsync(GameState.Combat).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 3);
            Assert(s.Player.Snapshot().Track?.State == GameState.Combat, "Combat did not interrupt silence.");
        });
        check("Continuous same-state playback overlaps the next track without an intentional gap", () =>
        {
            using var s = new FullTrackSession(library, t => Task.FromResult(PreparedTrack.Load(t))) { AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            s.RequestAsync(GameState.Combat, true).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 3);
            s.TickAsync().GetAwaiter().GetResult(); var first = s.Player.Snapshot().Track!.Id;
            Advance(s.Player, 48000 * 18); s.TickAsync().GetAwaiter().GetResult();
            Assert(s.Player.Snapshot().Starts == 2 && s.Player.Snapshot().Track is { } t && t.Id != first, "Did not rotate before EOF.");
            Assert(s.Player.Snapshot().ClampedSamples == 0, "Overlap clipped.");
        });
        check("Rapid state changes serialize background loads and discard superseded prepared tracks", () =>
        {
            var completion = new TaskCompletionSource<PreparedTrack>(TaskCreationOptions.RunContinuationsAsynchronously);
            var loads = 0;
            using var s = new FullTrackSession(library, t => ++loads == 1 ? completion.Task : Task.FromResult(PreparedTrack.Load(t)));
            var first = s.RequestAsync(GameState.CalmSpace, true);
            var obsolete = s.RequestAsync(GameState.Combat, true);
            var latest = s.RequestAsync(GameState.CalmSpace, true);
            Assert(loads == 1, "Started concurrent song decoding.");
            completion.SetResult(PreparedTrack.Load(library.For("ambient synth", GameState.CalmSpace)[0]));
            Task.WhenAll(first, obsolete, latest).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 7);
            Assert(loads == 2 && s.Player.Snapshot().Starts == 1 && s.Player.Snapshot().Track?.State == GameState.CalmSpace, "Stale preparation played.");
        });
        check("Invalid playback commands and out-of-range authored cues are rejected", () =>
        {
            var p = new FullTrackPlayer(); Throws<ArgumentException>(() => p.SetVolume(float.NaN));
            Throws<ArgumentException>(() => p.Submit(new(TrackCommandKind.Play)));
            Throws<ArgumentException>(() => p.Submit(new(TrackCommandKind.Silence, FadeSeconds: double.NaN)));
            Throws<ArgumentException>(() => p.Render(new float[3]));
            File.WriteAllText(Path.Combine(root, "track-cues.json"), JsonSerializer.Serialize(new Dictionary<string, TrackCues> { [library.Tracks[0].Id] = new(QuietSeconds: double.MaxValue) }));
            var changed = TrackLibraryLoader.Load(root);
            Assert(changed.Tracks.Count == 4 && changed.Warnings.Any(w => w.Contains("Entry cues")), "Bad cue accepted.");
            File.Delete(Path.Combine(root, "track-cues.json"));
        });
        check("Genre-first import warns on misplaced songs and resolves genre-prefixed authored cues", () =>
        {
            var layout = Path.Combine(temp, "layout-library");
            var valid = Path.Combine(layout, "ambient synth", "calm space"); Directory.CreateDirectory(valid);
            File.Copy(library.Tracks[0].Path, Path.Combine(valid, "song.wav"));
            var legacy = Path.Combine(layout, "combat", "ambient synth"); Directory.CreateDirectory(legacy);
            File.Copy(library.Tracks[0].Path, Path.Combine(legacy, "legacy.wav"));
            File.Copy(library.Tracks[0].Path, Path.Combine(layout, "ambient synth", "uncategorized.wav"));
            File.WriteAllText(Path.Combine(layout, "track-cues.json"), JsonSerializer.Serialize(new Dictionary<string, TrackCues>
            {
                ["ambient synth/calm space/song.wav"] = new(QuietSeconds: 2, StrongSeconds: 10)
            }));
            var imported = TrackLibraryLoader.Load(layout);
            Assert(imported.Tracks.Count == 1 && imported.Warnings.Count == 2, "Misplaced files were silently categorized.");
            Assert(imported.Tracks[0].Genre == "ambient synth" && imported.Tracks[0].QuietSeconds == 2 && imported.Tracks[0].StrongSeconds == 10, "Genre-prefixed cue did not apply.");
            Throws<InvalidDataException>(() => TrackLibraryLoader.Load(Path.Combine(layout, "ambient synth")));
        });
        check("Switching genres changes playback, preserves available situations, and falls back when missing", () =>
        {
            var genres = Path.Combine(temp, "multi-genre-library");
            foreach (var (genre, state) in new[] { ("ambient synth", "calm space"), ("ambient synth", "combat"), ("modern hiphop", "combat") })
            {
                var dir = Path.Combine(genres, genre, state); Directory.CreateDirectory(dir);
                File.Copy(library.Tracks[0].Path, Path.Combine(dir, "song.wav"));
            }
            var imported = TrackLibraryLoader.Load(genres);
            Assert(imported.Genres.Length == 2, "Genre menu candidates missing.");
            using var s = new FullTrackSession(imported, t => Task.FromResult(PreparedTrack.Load(t))) { AllowQuietBreaks = false, Entry = TrackEntry.Beginning };
            s.RequestAsync(GameState.CalmSpace, true).GetAwaiter().GetResult(); Advance(s.Player, 48000 * 7);
            s.SetGenreAsync("modern hiphop").GetAwaiter().GetResult(); Advance(s.Player, 48000 * 7);
            Assert(s.State == GameState.Combat && !s.HasState(GameState.CalmSpace) && s.Player.Snapshot().Track?.Genre == "modern hiphop", "Missing situation did not fall back within the new genre.");
            s.SetGenreAsync("ambient synth").GetAwaiter().GetResult(); Advance(s.Player, 48000 * 7);
            Assert(s.State == GameState.Combat && s.HasState(GameState.CalmSpace) && s.Player.Snapshot().Track?.Genre == "ambient synth", "Available situation was not preserved across genres.");
            var starts = s.Player.Snapshot().Starts;
            s.SetGenreAsync("ambient synth").GetAwaiter().GetResult(); Advance(s.Player, 48000);
            Assert(s.Player.Snapshot().Starts == starts && s.Player.Snapshot().ClampedSamples == 0, "Same-genre selection restarted or clipped playback.");
        });
        if (uploadedFolder is not null) check("Uploaded Suno files import unchanged and play proposed cues without clamping", () =>
        {
            var real = TrackLibraryLoader.Load(uploadedFolder);
            Assert(real.Tracks.Count == 5 && real.Warnings.Count == 0, "Uploaded tracks rejected.");
            foreach (var track in real.Tracks)
            {
                var p = new FullTrackPlayer(); p.Submit(new(TrackCommandKind.Play, PreparedTrack.Load(track), track.StrongSeconds, .1));
                var output = new float[96000]; p.Render(output);
                Assert(output.Any(v => Math.Abs(v) > .001) && p.Snapshot().ClampedSamples == 0, "Cue is silent or clipping.");
                Console.WriteLine($"      {track.Id}: {track.DurationSeconds:0.00}s; quiet {track.QuietSeconds:0.0}s; strong {track.StrongSeconds:0.0}s; gain {track.Gain:0.000}");
            }
        });
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private static void Advance(FullTrackPlayer player, int frames)
    {
        var buffer = new float[2048];
        while (frames > 0) { var count = Math.Min(frames, 1024); player.Render(buffer.AsSpan(0, count * 2)); frames -= count; }
    }
}
