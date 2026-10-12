using System.Collections.Frozen;
using System.Text.Json;
using Veda.Core;

var packPath = args.Length > 0 ? args[0] : "assets/soundtracks/demo-void";
var failures = 0;
var temp = Path.Combine(Path.GetTempPath(), "veda-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    Check("Manifest and every demo WAV agree on one musical clock", () =>
    {
        var pack = SoundtrackLoader.Load(packPath);
        Assert(pack.Layers.Count == 6 && pack.FrameCount == 480000, "Unexpected demo dimensions.");
        Assert(pack.Layers.All(x => x.Samples.Length == 960000), "Misaligned stem.");
        Assert(pack.Layers.All(x => x.Samples.Take(2).All(v => Math.Abs(v) < .0001)), "Loop begins with a click risk.");
        Assert(pack.Layers.All(x => x.Samples.TakeLast(2).All(v => Math.Abs(v) < .0001)), "Loop ends with a click risk.");
    });
    Check("Transitions are independent of device callback size", () =>
    {
        var pack = SoundtrackLoader.Load(packPath);
        var small = RenderSequence(pack, 127);
        var large = RenderSequence(pack, 2048);
        Assert(small.AsSpan().SequenceEqual(large), "Changing callback size changed the soundtrack.");
        Assert(small.All(float.IsFinite) && small.Any(x => Math.Abs(x) > .01f), "Output must be audible and finite.");
    });
    Check("Latest request wins, and full scenario finishes in calm", () =>
    {
        var pack = SoundtrackLoader.Load(packPath);
        var mixer = new AdaptiveMixer(pack);
        var director = new MusicDirector(pack);
        foreach (var state in Enum.GetValues<GameState>()) mixer.Submit(director.Plan(state));
        Advance(mixer, 48000 * 12);
        Assert(mixer.Snapshot().ActiveState == GameState.Relief, "An obsolete request was played.");
        mixer.Submit(director.Plan(GameState.CalmSpace));
        Advance(mixer, 48000 * 12);
        Assert(mixer.Snapshot().ActiveState == GameState.CalmSpace && !mixer.Snapshot().Transitioning, "Calm did not settle.");
        Assert(mixer.Snapshot().ClampedSamples == 0, "Demo mix clipped.");
    });
    Check("Combat responds immediately; travel waits for a bar", () =>
    {
        var pack = SoundtrackLoader.Load(packPath);
        var mixer = new AdaptiveMixer(pack);
        var director = new MusicDirector(pack);
        Advance(mixer, 48000 * 6 + 100);
        mixer.Submit(director.Plan(GameState.Travel));
        Advance(mixer, 100);
        Assert(mixer.Snapshot().ActiveState == GameState.CalmSpace, "Travel ignored bar quantization.");
        mixer.Submit(director.Plan(GameState.Combat));
        Advance(mixer, 1);
        Assert(mixer.Snapshot().ActiveState == GameState.Combat, "Combat waited for a bar.");
    });
    Check("Mute fades to silence without stopping the musical clock", () =>
    {
        var mixer = new AdaptiveMixer(SoundtrackLoader.Load(packPath));
        Advance(mixer, 48000 * 6);
        var before = mixer.Snapshot().RenderedFrames;
        mixer.SetVolume(0);
        Advance(mixer, 48000);
        var output = new float[960]; mixer.Render(output);
        Assert(output.All(x => x == 0), "Mute did not settle to exact silence.");
        Assert(mixer.Snapshot().RenderedFrames > before, "Mute stopped the clock.");
    });
    Check("Mix interruption keeps gain changes continuous", () =>
    {
        var original = SoundtrackLoader.Load(packPath);
        var constantLayers = original.Layers.Select(x => new AudioLayer(x.Id, Enumerable.Repeat(.04f, x.Samples.Length).ToArray())).ToArray();
        var pack = new LoadedSoundtrack(original.Definition, constantLayers);
        var mixer = new AdaptiveMixer(pack); var director = new MusicDirector(pack);
        Advance(mixer, 48000 * 6);
        mixer.Submit(director.Plan(GameState.Combat)); Advance(mixer, 1000);
        var before = new float[2]; mixer.Render(before);
        mixer.Submit(director.Plan(GameState.Relief) with { Boundary = TransitionBoundary.Immediate });
        var after = new float[2]; mixer.Render(after);
        Assert(Math.Abs(before[0] - after[0]) < .00001f, "A new fade jumped to an old gain.");
    });
    Check("Stereo position survives the mixer", () =>
    {
        var original = SoundtrackLoader.Load(packPath);
        var stereo = new float[original.FrameCount * 2];
        for (var i = 0; i < stereo.Length; i += 2) { stereo[i] = .1f; stereo[i + 1] = -.05f; }
        var layers = original.Layers.Select((x, i) => new AudioLayer(x.Id, i == 0 ? stereo : new float[stereo.Length])).ToArray();
        var mixer = new AdaptiveMixer(new LoadedSoundtrack(original.Definition, layers));
        Advance(mixer, 48000 * 6);
        var output = new float[2]; mixer.Render(output);
        Assert(output[0] > 0 && output[1] < 0 && Math.Abs(output[0] + output[1] * 2) < .000001f, "Stereo channels changed.");
    });
    Check("WAV round trip preserves sample values within PCM16 precision", () =>
    {
        var path = Path.Combine(temp, "roundtrip.wav");
        var samples = new[] { -.9f, .3f, 0, 0, .99f, -.99f };
        WavFile.WritePcm16(path, 48000, 2, samples);
        var decoded = WavFile.Read(path, 10);
        Assert(decoded.Samples.Zip(samples).All(x => Math.Abs(x.First - x.Second) < .00007), "PCM round trip lost precision.");
        File.WriteAllBytes(path, File.ReadAllBytes(path).Take(45).ToArray());
        Throws<InvalidDataException>(() => WavFile.Read(path, 10));
    });
    Check("Malformed, misaligned, and escaping soundtrack inputs are rejected", () =>
    {
        var original = SoundtrackLoader.Load(packPath);
        Throws<InvalidDataException>(() => (original.Definition with { TempoBpm = double.NaN }).Validate());
        Throws<InvalidDataException>(() => (original.Definition with { Layers = [] }).Validate());
        Throws<InvalidDataException>(() => (original.Definition with { States = [] }).Validate());
        Throws<InvalidDataException>(() => new LoadedSoundtrack(original.Definition, [new AudioLayer("bad", [0f, 0f])]));
        foreach (var path in new[] { "../outside.wav", "..\\outside.wav", "/outside.wav", "C:\\outside.wav", "not-a-wav.mp3" })
            Throws<InvalidDataException>(() => SoundtrackLoader.ResolveAsset(temp, path));
        var json = File.ReadAllText(Path.Combine(packPath, "soundtrack.json")).Replace("\"title\":", "\"typo\":");
        Throws<JsonException>(() => JsonSerializer.Deserialize<SoundtrackDefinition>(json, SoundtrackDefinition.JsonOptions));
    });
    Check("Rapid producers do not queue old arrangements", () =>
    {
        var mixer = new AdaptiveMixer(SoundtrackLoader.Load(packPath));
        var director = new MusicDirector(SoundtrackLoader.Load(packPath));
        Parallel.For(0, 2000, i => mixer.Submit(director.Plan((GameState)(i % 6))));
        mixer.Submit(director.Plan(GameState.Escape));
        Advance(mixer, 48000 * 6);
        Assert(mixer.Snapshot().ActiveState == GameState.Escape, "The final producer request was lost.");
    });
    Check("The audio callback allocates no managed memory after warmup", () =>
    {
        var mixer = new AdaptiveMixer(SoundtrackLoader.Load(packPath));
        var buffer = new float[960];
        for (var i = 0; i < 100; i++) mixer.Render(buffer);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 500; i++) mixer.Render(buffer);
        Assert(GC.GetAllocatedBytesForCurrentThread() == before, "The callback allocated memory.");
    });
    Check("Invalid runtime controls cannot poison audio", () =>
    {
        var pack = SoundtrackLoader.Load(packPath); var director = new MusicDirector(pack); var mixer = new AdaptiveMixer(pack);
        Throws<ArgumentException>(() => director.Plan(GameState.Combat, float.NaN));
        Throws<ArgumentException>(() => mixer.SetVolume(float.PositiveInfinity));
        Throws<ArgumentException>(() => mixer.Submit(new MixPlan(GameState.Combat,
            new Dictionary<string, float>().ToFrozenDictionary(), TransitionBoundary.Immediate, 1)));
        Throws<ArgumentException>(() => mixer.Render(new float[3]));
    });
    FullTrackChecks.Run(Check, temp, args.Length > 1 ? args[1] : null);
    CatalogChecks.Run(Check, temp);
}
finally { Directory.Delete(temp, recursive: true); }
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;

void Check(string title, Action body)
{
    try { body(); Console.WriteLine($"PASS  {title}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL  {title}: {error.Message}"); }
}
static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static void Advance(AdaptiveMixer mixer, int frames)
{
    var buffer = new float[2048];
    while (frames > 0)
    {
        var chunk = Math.Min(frames, buffer.Length / 2);
        mixer.Render(buffer.AsSpan(0, chunk * 2)); frames -= chunk;
    }
}
static float[] RenderSequence(LoadedSoundtrack pack, int blockFrames)
{
    var mixer = new AdaptiveMixer(pack); var director = new MusicDirector(pack);
    var sequence = new[] { GameState.CalmSpace, GameState.Travel, GameState.Uncertain, GameState.Combat, GameState.Escape, GameState.Relief, GameState.CalmSpace };
    const int durationFrames = 48000 * 12;
    var result = new float[durationFrames * sequence.Length * 2];
    for (var i = 0; i < sequence.Length; i++)
    {
        mixer.Submit(director.Plan(sequence[i]));
        for (var frame = 0; frame < durationFrames; frame += blockFrames)
            mixer.Render(result.AsSpan((i * durationFrames + frame) * 2, Math.Min(blockFrames, durationFrames - frame) * 2));
    }
    Assert(mixer.Snapshot().ClampedSamples == 0, "The scenario reached the peak guard.");
    return result;
}
