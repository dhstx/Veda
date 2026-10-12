# Next build prompt for your coding agent.

Use this after you have run the manual Windows prototype. Give the coding agent access to the whole repository, then send the following request.

---

Read `AGENTS.md`, `docs/PRODUCT_SPEC.md`, `docs/ARCHITECTURE.md`, and `docs/VERIFICATION.md`. First run the existing portable checks and build the Windows app. Resolve any real environment or build issues before adding a feature.

Implement **Prototype 0.2: explicit Star Citizen window capture**, while preserving the current manual music mode. This task is capture only; do not add a vision model or automatic combat classification yet.

Use Windows.Graphics.Capture and a Windows-owned picker so the user deliberately selects the game window. Do not guess a window, read process memory, inject a hook, modify game files, or suppress Windows capture indicators.

Implement the `IGameFrameSource` contract. Acquire frames in memory, downscale them for later analysis, and publish at most one sample about every 500 ms. Use a bounded latest-frame handoff so a slow consumer never builds a queue. Define ownership and disposal clearly, including resize, capture loss, game close, picker cancellation, and app shutdown.

Do not write frames to disk. Do not upload them. Do not include game screenshots, OCR text, or voice audio in logs. Keep diagnostic state in memory.

Add explicit Start capture / Stop capture controls and truthful status. A small temporary debug preview may be visible only when the user enables it; it must not claim that gameplay recognition is active.

Capture and conversion must run independently of audio rendering. The soundtrack should remain smooth when the selected window resizes, capture stalls, or capture stops. A stopped capture must release graphics resources and clear retained image data.

Keep dependencies minimal. Prefer Windows platform APIs already available through the app's target. If a graphics interop package is necessary, explain its purpose and add only that package. Do not introduce a backend, account system, cloud model, or video encoder.

Verify on Windows with a real selected window. Exercise cancellation, minimized/closed windows, resize, stop/start, and app shutdown. Measure capture CPU/GPU cost and effect on game frame times before claiming it is lightweight. Confirm no saved screenshots are produced. Re-run the existing audio checks and listen to playback during capture.

Provide a concise change summary and list exactly what was verified and what still needs manual checking. Keep the next milestone focused: confidence-bearing gameplay observations and temporal state policy, implemented only after the capture pipeline works.

---

Do not ask the agent to “finish the full AI soundtrack platform” in one pass. This request connects the next input component to the existing engine without changing the musical foundation.
