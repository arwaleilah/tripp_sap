using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using TMPro;

public class GazeCalibrator : MonoBehaviour
{
    [Header("Scene References")]
    public Camera mainCamera;
    public LeftGaze leftGaze;
    public RightGaze rightGaze;

    [Header("Calibration UI")]
    public GameObject centerDotPrefab;         // small sphere prefab
    public float dotRadius = 0.05f;            // visible target size (~10 cm)
    public TextMeshProUGUI instructionText;    // world-space TMP text
    public float textFadeDelay = 1.0f;

    [Header("Plane Settings (match spaceship logic)")]
    public float distanceFromCamera = 10f;
    public float heightOffset = -1.5f;

    [Header("Sampling Settings")]
    public int sampleFrames = 45;              // ~0.5 s at 90 Hz
    public float minValidityRatio = 0.6f;      // proportion of valid samples required

    private GameObject centerDot;
    private bool calibrated = false;

    void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (instructionText)
        {
            instructionText.text = "Look at the dot and press trigger to calibrate";
            instructionText.enabled = true;
        }

        // Wait briefly for XR rig to initialize
        StartCoroutine(SpawnDotAfterXRReady());
    }

    IEnumerator SpawnDotAfterXRReady()
    {
        yield return new WaitForSeconds(0.5f); // wait for tracking to stabilize

        // place the dot where spaceship normally spawns
        Vector3 center = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
        center.y += heightOffset;

        if (centerDotPrefab)
        {
            centerDot = Instantiate(centerDotPrefab, center, Quaternion.identity);
            centerDot.transform.localScale = Vector3.one * dotRadius;
        }
        else
        {
            // fallback sphere if prefab missing
            centerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerDot.transform.position = center;
            centerDot.transform.localScale = Vector3.one * dotRadius;
            Destroy(centerDot.GetComponent<Collider>());
        }
    }

    void Update()
    {
        if (calibrated || centerDot == null) return;

        if (Input.GetKeyDown(KeyCode.C) || AnyTriggerPressed())
        {
            StartCoroutine(CalibrateRoutine());
        }
    }

    IEnumerator CalibrateRoutine()
    {
        calibrated = true;
        if (instructionText) instructionText.text = "Calibrating...";

        Plane gazePlane = new Plane(
            -mainCamera.transform.forward,
            mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera
        );

        List<Vector3> hits = new List<Vector3>();
        int validCount = 0;

        for (int i = 0; i < sampleFrames; i++)
        {
            Vector3 dir = GetCombinedWorldGazeDirection(out bool valid);
            if (valid)
            {
                Ray r = new Ray(mainCamera.transform.position, dir);
                if (gazePlane.Raycast(r, out float enter))
                {
                    hits.Add(r.GetPoint(enter));
                    validCount++;
                }
            }
            yield return null;
        }

        float ratio = (sampleFrames == 0) ? 0f : (float)validCount / sampleFrames;
        Vector3 offset = Vector3.zero;

        if (ratio < minValidityRatio || hits.Count == 0)
        {
            Debug.LogWarning($"[Calibration] Not enough valid gaze samples ({validCount}/{sampleFrames}). Using zero offset.");
        }
        else
        {
            Vector3 avg = Vector3.zero;
            foreach (var h in hits) avg += h;
            avg /= hits.Count;

            Vector3 center = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;
            center.y += heightOffset;
            offset = center - avg;
        }

        ApplyOffset(offset);

        if (instructionText) instructionText.text = "Calibration complete!";
        yield return new WaitForSeconds(textFadeDelay);

        if (instructionText) instructionText.enabled = false;
        if (centerDot) centerDot.SetActive(false);

        yield return new WaitForSeconds(1f);

        // ✅ Move to the next scene (ConditionONE)
        SceneManager.LoadScene("ConditionONE");
    }

    void ApplyOffset(Vector3 offset)
    {
        if (CalibrationManager.Instance)
            CalibrationManager.Instance.SetOffset(offset);

        Debug.Log($"[Calibration] Stored offsetCorrection = {offset}");
    }

    Vector3 GetCombinedWorldGazeDirection(out bool valid)
    {
        valid = false;
        Vector3 sum = Vector3.zero;
        int c = 0;

        if (leftGaze && leftGaze.isValid) { sum += leftGaze.worldGazeDirection; c++; }
        if (rightGaze && rightGaze.isValid) { sum += rightGaze.worldGazeDirection; c++; }

        if (c == 0) return mainCamera.transform.forward;
        valid = true;
        return (sum / c).normalized;
    }

    bool AnyTriggerPressed()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller, devices);

        foreach (var d in devices)
        {
            if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip)
                return true;
        }
        return false;
    }
}
