using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class LeftPupil : MonoBehaviour
{
    public float pupilDiameter;
    public Vector2 pupilPosition;
    public bool isDiameterValid;
    public bool isPositionValid;

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] out_pupils);
        XrSingleEyePupilDataHTC leftPupil = out_pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
    
        isDiameterValid = leftPupil.isDiameterValid;
        isPositionValid = leftPupil.isPositionValid;
    
        if(leftPupil.isDiameterValid)
        {
            pupilDiameter = leftPupil.pupilDiameter;
        }
    
        if(leftPupil.isPositionValid)
        {
            pupilPosition = new Vector2(leftPupil.pupilPosition.x, leftPupil.pupilPosition.y);
        }
    }
}