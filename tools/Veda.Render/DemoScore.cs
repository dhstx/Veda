using System.Text.Json;
using Veda.Core;

namespace Veda.Render;

internal static class DemoScore
{
    private static readonly double[] Melody = [293.6648, 349.2282, 440d, 349.2282, 391.9954, 440d, 523.2511, 440d];
    public static void Generate(string directory)
    {
        const int rate = 48000, frames = 480000;
        var ids = new[] { "atmosphere", "pulse", "tension", "combat", "escape", "relief" };
        var definition = new SoundtrackDefinition
        {
            Id = "demo-void", Title = "The Void · Synthetic demo", TempoBpm = 96,
            BeatsPerBar = 4, LoopBars = 4, SampleRate = rate, Channels = 2,
            Layers = ids.Select(id => new LayerDefinition { Id = id, File = id + ".wav" }).ToList(),
            States = new Dictionary<GameState, Dictionary<string, float>>
            {
                [GameState.CalmSpace] = Gains(ids, .75f, 0, 0, 0, 0, 0),
                [GameState.Travel] = Gains(ids, .70f, .32f, 0, 0, .15f, 0),
                [GameState.Uncertain] = Gains(ids, .65f, .22f, .50f, 0, 0, 0),
                [GameState.Combat] = Gains(ids, .30f, .30f, .38f, .80f, 0, 0),
                [GameState.Escape] = Gains(ids, .45f, .55f, .16f, .25f, .60f, 0),
                [GameState.Relief] = Gains(ids, .65f, .08f, 0, 0, .12f, .55f)
            }
        };
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "soundtrack.json"), JsonSerializer.Serialize(definition, SoundtrackDefinition.JsonOptions) + Environment.NewLine);
        foreach (var id in ids)
        {
            var audio = new float[frames * 2];
            for (var frame = 0; frame < frames; frame++)
            {
                var t = frame / (double)rate;
                var edge = Math.Min(1, Math.Min(t / .04, (10 - t) / .04));
                edge = .5 - .5 * Math.Cos(Math.PI * edge);
                var beat = t % .625;
                var halfBeat = t % .3125;
                var noteIndex = (int)(t / 1.25) % 8;
                var melody = Melody[noteIndex];
                var noise = Noise(frame);
                for (var channel = 0; channel < 2; channel++)
                {
                    var phase = channel * .13;
                    var value = id switch
                    {
                        "atmosphere" => .055 * (Sin(73.4162, t, phase) + .65 * Sin(110, t, phase) + .5 * Sin(146.8324, t, phase)) * (.85 + .15 * Math.Sin(t * Math.PI / 5)),
                        "pulse" => .11 * Math.Exp(-beat * 8) * Sin(146.8324, t, phase),
                        "tension" => .035 * (Sin(155.5635, t, phase) + Sin(220, t, phase)) * (.6 + .4 * Math.Sin(t * Math.PI * 3.2)),
                        "combat" => .16 * Math.Exp(-beat * 20) * Math.Sin(2 * Math.PI * (55 * beat + 35 * .025 * (1 - Math.Exp(-beat / .025))))
                            + .055 * Math.Exp(-halfBeat * 65) * noise + .045 * Sin(73.4162, t, phase),
                        "escape" => .075 * Sin(melody, t, phase) * Math.Exp(-(t % 1.25) * 1.7) * Math.Min(1, (t % 1.25) / .02),
                        "relief" => .045 * (Sin(293.6648, t, phase) + .6 * Sin(349.2282, t, phase) + .45 * Sin(440, t, phase)),
                        _ => 0
                    };
                    audio[frame * 2 + channel] = (float)(value * edge);
                }
            }
            WavFile.WritePcm16(Path.Combine(directory, id + ".wav"), rate, 2, audio);
        }
    }

    private static Dictionary<string, float> Gains(string[] ids, params float[] values) => ids.Select((id, i) => (id, values[i])).ToDictionary(x => x.id, x => x.Item2);
    private static double Sin(double frequency, double t, double phase) => Math.Sin(2 * Math.PI * frequency * t + phase);
    private static double Noise(int frame)
    {
        var hash = unchecked((uint)frame * 747796405u + 2891336453u);
        hash = unchecked(((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u);
        return ((hash >> 22) ^ hash) / (double)uint.MaxValue * 2 - 1;
    }
}
