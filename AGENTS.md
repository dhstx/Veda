# Veda engineering instructions.

Read `docs/PRODUCT_SPEC.md` and `docs/ARCHITECTURE.md` before editing.

## Scope.

- This is a native Windows companion for space games. Star Citizen is the first game profile.
- Preserve the manual music prototype as an independent mode as capture and recognition are added.
- Use the existing .NET 10 / WinUI 3 / NAudio 3 stack. Propose a concrete reason before adding a service or major dependency.
- Keep scene interpretation, temporal state decisions, music direction, and sample rendering separate.
- Never imply that an unimplemented detector, capture feature, model, or latency target already works.
- Do not add authentication, subscriptions, a cloud database, streaming integrations, or live music generation during these milestones.

## Audio.

- `AdaptiveMixer.Render` has one rendering-thread owner. Do not call it concurrently.
- Never do file IO, inference, UI work, waits, locks, or per-block managed allocations in the render callback.
- Publish immutable plans using the latest-wins slot. Avoid queues of obsolete states.
- All layers in a family use one sample clock and equal frame counts.
- Preserve stereo channels, ramp volume changes, and begin interrupted fades from current gains.
- The output adapter uses shared mode so game sound can play at the same time.
- Hard peak clamping is a last guard, not a musical limiter. Keep mixes below it.

## Input and privacy.

- No game injection, process-memory inspection, game-file modification, or anti-cheat workarounds.
- Capture is absent in 0.1. When implemented, use an explicit Windows picker and capture only the chosen window.
- Do not persist frames, screenshots, voice chat, or OCR output by default. Diagnostic saving requires a clear opt-in.
- Do not add network calls or remote screenshot uploads without an explicit product decision.
- Do not train a model before gathering representative, consented observations and evaluating the needed cues.
- Low confidence should preserve a restrained arrangement, not invent combat or victory.

## Verification.

Run from the repository root:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
dotnet build src/Veda.Audio.Windows/Veda.Audio.Windows.csproj -c Release
```

On Windows also run:

```powershell
dotnet build src/Veda.App/Veda.App.csproj -c Release -p:Platform=x64
dotnet run --project src/Veda.App -c Debug -p:Platform=x64
```

For audible transition review:

```powershell
dotnet run --project tools/Veda.Render -c Release -- --render assets/soundtracks/demo-void artifacts/demo-preview.wav
```

State exactly what was compiled, executed, heard, or inspected. A portable render is not proof of device latency or a Windows UI run.

## Repository hygiene.

- Preserve ignored music, credentials, and user files. Do not stage arbitrary local audio.
- Keep dependency versions explicit. Do not upgrade them simply because a newer version exists.
- Do not initialize a remote, push, publish, or deploy unless the user requests that action.
- No open-source license has been selected. Do not replace `LICENSE.md` with MIT or another grant without the owner's decision.
