# ZFin3D: Compile Videos from png frame files using directory structure
## Faculty of MDHS and MDAP collab engagement  
### created 11 Jul 2025, last updated 13 Nov 2025

### Data source Scott Lab and analysis by Wei Quin
### Visualisations by Amanda Belton, Wei Quin and Ethan Scott

import os
from PIL import Image
import cv2
import numpy as np
import re



def extract_frame_number(filename):
    """Extract frame number from various filename formats"""
    # Remove extension
    name = filename.lower().replace('.png', '')
    
    # Try different patterns
    patterns = [
        r'frame_(\d+)',      # frame_1234
        r'(\d+)_frame',      # 1234_frame  
        r'(\d+)$',           # just 1234
        r'_(\d+)_',          # something_1234_something
        r'(\d+)',            # any number in the filename
    ]
    
    for pattern in patterns:
        match = re.search(pattern, name)
        if match:
            return int(match.group(1))
    
    return None

def interpolate_frames(img1, img2, num_frames):
    """Generate interpolated frames between img1 and img2"""
    frames = []
 
    # Check if images are valid
    if img1 is None or img2 is None:
        print("One of the images is None, skipping interpolation")
        return frames
        
    # Check if images have same dimensions
    if img1.shape != img2.shape:
        print(f"Image dimension mismatch: {img1.shape} vs {img2.shape}")
        return frames
    
    for i in range(1, num_frames + 1):
        alpha = i / (num_frames + 1)
        try:
            blended = cv2.addWeighted(img1, 1 - alpha, img2, alpha, 0)
            frames.append(blended)
        except cv2.error as e:
            print(f"Error blending frames: {e}")
            print(f"img1 shape: {img1.shape}, img2 shape: {img2.shape}")
            break
    
    return frames

def make_video(fishname, dir, outputdir, starttime, endtime, cropto):
    fname = fishname + "."+str(starttime)+"."+str(endtime)+".mp4"
    frame_repeat_count = 5  # Higher number will be slower video, >1 needed for interpolation
 
    # Get all PNG files
    all_pics = [i for i in os.listdir(dir) if i.lower().endswith(".png")]
    
    # Extract frame numbers and filter
    filtered_pics = []
    for pic in all_pics:
        frame_num = extract_frame_number(pic)
        if frame_num is not None and starttime <= frame_num <= endtime:
            filtered_pics.append((frame_num, pic))
    
    # Sort by frame number
    filtered_pics.sort(key=lambda x: x[0])
    pics = [pic for _, pic in filtered_pics]
    
    if not pics:
        print(f"No PNG images found in range {starttime}-{endtime} in {dir}")
        print(f"Available files sample: {all_pics[:10]}")
        return

    print(f"Found {len(pics)} images in timeframe {starttime}-{endtime}")
    print(f"Frame range: {filtered_pics[0][0]} to {filtered_pics[-1][0]}")
    
    # Read the first image to get dimensions
    first_image = cv2.imread(os.path.join(dir, pics[0]))
    original_height, original_width, layers = first_image.shape
    height = original_height
    width = original_width
    print(f"Original image dimensions: {original_width}x{original_height}")

    if cropto is not None:
        usecrop = True
        crop_x, crop_y, crop_width, crop_height = cropto
        if crop_x >= original_width:
            crop_width = original_width - 1 - crop_x
        if crop_y >= original_height:
            crop_height = original_height - 1 - crop_y

        print(f"Crop settings: x={crop_x}, y={crop_y}, width={crop_width}, height={crop_height}")
        print(f"Original: {original_width}x{original_height}")
        print(f"Crop area: {crop_x},{crop_y} to {crop_x + crop_width},{crop_y + crop_height}")
        # Adjust crop to fit within image
        crop_width = min(crop_width, original_width - crop_x)
        crop_height = min(crop_height, original_height - crop_y)
        width = crop_width
        height = crop_height
        print(f"Adjusted crop: width={crop_width}, height={crop_height}")
    else:
        usecrop = False

    print(f"Final video dimensions: {width}x{height}")
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(os.path.join(outputdir, fname), fourcc, 60, (width, height))
    frame_count = 0

    print(f"Creating video {fname} from frames {starttime} to {endtime}")
    for i in range(0, len(pics)-1):
        
        # Read and crop current frame
        current_frame = cv2.imread(os.path.join(dir, pics[i]))
        if current_frame is None:
            continue

        if usecrop:
            current_frame = current_frame[crop_y:crop_y + crop_height, crop_x:crop_x + crop_width]

        out.write(current_frame)
        frame_count += 1

        # Read and crop next frame
        next_frame = cv2.imread(os.path.join(dir, pics[i+1]))
        if next_frame is None:
            continue

        if usecrop:
            next_frame = next_frame[crop_y:crop_y + crop_height, crop_x:crop_x + crop_width]

        # Frame repetition
        for repeat in range(frame_repeat_count):
            out.write(next_frame)
            frame_count += 1

        # Interpolate frames between current and next
        try:
            interpolated_frames = interpolate_frames(current_frame, next_frame, 2)
            for interp_frame in interpolated_frames:
                out.write(interp_frame)
                frame_count += 1
        except Exception as e:
            print(f"Error in interpolation between frames {i} and {i+1}: {e}")

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

starttime = 2610
endtime = 2960

cropto = [100,0,800,1080]  # (x, y, width, height) crop area for video output

# fishies = ['Fish01', 'Fish07', 'Fish42']
for folder in folders:
        print(folder)
        fishname = folder.split('_')[1][0:6]
        if fishname in fishies:
            print(f"Processing folder: {folder} for fish: {fishname}")
            make_video(fishname, folder, video_directory,starttime, endtime, cropto)
            # make_video(fishname, folder, video_directory,starttime, endtime, None)