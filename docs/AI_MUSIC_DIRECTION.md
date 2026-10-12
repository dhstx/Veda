# Contextual music direction requirements.

The product should behave like an attentive music director. It needs to understand the player's activity and surrounding risk, choose a suitable piece and entry section, preserve a moment that is working, and sometimes choose silence. Switching whenever a button-like visual event appears is not the intended behavior.

These requirements come from the owner's October 10, 2026 direction. They are accepted design requirements, not implemented gameplay capabilities in Prototype 0.2.0. No capture, model, place understanding, hostile-target recognition, or AI song-quality evaluator is present yet.

## Separate the decisions.

1. **Observe gameplay.** The selected game window supplies ephemeral frames. Interpretation should describe the environment, activity, target type, hostility, damage/incoming fire, movement, and uncertainty. Do not treat a single screenshot as complete context.
2. **Estimate context over time.** Maintain a bounded history of observations with confidence and evidence. Keep liveliness, suspiciousness, and actual threat separate. Low confidence should hold a restrained choice rather than invent danger.
3. **Direct the music.** Decide whether to hold, change, enter a different section, finish naturally, leave silence, or resume. Consider current song position, recent playback, musical intensity, scene stability, and available cues.
4. **Render a validated plan.** Prepare audio away from the callback, then send the player a track, entry time, delay, and fade. Interpretation never runs on the audio thread.

## Danger needs context.

| Observed context | Required musical reasoning |
| --- | --- |
| Shooting a rock, door, prop, salvage target, or other inanimate object | Firing alone does not justify Combat. Use the activity and setting; keep the current music or a restrained scene-appropriate choice. |
| Weapon in view, aiming, testing a gun, or isolated muzzle flash | Treat these as ambiguous cues. Require supporting evidence of hostile engagement before escalating. |
| Incoming hostile fire, attacks directed at the player, damage plus corroborating hostile cues, or confirmed enemy engagement | Allow prompt escalation. Do not wait for the current song to end merely for musical convenience. |
| Crowded city, busy station, traffic, friendly NPCs, or lively nonthreatening activity | Liveliness can support motion or a more active groove without being scored as danger. |
| Remote, suspicious, or sketchy surroundings without active hostility | A restrained uncertain atmosphere may fit. Avoid escalating from an area's look alone. |
| Target shooting while a real hostile attacks nearby | The harmless target does not override actual danger; evaluate independent incoming-threat evidence. |
| Quiet after danger | Validate de-escalation over time, release tension gently, and allow Relief or silence. Avoid automatic victory music. |
| Capture lost, game minimized, ambiguous HUD, or stale observations | Report uncertainty and apply the chosen pause/hold policy. Do not hallucinate a new event. |

Area reputation, location appearance, and scene bustle are supporting context, not substitutes for live threat evidence. The system should be evaluated on representative Star Citizen observations before any detection claim. It must respect the existing privacy and anti-cheat boundaries in `AGENTS.md`.

## Choose the right music, not just a random file.

Keep user-selected genre and situation as candidate filters. Rank candidates using artistically reviewed suitability, entry-section intensity, rhythmic density, mood, recent playback, and how well the ending/starting material can blend. Do not equate louder with more appropriate. A soft section of a combat piece can remain threatening, and busy percussion in a peaceful place can remain friendly.

The current importer uses only the folder labels and an energy envelope, and the current selector uses recency and cue energy. This is a transparent testing baseline. Future metadata should support reviewed section boundaries and contextual suitability; future automated audio analysis needs validation against listening judgments. Model judgments should explain the supporting context and remain overridable.

Allow playback to start inside a song when that section fits the moment better than its opening. Cue points need enough remaining material and a suitable fade-in; entry cannot be assumed safe merely because it is loud or quiet. Exit decisions should consider phrase endings and texture, not only a fixed stopwatch. Independent Suno tracks are not guaranteed to share a beat grid even if prompted at the same tempo.

## Preserve patience and allow silence.

Repeated observations of the same situation should usually hold the current song. Small camera changes and isolated UI flashes should not create a new soundtrack request. Use persistence and confidence before nonurgent changes. Serious confirmed danger can interrupt that patience.

Calm and Relief may finish naturally into silence. Silence is an intentional musical state, separate from playback failure, unavailable songs, or loss of capture. During silence the director continues observing gameplay. It can resume an appropriate new piece, replay a suitable track after enough spacing, or enter a selected section if the situation changes. It should not always restart a track on a timer.

The current 20-second quiet break and fixed fade defaults are configurable test behavior only. The final policy should consider how long silence has lasted, whether the scene is stable, whether music would add value now, recent fatigue/repetition, and clear changes in activity. Music should neither rush to fill every pause nor remain silent after important danger evidence appears.

## Minimum evaluation set before enabling automation.

Test harmless shooting, real incoming combat, mining/salvage, friendly crowded areas, suspicious quiet areas, combat followed by ambiguous calm, prolonged peaceful play, sudden combat during silence, UI overlays, capture loss, and rapid apparent scene changes. Record false combat triggers, missed danger, unnecessary transitions, track-entry quality, time spent silent, repetition, and game/audio performance. Use opted-in, representative evidence; do not save frames by default.

The next implementation milestone remains explicit window capture. After reliable capture, add confidence-bearing observations and evaluate the interpretation before enabling automated music decisions. Manual full-song controls remain available throughout development.
