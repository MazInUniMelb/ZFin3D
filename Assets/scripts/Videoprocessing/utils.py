"""Utility functions and debugging tools"""
import os
import cv2
import re

class VideoLogger:
    def __init__(self, verbose=True):
        self.verbose = verbose
        self.frame_count = 0
        
    def log_frame_processing(self, frame_info):
        if self.verbose:
            print(f"Frame {self.frame_count}: {frame_info}")
        self.frame_count += 1
        
    def log_video_stats(self, output_path, dimensions, total_frames):
        file_size = os.path.getsize(output_path) / (1024 * 1024)  # MB
        print(f"Video created: {os.path.basename(output_path)}")
        print(f"   Dimensions: {dimensions[0]}x{dimensions[1]}")
        print(f"   Frames: {total_frames}")
        print(f"   Size: {file_size:.1f} MB")

def extract_frame_number(filename):
    """Extract frame number from various filename formats"""
    name = filename.lower().replace('.png', '')
    
    patterns = [
        r'frame_(\d+)',
        r'(\d+)_frame',  
        r'(\d+)$',
        r'_(\d+)_',
        r'(\d+)',
    ]
    
    for pattern in patterns:
        match = re.search(pattern, name)
        if match:
            return int(match.group(1))
    
    return None

def debug_single_frame(file_path, crop_config=None):
    """Debug function to test processing on single frame"""
    print(f"=== Testing single frame: {file_path} ===")
    
    if not os.path.exists(file_path):
        print(f"** File not found: {file_path}")
        return False
        
    crop_params = crop_config.to_list() if crop_config else None
    frame = cv2.imread(file_path)
    # Apply crop directly with cv2
    frame = frame[crop_params[1]:crop_params[1] + crop_params[3], crop_params[0]:crop_params[0] + crop_params[2]]
    
    try:
        test_output = "debug_frame_output.png"
        cv2.imwrite(test_output, frame)
        print(f"Test frame saved: {test_output} ({frame.shape})")
        return True
    except Exception as e:
        print(f"** Error processing test frame: {e}")
        return False

def process_directory(parent_dir):
    """Find all directories containing PNG files"""
    folders = []
    if not os.path.exists(parent_dir):
        print(f"Parent directory {parent_dir} does not exist.")
        return folders
        
    for root, dirs, files in os.walk(parent_dir):
        for folder in dirs:
            folder_path = os.path.join(root, folder)
            png_files = [f for f in os.listdir(folder_path) if f.lower().endswith('.png')]
            if png_files:
                folders.append(folder_path)
                
    return sorted(folders)