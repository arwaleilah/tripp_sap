using Cognitive3D;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

//version 1 - json and csv serialization options

public class DataConnectorLocalFile : MonoBehaviour
{
    private DataConnector dataConnector;
    string path;

    public enum FileType
    {
        Json,
        CSV,
    }
    public FileType SelectedFileType = FileType.Json;

    void Start()
    {
        dataConnector = GetComponent<DataConnector>();

        path = Application.dataPath.Substring(0, Application.dataPath.Length - ("Assets").Length);
        path += "Session Data/";
        if (Directory.Exists(path) == false)
            Directory.CreateDirectory(path);        
    }

    FileStream gazeFileStream;
    FileStream fixationFileStream;
    FileStream eyeTrackingFileStream;
    FileStream eventFileStream;
    FileStream dynamicFileStream;
    FileStream sensorFileStream;
    FileStream boundaryFileStream;

    bool hasWrittenGaze;
    bool hasWrittenFixation;
    bool hasWrittenEyeTracking;
    bool hasWrittenEvent;
    bool hasWrittenDynamic;
    bool hasWrittenSensor;
    bool hasWrittenBoundary;

    FileStream CreateFile(string path)
    {
        return File.Create(path);
    }

    private void Update()
    {
        switch(SelectedFileType)
        {
            case FileType.Json: SerializeJson(); break;
            case FileType.CSV: SerializeCSV(); break;
            default: break;
        }
    }
    
    private void SerializeJson()
    {
        if (dataConnector.gazeData.Count > 0)
        {
            if (hasWrittenGaze == false)
            {
                hasWrittenGaze = true;
                gazeFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_gaze.json");
                AddText(gazeFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.gazeData)
            {
                AddJsonText(gazeFileStream, v.ToJson());
            }
        }
        if (dataConnector.fixationData.Count > 0)
        {
            if (hasWrittenFixation == false)
            {
                hasWrittenFixation = true;
                fixationFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_fixation.json");
                AddText(fixationFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.fixationData)
            {
                AddJsonText(fixationFileStream, v.ToJson());
            }
        }
        if (dataConnector.eyeData.Count > 0)
        {
            if (hasWrittenEyeTracking == false)
            {
                hasWrittenEyeTracking = true;
                eyeTrackingFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_eyetracking.json");
                AddText(eyeTrackingFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.eyeData)
            {
                AddJsonText(eyeTrackingFileStream, v.ToJson());
            }
        }
        if (dataConnector.customEventData.Count > 0)
        {
            if (hasWrittenEvent == false)
            {
                hasWrittenEvent = true;
                eventFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_event.json");
                AddText(eventFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.customEventData)
            {
                AddJsonText(eventFileStream, v.ToJson());
            }
        }
        if (dataConnector.dynamicData.Count > 0)
        {
            if (hasWrittenDynamic == false)
            {
                hasWrittenDynamic = true;
                dynamicFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_dynamic.json");
                AddText(dynamicFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.dynamicData)
            {
                AddJsonText(dynamicFileStream, v.ToJson());
            }
        }
        if (dataConnector.sensorData.Count > 0)
        {
            if (hasWrittenSensor == false)
            {
                hasWrittenSensor = true;
                sensorFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_sensor.json");
                AddText(sensorFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.sensorData)
            {
                AddJsonText(sensorFileStream, v.ToJson());
            }
        }
        if (dataConnector.boundaryData.Count > 0)
        {
            if (hasWrittenBoundary == false)
            {
                hasWrittenBoundary = true;
                boundaryFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_boundary.json");
                AddText(boundaryFileStream, "{\"SessionID\":\"" + Cognitive3D_Manager.SessionID + "\",\"data\":[");
            }
            foreach (var v in dataConnector.boundaryData)
            {
                AddJsonText(boundaryFileStream, v.ToJson());
            }
        }

        dataConnector.ClearCaches();
    }
    
    private void SerializeCSV()
    {
        if (dataConnector.gazeData.Count > 0)
        {
            if (hasWrittenGaze == false)
            {
                hasWrittenGaze = true;
                gazeFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_gaze.csv");
                AddText(gazeFileStream, DataConnector.GazeData.CSVHeader());
            }
            foreach (var v in dataConnector.gazeData)
            {
                AddText(gazeFileStream, v.ToCSV());
            }
        }
        if (dataConnector.fixationData.Count > 0)
        {
            if (hasWrittenFixation == false)
            {
                hasWrittenFixation = true;
                fixationFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_fixation.csv");
                AddText(fixationFileStream, DataConnector.FixationData.CSVHeader() );
            }
            foreach (var v in dataConnector.fixationData)
            {
                AddText(fixationFileStream, v.ToCSV());
            }
        }
        if (dataConnector.eyeData.Count > 0)
        {
            if (hasWrittenEyeTracking == false)
            {
                hasWrittenEyeTracking = true;
                eyeTrackingFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_eyetracking.csv");
                AddText(eyeTrackingFileStream, DataConnector.EyeData.CSVHeader());
            }
            foreach (var v in dataConnector.eyeData)
            {
                AddText(eyeTrackingFileStream, v.ToCSV());
            }
        }
        if (dataConnector.customEventData.Count > 0)
        {
            if (hasWrittenEvent == false)
            {
                hasWrittenEvent = true;
                eventFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_event.csv");
                AddText(eventFileStream, DataConnector.EventData.CSVHeader() );
            }
            foreach (var v in dataConnector.customEventData)
            {
                AddText(eventFileStream, v.ToCSV());
            }
        }
        if (dataConnector.dynamicData.Count > 0)
        {
            if (hasWrittenDynamic == false)
            {
                hasWrittenDynamic = true;
                dynamicFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_dynamic.csv");
                AddText(dynamicFileStream, DataConnector.DynamicData.CSVHeader());
            }
            foreach (var v in dataConnector.dynamicData)
            {
                AddText(dynamicFileStream, v.ToCSV());
            }
        }
        if (dataConnector.sensorData.Count > 0)
        {
            if (hasWrittenSensor == false)
            {
                hasWrittenSensor = true;
                sensorFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_sensor.csv");
                AddText(sensorFileStream, DataConnector.SensorData.CSVHeader());
            }
            foreach (var v in dataConnector.sensorData)
            {
                AddText(sensorFileStream, v.ToCSV());
            }
        }
        if (dataConnector.boundaryData.Count > 0)
        {
            if (hasWrittenBoundary == false)
            {
                hasWrittenBoundary = true;
                boundaryFileStream = CreateFile(path + Cognitive3D_Manager.SessionID + "_boundary.csv");
                AddText(boundaryFileStream, DataConnector.BoundaryData.CSVHeader());
            }
            foreach (var v in dataConnector.boundaryData)
            {
                AddText(boundaryFileStream, v.ToCSV());
            }
        }

        dataConnector.ClearCaches();
    }

    private static void AddText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
    }
    private static void AddJsonText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
        info = new UTF8Encoding(true).GetBytes(",");
        fs.Write(info, 0, info.Length);
    }

    void OnDisable()
    {
        CloseFiles();
    }

    void CloseFiles()
    {
        if (gazeFileStream != null)
        {
            if (hasWrittenGaze)
            {
                gazeFileStream.Position = gazeFileStream.Position - 1;
                AddText(gazeFileStream, "]}");
            }
            gazeFileStream.Dispose();
            gazeFileStream = null;
        }
        if (fixationFileStream != null)
        {
            if (hasWrittenFixation)
            {
                fixationFileStream.Position = fixationFileStream.Position - 1;
                AddText(fixationFileStream, "]}");
            }
            fixationFileStream.Dispose();
            fixationFileStream = null;
        }
        if (eyeTrackingFileStream != null)
        {
            if (hasWrittenEyeTracking)
            {
                eyeTrackingFileStream.Position = eyeTrackingFileStream.Position - 1;
                AddText(eyeTrackingFileStream, "]}");
            }
            eyeTrackingFileStream.Dispose();
            eyeTrackingFileStream = null;
        }
        if (eventFileStream != null)
        {
            if (hasWrittenEvent)
            {
                eventFileStream.Position = eventFileStream.Position - 1;
                AddText(eventFileStream, "]}");
            }
            eventFileStream.Dispose();
            eventFileStream = null;
        }
        if (dynamicFileStream != null)
        {
            if (hasWrittenDynamic)
            {
                dynamicFileStream.Position = dynamicFileStream.Position - 1;
                AddText(dynamicFileStream, "]}");
            }
            dynamicFileStream.Dispose();
            dynamicFileStream = null;
        }
        if (sensorFileStream != null)
        {
            if (hasWrittenSensor)
            {
                sensorFileStream.Position = sensorFileStream.Position - 1;
                AddText(sensorFileStream, "]}");
            }
            sensorFileStream.Dispose();
            sensorFileStream = null;
        }
        if (boundaryFileStream != null)
        {
            if (hasWrittenBoundary)
            {
                boundaryFileStream.Position = boundaryFileStream.Position - 1;
                AddText(boundaryFileStream, "]}");
            }
            boundaryFileStream.Dispose();
            boundaryFileStream = null;
        }
    }
}