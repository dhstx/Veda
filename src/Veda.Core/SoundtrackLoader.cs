using System.Text.Json;

namespace Veda.Core;

public static class SoundtrackLoader
{
    public static LoadedSoundtrack Load(string directory)
    {
        var root = Path.GetFullPath(directory);
        var manifest = Path.Combine(root, "soundtrack.json");
        if (!File.Exists(manifest)) throw new InvalidDataException("Choose a folder containing soundtrack.json.");
        if (new FileInfo(manifest).Length > 128 * 1024) throw new InvalidDataException("The soundtrack manifest is too large.");
        var definition = JsonSerializer.Deserialize<SoundtrackDefinition>(File.ReadAllText(manifest), SoundtrackDefinition.JsonOptions)
            ?? throw new InvalidDataException("The soundtrack manifest is empty.");
        definition.Validate();
        var decodedBytes = (long)definition.ExpectedFrames * 2 * sizeof(float) * definition.Layers.Count;
        if (decodedBytes > 128L * 1024 * 1024) throw new InvalidDataException("Decoded stems exceed the 128 MiB prototype budget.");
        var layers = new List<AudioLayer>();
        foreach (var layer in definition.Layers)
        {
            var path = ResolveAsset(root, layer.File);
            var wav = WavFile.Read(path, definition.SampleRate * 60);
            if (wav.Samples.Length != definition.ExpectedFrames * definition.Channels)
                throw new InvalidDataException($"{layer.File} must contain exactly {definition.ExpectedFrames} frames. Export every stem over the same loop range.");
            layers.Add(new AudioLayer(layer.Id, wav.Samples));
        }
        return new LoadedSoundtrack(definition, layers);
    }

    public static string ResolveAsset(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new InvalidDataException("Stem paths must be relative to the soundtrack folder.");
        // Normalize either separator so manifests behave the same on Windows and in portable checks.
        var normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, normalized));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root, comparison) || !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Stem paths must point to WAV files inside the soundtrack folder.");
        return path;
    }
}
