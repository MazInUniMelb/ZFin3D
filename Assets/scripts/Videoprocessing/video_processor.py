"""Main video processing logic"""
import os
import cv2
from .utils import extract_frame_number, VideoLogger
from .frame_processor import FrameProcessor

class VideoProcessor:
    def __init__(self, config):
        self.config = config
        self.logger = VideoLogger()
        
    def create_video(self, fish_name, input_dir, start_time, end_time, crop_params=None):
        """Create video for a single fish"""
        try:
            # Get frame list
            frames = self._get_frame_list(input_dir, start_time, end_time)
            if not frames:
                print(f"** No frames found for {fish_name}")
                return False
                
            # Setup frame processor
            processor = FrameProcessor(crop_params)
            
            # Get video dimensions from first frame
            first_frame = processor.load_and_process_frame(
                os.path.join(input_dir, frames[0])
            )
            height, width = first_frame.shape[:2]
            
            # Create video writer
            output_path = self._get_output_path(fish_name, start_time, end_time)
            writer = self._create_video_writer(output_path, (width, height))
            if not writer:
                return False
                
            # Process all frames
            success = self._process_all_frames(writer, input_dir, frames, processor)
            
            writer.release()
            
            if success:
                self.logger.log_video_stats(output_path, (width, height), len(frames))
                return True
            else:
                print(f"** Failed to process frames for {fish_name}")
                return False
                
        except Exception as e:
            print(f"** Error processing {fish_name}: {e}")
            return False
    
    def _get_frame_list(self, input_dir, start_time, end_time):
        """Get filtered and sorted frame list"""
        all_pics = [f for f in os.listdir(input_dir) if f.lower().endswith(".png")]
        
        filtered_pics = []
        for pic in all_pics:
            frame_num = extract_frame_number(pic)
            if frame_num is not None and start_time <= frame_num <= end_time:
                filtered_pics.append((frame_num, pic))
        
        filtered_pics.sort(key=lambda x: x[0])
        return [pic for _, pic in filtered_pics]
    
    def _process_all_frames(self, writer, input_dir, frames, processor):
        """Process and write all frames to video"""
        frame_count = 0
        
        for i in range(len(frames) - 1):
            try:
                # Process current frame
                current_frame = processor.load_and_process_frame(
                    os.path.join(input_dir, frames[i])
                )
                writer.write(current_frame)
                frame_count += 1
                
                # Process next frame
                next_frame = processor.load_and_process_frame(
                    os.path.join(input_dir, frames[i + 1])
                )
                
                # Frame repetition
                for _ in range(self.config['frame_repeat_count']):
                    writer.write(next_frame)
                    frame_count += 1
                
                # Interpolation
                interpolated = processor.interpolate_frames(
                    current_frame, next_frame, 
                    self.config['interpolation_frames']
                )
                
                for interp_frame in interpolated:
                    writer.write(interp_frame)
                    frame_count += 1
                    
            except Exception as e:
                print(f"Error processing frame {i}: {e}")
                continue
        
        print(f"Total frames written: {frame_count}")
        return True
    
    def _get_output_path(self, fish_name, start_time, end_time):
        """Generate output file path"""
        filename = f"{fish_name}.{start_time}.{end_time}.mp4"
        return os.path.join(self.config['output_directory'], filename)
    
    def _create_video_writer(self, output_path, dimensions):
        """Create video writer with proper settings"""
        fourcc = cv2.VideoWriter_fourcc(*self.config['codec'])
        writer = cv2.VideoWriter(
            output_path, fourcc, 
            self.config['output_fps'], 
            dimensions
        )
        
        if not writer.isOpened():
            print(f"** Failed to create video writer for {output_path}")
            return None
            
        return writer