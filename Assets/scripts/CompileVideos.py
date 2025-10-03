# ZFin3D: Compile Videos from png frame files using directory structure
## Faculty of MDHS and MDAP collab engagement  
### created 11 Jul 2025, last updated 11 Jul 2025

### Data sourced by Scott Lab and pngs generated in Unity by AB
### Visualisations by Amanda Belton and Authors

import os
from PIL import Image
import cv2
import numpy as np


def interpolate_frame(frame1, frame2, factor):
    return cv2.addWeighted(frame1, 1 - factor, frame2, factor, 0)


def make_video(fname, dir, outputdir):
    slowdown_factor = 2  # Higher number will be slower video, >1 needed for interpolation
    pics = sorted([i for i in os.listdir(dir) if i.lower().endswith(".png")])
    if not pics:
        print(f"No suitable PNG images found in {dir}")
        return

    total_pics = len(pics)
    print(f"Total number of PNG files: {total_pics}")

    # Read the first image to get dimensions
    first_image = cv2.imread(os.path.join(dir, pics[0]))
    height, width, layers = first_image.shape
    print(first_image.shape)

    # Define the codec and create VideoWriter object
    fourcc = cv2.VideoWriter_fourcc(*'avc1')
    out = cv2.VideoWriter(os.path.join(outputdir, fname), fourcc, 60, (width, height))

    frame_count = 0

    for i in range(total_pics):
        frame1 = cv2.imread(os.path.join(dir, pics[i]))
        if frame1 is None:
            print(f"Failed to read frame: {pics[i]}")
            continue

        out.write(frame1)
        frame_count += 1

        if i < total_pics - 1:
            frame2 = cv2.imread(os.path.join(dir, pics[i+1]))
            if frame2 is None:
                print(f"Failed to read next frame: {pics[i+1]}")
                continue

            for j in range(1, slowdown_factor):
                factor = j / slowdown_factor
                interpolated_frame = interpolate_frame(frame1, frame2, factor)
                out.write(interpolated_frame)
                frame_count += 1
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
parent_directory = "Assets/SignalDataFrames"
video_directory = "Assets/Videos"

print('########## ZFin3D: Compile Videos from png frame files using directory structure')
# Process the directory structure
folders = process_directory(parent_directory)
# redo videos for the recalcitrant fishies
fishies = ['Fish07', 'Fish42']
# fishies = ['Fish01', 'Fish07', 'Fish42']
for folder in folders:
        print(folder)
        fishname = folder.split('_')[1][0:6]
        if fishname in fishies:
            print(f"Processing folder: {folder} for fish: {fishname}")
            videoName = fishname+".mp4"
            make_video(videoName, folder, video_directory)