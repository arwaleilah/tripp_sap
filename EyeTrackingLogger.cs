using UnityEngine;
using System.IO;
using System.Text;
using System;

public class EyeTrackingLogger : MonoBehaviour
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
    public string fileName = "EyeTrackingData";
    public float logFrequency = 90f; // Hz

    [Header("Optional References")]
    public Transform playerHead;
    public Transform targetObject;

    private StreamWriter writer;
    private string filePath;
    private float logTimer = 0f;
    private float logInterval;
    private int frameCount = 0;

    void Start()
    {
        if (!enableLogging) return;

        logInterval = 1f / logFrequency;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fullFileName = $"{fileName}_{timestamp}.csv";

        #if UNITY_ANDROID && !UNITY_EDITOR
        filePath = Path.Combine(Application.persistentDataPath, fullFileName);
        #else
        filePath = Path.Combine(Application.dataPath, fullFileName);
        #endif

        Debug.Log($"Eye tracking data will be saved to: {filePath}");

        try
        {
            writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteHeader();
            Debug.Log("Eye tracking logging started successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create log file: {e.Message}");
            enableLogging = false;
        }
    }

    void WriteHeader()
    {
        StringBuilder header = new StringBuilder();

        header.Append("Timestamp,Frame,");

        // Left Gaze (Local + World)
        header.Append("LeftGaze_Valid,");
        header.Append("Left_Local_PosX,Left_Local_PosY,Left_Local_PosZ,");
        header.Append("Left_Local_DirX,Left_Local_DirY,Left_Local_DirZ,");
        header.Append("Left_World_PosX,Left_World_PosY,Left_World_PosZ,");
        header.Append("Left_World_DirX,Left_World_DirY,Left_World_DirZ,");

        // Right Gaze (Local + World)
        header.Append("RightGaze_Valid,");
        header.Append("Right_Local_PosX,Right_Local_PosY,Right_Local_PosZ,");
        header.Append("Right_Local_DirX,Right_Local_DirY,Right_Local_DirZ,");
        header.Append("Right_World_PosX,Right_World_PosY,Right_World_PosZ,");
        header.Append("Right_World_DirX,Right_World_DirY,Right_World_DirZ,");

        // Left Pupil
        header.Append("LeftPupil_DiameterValid,LeftPupil_Diameter,");
        header.Append("LeftPupil_PositionValid,LeftPupil_PosX,LeftPupil_PosY,");

        // Right Pupil
        header.Append("RightPupil_DiameterValid,RightPupil_Diameter,");
        header.Append("RightPupil_PositionValid,RightPupil_PosX,RightPupil_PosY,");

        // Left Geometric
        header.Append("LeftGeo_Valid,LeftGeo_Openness,LeftGeo_Squeeze,LeftGeo_Wide,");

        // Right Geometric
        header.Append("RightGeo_Valid,RightGeo_Openness,RightGeo_Squeeze,RightGeo_Wide");

        if (playerHead != null)
        {
            header.Append(",Head_PosX,Head_PosY,Head_PosZ,Head_RotX,Head_RotY,Head_RotZ,Head_RotW");
        }

        if (targetObject != null)
        {
            header.Append(",Target_PosX,Target_PosY,Target_PosZ");
        }

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

            // Local
            data.Append($"{leftGazeTracker.localGazePosition.x:F6},{leftGazeTracker.localGazePosition.y:F6},{leftGazeTracker.localGazePosition.z:F6},");
            data.Append($"{leftGazeTracker.localGazeDirection.x:F6},{leftGazeTracker.localGazeDirection.y:F6},{leftGazeTracker.localGazeDirection.z:F6},");

            // World
            data.Append($"{leftGazeTracker.worldGazePosition.x:F6},{leftGazeTracker.worldGazePosition.y:F6},{leftGazeTracker.worldGazePosition.z:F6},");
            data.Append($"{leftGazeTracker.worldGazeDirection.x:F6},{leftGazeTracker.worldGazeDirection.y:F6},{leftGazeTracker.worldGazeDirection.z:F6},");
        }
        else
        {
            data.Append("False," + new string('0', 47).Replace("0", "0,"));
        }

        // --- RIGHT GAZE ---
        if (rightGazeTracker != null)
        {
            data.Append($"{rightGazeTracker.isValid},");

            // Local
            data.Append($"{rightGazeTracker.localGazePosition.x:F6},{rightGazeTracker.localGazePosition.y:F6},{rightGazeTracker.localGazePosition.z:F6},");
            data.Append($"{rightGazeTracker.localGazeDirection.x:F6},{rightGazeTracker.localGazeDirection.y:F6},{rightGazeTracker.localGazeDirection.z:F6},");

            // World
            data.Append($"{rightGazeTracker.worldGazePosition.x:F6},{rightGazeTracker.worldGazePosition.y:F6},{rightGazeTracker.worldGazePosition.z:F6},");
            data.Append($"{rightGazeTracker.worldGazeDirection.x:F6},{rightGazeTracker.worldGazeDirection.y:F6},{rightGazeTracker.worldGazeDirection.z:F6},");
        }
        else
        {
            data.Append("False," + new string('0', 47).Replace("0", "0,"));
        }

        // --- PUPIL & GEOMETRIC TRACKERS (unchanged) ---
        if (leftPupilTracker != null)
        {
            data.Append($"{leftPupilTracker.isDiameterValid},{leftPupilTracker.pupilDiameter:F6},");
            data.Append($"{leftPupilTracker.isPositionValid},{leftPupilTracker.pupilPosition.x:F6},{leftPupilTracker.pupilPosition.y:F6},");
        }
        else data.Append("False,0,False,0,0,");

        if (rightPupilTracker != null)
        {
            data.Append($"{rightPupilTracker.isDiameterValid},{rightPupilTracker.pupilDiameter:F6},");
            data.Append($"{rightPupilTracker.isPositionValid},{rightPupilTracker.pupilPosition.x:F6},{rightPupilTracker.pupilPosition.y:F6},");
        }
        else data.Append("False,0,False,0,0,");

        if (leftGeometricTracker != null)
        {
            data.Append($"{leftGeometricTracker.isValid},{leftGeometricTracker.eyeOpenness:F6},{leftGeometricTracker.eyeSqueeze:F6},{leftGeometricTracker.eyeWide:F6},");
        }
        else data.Append("False,0,0,0,");

        if (rightGeometricTracker != null)
        {
            data.Append($"{rightGeometricTracker.isValid},{rightGeometricTracker.eyeOpenness:F6},{rightGeometricTracker.eyeSqueeze:F6},{rightGeometricTracker.eyeWide:F6}");
        }
        else data.Append("False,0,0,0");

        if (playerHead != null)
        {
            data.Append($",{playerHead.position.x:F6},{playerHead.position.y:F6},{playerHead.position.z:F6},");
            data.Append($"{playerHead.rotation.x:F6},{playerHead.rotation.y:F6},{playerHead.rotation.z:F6},{playerHead.rotation.w:F6}");
        }

        if (targetObject != null)
        {
            data.Append($",{targetObject.position.x:F6},{targetObject.position.y:F6},{targetObject.position.z:F6}");
        }

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
            Debug.Log($"Eye tracking data saved to: {filePath}");
            Debug.Log($"Total frames logged: {frameCount}");
        }
    }

    public void ToggleLogging()
    {
        enableLogging = !enableLogging;
        Debug.Log($"Eye tracking logging {(enableLogging ? "enabled" : "disabled")}");
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
