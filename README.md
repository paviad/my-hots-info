# Install

Clone the repo into `C:\MyProjects\MyHotsInfo` the path is hard coded in some places. You may clone to another location, but update the source code accordingly.

# Usage

`MyHotsCli` is the command line tool, it is the only thing that has some working functionality.

Build it and run it, it uses the (yet experimental) `System.CommandLine` library by Microsoft, and will display usage information when run without arguments (or with improper arguments)

## Examples

* `MyHotsCli scan --list` - Try to find locations of multi player replays (under My Documents) and list them in order.
* `MyHotsCli scan -s 2` - Scan all replays from location #2 in the list (from the previous example).
* `MyHotsCli qp joeschmoe` - Query your replays for a player named `joeschmoe` and show some statistics about them.
* `MyHotsCli scrape` - Scrape your Hots installation for talent information.
* `MyHotsCli scrape -i` - Install the scraped data into the database (can only be run after the previous example).
* `MyHotsCli qh chromie` - Show statistics about games with *Chromie*

## Full Help Documentation

### General Activation
```
Usage:
  MyHotsCli [command] [options]

Options:
  -gm, --gamemode <aram|qm|sl|ud>
  --version                        Show version information
  -?, -h, --help                   Show help and usage information

Commands:
  scan       Scan replay folder
  qp <name>  Query player info
  qh <hero>  Query by hero
  q          Query all db for info
  scrape     Scrape hots installation for talents
  qr         Query replay info
```

The `gameMode` option is global and applies to most commands.

### Scan Command (scan)

```
Usage:
  MyHotsCli scan [options]

Options:
  -l, --list
  -a, --account <account>
  -r, --region <region>
  -s, --seq <seq>
  -w, --watch
  --rescan                         Clear scan cache, will rescan all files
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli scan --list`

```
1. Account 1831861, Region 1
2. Account 1831861, Region 2
3. Account 1831861, Region 98
4. Account 448709408, Region 2
```

`MyHotsCli scan -s 2`

```
...
info: MyReplayLibrary.Scanner[0]
      Scanning C:\Users\USER\Documents\Heroes of the Storm\Accounts\1831861\2-Hero-1-612637\Replays\Multiplayer\2023-09-09 19.26.12 Alterac Pass.StormReplay (2328/2329)
info: MyReplayLibrary.Scanner[0]
      ... done
info: MyReplayLibrary.Scanner[0]
      Scanning C:\Users\USER\Documents\Heroes of the Storm\Accounts\1831861\2-Hero-1-612637\Replays\Multiplayer\2023-09-09 19.05.53 Battlefield of Eternity.StormReplay (2329/2329)
info: MyReplayLibrary.Scanner[0]
      ... already scanned
...
```

### Query Player Command (qp)

```
Usage:
  MyHotsCli qp <name> [options]

Arguments:
  <name>

Options:
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli qp Jaimyth#2203`

```
Stats for Jaimyth#2203
----------------------------------------------------------------------------------------------------------
They Played | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
----------------------------------------------------------------------------------------------------------
Overall     | 10         | 5            | 2               | 3                | 2            | 3
Greymane    | 6          | 4            | 2               | 0                | 2            | 2
Hanzo       | 4          | 1            | 0               | 3                | 0            | 1
```

### Query Hero Command (qh)

```
Usage:
  MyHotsCli qh <hero> [options]

Arguments:
  <hero>

Options:
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli qh -gm sl qhira`

```
Stats for Qhira
----------------------------------------------------------------------------------------------------------
We Played   | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
----------------------------------------------------------------------------------------------------------
Overall     | 14         | 8            | 5               | 3                | 3            | 3
Chromie     | 4          | 2            | 1               | 1                | 1            | 1
Brightwing  | 2          | 1            | 1               | 1                | 0            | 0
Anduin      | 2          | 1            | 1               | 0                | 1            | 0
Alexstrasza | 2          | 1            | 1               | 1                | 0            | 0
Jaina       | 2          | 1            | 0               | 0                | 1            | 1
Arthas      | 1          | 1            | 0               | 0                | 0            | 1
Tyrande     | 1          | 1            | 1               | 0                | 0            | 0
```

### General Query Command (q)

```
Usage:
  MyHotsCli q [options]

Options:
  -m, --most <most>                Most <num> seen players, default 10 [default: 10]
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli -gm sl q`

```
-----------------------------------------
Player              | Played with/against
-----------------------------------------
tombor123#2685      | 6
NekrosPrime#21284   | 5
omia971#2917        | 5
ХитрыйСтраус#2761   | 5
bigsheep4500#3735   | 5
Øystein#11100       | 5
МедлякГад#2257      | 5
notSzifon#2487      | 5
Viceversa#2191      | 4
Maximilien#21434    | 4
```

### Query Replay Command (qr)

```
Usage:
  MyHotsCli qr [command] [options]

Options:
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information

Commands:
  list       List replays
  show <id>  Show details of a single replay
```

#### Replay List Command (qr list)

```
Usage:
  MyHotsCli qr list [options]

Options:
  -s, --since <since>
  -t, --to <to>
  --skip <skip>
  --take <take>
  --hero <hero>
  --map <map>
  --win
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli qr list --take 10`

```
----------------------------------------------------------------------------------------------------------
Id     | Date/Time          | Mode | Map         | Hero        | Length    | Win? | MVP?
----------------------------------------------------------------------------------------------------------
2251   | 29/03/2024 20:29:06| SL   | Tomb        | Chromie     | 00:24:45  | Yes  | Yes
2250   | 29/03/2024 19:57:58| SL   | Braxis      | Uther       | 00:16:04  | Yes  |
2249   | 29/03/2024 19:29:47| SL   | Volskaya    | Tyrande     | 00:24:04  |      |
2248   | 29/03/2024 17:54:12| SL   | Alterac     | Tyrande     | 00:24:12  | Yes  |
2247   | 29/03/2024 09:08:24| QM   | Battlefield | Chromie     | 00:19:35  |      |
2246   | 29/03/2024 08:32:43| QM   | Infernal    | Chromie     | 00:20:05  |      |
2245   | 28/03/2024 21:50:29| SL   | Towers      | Tyrande     | 00:20:20  | Yes  |
2244   | 28/03/2024 20:37:16| SL   | Garden      | Rexxar      | 00:25:38  |      |
2243   | 28/03/2024 19:49:44| SL   | Braxis      | Tyrande     | 00:15:51  |      |
2242   | 28/03/2024 19:27:59| SL   | Dragon      | Chromie     | 00:21:52  |      |
```

#### Replay Show Command (qr show)

```
Usage:
  MyHotsCli qr show <id> [options]

Arguments:
  <id>

Options:
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

#### Examples

`MyHotsCli qr show 2245`

```
Id: 2245
Game Time: 28/03/2024 21:50:29
Game Mode: StormLeague
Map: Towers of Doom
Mvp: its4you#2796
Winning Team:
-----------------
   Skywalker#23595      - Tyrande
   Dengzus#21798        - Muradin
   pickheal#2391        - Dehaka
   its4you#2796         - Zul'jin
   Galactica3#2948      - Junkrat
Losing Team:
-----------------
   Leave#21205          - Abathur
   wastedyouth#2103     - Li-Ming
   VipperV4#2567        - Anduin
   MeHateJuice#2145     - Varian
   Shadowstep#2971      - Diablo
```

### Scrape Command (scrape)

```
Usage:
  MyHotsCli scrape [options]

Options:
  -i, --import
  -gm, --gamemode <aram|qm|sl|ud>
  -?, -h, --help                   Show help and usage information
```

* Note: The `gameMode` option has no effect on this command

# Other Client Types

## MAUI

The folder `MyHotsInfo` is a (mostly) blank MAUI app.

## OCR

### Build

Adapted from `https://github.com/charlesw/tesseract/blob/master/docs/Compling_tesseract_and_leptonica.md`

```
vcpkg install giflib:x86-windows-static libjpeg-turbo:x86-windows-static liblzma:x86-windows-static libpng:x86-windows-static tiff:x86-windows-static zlib:x86-windows-static icu:x86-windows-static pango:x86-windows-static
vcpkg install giflib:x64-windows-static libjpeg-turbo:x64-windows-static liblzma:x64-windows-static libpng:x64-windows-static tiff:x64-windows-static zlib:x64-windows-static icu:x64-windows-static pango:x64-windows-static
```

```
git clone https://github.com/DanBloomberg/leptonica.git & cd leptonica	
git checkout -b 1.82.0 1.82.0
mkdir vs16-x86 & cd vs16-x86
cmake .. -G "Visual Studio 17 2022" -A Win32 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x86-windows-static -DCMAKE_INSTALL_PREFIX=..\..\build\x86
@REM cmake ..\..\..\leptonica -G "Visual Studio 17 2022" -A Win32 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x86-windows-static -DCMAKE_INSTALL_PREFIX=..\..\x86
cmake --build . --config Release --target install
cd ..
mkdir vs16-x64 & cd vs16-x64
cmake .. -G "Visual Studio 17 2022" -A x64 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON  -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x64-windows-static -DCMAKE_INSTALL_PREFIX=..\..\build\x64
@REM cmake ..\..\..\leptonica -G "Visual Studio 17 2022" -A x64 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON  -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x64-windows-static -DCMAKE_INSTALL_PREFIX=..\..\x64
cmake --build . --config Release --target install

git clone https://github.com/tesseract-ocr/tesseract.git
cd tesserct
git checkout -b 5.2.0 5.2.0
mkdir vs17-x86 & cd vs17-x86
cmake .. -G "Visual Studio 17 2022" -A Win32 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x86-windows-static -DCMAKE_INSTALL_PREFIX=..\..\build\x86
@REM cmake ..\..\..\tesseract -G "Visual Studio 17 2022" -A Win32 -DAUTO_OPTIMIZE=OFF -DSW_BUILD=OFF -DBUILD_TRAINING_TOOLS=OFF -DCMAKE_INSTALL_PREFIX=..\..\x86
cmake --build . --config Release --target install
cd ..
mkdir vs17-x64 & cd vs17-x64
cmake .. -G "Visual Studio 17 2022" -A x64 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON  -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\scripts\buildsystems\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=x64-windows-static -DCMAKE_INSTALL_PREFIX=..\..\build\x64
@REM cmake ..\..\..\tesseract -G "Visual Studio 17 2022" -A x64   -DAUTO_OPTIMIZE=OFF -DSW_BUILD=OFF -DBUILD_TRAINING_TOOLS=OFF -DCMAKE_INSTALL_PREFIX=..\..\x64
cmake --build . --config Release --target install
```
