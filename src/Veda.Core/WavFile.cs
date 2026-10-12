using System.Buffers.Binary;
using System.Text;

namespace Veda.Core;

public sealed record DecodedWav(int SampleRate, int Channels, float[] Samples);

/// <summary>Deliberately small format contract: RIFF PCM16 or IEEE float32, stereo, 48 kHz.</summary>
public static class WavFile
{
    public static DecodedWav Read(string path, int maxFrames)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (stream.Length < 12 || FourCc(reader) != "RIFF") throw new InvalidDataException("Expected a RIFF WAV file.");
        var riffEnd = 8L + reader.ReadUInt32();
        if (riffEnd > stream.Length || riffEnd < 12 || FourCc(reader) != "WAVE")
            throw new InvalidDataException("Invalid or truncated WAV header.");

        ushort format = 0, channels = 0, bits = 0, blockAlign = 0;
        uint rate = 0, byteRate = 0;
        long dataOffset = -1;
        uint dataLength = 0;
        var hasFormat = false;
        while (stream.Position + 8 <= riffEnd)
        {
            var id = FourCc(reader);
            var length = reader.ReadUInt32();
            var chunkEnd = stream.Position + length;
            var paddedEnd = chunkEnd + (length & 1);
            if (paddedEnd > riffEnd) throw new InvalidDataException("Truncated WAV chunk.");
            if (id == "fmt ")
            {
                if (hasFormat || length < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                format = reader.ReadUInt16(); channels = reader.ReadUInt16();
                rate = reader.ReadUInt32(); byteRate = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16(); bits = reader.ReadUInt16();
                hasFormat = true;
            }
            else if (id == "data")
            {
                if (dataOffset >= 0) throw new InvalidDataException("Multiple data chunks are not supported.");
                dataOffset = stream.Position; dataLength = length;
            }
            stream.Position = paddedEnd;
        }
        if (!hasFormat || dataOffset < 0 || channels != 2 || rate != 48000 ||
            !((format == 1 && bits == 16) || (format == 3 && bits == 32)) ||
            blockAlign != channels * bits / 8 || byteRate != rate * blockAlign)
            throw new InvalidDataException("Export as stereo 48 kHz PCM16 or IEEE float32 WAV (ordinary RIFF, not WAVE_EXTENSIBLE).");
        if (dataLength == 0 || dataLength % blockAlign != 0 || dataLength / blockAlign > maxFrames)
            throw new InvalidDataException("Invalid WAV length or audio exceeds the permitted duration.");
        stream.Position = dataOffset;
        var samples = new float[checked((int)(dataLength / (bits / 8)))];
        // Decode in bounded chunks rather than keeping a second full-song byte array.
        var bytes = new byte[65536];
        var bytesPerSample = bits / 8;
        for (var start = 0; start < samples.Length;)
        {
            var count = Math.Min(samples.Length - start, bytes.Length / bytesPerSample);
            stream.ReadExactly(bytes.AsSpan(0, count * bytesPerSample));
            for (var i = 0; i < count; i++)
            {
                var value = bits == 16
                    ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2)) / 32768f
                    : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4, 4)));
                if (!float.IsFinite(value) || value < -1 || value > 1)
                    throw new InvalidDataException("WAV samples must be finite and between -1 and 1.");
                samples[start + i] = value;
            }
            start += count;
        }
        return new DecodedWav((int)rate, channels, samples);
    }

    public static void WritePcm16(string path, int sampleRate, int channels, ReadOnlySpan<float> samples)
    {
        if (sampleRate <= 0 || channels <= 0 || samples.Length % channels != 0)
            throw new ArgumentException("Invalid audio format or incomplete audio frame.");
        var dataBytes = checked(samples.Length * 2);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(checked(36 + dataBytes));
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)channels); writer.Write(sampleRate);
        writer.Write(checked(sampleRate * channels * 2)); writer.Write((ushort)(channels * 2)); writer.Write((ushort)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
        foreach (var sample in samples)
        {
            if (!float.IsFinite(sample)) throw new ArgumentException("Audio contains a non-finite sample.");
            writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * 32767f));
        }
    }

    private static string FourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
