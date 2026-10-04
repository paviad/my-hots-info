---
name: casc-diagnostics
description: Extract and inspect Heroes of the Storm CASC game files with `MyHotsCli casc` to diagnose scraper failures (e.g. `MyHotsCli scrape` crashing on a newly released hero with KeyNotFoundException in Scraper.DoHero). Use when a scrape fails, a new hero/patch is out, or you need to see raw hero mod XML, GameStrings, or where an id is defined.
---

# CASC diagnostics

The scraper (`CascScraperCore/Scraper.cs`) reads game data straight from the CASC storage of the
local HotS install (`MYHOTSINFO_HOTS_DIR`, default `C:\Program Files (x86)\Heroes of the Storm`).
`MyHotsCli casc` (logic in `CascScraperCore/CascDiagnostics.cs`) extracts those files to disk and
replays the scraper's lookups so failures can be diagnosed without a debugger.

## Running

Build first, then run the exe from the repo root (the default output dir `casc\` is relative to the
current directory and is gitignored):

```
dotnet build MyHotsCli
./MyHotsCli/bin/Debug/net10.0/MyHotsCli.exe casc <subcommand> ...
```

Every invocation spends ~70s in `LoadListFile()` before doing anything — batch questions into as few
runs as possible, use a long timeout (≥ 600000 ms), and don't re-run just to re-read output: the
extracted files stay on disk under `casc\`. When filtering output, don't use `grep -v "^[0-9]* "`
(it also drops indented report lines); use `sed -n '/^===/,$p'` for `hero` output.

| Command | What it does |
|---|---|
| `casc hero <name>` | Extracts `mods\heromods\<name>.stormmod\base.stormdata\**` + `enus.stormdata\LocalizedData\GameStrings.txt`, lists the `GameData.xml` includes (marks `[0]`, the only one the scraper reads), counts CHero/CTalent/CButton per file, then checks every talent → Face button → Name/Tooltip string → Icon lookup that `DoHero` makes and says where anything missing really lives. Name match is fuzzy (`xalatath` matches `xalatath.stormmod`). |
| `casc extract "<glob>" [--list]` | Extracts (or just lists) CASC paths matching a glob. `*`, `**`, `?`; backslash or slash separators; case-insensitive. E.g. `"mods\heromods\xalatath.stormmod\**"`. |
| `casc find <id> [--in "<glob>"]` | Prints element name + CASC path of every XML element with `id="<id>"`. Default scope `mods\**\*.xml` (slow; narrow `--in` when possible). |

`-o/--out <dir>` works on any subcommand. For throwaway extractions (e.g. a control hero), point it at
the scratchpad instead of `casc\`.

## Diagnosing a failing scrape

1. Take the hero from the `Doing hero X` line before the exception and run `casc hero <x>`.
2. Run `casc hero` on a hero that scrapes fine (e.g. `valeera`) as a control — it should print
   `no problems found`. If it doesn't, the harness, not the data, is wrong.
3. Inspect the extracted XML under `casc\mods\heromods\<hero>.stormmod\base.stormdata\` directly with
   grep for anything the report doesn't cover.
4. Stack-trace line numbers can be offset from the source if the exe is stale — map them by content.

## Known data layouts

- Most heroes: everything (CHero, CTalent, CButton) is in `GameData/<Hero>Data.xml`, the first include.
- Xal'atath (Oct 2026): CButtons live in `GameData/ButtonData.xml`, a type-named file the engine loads
  by convention and which is **not** in `GameData.xml`'s include list. The scraper only reads
  `Catalog[0]`, so `buttonDic[iconKey]` throws. Expect future heroes to use this split layout too.
- Generic/shared data: `mods\heroesdata.stormmod\base.stormdata\GameData\{Talent,Button}Data.xml`
  and `mods\core.stormmod\base.stormdata\GameData\`.

## Extending

Add checks to `CascDiagnostics.DiagnoseHeroMod` when the scraper gains new lookups, so the harness keeps
mirroring `Scraper.DoHero`. New subcommands go in `SetupCascCommand` in `MyHotsCli/Program.cs`.
