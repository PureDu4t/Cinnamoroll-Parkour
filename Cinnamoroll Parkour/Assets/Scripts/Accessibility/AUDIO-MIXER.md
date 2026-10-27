# Three-channel volume settings

Startup flow: first voice Loading -> bilingual Audio Mixer -> Language -> Speed -> Ready.
The same Audio Mixer appears when opening voice settings from the Main Menu.

## Player controls

- Up/Down or W/S: choose Music, Sound Effects, or TTS.
- Left/Right or A/D: adjust by 5%, from 0 to 100%.
- Mouse: click or drag the selected slider.
- R: preview the selected channel. Music uses the currently playing music; effects use the UI action Event; TTS uses a short bilingual sample.
- Q: stop the speech preview. E: save and continue to language selection.
- Settings persist under Audio.Music, Audio.SoundEffects, Audio.Tts in PlayerPrefs.

## Unity configuration

Assets/Resources/AudioMixerSettings.asset:

- Music Volume Rtpc: Music_Volume
- Sound Effects Volume Rtpc: SFX_Volume
- Fade Milliseconds: 50

Both Wwise RTPCs are global, 0–100. TtsLoadingMusic reapplies the saved values after its SoundBank loads, before starting loading music. Changes take effect immediately. GameTtsManager separately applies TTS gain to its AudioSource. AudioListener.volume is not changed.

Mixer spoken prompts and TTS samples are editable in Assets/Resources/TtsSettingsContent.asset. These are prepared before the initial Loading page closes.

## Wwise setup (designer configuration)

1. In Game Syncs -> Game Parameters, create Music_Volume and SFX_Volume, Min 0, Max 100, Default 100.
2. Use separate Audio Buses below Master Audio Bus: Music and SFX (or equivalent existing buses).
3. Route music, loading loops and musical transition stingers into Music. Route all remaining Wwise sounds into SFX: UI, footsteps, navigation/ticks, gameplay, ambience and prerecorded voices. Synthesized TTS is Unity audio and needs no Wwise Bus.
4. On the Music bus RTPC tab bind Music_Volume to Bus Volume. On the SFX bus bind SFX_Volume to Bus Volume.
5. Suggested curve points (input %, output dB): (0,-96), (1,-40), (5,-26), (10,-20), (25,-12), (50,-6), (75,-2.5), (100,0). This approximates the linear amplitude used for TTS, with an effectively silent zero endpoint.
6. Keep the existing creative mix on child buses. Apply each user RTPC at one category parent bus only, avoiding double attenuation on its children.
7. Route reverb returns under the appropriate category. A shared Music/SFX reverb return cannot independently follow both sliders; split it if independent wet-path control is needed.
8. Generate the updated SoundBanks for each target platform, refresh/import in Unity, then rebuild the application.

Wwise authoring files are not modified by this implementation.
