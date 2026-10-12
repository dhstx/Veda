using Veda.Core;
using Veda.Render;

if (args.Length == 2 && args[0] == "--generate-pack")
{
    DemoScore.Generate(args[1]);
    Console.WriteLine($"Generated demo stems in {Path.GetFullPath(args[1])}");
    return 0;
}
if (args.Length == 3 && args[0] == "--render")
{
    var pack = SoundtrackLoader.Load(args[1]);
    var mixer = new AdaptiveMixer(pack);
    var director = new MusicDirector(pack);
    var sequence = new[] { GameState.CalmSpace, GameState.Travel, GameState.Uncertain, GameState.Combat, GameState.Escape, GameState.Relief, GameState.CalmSpace };
    const int secondsPerState = 12;
    var audio = new float[pack.Definition.SampleRate * pack.Definition.Channels * sequence.Length * secondsPerState];
    var segmentSamples = pack.Definition.SampleRate * pack.Definition.Channels * secondsPerState;
    for (var i = 0; i < sequence.Length; i++)
    {
        mixer.Submit(director.Plan(sequence[i]));
        Console.WriteLine($"{i * secondsPerState,2}s: {GameStateLabels.Display(sequence[i])}");
        for (var offset = i * segmentSamples; offset < (i + 1) * segmentSamples; offset += 960)
            mixer.Render(audio.AsSpan(offset, Math.Min(960, (i + 1) * segmentSamples - offset)));
    }
    WavFile.WritePcm16(args[2], pack.Definition.SampleRate, pack.Definition.Channels, audio);
    var snapshot = mixer.Snapshot();
    Console.WriteLine($"Rendered {Path.GetFullPath(args[2])}. Peak: {audio.Max(x => Math.Abs(x)):0.000}; clamped samples: {snapshot.ClampedSamples}.");
    return snapshot.ClampedSamples == 0 ? 0 : 1;
}
if (args.Length == 3 && args[0] == "--full-songs") return FullSongPreview.Run(args[1], args[2]);
Console.Error.WriteLine("Usage: dotnet run --project tools/Veda.Render -- --generate-pack <folder>");
Console.Error.WriteLine("   or: dotnet run --project tools/Veda.Render -- --render <soundtrack-folder> <output.wav>");
Console.Error.WriteLine("   or: dotnet run --project tools/Veda.Render -- --full-songs <library-folder> <output.wav>");
return 2;
