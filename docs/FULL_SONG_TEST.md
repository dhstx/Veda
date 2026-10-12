# Test your full Suno songs.

Prototype 0.2.0 adds library management to the manual full-song player. See [MUSIC_MANAGER.md](MUSIC_MANAGER.md) for Default/Custom songs, described situations, and import controls. It accepts your two Calm space WAVs and three Combat WAVs without conversion. Different situations can have different track counts. It does not yet watch or interpret gameplay.

## Set up the music folder.

1. Keep a backup of your old Veda project. Use the new `Veda` source folder from this ZIP for the test.
2. Create a separate Desktop folder named `VedaMusic`.
3. Inside `VedaMusic`, create `ambient synth`. Extract your `calm space.zip` and `combat.zip` into that genre folder. The five files should have these paths relative to `VedaMusic`:

| Situation | Relative file path |
| --- | --- |
| Calm space | `ambient synth/calm space/01.wav` |
| Calm space | `ambient synth/calm space/02.wav` |
| Combat | `ambient synth/combat/07(1).wav` |
| Combat | `ambient synth/combat/07.wav` |
| Combat | `ambient synth/combat/08.wav` |

The layout is **VedaMusic → genre → situation → songs**. Genre folders such as `ambient synth`, `modern hiphop`, `synth hard rock`, and `western` sit directly inside `VedaMusic`. Each contains its own Calm space, Travel, Uncertain, Combat, Escape, and Relief folders as needed. Genre names are your choice; spaces, underscores, or hyphens are allowed in situation names. You can have two, three, or any number of songs per situation.

Select `VedaMusic` in the app, rather than an individual genre folder. Songs directly inside a genre folder or in the old situation-first layout are skipped with a warning. Move your existing situation folders inside the matching genre folder; no audio conversion is needed.

Use stereo 48 kHz, 16-bit PCM or 32-bit IEEE float WAV, ordinary RIFF. Files may differ in length; they do not need to share a tempo, key, or frame count for this mode. Musical compatibility still needs listening review. Tracks must be 12 seconds to 10 minutes. MP3, 24-bit PCM WAV, mono, other sample rates, and WAVE_EXTENSIBLE are skipped with a warning. Your five uploaded files already match the supported format.

Original songs are read only. Import scans one track at a time to estimate energy and cue points; it may take a moment on a large library. Playback preloads individual tracks outside the audio callback rather than keeping the whole library decoded. This is not a disk-streaming player. Long tracks, rapid changes, and device performance need measurement on your PC.

## Launch on Windows.

For everyday launches, you can instead double-click **Run-Veda.bat** in that source folder. It runs the app command below without manually opening Terminal. Install the development tools first and extract the ZIP before launching. Keep the launcher in the source folder; a desktop shortcut can point to it.

To run manually, open Terminal in the source folder containing `Veda.slnx` and use:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
dotnet run --project src/Veda.App -c Debug -p:Platform=x64
```

Use your existing .NET 10 SDK and Visual Studio/WinUI development setup. The download is source code, not an executable installer. A complete Windows UI build and hardware playback remain unverified in the authoring environment.

1. Click **Load song library**, choose `VedaMusic`, and wait for inspection to finish.
2. Check that Veda reports two Calm space tracks and three Combat tracks for the sample folders.
3. Choose a genre in the **Genre** dropdown. It switches music within the loaded library, keeping the current situation when that genre has matching songs. Otherwise it selects an available situation. Missing situations are disabled. Adding or removing files requires **Load song library** again; genre switching does not.
4. Click **Play**, then use **Calm space** and **Combat**. Situations without songs in the selected genre are disabled. The keys 1–6 work while Veda has focus; they are not global game hotkeys.
5. Use **Entry for the next track** to try Beginning, Quieter section, and Stronger section. Changing this setting affects the next track, not the song already playing. **Automatic** uses quiet cues for calm/relief and strong cues for combat/escape.
6. Click **Next track / End quiet break** to test another available song or end a silence period.
7. With **Allow 20-second quiet breaks** checked, calm and relief finish with a gentle fade and a pause before another song. A new situation or Next track can interrupt the pause. Uncheck it to test continuous overlap instead.
8. Stay in Combat through a track ending to hear rotation. With three tracks it avoids the last two; with two it alternates. With one it can replay that same song.
9. Repeat requests for the same active situation: the player should keep the current music rather than restart it.

Automatic rotation prepares the next song near the ending and follows the **Crossfade** setting (0–20 seconds). At 0, it finishes the full file before starting the next prepared song. Positive values control overlap, limited by available audio. Manual changes also use that setting; a calmer change can still wait for an estimated lower-energy exit. These are transparent cue/recency rules, not AI taste or beat alignment.

Crossfade replaces Drama. Responsiveness is disabled in full-song mode because it belongs to the startup stem demo. Volume remains available. The Load stem pack button remains removed. The music manager now identifies existing songs as protected Defaults and adds removable Custom songs through the file picker. Custom imports must be WAVs no longer than four minutes, in the format described in the music manager.

## Listen to the preview first.

`Veda-Listening-Test.wav` is outside the source folder in the ZIP. It is a 60-second render through the same full-track player used by the app. It is an accelerated demonstration; the actual player does not switch every 12 seconds.

| Preview time | Action |
| --- | --- |
| 0 seconds | Calm 01 from its proposed quiet cue |
| 12 seconds | Combat 07(1) from its proposed strong cue |
| 24 seconds | Combat 07 from its proposed strong cue |
| 36 seconds | Calm 02 from its proposed quiet cue |
| 48 seconds | Four-second fade to silence |
| 55 seconds | Calm 01 returns |
| 58 seconds | Two-second preview ending fade |

The preview has zero numerically clamped samples. It was rendered and measured, not auditioned by a human in this environment. Listen for awkward mid-phrase entries, incompatible notes during overlap, harsh percussion, and noticeable loudness changes. A waveform energy estimate cannot certify a musically good cue or the absence of vocal sounds.

## Review or override the cue points.

The automatically estimated cue points from your uploads are listed below. Strong means a comparatively loud section, not a confirmed combat mood or rhythmic climax. Quiet also refers to audio energy. A track's quieter portion can still be tense.

| File | Length | Quiet cue | Strong cue |
| --- | ---: | ---: | ---: |
| Calm 01 | 3:29.60 | 0:16 | 2:32 |
| Calm 02 | 3:19.96 | 0:08 | 0:38 |
| Combat 07(1) | 2:53.56 | 0:11 | 2:06 |
| Combat 07 | 1:32.40 | 0:10 | 0:44 |
| Combat 08 | 3:14.00 | 0:09 | 2:26 |

The optional `Sample-cues/track-cues.json` beside the source folder contains those estimates. For the original sample folders, copy it into `VedaMusic` before first adoption if you want overrides; otherwise the importer computes them. Times are seconds. Keys are paths relative to the folder you select, using `/`, and must match your actual layout. Include the genre first in each key. After adoption, the standardized path includes `Default/`, such as `ambient synth/calm space/Default/01.wav`. Legacy paths continue to be read while their original files remain. Existing cue files from the old situation-first layout need their paths updated. Change the numbers after listening, then load the library again. Each entry must leave at least eight seconds before the end cue.

```json
{
  "ambient synth/calm space/01.wav": {
    "QuietSeconds": 16,
    "StrongSeconds": 152,
    "EndSeconds": 209.6
  }
}
```

No songs were rewritten, trimmed, or renamed. Playback reduces peaks when needed to leave headroom; it does not perform perceptual loudness matching. If mixes feel uneven, adjust working copies in an audio editor after listening. Matching tempo/key does not prove that two melodies overlap comfortably.

## What to record during your test.

Note the song, approximate timestamp, situation, entry setting, and what sounded wrong or right. Useful feedback includes an awkward cue, a crossfade that should be longer or shorter, a track that feels too intense for a situation, and whether the quiet break feels natural. Test pause/resume, rapid Calm/Combat changes, reloading folders while playing, switching genres, and closing during import. Test alongside Star Citizen with the game's music reduced and effects/dialogue preserved.

For the future AI's contextual requirements, see `AI_MUSIC_DIRECTION.md`. This build proves the playback mechanism and gives us cue feedback; it does not classify enemies, shooting, places, or danger.
