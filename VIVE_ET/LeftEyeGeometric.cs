using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class LeftEyeGeometric : MonoBehaviour
{
    public float eyeOpenness;
    public float eyeSqueeze;
    public float eyeWide;
    public bool isValid;

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyeGeometricData(out XrSingleEyeGeometricDataHTC[] out_geometrics);
        XrSingleEyeGeometricDataHTC leftGeometric = out_geometrics[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        isValid = leftGeometric.isValid;

        if(leftGeometric.isValid)
        {
            eyeOpenness = leftGeometric.eyeOpenness;
            eyeSqueeze = leftGeometric.eyeSqueeze;
            eyeWide = leftGeometric.eyeWide;
        }
    }  
}