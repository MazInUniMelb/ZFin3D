using UnityEngine;
using UnityEngine.UI;

public class CameraControlUI : MonoBehaviour
{
    public BrainCameraController cameraController;
    public Text controlHintsText;

    void Update()
    {
        if (cameraController == null || controlHintsText == null) return;

        string hints = "";

        if (cameraController.currentMode == BrainCameraController.CameraMode.Orbit)
        {
            hints = "<b>ORBIT MODE</b> (Press Tab to switch)\n" +
                   "Right-Click + Drag: Rotate\n" +
                   "Scroll Wheel: Zoom\n" +
                   "Middle-Click + Drag: Pan\n" +
                   "WASD: Pan Camera";
        }
        else
        {
            hints = "<b>FREE FLY MODE</b> (Press Tab to switch)\n" +
                   "Right-Click + Drag: Look Around\n" +
                   "WASD: Move Horizontally\n" +
                   "Q/E: Move Up/Down\n" +
                   "Hold Shift: Move Faster";
        }

        controlHintsText.text = hints;
    }
}