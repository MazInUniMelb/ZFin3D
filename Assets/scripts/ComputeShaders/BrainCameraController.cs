using UnityEngine;
using UnityEngine.InputSystem;

public class BrainCameraController : MonoBehaviour
{
    [Header("References")]
    public Transform brainCenter; // Center point to orbit around
    public NeuronGPUSystem neuronSystem;

    [Header("Orbit Controls")]
    public float orbitSpeed = 100f;
    public float zoomSpeed = 10f;
    public float minDistance = 10f;
    public float maxDistance = 500f;
    public float panSpeed = 1f;

    [Header("Free Fly Controls")]
    public float flySpeed = 50f;
    public float fastFlyMultiplier = 3f;
    public float lookSensitivity = 2f;
    public float smoothTime = 0.1f;

    [Header("Mode Settings")]
    public CameraMode currentMode = CameraMode.Orbit;
    public Key toggleModeKey = Key.Tab;

    [Header("Input Settings")]
    public Key forwardKey = Key.W;
    public Key backwardKey = Key.S;
    public Key leftKey = Key.A;
    public Key rightKey = Key.D;
    public Key upKey = Key.E;
    public Key downKey = Key.Q;
    public Key fastMoveKey = Key.LeftShift;

    // Private state
    private float currentDistance;
    private Vector3 targetPosition;
    private Vector2 orbitAngles; // x = horizontal, y = vertical
    private Vector3 velocity = Vector3.zero;
    private Vector2 lookVelocity = Vector2.zero;
    private Vector2 currentLookDelta = Vector2.zero;

    // Input devices
    private Mouse mouse;
    private Keyboard keyboard;

    // Free fly rotation
    private float pitch = 0f;
    private float yaw = 0f;

    public enum CameraMode
    {
        Orbit,
        FreeFly
    }

    void Start()
    {
        mouse = Mouse.current;
        keyboard = Keyboard.current;

        if (mouse == null || keyboard == null)
        {
            Debug.LogWarning("Mouse or Keyboard not detected by Input System");
        }

        if(brainCenter == null)
        {
            // Use neuron system's render bounds center as brain center
            GameObject centerObj = new GameObject("BrainCenter");
            brainCenter = centerObj.transform;
            enabled = false;
        }
    }

    public void SetBrainCenter(Vector3 center)
    {
        brainCenter.position = center;
        targetPosition = center;

        if (brainCenter != null)
        {
            enabled = true;
            currentDistance = Vector3.Distance(transform.position, brainCenter.position);
            targetPosition = brainCenter.position;

            // Calculate initial orbit angles
            Vector3 direction = transform.position - brainCenter.position;
            orbitAngles.x = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            orbitAngles.y = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;

            // Initialize free fly rotation
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }
    }

    void Update()
    {
        if (mouse == null) mouse = Mouse.current;
        if (keyboard == null) keyboard = Keyboard.current;

        if (mouse == null || keyboard == null) return;

        // Toggle camera mode
        if (keyboard[toggleModeKey].wasPressedThisFrame)
        {
            ToggleMode();
        }

        // Update based on current mode
        if (currentMode == CameraMode.Orbit)
        {
            UpdateOrbitMode();
        }
        else
        {
            UpdateFreeFlyMode();
        }
    }

    void ToggleMode()
    {
        currentMode = currentMode == CameraMode.Orbit ? CameraMode.FreeFly : CameraMode.Orbit;

        if (currentMode == CameraMode.FreeFly)
        {
            // Initialize free fly from current position
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180) pitch -= 360;

            Debug.Log("Switched to Free Fly mode - Use WASD/QE to move, Right-click + drag to look");
        }
        else
        {
            // Initialize orbit from current position
            if (brainCenter != null)
            {
                currentDistance = Vector3.Distance(transform.position, brainCenter.position);
                Vector3 direction = transform.position - brainCenter.position;
                orbitAngles.x = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                orbitAngles.y = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
            }

            Debug.Log("Switched to Orbit mode - Right-click + drag to rotate, Scroll to zoom, Middle-click + drag to pan");
        }
    }

    void UpdateOrbitMode()
    {
        if (brainCenter == null) return;

        // Mouse rotation (right-click drag)
        if (mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            orbitAngles.x += mouseDelta.x * orbitSpeed * Time.deltaTime;
            orbitAngles.y -= mouseDelta.y * orbitSpeed * Time.deltaTime;
            orbitAngles.y = Mathf.Clamp(orbitAngles.y, -89f, 89f);
        }

        // Zoom (scroll wheel)
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= scroll * zoomSpeed * Time.deltaTime;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        // Pan (middle-click drag)
        if (mouse.middleButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            Vector3 right = transform.right;
            Vector3 up = transform.up;

            targetPosition -= right * mouseDelta.x * panSpeed * Time.deltaTime;
            targetPosition -= up * mouseDelta.y * panSpeed * Time.deltaTime;

            if (brainCenter != null)
            {
                brainCenter.position = targetPosition;
            }
        }

        // Keyboard pan (WASD for panning in orbit mode)
        Vector3 panInput = Vector3.zero;
        if (keyboard[forwardKey].isPressed) panInput += Vector3.forward;
        if (keyboard[backwardKey].isPressed) panInput += Vector3.back;
        if (keyboard[leftKey].isPressed) panInput += Vector3.left;
        if (keyboard[rightKey].isPressed) panInput += Vector3.right;
        if (keyboard[upKey].isPressed) panInput += Vector3.up;
        if (keyboard[downKey].isPressed) panInput += Vector3.down;

        float speed = panSpeed;
        if (keyboard[fastMoveKey].isPressed)
        {
            speed *= fastFlyMultiplier;
        }

        if (panInput.magnitude > 0.01f)
        {
            Vector3 worldPan = transform.TransformDirection(panInput.normalized);
            targetPosition += worldPan * speed * 10f * Time.deltaTime;

            if (brainCenter != null)
            {
                brainCenter.position = targetPosition;
            }
        }

        // Calculate camera position
        Quaternion rotation = Quaternion.Euler(orbitAngles.y, orbitAngles.x, 0);
        Vector3 targetCameraPosition = targetPosition - (rotation * Vector3.forward * currentDistance);

        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, targetCameraPosition, ref velocity, smoothTime);
        transform.LookAt(targetPosition);
    }

    void UpdateFreeFlyMode()
    {
        // Look with mouse (right-click drag)
        if (mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            Vector2 targetLookDelta = mouseDelta * lookSensitivity * Time.deltaTime;
            currentLookDelta = Vector2.SmoothDamp(currentLookDelta, targetLookDelta, ref lookVelocity, smoothTime);

            yaw += currentLookDelta.x;
            pitch -= currentLookDelta.y;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }
        else
        {
            currentLookDelta = Vector2.zero;
        }

        // Apply rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // Movement
        Vector3 moveInput = Vector3.zero;

        if (keyboard[forwardKey].isPressed) moveInput += Vector3.forward;
        if (keyboard[backwardKey].isPressed) moveInput += Vector3.back;
        if (keyboard[leftKey].isPressed) moveInput += Vector3.left;
        if (keyboard[rightKey].isPressed) moveInput += Vector3.right;
        if (keyboard[upKey].isPressed) moveInput += Vector3.up;
        if (keyboard[downKey].isPressed) moveInput += Vector3.down;

        // Apply speed multiplier
        float speed = flySpeed;
        if (keyboard[fastMoveKey].isPressed)
        {
            speed *= fastFlyMultiplier;
        }

        // Transform to world space and move
        if (moveInput.magnitude > 0.01f)
        {
            Vector3 worldMove = transform.TransformDirection(moveInput.normalized);
            transform.position += worldMove * speed * Time.deltaTime;
        }
    }

    public void FocusOnPoint(Vector3 point, float distance = -1)
    {
        if (currentMode == CameraMode.Orbit)
        {
            SetBrainCenter(point);
            if (distance > 0)
            {
                currentDistance = distance;
            }
        }
        else
        {
            // In free fly mode, just move camera to look at point
            transform.position = point - transform.forward * (distance > 0 ? distance : 50f);
            transform.LookAt(point);

            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180) pitch -= 360;
        }
    }

    public void ResetToDefault()
    {
        if (brainCenter != null && neuronSystem != null)
        {
            Vector3 center = neuronSystem.GetBrainCenter();
            float distance = neuronSystem.GetBrainSize() * 1.5f;

            SetBrainCenter(center);
            currentDistance = distance;
            orbitAngles = new Vector2(45f, 20f);

            currentMode = CameraMode.Orbit;
        }
    }
}