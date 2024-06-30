# Open Sync Dance

> [!WARNING]  
> This package is under heavy development. Things may break! Use at your own risk.

Open Sync Dance is a utility and Unity prefab to have player-synchronized dances in VRChat [like in this video](https://www.youtube.com/watch?v=I_MiNH-j1dw).

## Dependencies

- [Haï's Animator As Code](https://github.com/hai-vr/av3-animator-as-code)
- [Haï's Animator As Code - VRChat](https://github.com/hai-vr/animator-as-code-vrchat), version `1.1.0-beta` or higher
- [VRCFury (optional)](https://vrcfury.com/)

## How to use

1. Get the above dependencies.
2. Add the Open Sync Dance package to VCC via the listing at [`befuddledlabs.github.io/OpenSyncDance`](https://befuddledlabs.github.io/OpenSyncDance/).
3. Drag and drop the `OpenSyncDance` prefab from `Packages/Open Sync Dance/Samples` onto a VRChat avatar.
4. On the prefab, download the missing audio clips. This will download from the supplied URLs and cut them to length. You can configure other things here too, like selecting animations and swapping songs.
5. If you have VRCFury, the avatar will be ready to upload. If not, please merge the animators, parameters and menu 0 with your current avatar.
6. Upload & dance! 💃💃

## Included Dances

- Arona by [*THEDAO77*](https://thedao77.booth.pm/)
- Helltaker by [*THEDAO77*](https://thedao77.booth.pm/)
- Pokemon by [*THEDAO77*](https://thedao77.booth.pm/)
- Anhka by [*THEDAO77*](https://thedao77.booth.pm/)
- Badger by [*Krysiek*](https://github.com/Krysiek)
- Shoulder Shake by [*Krysiek*](https://github.com/Krysiek)
- SAR Default by [*Krysiek*](https://github.com/Krysiek)
- Zufolo Impazzito by [*Krysiek*](https://github.com/Krysiek)
- Distraction by *Spooki Boy*

Want to add to this list? [Check the contributing guide for animations!](/Docs/contributing_animations.md)

## Development

To make modifications to this package:

1. Clone this repository to a non-unity project folder.
2. Create a symbolic link from the package into a Unity project's package folder.
3. The package should be editable via Unity and any external editor.

## Acknowledgements

- [*DeltaNeverUsed*](https://github.com/DeltaNeverUsed) 💻
- [*Nara*](https://github.com/Naraenda) 💻
- [*Airishayn*](https://x.com/Airishayn1/) 🎨
  - For making the banner for the package listing
- [*THEDAO77*](https://thedao77.booth.pm/) 💃
  - For allowing us to redistribute animation files.
- [*Krysiek*](https://github.com/Krysiek) 💃
  - For creating [CuteDancer](https://github.com/Krysiek/CuteDancer) and animation files.
- *Spooki Boy* 💃
  - For creating [CuteDancer](https://github.com/Krysiek/CuteDancer) and animation files.
- [*yt-dlp*](https://github.com/yt-dlp/yt-dlp) 🛠️
  - For downloading and extracting audio files from YouTube videos.
- [*FFmpeg*](https://ffmpeg.org/) 🎞️
  - For handling audio extraction and conversion.

## Examples

> _The Open Sync Dance component (0.0.7)_
>
> ![UI Preview of OSD v0.0.7](/Docs/osd_ui_preview.png)
