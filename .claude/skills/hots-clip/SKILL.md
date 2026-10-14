---
name: hots-clip
description: Record a highlight clip of a Heroes of the Storm moment from the user's replays with the myhots-observer MCP server (record_clip) and OBS. Use whenever the user asks to clip, record, capture or make a video of a moment ("clip my triple kill", "record the Deathwing chase", "now from the victim's point of view", "re-record it following X", "clip it"), including when the moment still has to be found first.
---

# Clipping a replay moment

A clip request runs four steps: **find the moment → record it → check the video → report**.
Do them in one go. When the moment is clear, don't stop to ask "shall I clip it?": the request
already said to clip it. Stop and ask only when there's more than one plausible moment (step 1).

The tools are the `myhots-observer` MCP server (`MyHotsObserver/`, tools in
`MyHotsObserver/Tools/`). It drives the running HotS client and records through OBS into
`%USERPROFILE%\Videos\MyHotsInfo Recordings\Clips`.

## 0. Preconditions

- **The observer runs its own client.** When the game isn't running, `open_replay` (and
  `record_clip` with a replay) swap in the observer's Variables.txt (`MyHotsObserver/ObserverVariables.txt`,
  camera follow on, default hotkeys, 1920x1080 windowed fullscreen), start the game through
  Battle.net signed in, and wait for it (about a minute). The user's own Variables.txt files go back
  30s after the client closes. If the user's own client is running, every tool refuses: ask the
  user to close the game, then retry. Never start `HeroesSwitcher_x64.exe` yourself without a
  running client: that opens an unauthenticated client at the login screen.
- **A replay from an older game build restarts the client.** The Switcher closes the running
  client and starts the replay's build (`Versions\Base<build>`), which takes a minute or two.
  `open_replay`/`record_clip` wait for this (up to 4 minutes). If one still fails with "isn't
  running" or "didn't restart" right after opening such a replay, the client is probably still
  starting: check `Get-Process HeroesOfTheStorm_x64 | select Path`, wait, and retry. Don't tell
  the user the game closed.
- **More older-build clips work without the user.** A client started for an older build isn't signed in, and quitting its replay lands on the login screen. So `open_replay` doesn't quit there: it closes the client and starts the Switcher with the next replay, which goes through the current build to the replay's build (1-2 min; OBS shows black frames until its capture re-hooks). A replay the user opened is used as is when its length matches.
- **Getting back to the current build restarts the client through Battle.net.** Through the
  Switcher it would land on the login screen, so `open_replay` closes the old-build client and
  starts the game again (about a minute). Still, record current-build clips first and older
  builds last, to restart less.
- **OBS must be running** with its websocket on and the scene `MyHotsInfo Observer`. Don't touch
  the user's `Record Hots` scene.
- **The server must be on the current build.** If the observer code was rebuilt during this
  session, the running server is stale until Claude Code restarts. Tell the user and wait for
  "restarted".
- `get_state` is a cheap check that the game and OBS are reachable. It also reads the clock.

## 1. Find the moment

- **The user gives a replay and times:** use them. Times from the user are usually the **replay
  panel clock** (the clock `rq`/`find_moments` use). If they say "game time" or read the in-game
  timer, add about 38s to get replay time (e.g. 11:46 game = 12:24 replay). If unsure, check
  with `rq events` around that time.
- **Otherwise:** follow the **hots-find-moment** skill. In short: `MyHotsCli rq --last N find`,
  newest first, small N, and stop at the first hit that fits.
- **Several candidates:** show them in a short table (game, time, what happens) and ask which,
  or ask for a detail that settles it: map, enemy hero, teammate, where on the map, what happened
  just after. The user remembers the game better than the data shows. Recording a guess costs a
  minute or more per clip, and a hunt that recorded four wrong guesses annoyed the user.
- **Follow-ups** ("now from Valla's view", "again but follow Deathwing") reuse the same moment and
  times. Leave out `replay` so it uses the replay on screen, which skips reopening it.

## 2. Record it with `record_clip`

One call does it all: opens the replay (if `replay` is given), plays to `start - leadIn`, pauses,
sets the camera, records to `end + tail` and saves the file.

| Parameter | How to choose it |
|---|---|
| `replay` | Full path from `rq` output (or `latest` / N-th newest). Omit it for the replay already on screen. |
| `start` | The first event that matters, e.g. Deathwing's landing or the first kill. **Not** a long run-up: the user complained about a 17 s "trailer" before the action. |
| `end` | The last event that matters (the kill, the taunt). Max 10 minutes after `start`. |
| `leadIn` / `tail` | Default 5 / 3. Use about 2–3 s of lead-in, and 3–6 s of tail to show the aftermath (kill banner, taunt). |
| `player` | Whose camera to follow. Default `me` for the user's own plays. For "from the victim's point of view", pass the victim's hero or player name. `all` gives the free camera; omit it to keep the current camera. |
| `view` | `hero` (default): number-key follow, with that hero's ability panel. It's reliable, and the camera stays where the hero falls when they die. `everyone`: E plus a portrait click, with no hero panel. See the caveats below. |
| `zoom` | `out` (Z) or `far` (Shift+Z) for a wider view, e.g. for long-range heroes like Chromie; `default` to reset. Omit to leave it as it is. The keys only toggle, so the tool reads the current zoom off the minimap (the width of the white camera outline, against a per-map default width in `cacheminimap-zoom.json`, measured on the map's first zoomed clip) and presses the one key needed. Z goes to out or back from out; Shift+Z likewise for far. If the minimap can't be read it falls back to Shift+Z, Z, Z, which always lands on the default. A hero-view follow also resets the zoom. |
| `name` | `<Map> - <what happens> (<m-ss of the key moment>)`, plus ` - <Hero>'s view` or ` - everyone view` for alternative cameras, e.g. `Battlefield of Eternity - Chromie triple kill (1-20) - Valla's view`. An existing name gets a unique suffix; it's never overwritten. |

**Caveats of the everyone view:**
- A portrait click follows the hero only in camera follow mode (L), which only toggles. With it
  off, the camera stays where it is, e.g. on a core. The user's rule: right after the replay
  starts, click the portrait once; if the camera doesn't move, press L, which centres it.
  `record_clip` does this once per client process, reopening the replay if needed. A paused
  replay's camera doesn't move, so a screenshot taken while paused can't show whether it follows.
- It doesn't follow D.Va in her mech, and nothing follows Deathwing while he flies.
- Death and flight holds work only on minimap-calibrated maps (Dragon Shire and Tomb of the
  Spider Queen; see `MyHotsObserver/Minimap.cs`). Elsewhere, the camera jumps to the core when the
  followed hero dies.
- The tool holds the left mouse button on the minimap during those holds. Tell the user not to
  touch the mouse while it records.
- The camera reaches Deathwing's landing spot about 1.8 s late after take-off (a known open issue).

Prefer the hero view unless the user asks for the everyone view or "full vision without the
panel". The observer always has full vision of both teams in every view.

**Timing:** a forward seek into time the replay hasn't played yet runs at about 11× real time
(about 80 s for 15 minutes). Going backwards, or to time already played, takes seconds. So a
second clip of the same replay that ends earlier than the first is quick. Tell the user before a
long first seek. The clip itself then records in real time.

**If the clip fails or hangs:** don't retry blindly. Call `screenshot` to see what's on screen
(MVP screen, menus, login screen, a paused replay), and say what went wrong. Past causes: a stale
server, the client not running, a replay left on the end screen (fixed), and a missed P press
when resuming (now retried).

## 3. Check the video before reporting

Look at it; don't trust the requested times. Make a contact sheet and read it:

```bash
F="C:/Users/pavia/Videos/MyHotsInfo Recordings/Clips/<name>.mkv"
S="<scratchpad>"
ffprobe -v error -show_entries format=duration -of csv=p=0 "$F"
ffmpeg -v error -y -i "$F" -vf "fps=1,scale=320:-1,tile=6x4" -frames:v 1 "$S/sheet.png"
```

Then use Read on `sheet.png`. For a long clip, lower `fps`, or make sheets of just the key
seconds (`-ss <t> -t <len>` before `-i`). The replay clock is at the top right of each frame, so
you can map clip time to replay time. Check that:
- the key action is in the clip and isn't preceded by a long run-up;
- the camera is on the right hero and there are no long stretches on an empty base or core;
- the clip doesn't start or end on a still frame.

If something is clearly wrong (wrong moment, camera lost), say so and offer a re-record with the
fix you'd make. Don't silently re-record many times.

## 4. Report

- The file path, its length, and the replay range it covers.
- A small table: clip time | replay time | what's on screen, covering the key beats.
- Anything off: a late or early start, a camera glitch, a moment that doesn't match the request.
- Earlier near-duplicate or test clips: list them for the user to delete. Never delete clips
  yourself.

## Related

- **hots-find-moment**: searching replays for the moment.
- **hots-clip-merge**: combining several views of one moment into one video.
