# ZFin3D: Compile Videos from png frame files using directory structure
## Faculty of MDHS and MDAP collab engagement  
### created 11 Jul 2025, last updated 31 Oct 2025

### Data source Scott Lab and analysis by Wei Quin
### Visualisations by Amanda Belton, Wei Quin and Ethan Scott

import os
from PIL import Image
import cv2
import numpy as np


def interpolate_frame(frame1, frame2, factor):
    return cv2.addWeighted(frame1, 1 - factor, frame2, factor, 0)


def make_video(fishname, dir, outputdir, starttime, endtime, cropto):
    fname = fishname + "."+str(starttime)+"."+str(endtime)+".mp4"
    frame_repeat_count = 10   # Higher number will be slower video, >1 needed for interpolation
    pics = sorted([i for i in os.listdir(dir) if i.lower().endswith(".png")])
    if not pics:
        print(f"No suitable PNG images found in {dir}")
        return


    # Read the first image to get dimensions
    first_image = cv2.imread(os.path.join(dir, pics[0]))
    height, width, layers = first_image.shape
    print(first_image.shape)

    # Extract crop parameters**
    crop_x, crop_y, crop_width, crop_height = cropto
    print(f"Cropping to: x={crop_x}, y={crop_y}, width={crop_width}, height={crop_height}")

    # Validate crop parameters**
    if crop_x + crop_width > width or crop_y + crop_height > height:
        print(f"Error: Crop dimensions exceed image size!")
        print(f"Image size: {width}x{height}, Crop area: {crop_x},{crop_y} to {crop_x+crop_width},{crop_y+crop_height}")
        return

    # Define codec with CROPPED dimensions**
    fourcc = cv2.VideoWriter_fourcc(*'avc1')
    out = cv2.VideoWriter(os.path.join(outputdir, fname), fourcc, 60, (crop_width, crop_height))

    total_pics = len(pics)
    frame_count = 0

    for i in range(starttime, min(endtime, total_pics)):
        
        frame= cv2.imread(os.path.join(dir, pics[i]))
        if frame is None:
            print(f"Failed to read frame: {pics[i]}")
            continue

        # Crop frame1 in memory and write directly to video**
        cropped_frame1 = frame[crop_y:crop_y+crop_height, crop_x:crop_x+crop_width]
        out.write(cropped_frame1)
        frame_count += 1

        if i < total_pics - 1:
            frame = cv2.imread(os.path.join(dir, pics[i+1]))
            if frame is None:
                print(f"Failed to read next frame: {pics[i+1]}")
                continue

            cropped_frame2 = frame[crop_y:crop_y+crop_height, crop_x:crop_x+crop_width]

            for repeat in range(frame_repeat_count):
                out.write(cropped_frame2)
                frame_count += 1
            #cropped_frame1 = cropped_frame2 # needed for interpolation but not using right now
    print(f"Total frames written: {frame_count}")

    out.release()
    print(f"Video created: {fname}")

def process_directory(parent_dir):
    folders = []
    if not os.path.exists(parent_dir):
        print(f"Parent directory {parent_dir} does not exist.")
        return folders
    for root, dirs, files in os.walk(parent_dir):
        for folder in dirs:
            png_files = True
            png_files = [file for file in os.listdir(os.path.join(root, folder)) if file.lower().endswith('.png')]
            if png_files:
                folder_name = os.path.join(root, folder)  # Get the full path of the folder
                folders.append(folder_name)  # Store both folder name and full path
    return sorted(folders)

    return folders

# Specify the parent directory
parent_directory = "SignalDataFrames"
video_directory = "Videos"

print('########## ZFin3D: Compile Videos from png frame files using directory structure')
# Process the directory structure
folders = process_directory(parent_directory)
# redo videos for the recalcitrant fishies
fishies = ['Fish62']

# starttime = 0
# endtime = 4200

starttime = 3750
endtime = 3800

cropto = (0, 240, 900, 840)  # x,y,w,h

# fishies = ['Fish01', 'Fish07', 'Fish42']
for folder in folders:
        print(folder)
        fishname = folder.split('_')[1][0:6]
        if fishname in fishies:
            print(f"Processing folder: {folder} for fish: {fishname}")
            make_video(fishname, folder, video_directory,starttime, endtime,cropto)