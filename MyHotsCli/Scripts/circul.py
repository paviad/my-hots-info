from PIL import Image, ImageDraw
import os
import shutil
from pathlib import Path

def circle_crop(filename, output_folder):
    filepath = os.path.join(input_folder, filename)
    img = Image.open(filepath)
    size = img.size

    mask = Image.new('L', size, 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((0, 0) + size, fill=255)

    output = img.copy()
    output.putalpha(mask)

    output_filename = os.path.splitext(filename)[0] + '_circle.png'
    output_filepath = os.path.join(output_folder, output_filename)
    print(output_filepath)
    output.save(output_filepath)

# Specify your image folder path and output folder path
REPO_ROOT = Path(__file__).resolve().parents[2]
input_folder = REPO_ROOT / "bin" / "pub" / "Portraits"
output_folder = REPO_ROOT / "MyHotsInfo" / "Resources" / "Images" / "Portraits"

# Create the output folder if it doesn't exist
os.makedirs(output_folder, exist_ok=True)

# Only add heroes the app doesn't have yet, so hand-fixed portraits (e.g. fenix) aren't overwritten
for filename in os.listdir(input_folder):
    if filename.endswith('.jpg') or filename.endswith('.png'):
        target = os.path.join(output_folder, filename)
        if os.path.exists(target):
            continue
        print(target)
        shutil.copyfile(os.path.join(input_folder, filename), target)
        circle_crop(filename, output_folder)

