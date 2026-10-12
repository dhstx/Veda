using NAudio.Wave;
using Veda.Core;

namespace Veda.Audio.Windows;

public sealed class WasapiOutput : IDisposable, IAsyncDisposable
{
    private readonly WasapiPlayer _player;
    private bool _disposed;
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    private WasapiOutput(WasapiPlayer player)
    {
        _player = player;
        _player.PlaybackStopped += OnStopped;
    }

    public static async Task<WasapiOutput> CreateAsync(IAudioRenderer mixer)
    {
        // Shared mode preserves game effects and dialogue. Do not take exclusive control of the device.
        var player = await new WasapiPlayerBuilder()
            .WithDefaultDeviceStreamRouting()
            .WithLatency(80)
            .WithMmcssThreadPriority("Audio")
            .BuildAsync();
        try
        {
            player.Init(new MixerSampleProvider(mixer).ToWaveProvider());
            return new WasapiOutput(player);
        }
        catch
        {
            await player.DisposeAsync();
            throw;
        }
    }

    public void Play() { ObjectDisposedException.ThrowIf(_disposed, this); _player.Play(); }
    public void Pause() { ObjectDisposedException.ThrowIf(_disposed, this); _player.Pause(); }
    private void OnStopped(object? sender, StoppedEventArgs args) => PlaybackStopped?.Invoke(this, args);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.PlaybackStopped -= OnStopped;
        _player.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _player.PlaybackStopped -= OnStopped;
        await _player.DisposeAsync();
    }

    private sealed class MixerSampleProvider(IAudioRenderer mixer) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(mixer.SampleRate, mixer.Channels);
        public int Read(Span<float> buffer) => mixer.Render(buffer);
    }
}
