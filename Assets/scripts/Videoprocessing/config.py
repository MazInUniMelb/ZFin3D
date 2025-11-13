"""Configuration classes for video processing"""
import os

class VideoConfig:
    def __init__(self):
        self.frame_repeat_count = 5
        self.interpolation_frames = 2
        self.output_fps = 60
        self.codec = 'mp4v'
        self.parent_directory = "SignalDataFrames"
        self.output_directory = "Videos"
        
    def validate(self):
        """Validate configuration"""
        if not os.path.exists(self.parent_directory):
            raise FileNotFoundError(f"Input directory not found: {self.parent_directory}")
            
        os.makedirs(self.output_directory, exist_ok=True)
        return True

class CropConfig:
    def __init__(self, x, y, width, height):
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        
    def validate_against_image(self, image_shape):
        """Validate crop against actual image dimensions"""
        h, w = image_shape[:2]
        
        if self.x >= w or self.y >= h:
            raise ValueError(f"Crop position ({self.x},{self.y}) outside image ({w}x{h})")
            
        if self.x + self.width > w or self.y + self.height > h:
            print(f"** Crop size adjusted to fit image bounds")
            self.width = min(self.width, w - self.x)
            self.height = min(self.height, h - self.y)
            
        return [self.x, self.y, self.width, self.height]
    
    def to_list(self):
        return [self.x, self.y, self.width, self.height]
    
    def __str__(self):
        return f"Crop({self.x},{self.y},{self.width}x{self.height})"