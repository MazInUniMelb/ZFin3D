"""Frame processing and manipulation"""
import cv2
import os

class FrameProcessor:
    def __init__(self, crop_params=None):
        self.crop_params = crop_params
        self.use_crop = crop_params is not None
        
    def load_and_process_frame(self, file_path):
        """Load a frame and apply processing"""
        frame = cv2.imread(file_path)
        if frame is None:
            raise ValueError(f"Failed to load frame: {file_path}")
            
        if self.use_crop:
            frame = self._apply_crop(frame)
            
        return frame
    
    def _apply_crop(self, frame):
        """Apply crop parameters to frame"""
        if not self.crop_params:
            return frame
            
        crop_x, crop_y, crop_width, crop_height = self.crop_params
        
        # Validate crop bounds
        h, w = frame.shape[:2]
        crop_x = max(0, min(crop_x, w - 1))
        crop_y = max(0, min(crop_y, h - 1))
        crop_width = min(crop_width, w - crop_x)
        crop_height = min(crop_height, h - crop_y)
        
        return frame[crop_y:crop_y + crop_height, crop_x:crop_x + crop_width]
    
    def interpolate_frames(self, frame1, frame2, num_frames):
        """Generate interpolated frames"""
        if frame1.shape != frame2.shape:
            raise ValueError(f"Frame size mismatch: {frame1.shape} vs {frame2.shape}")
            
        frames = []
        for i in range(1, num_frames + 1):
            alpha = i / (num_frames + 1)
            try:
                blended = cv2.addWeighted(frame1, 1 - alpha, frame2, alpha, 0)
                frames.append(blended)
            except cv2.error as e:
                print(f"Error blending frames: {e}")
                break
                
        return frames