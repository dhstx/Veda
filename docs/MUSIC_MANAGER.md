# Customize Veda's music.

This is Prototype 0.2.0. It adds a music manager and a disconnected observation control. Music remains manually directed; described situations do not detect themselves yet.

## Start with your existing songs.

1. Back up your current Veda source and VedaMusic folders. Extract this update and run `Run-Veda.bat` in its `Veda` folder, using your existing development tools.
2. Click **Load song library** and select your existing **VedaMusic** folder. You can also select a new empty folder to start with custom music.
3. Veda adopts existing WAVs in the four default genres and six default situations as protected Default songs. It copies legacy genre/situation files into a `Default` subfolder, leaving the originals untouched. Loading again does not duplicate them. This first adoption can take time and needs enough disk space for the copies.
4. Expand **Manage music, genres, and situations**. Choose the genre and situation you want to manage. Their songs appear with **Default** or **Custom** labels and individual on/off checkboxes.

The four protected genres are ambient synth, synth hard rock, modern hiphop, and western. The six protected situations are Calm space, Travel, Uncertain, Combat, Escape, and Relief. They remain available even when empty. Default songs can be turned off individually or through **Include Default songs**, but have no Remove action. Default categories also have no removal action.

## Add custom songs.

Choose a genre and situation in the manager, then click **Add WAV songs from device…**. The desktop file picker lets you select one or multiple WAVs. Accepted songs are copied into VedaMusic and mixed with the enabled Default songs for that selection.

Custom imports must satisfy these requirements:

- A real `.wav` file, no longer than **4:00 / 240 seconds**, inclusive. Renaming MP3 to WAV does not convert it.
- The current player supports **stereo 48 kHz, 16-bit PCM or 32-bit IEEE float, ordinary RIFF WAV**. Other WAV encodings, mono, other sample rates, and WAVE_EXTENSIBLE need conversion first.
- Audio must be readable, contain finite valid sample values, and contain an audible signal. Extremely short files below 0.1 second are rejected.

The picker filters for WAV, and Veda validates every selected file before adding it. Rejected files are listed with the reason and requirements. A batch can include both accepted and rejected songs. Accepted files receive **Custom** labels regardless of whether they were put in a default or custom category.

Use **Include Custom songs** to switch that entire source off, or use a song's checkbox. Switching a source off preserves individual selections. With both sources off, playback settles to silence. Removing a Custom song removes Veda's copy and its catalog entry; the original on your device stays untouched. You confirm removal inside the app.

## Add genres and situations.

Enter a genre name, such as Industrial, Classical, or Country, and click **Add genre**. A genre name is a label for your collection; Veda does not infer a song's genre.

For a new situation, enter a short name and describe when it should apply. Examples:

| Name | Description |
| --- | --- |
| Blue star | Whenever it is evident that I am near a blue star. |
| Ice cave | Whenever I am in a dark cave with ice. |

Click **Add situation**, choose it in the manager, and import its custom songs. Custom situations are shared across the library, so you can put different music for the same situation in several genres. The **Playback situation** dropdown lets you test any situation that has enabled songs in the selected playback genre. The six original manual buttons and keys remain available.

Only Custom genres and situations have a Remove action. Removing one also removes its imported custom songs across the library; the confirmation states the song count. Protected default items remain. Descriptions persist for future AI matching, but no screen analysis or matching currently occurs.

## Folder layout and saved choices.

All music uses genre → situation → origin → song:

| Example | Origin |
| --- | --- |
| `VedaMusic/ambient synth/calm space/Default/01.wav` | Protected Default |
| `VedaMusic/ambient synth/calm space/Custom/<song-id>.wav` | Removable Custom |
| `VedaMusic/<custom-genre>/<custom-situation>/Custom/<song-id>.wav` | Custom categories |

Custom names and descriptions are stored as typed. Veda generates safe folder names with stable identifiers, so punctuation in your description cannot become an invalid Windows path. Imported filenames use IDs; the manager and player display the original song titles.

`veda-library.json` inside VedaMusic stores categories, descriptions, origin labels, individual and source switches, and the crossfade value. Keep it with the music folder and back it up. On a later launch, load the same VedaMusic folder to restore those choices. Avoid manually moving managed files or editing this catalog; use the manager for custom items. Files manually dropped in Custom folders are not imported until you use the Add button.

Future default music goes into the matching protected genre/situation/Default folder. Legacy direct genre/situation WAVs are still adopted. Reload VedaMusic to discover new default songs. Only default songs use the older 12-second to 10-minute content contract; the four-minute limit applies to every Custom import.

An optional `track-cues.json` at the VedaMusic root can still override Default song cues. Keys should use their actual relative paths, such as `ambient synth/calm space/Default/01.wav`; legacy original paths continue to work during adoption. Reload after editing. Veda never rewrites the original audio.

## Crossfade and quiet breaks.

**Crossfade** replaces Drama and spans **0–20 seconds**. At 0, Veda plays the full current file to its end before the next prepared song starts, with no overlap. This also applies to a queued manual genre, situation, or Next request. Above 0, Veda overlaps successive songs by the selected duration, limited by the available audio. Settings apply to upcoming transitions; an existing fade completes normally. File preparation can cause a gap if a next song is not ready in time.

Automatic cue selection and the explicit Beginning / Quieter / Stronger setting still control where the next song enters. **Allow 20-second quiet breaks** remains independent: calm and relief can finish, leave silence, then resume. Uncheck it to test continuous rotation. At 0 crossfade a calm/relief quiet break begins after the full file ends. Volume remains independent. Responsiveness is only used by the startup stem demo.

## Gameplay observation.

Turn on **Gameplay observation mode** to see **Disconnected**. This is the requested control for the next milestone. It starts no capture, uploads no frames, calls no model, and changes no music states. Custom description matching will be implemented and tested later.

## Check this update on Windows.

Try one Default song switch, the two source switches, one custom import, an over-four-minute rejection, a custom genre and described situation, removal of a custom song/category, and reopening the same library. Compare crossfade 0 with 8 seconds while a song reaches its end. Confirm the observation toggle reports Disconnected. This update's core checks and compile-only UI verification do not replace a real Windows launch and listening test.
