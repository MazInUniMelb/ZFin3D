# ZFin3D: Compile Videos from png frame files using directory structure
## Faculty of MDHS and MDAP collab engagement  
### created 11 Jul 2025, last updated 13 Nov 2025

### Data source Scott Lab and analysis by Wei Quin
### Visualisations by Amanda Belton, Wei Quin and Ethan Scott

import os
from Videoprocessing import (
    VideoConfig, CropConfig, VideoProcessor,
    debug_single_frame, process_directory
)

def main():
    """Main execution function"""
    # Configuration
    config = VideoConfig()
    config.validate()
    
    # Processing parameters
    fish_list = ['Fish62']
    start_time = 2606
    end_time = 2956
    crop_config = CropConfig(100, 0, 1800, 1080)
    
    print('########## ZFin3D: Compile Videos from png frame files')
    
    # Find directories
    folders = process_directory(config.parent_directory)
    
    # Debug single frame first
    test_folder = None
    for folder in folders:
        folder_name = os.path.basename(folder)
        fish_name = folder_name.split('_')[1][:6]
        if fish_name in fish_list:
            test_folder = folder
            break
    
    if test_folder:
        test_files = [f for f in os.listdir(test_folder) if f.endswith('.png')]
        if test_files:
            test_file = os.path.join(test_folder, test_files[0])
            print(f"\n=== Testing crop settings ===")
            if not debug_single_frame(test_file, crop_config):
                print("** Single frame test failed, aborting")
                return
    
    # Process videos
    processor = VideoProcessor(config.__dict__)
    
    for folder in folders:
        folder_name = os.path.basename(folder)
        fish_name = folder_name.split('_')[1][:6]
        
        if fish_name in fish_list:
            print(f"\n=== Processing {fish_name} ===")
            processor.create_video(
                fish_name, folder, start_time, end_time, 
                crop_config.to_list()
            )

if __name__ == "__main__":
    main()