/*
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class LeftGaze : MonoBehaviour
{
    public Vector3 gazePosition;
    public Quaternion gazeRotation;
    public Vector3 gazeDirection;
    public bool isValid;

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);
        XrSingleEyeGazeDataHTC leftGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        isValid = leftGaze.isValid;

        if (leftGaze.isValid)
        {
            gazePosition = leftGaze.gazePose.position.ToUnityVector();
            gazeRotation = leftGaze.gazePose.orientation.ToUnityQuaternion();
            gazeDirection = gazeRotation * Vector3.forward; // this uses local gaze rotation defined in HMD local coordinate frame, not the Unity world frame; this is being fed to the controller as if it's in world space, so the gaze ray is being projected as misaligned (controller to the left)

            transform.position = gazePosition;
            transform.rotation = gazeRotation;
        }
    }
}
*/

using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class LeftGaze : MonoBehaviour
{
    [Header("Raw (HMD-local) Data")]
    public Vector3 localGazePosition;
    public Quaternion localGazeRotation;
    public Vector3 localGazeDirection;

    [Header("World-space Data (for gameplay)")]
    public Vector3 worldGazePosition;
    public Quaternion worldGazeRotation;
    public Vector3 worldGazeDirection;

    [Header("Tracking State")]
    public bool isValid;

    private Camera mainCamera;
    private Transform referenceSpace;   // ViveRig root

    void Start()
    {
        mainCamera = Camera.main;
        // ViveRig is the root of XR rig
        referenceSpace = mainCamera.transform.root;
    }

    void Update()
    {
        // --- Get raw gaze data from SDK ---
        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);
        XrSingleEyeGazeDataHTC leftGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        isValid = leftGaze.isValid;
        if (!isValid) return;

        // --- Local (HMD space) data ---
        localGazePosition  = leftGaze.gazePose.position.ToUnityVector();
        localGazeRotation  = leftGaze.gazePose.orientation.ToUnityQuaternion();
        localGazeDirection = localGazeRotation * Vector3.forward;

        // --- Convert to world space for gameplay ---
        worldGazePosition  = referenceSpace.TransformPoint(localGazePosition);
        worldGazeRotation  = referenceSpace.rotation * localGazeRotation;
        worldGazeDirection = referenceSpace.TransformDirection(localGazeDirection);

        // --- Optional: visualize or debug ---
        transform.position = worldGazePosition;
        transform.rotation = worldGazeRotation;

        // --- Optional: log both local + world for research ---
        if (Time.frameCount % 90 == 0) // log every ~1.5 s @ 60fps
        {
            Debug.Log(
                $"[LeftGaze] valid={isValid}\n" +
                $"localDir={localGazeDirection}  worldDir={worldGazeDirection}\n" +
                $"localPos={localGazePosition}  worldPos={worldGazePosition}"
            );
        }
    }


}
