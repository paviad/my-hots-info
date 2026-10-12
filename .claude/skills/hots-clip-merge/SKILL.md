---
name: hots-clip-merge
description: Combine several recorded HotS clips of the same moment (e.g. the user's view and the victim's view) into one video with ffmpeg, synced on the replay clock: checkerboard or grid, side by side, or picture in picture. Use when the user asks to merge, combine, stack, sync or put clips side by side, split screen or picture-in-picture.
---

# Merging clips of one moment

Clips from **hots-clip** live in `%USERPROFILE%\Videos\MyHotsInfo Recordings\Clips` (MKV,
1920×1080, 60 fps, with game audio). Each one starts at a slightly different replay time:
`record_clip` reports `from` only to the whole second, and the actual start varies by up to a
couple of seconds. **Sync on the replay clock in the footage, not on the reported times.**

Work in the scratchpad. Write the result to the Clips folder, and never overwrite or delete the
source clips.

## 1. Find each clip's offset from the clock ticks

At 1920×1080, the replay panel's clock is the 110×34 region at (1556, 116). Frames where it
changes are the moments the clock ticks to the next second:

```bash
cd "$USERPROFILE/Videos/MyHotsInfo Recordings/Clips"
for f in "A.mkv" "B.mkv"; do
  echo "== $f"
  ffmpeg -i "$f" -an -vf "crop=110:34:1556:116,select='gt(scene\,0.02)',showinfo" -f null - 2>&1 \
    | grep -o 'pts_time:[0-9.]*' | tr '\n' ' '; echo
done
```

Ticks are a second apart. Ignore hits less than a second from the previous one (e.g. 0.07 and
0.34 followed by 2.35, 3.35), which are scene changes at the start of the clip.

Read the clock at one tick in each clip to learn which second it ticked to. Crop that frame,
scale it up and Read it, e.g. `ffmpeg -ss <t> -i A.mkv -frames:v 1 -vf "crop=110:34:1556:116,scale=330:-1" a.png`.
For each clip, (clip time of a tick, replay second it ticked to) gives that clip's start in
replay time. The offset between two clips is the difference of their starts (expect 0–3 s).

## 2. Compose

Trim the clip that starts earlier by the offset, so both start on the same replay instant and
neither shows a still frame:

```bash
ffmpeg -y -ss <offset> -i "Early.mkv" -i "Late.mkv" -filter_complex "<layout>" \
  -map "[v]" -map "1:a" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p \
  -c:a aac -b:a 192k -shortest -movflags +faststart "<Map> - <moment> - <A> + <B> synced.mp4"
```

If the user wants the earlier clip's opening seconds kept, pad the later clip instead:
`tpad=start_duration=<offset>:start_mode=clone` on its video and `adelay=<ms>:all=1` on its
audio. That clip holds its first frame until the other catches up.

Layouts (`[0:v]` and `[1:v]` are the two inputs):

| Layout | Filter |
|---|---|
| Checkerboard: A top-left, B bottom-right, black corners (3840×2160, full-size views) | `color=black:s=1920x1080:r=60[b1];color=black:s=1920x1080:r=60[b2];[0:v][b1][b2][1:v]xstack=inputs=4:layout=0_0\|w0_0\|0_h0\|w0_h0:shortest=1[v]` |
| Side by side (3840×1080) | `[0:v][1:v]hstack=inputs=2:shortest=1[v]` |
| Side by side at 1920 wide | `[0:v]scale=960:-2[a];[1:v]scale=960:-2[b];[a][b]hstack=inputs=2:shortest=1[v]` |
| Picture in picture (B small, top right) | `[1:v]scale=640:-2[p];[0:v][p]overlay=W-w-24:24:shortest=1[v]` |

For 2×2 grids of more clips, use `xstack` with one input per view. Find every clip's offset
against the clip that starts latest, and trim each by its own offset.

**Audio:** use one clip's game audio only. Mixing two recordings of the same game sound echoes.
Say which one you kept and offer to add the other.

The output is 4K for the full-size layouts. Mention it, and offer a 1920×1080 version (each view
scaled down) if the user wants something lighter to share.

## 3. Verify the sync

Run the tick detection on each view's clock region in the **output** (offset the crop by the
view's position, e.g. `crop=110:34:3476:1196` for the bottom-right view of the checkerboard). The
tick times should agree to within a couple of frames. Also make a frame sheet (see hots-clip,
step 3) and look at it.

Report the output path, the layout, the length, the replay range it covers, the measured offset,
the audio choice, and the residual sync error.
