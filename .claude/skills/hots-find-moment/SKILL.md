---
name: hots-find-moment
description: Find a moment in the user's Heroes of the Storm replays (a kill, a multikill, a chase, a save, a heroic, "the game where I...") with the replay query language, through `MyHotsCli rq` or the observer's find_moments. Use when the user asks to find, locate or list moments, events or games, and as the first step of a clip request (see hots-clip).
---

# Finding a moment in the replays

The replay query language (`rq`) turns each replay into a timeline of events (deaths, casts,
talents, pings, chat, unit deaths, form changes) and searches it for patterns. The full reference
is [rqdocs/index.html](../../../rqdocs/index.html), and `MyHotsCli rq find --help` prints the
language summary. Library: `MyReplayQuery/`.

## How to search

**Across games, use the CLI, newest first, and stop at the first hit that fits:**

```bash
MYHOTSINFO_DB=W:/MyHotsInfo/my.db /w/MyHotsInfo/bin/pub/MyHotsCli.exe rq --last 10 find "<query>"
```

- Start with a small `--last` (5–15) and widen only if nothing fits. "A while back" or
  "recently" means the last week or two (the current build) unless the user says otherwise.
- **Never scan the whole history**, and never call `find_moments` once per replay. A search that
  did both took minutes over 200+ replays, and the user interrupted it. The first replay checked
  had been the right one.
- Don't use `list_replays` with a big `scan` either: it keeps every parsed replay in the server's
  memory.
- Each replay costs a few seconds to parse. The first run of a new build may add replays to the
  ability table first (`Adding N new replays...`).
- Add `--json` to get the full replay path (`record_clip` needs it), and times in seconds.
- Use `--from`/`--to m:ss` for "early in the game" or "before the first objective".
- `rq events -r <path> --from a --to b --player me,<name>` prints the raw timeline. Use it to
  check a candidate, or to see how something actually appears in the data before you write the
  query.

**Within one known replay**, `find_moments(query, replay)` on the MCP server is equivalent. It
avoids a CLI start, and its times are the same.

**Limited to one hero ("my last 50 Chromie games"):** `rq` has no hero filter, so take the
games from the database and run `--last K`, where K is the number of replays that reaches back
to the oldest of them. Then keep only the matches from that hero's games:

```bash
sqlite3 W:/MyHotsInfo/my.db "select strftime('%Y%m%d%H%M%S', r.TimestampReplay, 'localtime'), c.CharacterId
  from Replays r join ReplayCharacters c on c.ReplayId = r.Id where c.IsMe = 1
  order by r.TimestampReplay desc limit 200"
```

- The DB time is UTC. Converted with `localtime`, it equals the digits of the replay file name's
  first 19 characters (`2026-10-12 01.36.13 ...` gives `20261012013613`). Compare digits only,
  because the user renames some files (`2026-07-23 205642 Garden of Terror funny stomp QM`).
- Python is broken in the Bash shell (a bad `PYTHONHOME`), so do the filtering in PowerShell
  (`ConvertFrom-Json`).
- Parsing about 150 replays takes about 4 minutes. When the replays include an older build, its
  ability table is built first.

**"My triple kill":** the user means **killing blows** (`killer: me`), not takedowns
(`credit: me`). Takedowns give about 20 times as many hits, mostly teamfights where the user was
only credited.

Times are on the **replay panel clock**, which is what `record_clip` and `seek` take. The in-game
timer runs about 38 s behind it.

## Writing the query

```
death(victim: enemy, killer: me) count >= 3 within 12s            # my triple kill (killing blows)
death(victim: enemy, credit: me) count >= 3 within 12s            # ...or one I took part in
let land = cast(hero: Deathwing, slot: D) near(me, 6)
find land then death(victim: Deathwing, credit: me) within 45s    # Deathwing lands on me, then dies
unitdied(unit: ChromieTimeTrap) near(ally, 3)                     # a Time Trap of mine going off by a teammate
form(hero: Deathwing, unit: DeathwingDragonflightUnit)            # Deathwing taking off
talent(player: me, tier: 10)                                      # when I took my heroic
```

- The patterns above are bare; a query must start with `let` or `find`
  (`find death(victim: enemy, killer: me) count >= 3 within 12s`).
- `me` is the user (the toon whose folder holds the replay; in-game name "Player"). `ally` means
  teammates **not** including the user; use `me|ally` for the whole team. `credit` is everyone
  credited with the kill; `killer` is the killing blow only.
- `credit: !me` means the user wasn't credited at all, not that "someone else was".
- Ability labels are hotkey slots voted per build (`Chromie.Q`, `Deathwing.D?`). A `?` means low
  confidence, or a heroic inferred from talents. An ability the table doesn't know shows as
  `Hero.#id` and won't match `slot:`. If a search by slot finds nothing, look at `rq events` for
  `#id` casts.
- Talent names are internal ids (`ChromieHeroicAbilitySlowingSands`), so match them with
  wildcards.

## What the data can't tell you

No damage, no health, no buffs or stasis, and no record of who stood in a zone. Positions are
estimates from move commands (usually within a few units, sometimes 8 or more off). Taunts and
emotes aren't recorded as such; a `ping` from the user right after a kill may be the taunt.
Spawn and unit-death times are whole seconds.

So a "save", a "dodge" or a "clutch" can only be narrowed to candidates. For these:
- **Ask the user for anything they remember before searching wide:** map, roughly when, the
  enemy hero or heroic, the teammate, where on the map. One detail ("Spider Queen, saved our
  Nazeebo from Falstad's ult, top half of the map") beats several rounds of guessing. A search
  without it produced four wrong clips.
- Don't assume an order of events. In the real Time Trap save, the stasis came first and the
  heroics passed through it afterwards.
- Present candidates as a table (game, time, what the data shows, why it might or might not be
  it) and let the user pick. Don't record a clip of each to check.

## Hand-off

Report the replay (date, map), the span (start–end, replay clock) and the key events. If the
user wants a clip, or asked for one, go on with **hots-clip** using the full path from `--json`.
