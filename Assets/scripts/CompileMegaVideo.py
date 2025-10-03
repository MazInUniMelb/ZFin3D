
# ZFin3D: Compile Videos from png frame files using directory structure
## Faculty of MDHS and MDAP collab engagement  
### created 11 Jul 2025, last updated 11 Jul 2025

### Data sourced by Scott Lab and pngs generated in Unity by AB
### Visualisations by Amanda Belton and Authors


import os
from PIL import Image
import cv2
import numpy as np
import pandas as pd


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
            png_files = [os.path.join(root,folder,f) for f in os.listdir(os.path.join(root, folder)) if f.lower().endswith('.png')]
            if png_files:
                folder_name = os.path.join(root, folder)  # Get the full path of the folder
                folders.append(folder_name)  # Store both folder name and full path

    print(f"Found {len(png_files)} PNG files in total across all folders.")
    df = pd.DataFrame(png_files, columns=['FileName'])
    df['FrameNbr'] = df['FileName'].str.extract(r'_(\d+)\.png').astype(int)
    return df


def create_composite_images(df, output_dir, grid_size=(6, 6), output_size=(3840, 5424)):
    # Read the first image in the first folder to get dimensions
    first_image_file = df['FileName'].sample(1).values[0]
    first_image = cv2.imread(first_image_file)
    if first_image is None:
        print(f"Error: Unable to read the first image: {first_image_file}")
        return
    height, width, layers = first_image.shape
    fps = 60

    # Calculate the dimensions of the composite frame
    composite_width = width * grid_size[0]
    composite_height = height * grid_size[1]
    
    print(f"Composite dimensions: {composite_width}x{composite_height}")
    print(f"Output dimensions: {output_size[0]}x{output_size[1]}")
    
    # Get the maximum number of frames across all folders
    max_frames = df['FrameNbr'].max()
    max_frames = min(max_frames, 10)  # Set a maximum number of frames to process for testing
    print(f"Processing {max_frames} frames")

    os.makedirs(output_dir, exist_ok=True)

    # calculate the x and y columns for the df based ont he unique fish names
    df['x'] = df['BoxNbr'] % grid_size[0]
    df['y'] = df['BoxNbr'] // grid_size[0]

    for frame_num in range(1, max_frames + 1):
        composite_frame = np.zeros((composite_height, composite_width, 3), dtype=np.uint8)
        for row in df[df['FrameNbr']==frame_num].iterrows():
            print(row)
            print('=====')
            frame = cv2.imread(row['FileName'])
            if frame is None:
                print(f"Warning: Unable to read frame: {frame_path}")
            else:
              # If the frame doesn't exist, use a blank (black) frame
                frame = np.zeros((height, width, 3), dtype=np.uint8)
            composite_frame[row['y']:y+height, row['x']:x+width] = frame
        # Resize the composite frame
        resized_frame = cv2.resize(composite_frame, output_size)

        # Write the frame as an image
        output_path = os.path.join(output_dir, f"frame_{frame_num:05d}.png")
        success = cv2.imwrite(output_path, resized_frame)
        if not success:
            print(f"Error: Failed to write frame {frame_num}")
        
        if frame_num % 10 == 0:
            print(f"Processed frame {frame_num}")

    print(f"Composite images created in: {output_dir}")



# Specify the parent directory
parent_directory = "Assets/SignalDataFrames"
video_directory = "Assets/Videos"


print('########## ZFin3D: Compile MegaVideo from png frame files using directory structure')
# Process the directory structure
filesdf = process_directory(parent_directory)
# create a dictionary with unique fish name and a unique number for each fish
fishies = filesdf['FileName'].str.extract(r'(\w+)_\d+\.png')[0].unique()
fish_dict = {fish: i for i, fish in enumerate(fishies)}
# use the fish number to create a new column in the dataframe from the fish dictionary
filesdf['FishNbr'] = filesdf['FileName'].str.extract(r'(\w+)_\d+\.png')[0]
filesdf['BoxNbr'] = filesdf['FishNbr'].map(fish_dict)
#create_composite_video(folders,"compositeVideo.250711.mp4")
output_dir = "Assets/SignalDataFrames/CompositeFrames"
# drop rows from filesdf where BoxNbr is fish nbr doesn't start with 'Fish'
filesdf = filesdf[filesdf['FishNbr'].str.startswith('Fish')]
create_composite_images(filesdf.sort_values(['FrameNbr','BoxNbr']), output_dir)




