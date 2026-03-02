using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class RightPupil : MonoBehaviour
{
    public float pupilDiameter;
    public Vector2 pupilPosition;
    public bool isDiameterValid;
    public bool isPositionValid;

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] out_pupils);
        XrSingleEyePupilDataHTC rightPupil = out_pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
    
        isDiameterValid = rightPupil.isDiameterValid;
        isPositionValid = rightPupil.isPositionValid;
    
        if(rightPupil.isDiameterValid)
        {
            pupilDiameter = rightPupil.pupilDiameter;
        }
    
        if(rightPupil.isPositionValid)
        {
            pupilPosition = new Vector2(rightPupil.pupilPosition.x, rightPupil.pupilPosition.y);
        }
    }
}