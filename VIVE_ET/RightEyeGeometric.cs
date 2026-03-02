using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class RightEyeGeometric : MonoBehaviour
{
    public float eyeOpenness;
    public float eyeSqueeze;
    public float eyeWide;
    public bool isValid;

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyeGeometricData(out XrSingleEyeGeometricDataHTC[] out_geometrics);
        XrSingleEyeGeometricDataHTC rightGeometric = out_geometrics[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

        isValid = rightGeometric.isValid;

        if(rightGeometric.isValid)
        {
            eyeOpenness = rightGeometric.eyeOpenness;
            eyeSqueeze = rightGeometric.eyeSqueeze;
            eyeWide = rightGeometric.eyeWide;
        }
    }  
}