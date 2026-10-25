from PIL import Image
import hashlib
import json
import os
import re
from pathlib import Path

# Copies the scraped talent icons (bin\pub\TalentImages, from `MyHotsCli scrape`) into the MAUI app,
# shrunk to what the game summary shows. Rerun after each scrape, then rebuild/republish MyHotsInfo.
#
# Icons are versioned by the scraped build so old games keep the art they had: a talent's first icon
# is talent_x.jpg, and a later scrape that redraws it adds talent_x_{build}.jpg next to it. Whether the
# art changed is decided from talent_icons.json (a hash of each version's scraped pixels), not from the
# re-encoded JPEGs.
SIZE = 64

REPO_ROOT = Path(__file__).resolve().parents[2]
input_folder = REPO_ROOT / "bin" / "pub" / "TalentImages"
output_folder = REPO_ROOT / "MyHotsInfo" / "Resources" / "Raw" / "Talents"
manifest_path = Path(__file__).with_name("talent_icons.json")

# The scraper's XML declares utf-16 but is written as UTF-8; go by the BOM.
raw = (REPO_ROOT / "bin" / "pub" / "talentInfo.xml").read_bytes()
talent_info = raw.decode("utf-16") if raw[:2] in (b"\xff\xfe", b"\xfe\xff") else raw.decode("utf-8-sig")
build = int(re.search(r"<Build>(\d+)</Build>", talent_info).group(1))

# {"talent_x": {"<build>": "<pixel hash>", ...}}
manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}

os.makedirs(output_folder, exist_ok=True)

added = redrawn = 0
for filename in sorted(os.listdir(input_folder)):
    if not filename.endswith('.png'):
        continue
    name = os.path.splitext(filename)[0]
    img = Image.open(input_folder / filename).convert('RGBA')
    pixels = hashlib.sha1(img.tobytes()).hexdigest()[:16]

    versions = manifest.setdefault(name, {})
    if versions:
        newest = max(versions, key=int)
        if versions[newest] == pixels:
            continue
        if build < int(newest):
            print(f"skipping {name}: build {build} is older than its newest icon ({newest})")
            continue
        redrawn += 1
    else:
        added += 1
    versions[str(build)] = pixels

    first = min(versions, key=int)
    target = name if str(build) == first else f"{name}_{build}"
    print(f"{target}.jpg")
    img.convert('RGB').resize((SIZE, SIZE), Image.LANCZOS).save(output_folder / f"{target}.jpg", quality=88)

# One talent per line, so a scrape's diff shows just the icons it touched.
lines = [f"  {json.dumps(k)}: {json.dumps(manifest[k], sort_keys=True)}" for k in sorted(manifest)]
manifest_path.write_text("{\n" + ",\n".join(lines) + "\n}\n")

print(f"build {build}: {added} new, {redrawn} redrawn")
