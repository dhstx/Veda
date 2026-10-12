namespace Veda.Core;

/// <summary>Exactly one audio thread calls Render; control and snapshots run elsewhere.</summary>
public interface IAudioRenderer
{
    int SampleRate { get; }
    int Channels { get; }
    int Render(Span<float> destination);
    void SetVolume(float value);
}
