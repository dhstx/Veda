using System.Text.Json;
using Veda.Core;

namespace Veda.Render;

internal static class FullSongPreview
{
    public static int Run(string root, string output)
    {
        var library = TrackLibraryLoader.Load(root);
        var calm = library.For(library.Genres[0], GameState.CalmSpace);
        var combat = library.For(library.Genres[0], GameState.Combat);
        if (calm.Length < 2 || combat.Length < 2) throw new InvalidDataException("Preview requires two calm and two combat tracks.");
        var p = new FullTrackPlayer(); p.SetVolume(.7f);
        var audio = new float[48000 * 60 * 2];
        var events = new Dictionary<int, TrackPlaybackCommand>
        {
            [0] = new(TrackCommandKind.Play, PreparedTrack.Load(calm[0]), calm[0].QuietSeconds, 2),
            [12] = new(TrackCommandKind.Play, PreparedTrack.Load(combat[0]), combat[0].StrongSeconds, 2),
            [24] = new(TrackCommandKind.Play, PreparedTrack.Load(combat[1]), combat[1].StrongSeconds, 4),
            [36] = new(TrackCommandKind.Play, PreparedTrack.Load(calm[1]), calm[1].QuietSeconds, 6),
            [48] = new(TrackCommandKind.Silence, FadeSeconds: 4),
            [55] = new(TrackCommandKind.Play, PreparedTrack.Load(calm[0]), calm[0].QuietSeconds, 2),
            [58] = new(TrackCommandKind.Silence, FadeSeconds: 2)
        };
        for (var second = 0; second < 60; second++)
        {
            if (events.TryGetValue(second, out var command))
            {
                p.Submit(command);
                Console.WriteLine($"{second}s: {command.Kind} {command.Track?.Info.Id}, entry {command.EntrySeconds:0.0}s, fade {command.FadeSeconds:0.0}s");
            }
            for (var frame = 0; frame < 48000; frame += 480)
                p.Render(audio.AsSpan(second * 96000 + frame * 2, 960));
        }
        WavFile.WritePcm16(output, 48000, 2, audio);
        var cueFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "estimated-track-cues.json");
        File.WriteAllText(cueFile, JsonSerializer.Serialize(library.Tracks.ToDictionary(t => t.Id,
            t => new TrackCues(t.QuietSeconds, t.StrongSeconds, t.EndSeconds)), new JsonSerializerOptions { WriteIndented = true }));
        var report = library.Tracks.Select(t => $"| {t.Id} | {t.DurationSeconds:0.00} | {t.QuietSeconds:0} | {t.StrongSeconds:0} | {t.EndSeconds:0.00} |");
        File.WriteAllText(Path.ChangeExtension(output, ".md"), "| File | Duration (s) | Quiet cue (s) | Strong cue (s) | End (s) |\n| --- | ---: | ---: | ---: | ---: |\n" + string.Join("\n", report));
        var snapshot = p.Snapshot();
        Console.WriteLine($"60-second preview: peak {audio.Max(v => Math.Abs(v)):0.000}, clamped samples {snapshot.ClampedSamples}.");
        return snapshot.ClampedSamples == 0 ? 0 : 1;
    }
}
