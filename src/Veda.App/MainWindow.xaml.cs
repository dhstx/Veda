using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using NAudio.Wave;
using Veda.Audio.Windows;
using Veda.Core;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;

namespace Veda.App;

public sealed partial class MainWindow : Window
{
    private AdaptiveMixer? _mixer;
    private MusicDirector? _director;
    private FullTrackSession? _session;
    private MusicCatalog? _catalog;
    private bool _updatingUi;
    private string? _manageGenreId, _manageSituationId;
    private double? _pendingCrossfade;
    private readonly DispatcherTimer _crossfadeTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly CancellationTokenSource _loadCancellation = new();
    private WasapiOutput? _output;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private GameState _requested = GameState.CalmSpace;
    private bool _playing, _loading, _loaded, _closed;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Veda · Prototype 0.2.0";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Resize(new SizeInt32(860, 870));
        _timer.Tick += async (_, _) =>
        {
            if (_session is not null && _playing && !_loading) await _session.TickAsync();
            if (!_closed) RefreshStatus();
        };
        _crossfadeTimer.Tick += async (_, _) =>
        {
            _crossfadeTimer.Stop();
            if (_loading) { _crossfadeTimer.Start(); return; }
            if (_pendingCrossfade is not double seconds || _catalog is null) return;
            _pendingCrossfade = null;
            await EditCatalogAsync(() => _catalog.SetCrossfade(seconds));
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop(); _crossfadeTimer.Stop();
            if (!_loading && _catalog is not null && _pendingCrossfade is double seconds)
            {
                try { _catalog.SetCrossfade(seconds); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            _loadCancellation.Cancel();
            _session?.Dispose();
            _output?.Dispose();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_loaded) return;
        _loaded = true;
        await LoadPackAsync(Path.Combine(AppContext.BaseDirectory, "Soundtracks", "demo-void"));
        if (!_closed) _timer.Start();
    }

    private async Task LoadPackAsync(string path)
    {
        if (_loading || _closed) return;
        _loading = true;
        SetEnabled(false);
        StatusText.Text = "Loading soundtrack…";
        WasapiOutput? replacement = null;
        try
        {
            var pack = await Task.Run(() => SoundtrackLoader.Load(path));
            if (_closed) return;
            var mixer = new AdaptiveMixer(pack);
            mixer.SetVolume((float)VolumeSlider.Value / 100);
            var director = new MusicDirector(pack);
            mixer.Submit(director.Plan(_requested, (float)50d / 100, (float)ResponseSlider.Value / 100)
                with { Boundary = TransitionBoundary.Immediate });
            replacement = await WasapiOutput.CreateAsync(mixer);
            if (_closed) { await replacement.DisposeAsync(); replacement = null; return; }
            var wasPlaying = _playing;
            // Detach the old device before releasing it. If disposal or the new
            // device's Play call fails, the UI must not keep claiming playback.
            var previous = _output;
            _output = null; _mixer = null; _director = null;
            _session?.Dispose(); _session = null;
            SongControls.Visibility = Visibility.Collapsed;
            _playing = false; PlayButton.Content = "Play";
            if (previous is not null) await previous.DisposeAsync();
            if (_closed) { await replacement.DisposeAsync(); replacement = null; return; }
            _output = replacement; replacement = null;
            _mixer = mixer; _director = director;
            _output.PlaybackStopped += OnPlaybackStopped;
            PackText.Text = pack.Definition.Title;
            PackDetailText.Text = $"{pack.Layers.Count} aligned layers · {pack.Definition.TempoBpm:0.##} BPM · {pack.Definition.LoopBars} bars";
            if (wasPlaying)
            {
                _output.Play();
                _playing = true; PlayButton.Content = "Pause";
            }
            StatusText.Text = "Ready. Lower Star Citizen’s music volume and keep its effects and dialogue on.";
        }
        catch (Exception error)
        {
            if (replacement is not null)
            {
                try { await replacement.DisposeAsync(); }
                catch (Exception cleanupError)
                {
                    if (!_closed) StatusText.Text = $"Could not load soundtrack: {error.Message}. Audio cleanup also failed: {cleanupError.Message}";
                    return;
                }
            }
            if (!_closed) StatusText.Text = $"Could not load soundtrack: {error.Message}";
        }
        finally
        {
            _loading = false;
            if (!_closed) SetEnabled(_output is not null);
        }
    }


    private async void OnLoadSongsClicked(object sender, RoutedEventArgs args)
    {
        if (_loading || _closed) return;
        try
        {
            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null && !_closed) await LoadLibraryAsync(folder.Path);
        }
        catch (Exception error) { if (!_closed) StatusText.Text = $"Could not choose music folder: {error.Message}"; }
    }
    private async Task LoadLibraryAsync(string path)
    {
        if (_loading || _closed) return;
        _loading = true; SetEnabled(false);
        StatusText.Text = "Inspecting song formats and estimating cue points…";
        WasapiOutput? replacement = null;
        FullTrackSession? session = null;
        try
        {
            var catalog = await Task.Run(() => MusicCatalog.Open(path, _loadCancellation.Token));
            var library = await Task.Run(() => catalog.BuildLibrary(_loadCancellation.Token));
            if (_closed) return;
            session = new FullTrackSession(library)
            {
                Entry = (TrackEntry)Math.Max(0, EntryBox.SelectedIndex),
                AllowQuietBreaks = QuietBox.IsChecked == true,
                CrossfadeSeconds = catalog.Data.CrossfadeSeconds
            };
            session.Player.SetVolume((float)VolumeSlider.Value / 100);
            await session.RequestSituationAsync(session.SituationId, force: true);
            if (_closed) return;
            replacement = await WasapiOutput.CreateAsync(session.Player);
            if (_closed) return;
            var wasPlaying = _playing;
            var previous = _output;
            _output = null; _playing = false; PlayButton.Content = "Play";
            _mixer = null; _director = null;
            _session?.Dispose(); _session = null;
            if (previous is not null) await previous.DisposeAsync();
            if (_closed) return;
            _output = replacement; replacement = null;
            _session = session; session = null; _catalog = catalog;
            _output.PlaybackStopped += OnPlaybackStopped;
            SongControls.Visibility = Visibility.Visible;
            RebindCatalog();
            if (wasPlaying) { _output.Play(); _playing = true; PlayButton.Content = "Pause"; }
            RefreshStatus();
        }
        catch (Exception error)
        {
            if (!_closed) StatusText.Text = $"Could not load song library: {error.Message}";
        }
        finally
        {
            session?.Dispose();
            if (replacement is not null) await replacement.DisposeAsync();
            _loading = false;
            if (!_closed) SetEnabled(_output is not null);
        }
    }
    private async void OnNextTrackClicked(object sender, RoutedEventArgs args)
    {
        if (_session is null || _loading) return;
        var session = _session;
        await session.RequestSituationAsync(session.SituationId, force: true);
        if (!_closed && ReferenceEquals(_session, session)) RefreshStatus();
    }
    private async void OnGenreChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_session is null || _loading || _updatingUi || GenreBox.SelectedItem is not string genre) return;
        var session = _session;
        await session.SetGenreAsync(genre);
        if (_closed || _loading || !ReferenceEquals(_session, session)) return;
        RebindCatalog(); SetEnabled(true); RefreshStatus();
    }
    private async void OnSituationChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_session is null || _loading || _updatingUi || SituationBox.SelectedItem is not CatalogSituation situation) return;
        var session = _session;
        await session.RequestSituationAsync(situation.Id);
        if (_closed || !ReferenceEquals(_session, session)) return;
        RebindCatalog(); RefreshStatus();
    }
    private void OnEntryChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_session is not null) _session.Entry = (TrackEntry)Math.Max(0, EntryBox.SelectedIndex);
    }
    private void OnQuietChanged(object sender, RoutedEventArgs args)
    {
        if (_session is not null) _session.AllowQuietBreaks = QuietBox.IsChecked == true;
    }

    private void SetEnabled(bool ready)
    {
        LoadSongsButton.IsEnabled = !_loading;
        NextTrackButton.IsEnabled = ready && _session is not null;
        GenreBox.IsEnabled = ready && _session is not null;
        EntryBox.IsEnabled = ready && _session is not null;
        QuietBox.IsEnabled = ready && _session is not null;
        PlayButton.IsEnabled = ready;
        CrossfadeSlider.IsEnabled = ready && _session is not null;
        ResponseSlider.IsEnabled = ready && _session is null;
        VolumeSlider.IsEnabled = ready;
        SituationBox.IsEnabled = ready && _session is not null;
        LibraryManager.IsEnabled = !_loading && _catalog is not null;
        AddGenreButton.IsEnabled = !_loading && _catalog is not null;
        AddSituationButton.IsEnabled = !_loading && _catalog is not null;
        AddSongsButton.IsEnabled = !_loading && _catalog is not null;
        RemoveGenreButton.IsEnabled = !_loading && ManageGenreBox.SelectedItem is CatalogGenre { IsDefault: false };
        RemoveSituationButton.IsEnabled = !_loading && ManageSituationBox.SelectedItem is CatalogSituation { IsDefault: false };
        foreach (var button in ManualButtons.Children.OfType<Button>())
            button.IsEnabled = ready && (_session is null || button.Tag is string tag && Enum.TryParse<GameState>(tag, out var state) && _session.HasState(state));
    }

    private void RebindCatalog()
    {
        if (_catalog is null || _session is null) return;
        _updatingUi = true;
        try
        {
            var data = _catalog.Data;
            GenreBox.ItemsSource = data.Genres.Select(g => g.Name).ToArray(); GenreBox.SelectedItem = _session.Genre;
            var available = data.Situations.Where(s => _session.HasSituation(s.Id)).ToArray();
            SituationBox.ItemsSource = available; SituationBox.SelectedItem = available.FirstOrDefault(s => s.Id == _session.SituationId);
            StateText.Text = data.Situations.FirstOrDefault(s => s.Id == _session.SituationId)?.Name ?? "Choose a situation";
            ManageGenreBox.ItemsSource = data.Genres; ManageGenreBox.SelectedItem = data.Genres.FirstOrDefault(g => g.Id == _manageGenreId) ?? data.Genres[0];
            ManageSituationBox.ItemsSource = data.Situations; ManageSituationBox.SelectedItem = data.Situations.FirstOrDefault(s => s.Id == _manageSituationId) ?? data.Situations[0];
            DefaultSongsBox.IsChecked = data.IncludeDefault; CustomSongsBox.IsChecked = data.IncludeCustom;
            CrossfadeSlider.Value = _pendingCrossfade ?? data.CrossfadeSeconds;
            CrossfadeText.Text = CrossfadeSlider.Value == 0 ? "0 seconds · full song, then next; no overlap" : $"{CrossfadeSlider.Value:0} seconds of overlap";
            PackText.Text = Path.GetFileName(_catalog.Root);
            PackDetailText.Text = $"{_session.Library.Tracks.Count(t => t.Enabled)} enabled / {data.Songs.Length} songs · {data.Genres.Length} genres · {data.Situations.Length} situations";
            if (_session.Library.Warnings.Count > 0) PackDetailText.Text += "\n" + string.Join("\n", _session.Library.Warnings.Take(3));
            RefreshSongRows();
        }
        finally { _updatingUi = false; }
    }
    private void OnManageSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingUi || _loading) return;
        _manageGenreId = (ManageGenreBox.SelectedItem as CatalogGenre)?.Id;
        _manageSituationId = (ManageSituationBox.SelectedItem as CatalogSituation)?.Id;
        RefreshSongRows(); SetEnabled(_output is not null);
    }
    private void RefreshSongRows()
    {
        SongRows.Children.Clear();
        if (_catalog is null || ManageGenreBox.SelectedItem is not CatalogGenre genre || ManageSituationBox.SelectedItem is not CatalogSituation situation) return;
        _manageGenreId = genre.Id; _manageSituationId = situation.Id;
        SituationDescriptionText.Text = situation.Description + (situation.IsDefault ? "" : " Manual selection now; AI matching is not connected yet.");
        var songs = _catalog.Data.Songs.Where(s => s.GenreId == genre.Id && s.SituationId == situation.Id).ToArray();
        foreach (var song in songs)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var enabled = new CheckBox { Content = new TextBlock { Text = song.Title, TextWrapping = TextWrapping.Wrap }, IsChecked = song.Enabled };
            async void Changed(object sender, RoutedEventArgs args)
            {
                if (_loading || _updatingUi) return;
                var on = enabled.IsChecked == true;
                await EditCatalogAsync(() => _catalog.SetSongEnabled(song.Id, on));
            }
            enabled.Checked += Changed; enabled.Unchecked += Changed;
            var track = _session?.Library.Tracks.FirstOrDefault(t => t.Id == song.Id);
            var sourceEnabled = song.IsDefault ? _catalog.Data.IncludeDefault : _catalog.Data.IncludeCustom;
            var detail = new TextBlock { Text = (song.IsDefault ? "Default" : "Custom") + (track is null ? " · unavailable" : $" · {TimeSpan.FromSeconds(track.DurationSeconds):m\\:ss}") + (sourceEnabled ? "" : " · source off"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(detail, 1); row.Children.Add(enabled); row.Children.Add(detail);
            if (!song.IsDefault)
            {
                var remove = new Button { Content = "Remove" }; Grid.SetColumn(remove, 2);
                remove.Click += async (_, _) =>
                {
                    if (_loading || !await ConfirmRemovalAsync($"Remove {song.Title}?", "This removes Veda's imported copy. Your original song on your device stays untouched.")) return;
                    await EditCatalogAsync(() => _catalog.RemoveSong(song.Id));
                };
                row.Children.Add(remove);
            }
            else { var protectedLabel = new TextBlock { Text = "Protected", VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(protectedLabel, 2); row.Children.Add(protectedLabel); }
            SongRows.Children.Add(row);
        }
        if (songs.Length == 0) SongRows.Children.Add(new TextBlock { Text = "No songs here yet. Add a WAV song to this genre and situation." });
    }
    private async Task<bool> EditCatalogAsync(Action edit)
    {
        if (_catalog is null || _session is null || _loading || _closed) return false;
        _loading = true; SetEnabled(false);
        try
        {
            await Task.Run(edit);
            var library = await Task.Run(() => _catalog.BuildLibrary(_loadCancellation.Token));
            if (_closed) return false;
            _session.CrossfadeSeconds = _pendingCrossfade ?? _catalog.Data.CrossfadeSeconds;
            await _session.UpdateLibraryAsync(library);
            if (_closed) return false;
            RebindCatalog(); ManagerStatusText.Text = "Saved."; RefreshStatus();
            return true;
        }
        catch (Exception error)
        {
            if (!_closed) { RebindCatalog(); ManagerStatusText.Text = $"Could not complete that change: {error.Message}"; }
            return false;
        }
        finally { _loading = false; if (!_closed) SetEnabled(_output is not null); }
    }
    private async void OnSourcesChanged(object sender, RoutedEventArgs args)
    {
        if (_updatingUi || _loading || _catalog is null) return;
        var defaults = DefaultSongsBox.IsChecked == true; var custom = CustomSongsBox.IsChecked == true;
        await EditCatalogAsync(() => _catalog.SetSources(defaults, custom));
    }
    private void OnCrossfadeChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (CrossfadeText is not null) CrossfadeText.Text = args.NewValue == 0 ? "0 seconds · full song, then next; no overlap" : $"{args.NewValue:0} seconds of overlap";
        if (_updatingUi || _loading || _catalog is null) return;
        _session!.CrossfadeSeconds = args.NewValue;
        _pendingCrossfade = args.NewValue;
        _crossfadeTimer.Stop(); _crossfadeTimer.Start();
    }
    private async void OnAddGenreClicked(object sender, RoutedEventArgs args)
    {
        var name = NewGenreName.Text;
        if (await EditCatalogAsync(() => { _manageGenreId = _catalog!.AddGenre(name).Id; })) NewGenreName.Text = "";
    }
    private async void OnAddSituationClicked(object sender, RoutedEventArgs args)
    {
        var name = NewSituationName.Text; var description = NewSituationDescription.Text;
        if (await EditCatalogAsync(() => { _manageSituationId = _catalog!.AddSituation(name, description).Id; }))
        { NewSituationName.Text = ""; NewSituationDescription.Text = ""; }
    }
    private async Task<bool> ConfirmRemovalAsync(string title, string detail)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = detail,
            PrimaryButtonText = "Remove", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async void OnRemoveGenreClicked(object sender, RoutedEventArgs args)
    {
        if (_loading || ManageGenreBox.SelectedItem is not CatalogGenre { IsDefault: false } genre || _catalog is null) return;
        var count = _catalog.Data.Songs.Count(s => s.GenreId == genre.Id);
        if (!await ConfirmRemovalAsync($"Remove {genre.Name}?", $"This removes the custom genre and its {count} imported songs across all situations. Original device files stay untouched.")) return;
        await EditCatalogAsync(() => _catalog.RemoveGenre(genre.Id));
    }
    private async void OnRemoveSituationClicked(object sender, RoutedEventArgs args)
    {
        if (_loading || ManageSituationBox.SelectedItem is not CatalogSituation { IsDefault: false } situation || _catalog is null) return;
        var count = _catalog.Data.Songs.Count(s => s.SituationId == situation.Id);
        if (!await ConfirmRemovalAsync($"Remove {situation.Name}?", $"This removes the custom situation and its {count} imported songs across all genres. Original device files stay untouched.")) return;
        await EditCatalogAsync(() => _catalog.RemoveSituation(situation.Id));
    }
    private async void OnAddSongsClicked(object sender, RoutedEventArgs args)
    {
        if (_loading || _catalog is null || ManageGenreBox.SelectedItem is not CatalogGenre genre || ManageSituationBox.SelectedItem is not CatalogSituation situation) return;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
            picker.FileTypeFilter.Add(".wav");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var files = await picker.PickMultipleFilesAsync();
            if (_closed || files.Count == 0) return;
            var imported = 0; var errors = new List<string>();
            await EditCatalogAsync(() =>
            {
                foreach (var file in files)
                {
                    try { _catalog.ImportSong(file.Path, genre.Id, situation.Id); imported++; }
                    catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { errors.Add($"{file.Name}: {e.Message}"); }
                }
            });
            if (!_closed) ManagerStatusText.Text = $"Imported {imported} song(s)." + (errors.Count > 0 ? "\nRejected files:\n" + string.Join("\n", errors) + "\n" + MusicCatalog.ImportRequirements : "");
        }
        catch (Exception error) { if (!_closed) ManagerStatusText.Text = $"Could not import songs: {error.Message}"; }
    }
    private void OnObservationToggled(object sender, RoutedEventArgs args)
    {
        if (ObservationStatusText is null) return;
        ObservationStatusText.Text = ObservationToggle.IsOn
            ? "Disconnected · Gameplay AI is not connected yet. Music stays under manual control; no screen capture is running."
            : "Disconnected · Observation is off. No screen capture is running.";
    }

    private void OnPlayClicked(object sender, RoutedEventArgs args)
    {
        if (_output is null || _loading) return;
        try
        {
            if (_playing) _output.Pause(); else _output.Play();
            _playing = !_playing;
            PlayButton.Content = _playing ? "Pause" : "Play";
            StatusText.Text = _playing ? "Playing through the default Windows audio output." : "Paused. The musical clock is held.";
        }
        catch (Exception error)
        {
            _playing = false;
            PlayButton.Content = "Play";
            StatusText.Text = $"Audio stopped: {error.Message}. Load the soundtrack folder again to reconnect.";
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is null) return;
        Root.DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed || sender != _output) return;
            _playing = false; PlayButton.Content = "Play";
            StatusText.Text = $"Audio device stopped: {args.Exception.Message}. Load the soundtrack folder again to reconnect.";
        });
    }

    private void OnStateClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button button && button.Tag is string name && Enum.TryParse<GameState>(name, out var state))
            Request(state);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(Root.XamlRoot) is TextBox) return;
        var number = (int)args.Key - (int)VirtualKey.Number1;
        if (number is >= 0 and < 6 && !_loading && (_director is not null || _session is not null))
        {
            Request((GameState)number);
            args.Handled = true;
        }
    }

    private async void Request(GameState state)
    {
        if (_session is not null && !_loading)
        {
            var session = _session;
            await session.RequestAsync(state);
            if (_closed || _loading || !ReferenceEquals(_session, session)) return;
            _requested = _session.State;
            RebindCatalog();
            RefreshStatus();
            return;
        }
        if (_director is null || _mixer is null || _loading) return;
        _requested = state;
        _mixer.Submit(_director.Plan(state, (float)50d / 100, (float)ResponseSlider.Value / 100));
        StateText.Text = GameStateLabels.Display(state);
        TransitionText.Text = _playing ? "Arranging the transition…" : "Press Play to hear this arrangement.";
    }

    private void OnArrangementChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_director is not null && !_loading) Request(_requested);
    }

    private void OnVolumeChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        _mixer?.SetVolume((float)args.NewValue / 100);
        _session?.Player.SetVolume((float)args.NewValue / 100);
    }

    private void RefreshStatus()
    {
        if (_session is not null)
        {
            var snapshot = _session.Player.Snapshot();
            TransitionText.Text = snapshot.Track is not null
                ? $"{snapshot.Track.Title} · {TimeSpan.FromSeconds(snapshot.PositionSeconds):mm\\:ss} / {TimeSpan.FromSeconds(snapshot.Track.EndSeconds):mm\\:ss}" + (snapshot.Transitioning ? " · crossfading" : "")
                : _playing ? "Quiet / preparing the next track" : "Press Play to test your tracks.";
            StatusText.Text = _session.Status;
            if (snapshot.ClampedSamples > 0) StatusText.Text += " Peak guard reached; review source levels.";
            return;
        }
        if (_mixer is null || !_playing) return;
        var state = _mixer.Snapshot();
        var elapsed = TimeSpan.FromSeconds(state.RenderedFrames / (double)_mixer.SampleRate);
        TransitionText.Text = state.Transitioning
            ? $"Moving toward {GameStateLabels.Display(state.RequestedState).ToLowerInvariant()} · {elapsed:mm\\:ss}"
            : $"Arrangement settled · {elapsed:mm\\:ss}";
        if (state.ClampedSamples > 0)
            StatusText.Text = "The mix reached its peak guard. Lower the volume or reduce stem gains in the soundtrack manifest.";
    }
}
