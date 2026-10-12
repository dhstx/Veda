# Starter verification.

Checked on October 8, 2026 (America/Chicago) using a Linux environment and the stable .NET 10.0.100 SDK. This environment does not run native Windows windows or audio devices.

The starter was renamed to **Veda** after the application name was confirmed. The repository folder, projects, namespaces, app title, executable name, scripts, and documentation now use Veda. All 12 core checks were rerun successfully after the rename.

## Follow-up review: October 9, 2026.

The 12 checks were rerun successfully during a source and archive audit. The review corrected the soundtrack-reload error path: the old device is detached before disposal, and the UI reports resumed playback only after the new device starts successfully. Cleanup errors are reported instead of escaping the async load handler. These paths have been reviewed and C# type-checked; device failure behavior still requires a Windows test.

README and setup instructions now place the Windows verification gap beside the startup steps. Git exclusions are described as filename patterns rather than a guarantee that all private files are excluded.

## Completed.

- Core and offline render projects compiled and executed successfully.
- `Veda.Audio.Windows` compiled against NAudio.Wasapi 3.1.0 and the Windows target with zero warnings and zero errors. This is a compile check, not a device-playback check.
- The desktop C# source and 130 markup property names were checked against the actual Windows App SDK assemblies using a temporary named-control shim. That check caught and corrected invalid panel enable/disable calls. It does not generate the real XAML controls, validate XAML value conversion, or run the UI.
- The executable verification suite passed all 12 checks:
  1. Manifest and demo WAV alignment, including loop edges.
  2. Identical audio across different callback block sizes.
  3. Latest request wins and the scenario settles to calm.
  4. Immediate combat response and bar-quantized travel.
  5. Mute settles to silence while the clock continues.
  6. An interrupted fade starts from current gains without a jump.
  7. Stereo position is preserved.
  8. PCM16 WAV round trip and truncated-file rejection.
  9. Malformed manifests, misaligned audio, and path escape are rejected.
  10. Concurrent state producers do not leave a queue of stale arrangements.
  11. Zero managed allocation in repeated render callbacks after warmup.
  12. Invalid runtime controls are rejected.
- The real mixer rendered an 84-second Calm → Travel → Uncertain → Combat → Escape → Relief → Calm walkthrough at 48 kHz stereo. Maximum sample magnitude was approximately 0.121 at the default volume, with zero clamped samples.
- The JSON Schema passed Draft 2020-12 validation and the shipped manifest passed that schema. JSON/XML project and UI files parsed, the solution enumerated all five projects, and repository ignore rules included the six demo WAVs while excluding build artifacts and private music/credential examples.
- All six demo WAVs reproduced byte for byte from the shipped generator.

## Windows checks still required.

The app's NuGet dependencies restored, but a complete WinUI build could not run in this Linux environment: its Windows `XamlCompiler.exe` cannot execute here. The native XAML build, app launch, layout, accessibility, folder picker, and WASAPI playback still need verification on Windows.

Run from the repository root:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
dotnet build src/Veda.App/Veda.App.csproj -c Release -p:Platform=x64
dotnet run --project src/Veda.App -c Debug -p:Platform=x64
```

Then verify:

1. The demo loads, and audio begins only after pressing Play.
2. Each manual state changes the arrangement at its expected boundary.
3. Pause/resume preserves the musical position.
4. Drama and responsiveness alter the arrangement and fade behavior.
5. Volume does not change the game's own volume.
6. Number keys operate only while this app has focus.
7. A valid custom folder loads; an invalid folder gives a useful error.
8. Changing the default output device or disconnecting headphones behaves correctly. Automatic routing is configured, but has not been tested on hardware here.
9. Closing the app, including during loading, releases output resources.
10. A real Suno family loops and transitions well by ear. The numeric checks do not establish musical quality.
11. Reloading while playing preserves playback on success. If the new device fails to start or the old device fails to dispose, the button must show Play and the error must remain visible; a new folder load should permit recovery. An invalid manifest encountered before replacement must leave the existing player usable.

The GitHub workflow performs the portable checks and a Windows compile after you upload the repo. Interactive UI and headset tests remain manual.

## Reproduce the offline review.

```powershell
dotnet run --project tools/Veda.Render -c Release -- --render assets/soundtracks/demo-void artifacts/demo-preview.wav
```

This intermediate file is ignored by Git and omitted from the starter ZIP. The included six demo stems are sufficient to reproduce it.

## Full-song update: October 10, 2026.

Prototype 0.1.1 adds a separate manual full-song test mode. All 22 portable checks passed, including the original 12 stem checks and ten new full-song checks. An additional check imported and rendered cue starts from all five uploaded Suno WAVs, for 23 passing checks when supplied with the private sample library.

The new checks cover mixed two/three-track counts, folder labels, energy cue estimation, two/three-track recency, stereo and entry offsets, callback-size independence, latest pending request and cancellation, interrupted fades, zero managed allocation in render, silence while the sample clock runs, quiet-break expiry, urgent changes during silence/fades, repeated-state patience including a pending quiet fade, missing-state preservation, continuous automatic rotation, serialized background preparation with stale-result rejection, malformed commands, and invalid authored cue rejection.

The uploaded songs are stereo 48 kHz ordinary PCM16 WAV. Durations are 209.60, 199.96, 173.56, 92.40, and 194.00 seconds. Originals were not changed. The offline full-song renderer produced a 60-second preview with maximum sample magnitude approximately 0.439 at 70% volume and zero clamped samples. The preview demonstrates calm/strong entry, transitions, silence, resumption, and a final fade; it is not a real gameplay-driven schedule. No listening judgment, vocal-free certification, beat-grid analysis, or Windows device-playback claim is inferred from those numbers.

The updated Windows audio adapter compiled with zero warnings and zero errors. The desktop C# source and named markup properties compiled against the actual Windows SDK/WinUI/NAudio assemblies using a temporary generated-name shim and compile-only target. Project XML and XAML parsed. The real full Windows app build was attempted after successful dependency restore, but Linux cannot execute the Windows XamlCompiler.exe. Actual XAML compilation/value conversion, app launch, new control layout, file picker behavior, audio device behavior, import cancellation, and audible quality must still be checked on Windows.

Reproduce the sample-specific checks after extracting the user's private songs into a common parent:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void "C:\path\to\VedaMusic"
dotnet run --project tools/Veda.Render -c Release -- --full-songs "C:\path\to\VedaMusic" artifacts/full-song-preview.wav
```

The optional sample-specific check expects these five sample files. The normal 24 checks use synthetic files generated in a temporary directory and do not require the user's music. The accelerated preview needs at least two CalmSpace and two Combat tracks within the first sorted genre. It preloads its small example sequence for offline rendering; live playback uses the session's serialized per-track preparation.

Follow `FULL_SONG_TEST.md` for the Windows test and `AI_MUSIC_DIRECTION.md` for future contextual behavior. No gameplay capture or AI was added in this update.


## Genre-first update: October 11, 2026.

Prototype 0.1.2 requires `VedaMusic/genre/situation/song.wav`. Genre folders are the first level under the selected root; situation folders must be directly inside each genre. Misplaced files are skipped with an actionable warning. The Load stem pack button and its picker handler were removed; the startup demo remains.

All 24 portable checks passed, plus the uploaded five-song import/cue-render check, for 25 passing checks. The two added checks exercise genre-prefixed cue overrides, warning on old or uncategorized layouts, selection of the wrong root, playback switching between two genres, preservation of available situations, fallback when a situation is missing, and repeated genre selection without restarting. Private test copies use the new folders; original WAV bytes remain unchanged.

The Windows audio adapter and the app's C# handlers were compiled against the actual Windows App SDK assemblies. Current XAML names and properties were checked in that compile-only harness. This does not run the Windows XAML compiler, app UI, or an audio device. Confirm those on Windows using the commands above.


## Double-click launcher: October 11, 2026.

`Run-Veda.bat` changes to its own source directory, checks for the project and `dotnet`, and invokes the existing Debug x64 run command. It preserves the process exit code, restores the working directory, and keeps failure details visible. Paths are quoted, delayed expansion is disabled, and the file uses Windows CRLF endings. The command and labels were inspected against the actual repository. Windows CMD execution and app launch cannot be exercised on this Linux host; these remain part of the Windows test. No playback logic or dependency versions changed.


## Music manager update: October 12, 2026.

Prototype 0.2.0 adds protected Default identities, Custom imports and category descriptions, saved source/per-song switches, a 0–20-second crossfade slider, and the disconnected observation toggle. All 33 portable checks and the private five-song check passed, for 34 checks. Nine new catalog checks cover default protection, non-destructive adoption and cue override reload, origin folders, persisted sources and individual choices, the inclusive 240-second boundary and one-frame-over rejection, malformed/non-WAV rejection, safe user names and duplicate rejection, source preservation during removal, custom playback and all-disabled silence, exact EOF no-overlap scheduling and positive overlap, changing to zero after an authored shortened ending, custom cascades, and invalid catalog/path rejection.

The Windows audio adapter compiled with no warnings or errors. UI handlers, dynamically created controls, and current XAML property names compile against actual Windows App SDK/Windows SDK/NAudio assemblies using the temporary compile-only generated-name harness. XAML parses and event handler names resolve. This environment cannot execute the Windows XAML compiler or app; the new manager layout, file picker, removal dialogs, and device playback must be checked on Windows. No listening verdict is implied. The owner's previous manual build was reported to work; that does not verify this update's new controls.

Follow `MUSIC_MANAGER.md` for the Windows acceptance sequence. Imported Custom files are constrained to four minutes; existing Default content retains its earlier 12-second to 10-minute contract. No gameplay capture, model, network calls, or automatic situation matching were added.
