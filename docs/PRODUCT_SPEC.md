# Prototype 0.2.0 music manager.

Default identities are the four existing genres (ambient synth, synth hard rock, modern hiphop, western) and six standard situations. Default WAVs in their genre/situation folders are adopted as protected Default songs. Default categories cannot be deleted. Default songs have individual and source switches but cannot be removed through Veda. Custom categories have names, and custom situations also have a user description. Custom songs can live alongside defaults or in custom categories, and can be switched off or removed. Removing a custom category removes its custom imported copies across the library after an explicit in-app confirmation. Device originals remain untouched.

The standardized folders are `VedaMusic/genre/situation/Default/song.wav` and `VedaMusic/genre/situation/Custom/song.wav`. The catalog preserves stable custom category and song identities, with safe folder names independent of arbitrary user text. A native file picker imports WAVs up to 240 seconds, inclusive, in the existing stereo 48 kHz PCM16/float32 RIFF format. Import rejection is explained in-app. Source switches, individual choices, category descriptions, and crossfade settings persist in `veda-library.json`.

Crossfade replaces Drama in the UI and spans 0–20 seconds. At 0, a queued next song begins only at full file EOF, including an authored end cue's remaining tail. Positive values set the transition envelope; overlap is limited by available audio. Settings apply to future transitions; an active fade settles normally. The existing optional calm/relief quiet break remains separate.

The observation toggle always reports Disconnected. No model, screen capture, remote service, or automatic description matching is implemented. Custom situations can be tested manually. The original stem demo and manual prototype remain independent. See `MUSIC_MANAGER.md` for the user workflow.

# Prototype 0.1.2 full-song addition.

The original 0.1 stem specification is retained below for that mode. The new independent full-song mode supports genre/situation folders, different track counts and durations, quiet/strong cue estimates, recency rotation, crossfades, and optional 20-second quiet breaks after CalmSpace/Relief. It uses stereo 48 kHz ordinary RIFF PCM16/float32 WAV, 12 seconds to 10 minutes per track, without equal-length stems or a manifest. It does not detect gameplay. See `FULL_SONG_TEST.md` for exact test behavior and `AI_MUSIC_DIRECTION.md` for accepted future contextual requirements.

# Prototype 0.1 product specification.

## Intent.

Create a continuous external score for space-game play. Star Citizen supplies the first real gameplay context, but the musical states remain generic.

The first milestone proves whether related music can move naturally through calm, travel, uncertainty, danger, escape, and relief. A person supplies the state changes. A later interpreter will supply the same state requests.

## Delivered scope.

- Native Windows x64 desktop app with a restrained settings window.
- Six situations: `CalmSpace`, `Travel`, `Uncertain`, `Combat`, `Escape`, `Relief`.
- One loaded family of up to eight synchronized loop layers.
- Manual state buttons and number keys while the window is focused.
- Drama, responsiveness, and independent soundtrack volume.
- Play/pause, with no automatic playback at startup.
- Folder import with a validated JSON manifest and WAV assets.
- Shared-mode audio output so effects and dialogue remain audible.
- Original synthetic demo family and an offline renderer.

This stage does not perform capture or gameplay interpretation. It has no account, backend, ongoing API dependency, or connection to Suno during playback.

## Musical behavior.

| Situation | Start boundary | Default authored fade before responsiveness scaling |
|---|---|---|
| CalmSpace | Next bar | 8 beats |
| Travel | Next bar | 4 beats |
| Uncertain | Next beat | 2 beats |
| Combat | Immediate | 0.5 beat |
| Escape | Next beat | 2 beats |
| Relief | Next bar | 8 beats |

Responsiveness scales fade durations between 1.6× and 0.55×. At the initial 50% setting, the scale is 1.075×. It does not erase beat/bar alignment. Combat starts a short ramp without waiting for a musical boundary. No stingers are implemented yet.

Drama scales non-base layer gains between 0.5× and 1.5×, capped at 1. The first manifest layer is the base atmosphere and keeps its authored gain. This is a simple prototype arrangement control, not a final musical model.

An escape request retains motion and a trace of danger. Relief releases tension gradually. The prototype does not infer these states from the game or automatically return to calm; the operator explicitly requests each stage.

## Content contract.

All layers are exported from a common start to a common end. They are stereo, 48 kHz, ordinary RIFF PCM16 or IEEE float32 WAV. Their frame count must equal the manifest's tempo, meter, and bar length calculation. A loop is at most 60 seconds. Up to 128 MiB of decoded sample data is accepted.

The player repeats the entire authored loop and controls gains. The music itself must make a clean seam. Phase alignment does not repair incompatible harmony, a poor loop point, or overlapping full arrangements.

## Performance direction.

Rendering happens independently of the UI. After preload, the callback uses fixed arrays and a sample clock; it performs no IO, inference, locks, or per-block managed allocation. Each new request replaces one pending request.

The configured output latency is a request to the device, not a measured guarantee. GPU use, actual game frame times, output latency, underruns, and headset changes must be measured on Windows before claiming a performance target.

For the later detector, initial targets are prompt escalation for clear danger, slower validation for broader scene changes, and sustained evidence before de-escalation. Exact thresholds must follow measured gameplay observations.

## Next milestones.

1. Verify the real Windows app and listening experience with the demo and one prepared Suno family.
2. Add explicit window selection and ephemeral Windows.Graphics.Capture frames.
3. Add an observable Star Citizen interpreter with confidence and a bounded history of state estimates.
4. Add anti-flapping transition policies, tested on representative gameplay.
5. Add tray operation, focus/capture-loss policies, saved settings, and a release package.

Do not claim “Star Citizen supported” or “fully local AI” until the corresponding behavior is implemented and tested. Keep game profiles separate from the generic soundtrack vocabulary.
