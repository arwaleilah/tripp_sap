using UnityEngine;

public class v3SpaceshipController : MonoBehaviour
{
    [Header("Eye Tracking References")]
    public LeftGaze leftGazeTracker;
    public RightGaze rightGazeTracker;

    [Header("Movement Settings")]
    public float moveSpeed = 5f; 
    
    [Header("Position Constraints")]
    public float maxHorizontalDistance = 5f;
    public float maxVerticalDistance = 3f;

    [Header("Positioning")]
    public float distanceFromCamera = 10f;
    public float heightOffset = -1.5f;

    [Header("EMA Filter Settings")]
    [Range(0.1f, 1f)] 
    public float alpha = 0.7f; // How much new gaze values influence the filtered output (higher = more responsive, more jitter); starting at 0.7 (as suggested by David)

    [Header("Calibration")]
    public Vector3 offsetCorrection = new Vector3(0.0f, 0.0f, 0.0f);

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Camera mainCamera;
    private bool eyeTrackingAvailable;
    private Vector3 filteredGazeHit; // EMA filtered gaze hit point
    private bool emaInitialized = false;

    void Start()
    {
        mainCamera = Camera.main;
        
        if (!mainCamera)
        {
            Debug.LogError("Main camera not found!");
            enabled = false;
            return;
        }

        // Log the calibration offset being used (set in Inspector)
        if (offsetCorrection != Vector3.zero)
        {
            Debug.Log($"[Spaceship] Using calibration offset: {offsetCorrection}");
        }

        // Initialize position
        Vector3 startPos = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
        startPos.y += heightOffset;
        transform.position = startPos;
        filteredGazeHit = startPos;
    }

    void Update()
    {
        // Detect valid gaze
        eyeTrackingAvailable = (leftGazeTracker && leftGazeTracker.isValid) ||
                               (rightGazeTracker && rightGazeTracker.isValid);

        if (!eyeTrackingAvailable)
        {
            UpdateWithHeadTracking();
            return;
        }

        // Get combined gaze direction
        Vector3 gazeDirection = GetCombinedGazeDirection();
        Vector3 gazeOrigin = mainCamera.transform.position;

        // Find intersection with plane in front of camera
        Plane gazePlane = new Plane(-mainCamera.transform.forward,
            mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera);
        Ray gazeRay = new Ray(gazeOrigin, gazeDirection); // creates a ray staring from eyes, pointing where you're looking

        if (gazePlane.Raycast(gazeRay, out float enter))
        {
            Vector3 rawGazeHit = gazeRay.GetPoint(enter) + offsetCorrection; // exact 3D position hwere gaze hit the plane, this is the raw unfiltered "hit point" from the sensors

            // Apply EMA filter to gaze hit point
            if (!emaInitialized)
            {
                filteredGazeHit = rawGazeHit;
                emaInitialized = true;
            }
            // ^on the virst frame with valid gaze, filter is initialized , the filtered value is equal to the first raw value (b/c no history yet) and then marked as initalized so it doesn't run again
            else
            {
                // Exponential Moving Average: new_filtered = alpha * new_raw + (1 - alpha) * previous_filtered
                filteredGazeHit = alpha * rawGazeHit + (1f - alpha) * filteredGazeHit;
            }
                //^ this is the ema filter, with alpha of X meaning that new raw value contributes X% and previous filtered value contributing the rest (higher alpha, more responsive)

            // Clamp filtered gaze hit within bounds
            Vector3 center = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
            center.y += heightOffset; //calculating center point of play area, same as starting position logic
            filteredGazeHit.x = Mathf.Clamp(filteredGazeHit.x, center.x - maxHorizontalDistance, center.x + maxHorizontalDistance);
            filteredGazeHit.y = Mathf.Clamp(filteredGazeHit.y, center.y - maxVerticalDistance, center.y + maxVerticalDistance);
            filteredGazeHit.z = center.z;

            // Smooth ship movement toward filtered gaze position
            // Lerp factor = speed * deltaTime for frame-rate independent movement
            float lerpFactor = moveSpeed * Time.deltaTime; // smoothing factor calculation, makes movement frrame-rate independent
            transform.position = Vector3.Lerp(transform.position, filteredGazeHit, lerpFactor); // single lerp (ship follows filtered gaze hit)
            // Vector3.Lerp(A,B,t) with A being start point, B being end point, and t being how far between them (0 to 1)
        }

        // Rotation (stay facing forward)
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0;
        if (forward != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(forward) * Quaternion.Euler(0, 180, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, moveSpeed * Time.deltaTime * 0.5f);
        }

        // Debug log
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Spaceship] EyeTracking={eyeTrackingAvailable}, RawGaze={gazeRay.GetPoint(enter)}, FilteredGaze={filteredGazeHit}, ShipPos={transform.position}");
        }
    }

    private void UpdateWithHeadTracking()
    {
        Vector3 center = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
        center.y += heightOffset;

        float lerpFactor = moveSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, center, lerpFactor);

        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0;
        if (forward != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, 180, 0);
        }

        if (showDebugInfo && Time.frameCount % 180 == 0)
            Debug.LogWarning("[Spaceship] Eye tracking not available, using head tracking fallback");
    }

    private Vector3 GetCombinedGazeDirection()
    {
        Vector3 combined = Vector3.zero;
        int count = 0;

        if (leftGazeTracker && leftGazeTracker.isValid)
        {
            combined += leftGazeTracker.worldGazeDirection;  // Already in world space
            count++;
        }
        if (rightGazeTracker && rightGazeTracker.isValid)
        {
            combined += rightGazeTracker.worldGazeDirection;  // Already in world space
            count++;
        }

        if (count == 0)
            return mainCamera.transform.forward;

        return (combined / count).normalized;  // Just average and normalize, no conversion needed
    }
}