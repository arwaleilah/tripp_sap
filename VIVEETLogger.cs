using UnityEngine;
using System.IO;
using System.Text;
using System;

public class EyeTrackingLogger_Focus : MonoBehaviour
{
    [Header("Eye Tracking References")]
    public LeftGaze leftGazeTracker;
    public RightGaze rightGazeTracker;
    public LeftPupil leftPupilTracker;
    public RightPupil rightPupilTracker;
    public LeftEyeGeometric leftGeometricTracker;
    public RightEyeGeometric rightGeometricTracker;

    [Header("Logging Settings")]
    public bool enableLogging = true;
    public float logFrequency = 90f; // Hz

    [Header("Optional References")]
    public Transform playerHead; // head position/rotation
    public Transform targetObject; // object looked at
    public SessionNameController sessionController; // session manager (must expose sessionName)

    private StreamWriter writer;
    private string filePath;
    private float logTimer = 0f;
    private float logInterval;
    private int frameCount = 0;
    private string participantID = "Unknown";

    void Start()
    {
        if (!enableLogging) return;

        logInterval = 1f / logFrequency;

        // Get participant/session ID
        participantID = (sessionController != null && !string.IsNullOrEmpty(sessionController.sessionName))
            ? sessionController.sessionName
            : "Unknown";

        // Create folder path and filename
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string folderPath = Path.Combine(Application.persistentDataPath, "ArwaSAP", "RawET");
        Directory.CreateDirectory(folderPath);

        filePath = Path.Combine(folderPath, $"ViveET_{sceneName}_{participantID}_{timestamp}.csv");
        Debug.Log($"[EyeTrackingLogger] Eye tracking data will be saved to: {filePath}");

        try
        {
            writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteHeader();
            Debug.Log("[EyeTrackingLogger] Logging started successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeTrackingLogger] Failed to create log file: {e.Message}");
            enableLogging = false;
        }
    }

    void WriteHeader()
    {
        StringBuilder header = new StringBuilder();
        header.Append("Timestamp,Frame,");

        // --- LEFT GAZE ---
        header.Append("LeftGaze_Valid,");
        header.Append("Left_Local_PosX,Left_Local_PosY,Left_Local_PosZ,");
        header.Append("Left_Local_DirX,Left_Local_DirY,Left_Local_DirZ,");
        header.Append("Left_World_PosX,Left_World_PosY,Left_World_PosZ,");
        header.Append("Left_World_DirX,Left_World_DirY,Left_World_DirZ,");

        // --- RIGHT GAZE ---
        header.Append("RightGaze_Valid,");
        header.Append("Right_Local_PosX,Right_Local_PosY,Right_Local_PosZ,");
        header.Append("Right_Local_DirX,Right_Local_DirY,Right_Local_DirZ,");
        header.Append("Right_World_PosX,Right_World_PosY,Right_World_PosZ,");
        header.Append("Right_World_DirX,Right_World_DirY,Right_World_DirZ,");

        // --- PUPIL + GEOMETRIC ---
        header.Append("LeftPupil_DiameterValid,LeftPupil_Diameter,LeftPupil_PositionValid,LeftPupil_PosX,LeftPupil_PosY,");
        header.Append("RightPupil_DiameterValid,RightPupil_Diameter,RightPupil_PositionValid,RightPupil_PosX,RightPupil_PosY,");
        header.Append("LeftGeo_Valid,LeftGeo_Openness,LeftGeo_Squeeze,LeftGeo_Wide,");
        header.Append("RightGeo_Valid,RightGeo_Openness,RightGeo_Squeeze,RightGeo_Wide,");

        // --- Head and Target ---
        if (playerHead != null)
            header.Append("Head_PosX,Head_PosY,Head_PosZ,Head_RotX,Head_RotY,Head_RotZ,Head_RotW,");

        if (targetObject != null)
            header.Append("Target_PosX,Target_PosY,Target_PosZ,");

        writer.WriteLine(header.ToString());
        writer.Flush();
    }

    void Update()
    {
        if (!enableLogging || writer == null) return;

        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            LogEyeTrackingData();
            logTimer = 0f;
        }
    }

    void LogEyeTrackingData()
    {
        StringBuilder data = new StringBuilder();
        data.Append($"{Time.time:F4},{frameCount},");
        frameCount++;

        // --- LEFT GAZE ---
        if (leftGazeTracker != null)
        {
            data.Append($"{leftGazeTracker.isValid},");
            data.Append($"{leftGazeTracker.localGazePosition.x:F6},{leftGazeTracker.localGazePosition.y:F6},{leftGazeTracker.localGazePosition.z:F6},");
            data.Append($"{leftGazeTracker.localGazeDirection.x:F6},{leftGazeTracker.localGazeDirection.y:F6},{leftGazeTracker.localGazeDirection.z:F6},");
            data.Append($"{leftGazeTracker.worldGazePosition.x:F6},{leftGazeTracker.worldGazePosition.y:F6},{leftGazeTracker.worldGazePosition.z:F6},");
            data.Append($"{leftGazeTracker.worldGazeDirection.x:F6},{leftGazeTracker.worldGazeDirection.y:F6},{leftGazeTracker.worldGazeDirection.z:F6},");
        }
        else data.Append("False,0,0,0,0,0,0,0,0,0,0,0,");

        // --- RIGHT GAZE ---
        if (rightGazeTracker != null)
        {
            data.Append($"{rightGazeTracker.isValid},");
            data.Append($"{rightGazeTracker.localGazePosition.x:F6},{rightGazeTracker.localGazePosition.y:F6},{rightGazeTracker.localGazePosition.z:F6},");
            data.Append($"{rightGazeTracker.localGazeDirection.x:F6},{rightGazeTracker.localGazeDirection.y:F6},{rightGazeTracker.localGazeDirection.z:F6},");
            data.Append($"{rightGazeTracker.worldGazePosition.x:F6},{rightGazeTracker.worldGazePosition.y:F6},{rightGazeTracker.worldGazePosition.z:F6},");
            data.Append($"{rightGazeTracker.worldGazeDirection.x:F6},{rightGazeTracker.worldGazeDirection.y:F6},{rightGazeTracker.worldGazeDirection.z:F6},");
        }
        else data.Append("False,0,0,0,0,0,0,0,0,0,0,0,");

        // --- PUPIL DATA ---
        if (leftPupilTracker != null)
            data.Append($"{leftPupilTracker.isDiameterValid},{leftPupilTracker.pupilDiameter:F6},{leftPupilTracker.isPositionValid},{leftPupilTracker.pupilPosition.x:F6},{leftPupilTracker.pupilPosition.y:F6},");
        else
            data.Append("False,0,False,0,0,");

        if (rightPupilTracker != null)
            data.Append($"{rightPupilTracker.isDiameterValid},{rightPupilTracker.pupilDiameter:F6},{rightPupilTracker.isPositionValid},{rightPupilTracker.pupilPosition.x:F6},{rightPupilTracker.pupilPosition.y:F6},");
        else
            data.Append("False,0,False,0,0,");

        // --- GEOMETRIC DATA ---
        if (leftGeometricTracker != null)
            data.Append($"{leftGeometricTracker.isValid},{leftGeometricTracker.eyeOpenness:F6},{leftGeometricTracker.eyeSqueeze:F6},{leftGeometricTracker.eyeWide:F6},");
        else
            data.Append("False,0,0,0,");

        if (rightGeometricTracker != null)
            data.Append($"{rightGeometricTracker.isValid},{rightGeometricTracker.eyeOpenness:F6},{rightGeometricTracker.eyeSqueeze:F6},{rightGeometricTracker.eyeWide:F6},");
        else
            data.Append("False,0,0,0,");

        // --- HEAD + TARGET ---
        if (playerHead != null)
            data.Append($"{playerHead.position.x:F6},{playerHead.position.y:F6},{playerHead.position.z:F6},{playerHead.rotation.x:F6},{playerHead.rotation.y:F6},{playerHead.rotation.z:F6},{playerHead.rotation.w:F6},");
        if (targetObject != null)
            data.Append($"{targetObject.position.x:F6},{targetObject.position.y:F6},{targetObject.position.z:F6},");

        writer.WriteLine(data.ToString());
    }

    void OnApplicationQuit() => CloseLog();
    void OnDestroy() => CloseLog();

    void CloseLog()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            Debug.Log($"[EyeTrackingLogger] Data saved to: {filePath}");
            Debug.Log($"[EyeTrackingLogger] Total frames logged: {frameCount}");
        }
    }

    public void ToggleLogging()
    {
        enableLogging = !enableLogging;
        Debug.Log($"[EyeTrackingLogger] Logging {(enableLogging ? "enabled" : "disabled")}");
    }

    public void AddMarker(string markerText)
    {
        if (writer != null && enableLogging)
        {
            writer.WriteLine($"# MARKER at {Time.time:F4}: {markerText}");
            writer.Flush();
        }
    }
}
