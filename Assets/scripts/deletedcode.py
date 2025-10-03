
def create_composite_video(folders, output_file, grid_size=(6, 6), output_size=(3840, 5424)):
    print(f"Starting create_composite_video with {len(folders)} folders")
    print(f"Output file: {output_file}")

    # Check if folders list is empty
    if not folders:
        print("Error: No folders provided")
        return

    # Read the first image in the first folder to get dimensions
    first_folder = folders[0]
    first_image_files = [f for f in os.listdir(first_folder) if f.lower().endswith('.png')]
    if not first_image_files:
        print(f"Error: No PNG files found in the first folder {first_folder}")
        return

    first_image_path = os.path.join(first_folder, first_image_files[0])
    first_image = cv2.imread(first_image_path)
    if first_image is None:
        print(f"Error: Unable to read the first image: {first_image_path}")
        return

    height, width, layers = first_image.shape
    fps = 60
    
    print(f"Image dimensions: {width}x{height}")
    
    # Calculate the dimensions of the composite frame
    composite_width = width * grid_size[0]
    composite_height = height * grid_size[1]
    
    print(f"Composite dimensions: {composite_width}x{composite_height}")
    print(f"Output dimensions: {output_size[0]}x{output_size[1]}")

    # Create VideoWriter object
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(output_file, fourcc, fps, output_size)
    
    if not out.isOpened():
        print(f"Error: Unable to create VideoWriter for {output_file}")
        return
    
    # Get the maximum number of frames across all folders
    max_frames = max(len([f for f in os.listdir(folder) if f.lower().endswith('.png')]) for folder in folders)
    max_frames = min(max_frames, 100)  # Set a maximum number of frames to process for testing
    print(f"Processing {max_frames} frames")

    for frame_num in range(1, max_frames + 1):
        composite_frame = np.zeros((composite_height, composite_width, 3), dtype=np.uint8)

        for i, folder in enumerate(folders):
            if i >= grid_size[0] * grid_size[1]:
                break  # Stop if we've filled all grid positions

            frame_file = f"{os.path.basename(folder)}_{frame_num:05d}.png"
            frame_path = os.path.join(folder, frame_file)

            if os.path.exists(frame_path):
                frame = cv2.imread(frame_path)
                if frame is None:
                    print(f"Warning: Unable to read frame: {frame_path}")
                    frame = np.zeros((height, width, 3), dtype=np.uint8)
            else:
                # If the frame doesn't exist, use a blank (black) frame
                frame = np.zeros((height, width, 3), dtype=np.uint8)

            row = i // grid_size[0]
            col = i % grid_size[0]
            y = row * height
            x = col * width
            composite_frame[y:y+height, x:x+width] = frame

        # Resize the composite frame
        resized_frame = cv2.resize(composite_frame, output_size)

        # Write the frame
        if not out.write(resized_frame):
            print(f"Error: Failed to write frame {frame_num}")

        if frame_num % 10 == 0:
            print(f"Processed frame {frame_num}")

    out.release()
    print(f"Composite video created: {output_file}")