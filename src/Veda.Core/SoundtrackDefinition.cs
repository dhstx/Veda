using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veda.Core;

public sealed record LayerDefinition
{
    public required string Id { get; init; }
    public required string File { get; init; }
}

public sealed record SoundtrackDefinition
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Title { get; init; }
    public double TempoBpm { get; init; }
    public int BeatsPerBar { get; init; }
    public int LoopBars { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public required List<LayerDefinition> Layers { get; init; }
    public required Dictionary<GameState, Dictionary<string, float>> States { get; init; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<GameState>(allowIntegerValues: false) }
    };

    [JsonIgnore]
    public int ExpectedFrames => checked((int)Math.Round(
        SampleRate * 60d / TempoBpm * BeatsPerBar * LoopBars,
        MidpointRounding.AwayFromZero));

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("Only soundtrack schema version 1 is supported.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title))
            throw new InvalidDataException("The soundtrack needs an id and title.");
        if (!double.IsFinite(TempoBpm) || TempoBpm < 40 || TempoBpm > 240)
            throw new InvalidDataException("tempoBpm must be between 40 and 240.");
        if (BeatsPerBar < 1 || BeatsPerBar > 12 || LoopBars < 1 || LoopBars > 64)
            throw new InvalidDataException("Use 1–12 beats per bar and 1–64 loop bars.");
        if (SampleRate != 48000 || Channels != 2)
            throw new InvalidDataException("This prototype requires 48,000 Hz stereo WAV stems.");
        if (ExpectedFrames > SampleRate * 60)
            throw new InvalidDataException("A prototype loop must be 60 seconds or shorter.");
        if (Layers is null || Layers.Count < 1 || Layers.Count > 8)
            throw new InvalidDataException("A soundtrack needs 1–8 layers.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layer in Layers)
        {
            if (layer is null || string.IsNullOrWhiteSpace(layer.Id) || string.IsNullOrWhiteSpace(layer.File))
                throw new InvalidDataException("Every layer needs an id and WAV file.");
            if (!ids.Add(layer.Id)) throw new InvalidDataException($"Duplicate layer id: {layer.Id}.");
        }
        if (States is null || States.Count != Enum.GetValues<GameState>().Length)
            throw new InvalidDataException("Define exactly the six prototype states.");
        foreach (var state in Enum.GetValues<GameState>())
        {
            if (!States.TryGetValue(state, out var gains) || gains is null || gains.Count != ids.Count)
                throw new InvalidDataException($"{state} must define a gain for every layer.");
            foreach (var (id, gain) in gains)
                if (!ids.Contains(id) || !float.IsFinite(gain) || gain < 0 || gain > 1)
                    throw new InvalidDataException($"Invalid layer gain in {state}: {id}.");
        }
    }
}

public sealed record AudioLayer(string Id, float[] Samples);

public sealed class LoadedSoundtrack
{
    public SoundtrackDefinition Definition { get; }
    public IReadOnlyList<AudioLayer> Layers { get; }
    public int FrameCount { get; }
    public FrozenDictionary<GameState, FrozenDictionary<string, float>> StateGains { get; }

    public LoadedSoundtrack(SoundtrackDefinition definition, IReadOnlyList<AudioLayer> layers)
    {
        definition.Validate();
        if (layers.Count != definition.Layers.Count) throw new InvalidDataException("Layer count does not match the manifest.");
        FrameCount = definition.ExpectedFrames;
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i].Id != definition.Layers[i].Id || layers[i].Samples.Length != FrameCount * definition.Channels)
                throw new InvalidDataException($"Layer {i + 1} is not aligned with the manifest.");
        }
        Definition = definition;
        Layers = layers.ToArray();
        StateGains = definition.States.ToFrozenDictionary(
            x => x.Key, x => x.Value.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
