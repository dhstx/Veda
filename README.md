# Veda.

A Windows soundtrack companion for space games. Star Citizen is the first reference game.

This repository is **Prototype 0.2.0: customizable music library and manual playback**. It includes protected Default songs and categories, removable Custom songs and categories, per-song and source on/off controls, described custom situations, WAV import from a file picker, a saved 0–20-second crossfade setting, and a disconnected gameplay observation toggle. The original synchronized-stem demo remains at startup. It is a starting codebase, not an installer or a finished gameplay detector.

**For music management, start with [docs/MUSIC_MANAGER.md](docs/MUSIC_MANAGER.md).** The listening guide remains in [docs/FULL_SONG_TEST.md](docs/FULL_SONG_TEST.md). General development setup remains in [START_HERE.md](START_HERE.md).

The 33 portable checks and an additional check against the five uploaded songs pass, and the Windows audio adapter compiles. The full WinUI build, app launch, and playback on a Windows device still need testing. See [docs/VERIFICATION.md](docs/VERIFICATION.md) before treating this as a runnable Windows release.

## Try the prototype.

On Windows, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and current [Visual Studio Windows app development tools](https://learn.microsoft.com/windows/apps/get-started/start-here), including the Windows SDK and WinUI tooling. Use a Visual Studio release that supports .NET 10. Choose an x64 development environment.

Open PowerShell in this folder, then run:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
dotnet run --project src/Veda.App -c Debug -p:Platform=x64
```

The app loads the included demo but waits for you to press **Play**. Use the six buttons, or number keys 1–6 while the app window has focus. Pause holds the musical clock. Volume affects only this app.

For repeated launches, double-click **Run-Veda.bat** in this folder. It runs that same app command from the correct directory and keeps failure messages visible. The development tools are still required. You can create a desktop shortcut to the launcher; keep the actual file beside `Veda.slnx`.

The demo contains original, simple synthesized tones and percussion. It demonstrates arrangement changes; it is not a Suno soundtrack or a production score.

## Implemented in this starter.

| Capability | Implementation |
|---|---|
| Calm space, travel, uncertainty, combat, escape, relief | Manual buttons and local window shortcuts |
| Musical continuity | All stems share one sample clock and loop range |
| Escalation | Combat begins its gain ramp immediately; uncertainty changes at a beat |
| Resolution | Relief and calm fade at bar boundaries |
| Demo arrangement | Fixed mid-level emphasis; Drama slider replaced with the full-song crossfade control |
| Responsiveness | Changes fade duration; authored beat/bar boundaries stay intact |
| Playback | Shared-mode WASAPI through NAudio 3; default-device routing is configured |
| Built-in stem demo | Original synchronized mixer remains available at startup; external stem-load button removed |
| Full-song import | Genre → situation → Default or Custom folders; unequal counts and durations; compatible WAVs |
| Music manager | Default/Custom origin labels, source and per-song switches; protected defaults; removable custom copies |
| Custom categories | User-named genres and described situations; manually selected until AI exists |
| Custom import | Device file picker; WAV only, up to 240 seconds, stereo 48 kHz PCM16/float32 |
| Crossfade | 0–20 seconds; 0 finishes the full file before the next starts |
| Observation | Disconnected toggle; no capture or model |
| Genre selection | In-app dropdown switches within the loaded library; missing situations are disabled |
| Full-song transitions | Estimated quiet/strong cues, crossfades, recency rotation, optional calm/relief silence |
| Verification | Portable checks and an offline WAV renderer use the same mixer as the app |

Capture, gameplay AI, temporal recognition, a tray icon, global hotkeys, saved preferences, and a release installer are later milestones. The app does not currently inspect Star Citizen, detect enemies, or know whether an escape succeeded. It does not include a fake detector.

## Repository map.

```text
src/Veda.Core/            States, soundtrack loading, director, audio mixer
src/Veda.Audio.Windows/   Small NAudio WASAPI output adapter
src/Veda.App/             WinUI 3 desktop prototype
tests/Veda.Checks/        Executable verification suite, no test packages
tools/Veda.Render/        Demo generator and offline scenario renderer
assets/soundtracks/demo-void/   Six aligned demo WAVs and their manifest
schemas/                       Soundtrack JSON schema
scripts/                       Windows build, run, check, and publish commands
docs/                          Specification, architecture, music preparation, next AI task
```

The application is named Veda. No Star Citizen assets or branding are included.

## Add your music.

For full songs, follow [docs/FULL_SONG_TEST.md](docs/FULL_SONG_TEST.md). No stem extraction is needed for this mode.

For synchronized stems, read [docs/SOUNDTRACK_AUTHORING.md](docs/SOUNDTRACK_AUTHORING.md). Copy the demo manifest into a new soundtrack folder, replace its stem paths and gains, then export all stems over the **same loop region** as stereo 48 kHz PCM16 or IEEE float32 WAVs. The app validates the file lengths before playback.

Independent full songs can crossfade in the new mode, but they will not automatically line up on a common beat or share compatible melodies. The original stem mode still supports one aligned family at a time. Gameplay understanding, artistic cue analysis, stingers, and AI music direction remain future work. The accepted contextual behavior is specified in [docs/AI_MUSIC_DIRECTION.md](docs/AI_MUSIC_DIRECTION.md).

## Continue with a coding agent.

Open this folder in your coding agent. It should read [AGENTS.md](AGENTS.md), [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md), and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). The next contained implementation request is [docs/NEXT_BUILD_PROMPT.md](docs/NEXT_BUILD_PROMPT.md).

## Verification and licensing.

See [docs/VERIFICATION.md](docs/VERIFICATION.md) for the checks run on this starter and the remaining Windows checks. The workflow in `.github/workflows/build.yml` builds the Windows app and runs the portable checks after you push to GitHub; it does not publish or deploy anything.

No public open-source license has been selected. [LICENSE.md](LICENSE.md) records that choice, and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) identifies dependencies. The supplied ignore rules cover common private audio and credential filenames; review staged files before committing your own material.
