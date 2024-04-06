
import os
from PIL import Image

def resize_images(input_folder, output_folder):
    for filename in os.listdir(input_folder):
        if filename.endswith(".jpg") or filename.endswith(".png"):  # Add more extensions if needed
            img = Image.open(os.path.join(input_folder, filename))
            width, height = img.size
            new_width = int(width * 0.3)
            new_height = int(height * 0.3)
            resized_img = img.resize((new_width, new_height), Image.BICUBIC)

            output_filename = os.path.splitext(filename)[0] + ".png"  # Change extension if needed
            resized_img.save(os.path.join(output_folder, output_filename))

# Example usage
input_folder = "C:/myprojects/myhotsinfo/bin/pub/Maps"
output_folder = "C:/myprojects/myhotsinfo/MyHotsInfo/Resources/Images/Maps" 

# Create the output folder if it doesn't exist
os.makedirs(output_folder, exist_ok=True)

resize_images(input_folder, output_folder)
