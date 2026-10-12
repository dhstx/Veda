# Music catalog and controls in 0.2.0.

`MusicCatalog` manages code-defined protected categories, metadata, default adoption, and custom copies. It validates before import, checks the copied audio again before commit, and writes catalog JSON through a temporary file and replacement. Mutations have one UI owner; scanning, copying, and analysis run on a worker and never in a rendering callback. File analysis caches only duration/cues/energy, keyed by identity and file size/timestamp. All settings and custom descriptions live in the selected music folder. No cloud component is added.

`LibraryTrack.SituationId` and origin/enable metadata extend the original six-state track model. The enum remains for the stem demo and compatibility checks; custom playback and recency use stable situation IDs. `FullTrackSession.UpdateLibraryAsync` invalidates stale preparations and queued rotations, preserves an eligible current song, chooses a replacement when needed, and settles to silence when no songs qualify. Genre lists include empty protected/custom categories.

`FullTrackPlayer` accepts a zero-duration transition envelope and schedules the next prepared voice on its audio clock, without overlapping or discarding the outgoing file before EOF. `FinishAtFileEnd` restores a currently playing file's full end when no-overlap is selected. The small EOF safety envelope remains. Positive fades continue using the existing convex envelope; available audio limits their duration. The slider is saved after a short debounce so dragging does not repeatedly disable the UI.

Observation is a disconnected UI request only. It never starts capture or dispatches music states. All custom descriptions are future interpreter input, not a implemented recognizer.

# Architecture.

## Full-song runtime in 0.1.2.

`TrackLibraryLoader` inspects each WAV off the audio thread and retains lightweight duration/energy/cue metadata. It requires genre folders at the selected root, with recognized situation folders directly inside each genre; it recognizes genre/situation folders and can apply `track-cues.json` overrides. It never changes source files. `TrackSelector` ranks by recency and cue energy; this is not an AI judgment.

`FullTrackSession` has one control-thread owner, normally the WinUI dispatcher. It serializes background audio preparation, discards superseded results using a revision, and posts immutable playback commands to `FullTrackPlayer`. Only the current/outgoing voices and prepared pending material remain decoded; the entire song library is not preloaded. This is bounded track preloading, not disk streaming. Import retains at most one analysis decode at a time.

`FullTrackPlayer` has exactly one render-thread owner and two preloaded voices. It consumes the latest pending command, advances independent song positions on one output clock, and uses smooth convex gain crossfades. Requests during an active fade retain both voices until the fade settles. Urgent immediate plans shorten that existing envelope smoothly before the newest transition. Plans specify arbitrary entry/delay/fade seconds; energy estimates do not establish beat alignment. Audio EOF has a small safety envelope. The callback performs no IO, inference, locks, waits, or managed allocation.

Session ticks schedule rotation and optional calm/relief silence; the audio clock measures silence so pause also holds the quiet duration. Missing states preserve current music. Quiet and stronger entries are based on the audio energy envelope or curated overrides, not semantic recognition. No automatic musical-quality or gameplay model is present.

Both players implement `IAudioRenderer`, used by the shared WASAPI adapter. The original stem player stays independent. Loading a different library is a reload, not an authored transition. Snapshot fields are best-effort display/scheduling information, not an atomic transaction. Actual device latency and the Windows UI remain unverified in this environment.

## Original stem runtime.

The WinUI window requests a `GameState`. `MusicDirector` converts it to a validated immutable `MixPlan`. `AdaptiveMixer` consumes the latest plan and changes layer gains on its own sample clock. `WasapiOutput` wraps that mixer in NAudio's `ISampleProvider` and feeds shared-mode WASAPI.

The app has no dependency on a running game in this milestone.

| Project | Responsibility | Dependencies |
|---|---|---|
| Veda.Core | States, manifest/WAV validation, immutable gain plans, synchronized mixing | .NET standard library only |
| Veda.Audio.Windows | Windows audio device and sample-provider bridge | Core, NAudio.Wasapi 3.1.0 |
| Veda.App | Native controls, folder selection, playback lifecycle | Core, Windows audio, Windows App SDK 2.5.1 |
| Veda.Checks | Deterministic executable verification | Core |
| Veda.Render | Generated demo and offline scenario audio | Core |

## Audio ownership.

One thread owns `AdaptiveMixer.Render`. Callers may submit plans concurrently, but they may not render concurrently. Plans contain frozen gain dictionaries. UI producers allocate outside rendering; the callback reads one latest-wins reference.

All decoded arrays live for the lifetime of the loaded soundtrack and must not be mutated after loading. A sample index advances once per stereo frame, and every layer reads the same index. No individual stem player has its own clock.

A requested change waits for its boundary. At activation, the engine captures the current gains, including any partially completed fade, and starts a new eased ramp from there. This prevents an interrupted transition from jumping back to an old mix. Intermediate obsolete requests are not replayed.

Volume moves toward its target over about 50 ms. It affects only the mixed output. Pause occurs at the output adapter and preserves the mixer's frame position. A soundtrack reload starts a new clock; it is not an authored transition between families.

Snapshots are best-effort display data read with volatile/interlocked fields. They are not an atomic transaction across all fields and are not used to schedule audio.

## Soundtrack loading.

`SoundtrackLoader` reads only the selected folder's manifest and WAV assets. It rejects unknown JSON fields, absent states, bad gains, mismatched lengths, unsupported WAV formats, path escape, and excessive decoded size. Loading happens away from the UI and audio callback. A failed folder load normally leaves the existing soundtrack available.

The custom WAV reader intentionally accepts a narrow export format. It does not silently resample, convert mono, decode MP3, or correct a loop length. Supporting more formats is a separate task.

## Later gameplay input.

`GameplayContracts.cs` reserves interfaces for an ephemeral BGRA frame source and a gameplay interpreter. Neither interface has a placeholder implementation that pretends to recognize gameplay.

The intended later flow is selected game window → current frame → observation with confidence → recent state history → situation request → existing music director. An uncertain observation should not directly pick a track.

Use a bounded latest-frame handoff. Dispose old frames promptly. Store compact state estimates rather than video. Input work must never run on the audio thread or gate audio output. Scene recognition and anti-flapping policy should be replaceable without changing the mixer.

## Deployment.

The WinUI app is unpackaged and targets x64. The Windows App SDK runtime is copied into the application output through `WindowsAppSDKSelfContained`; development still uses the installed .NET 10 SDK/runtime. `scripts/Publish.ps1` produces a folder with both runtimes for later manual testing, not a signed installer.

This starter has no code signing, auto-update channel, startup registration, installer, or release deployment.
