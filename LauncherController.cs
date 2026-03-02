using UnityEngine;
using TMPro;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

public class LauncherController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI displayText;

    [Header("Participant")]
    public string participantID = "KH_100";

    private bool countdownStarted = false;
    private bool acceptingInput = false;

    void Start()
    {
        string id = participantID;

        if (ExperimentManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(ExperimentManager.Instance.participantID))
            {
                id = ExperimentManager.Instance.participantID;
            }
            else
            {
                ExperimentManager.Instance.participantID = id;
            }
        }

        displayText.text =
            $"Hello {id}!\n" +
            "Try to collect as many coins as you can by moving the spaceship with your gaze. " +
            "Each coin is worth 1 point!\n\n" +
            "Press any trigger to begin";

        Debug.Log("[Launcher] Ready. Each coin is worth 1 point.");
        StartCoroutine(LogConnectedDevices());
        StartCoroutine(EnableInputAfterDelay());
    }

    IEnumerator EnableInputAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);
        acceptingInput = true;
        Debug.Log("[Launcher] Now accepting input");
    }

    void Update()
    {
        if (acceptingInput && !countdownStarted && CheckAnyTriggerPressed())
        {
            countdownStarted = true;
            StartCoroutine(CountdownAndLoad());
        }
    }

    IEnumerator CountdownAndLoad()
    {
        for (int i = 3; i > 0; i--)
        {
            displayText.text = $"Trial starting in {i}...";
            Debug.Log($"[Launcher] Countdown: {i}");
            yield return new WaitForSeconds(1f);
        }

        if (ExperimentManager.Instance != null)
        {
            Debug.Log("[Launcher] Using ExperimentManager.LoadNextScene()");
            ExperimentManager.Instance.LoadNextScene();
        }
        else
        {
            Debug.LogError("[Launcher] ExperimentManager not found! Cannot advance to next scene.");
        }
    }

    private bool CheckAnyTriggerPressed()
    {
        InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice leftController  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool rightTrigger = false, leftTrigger = false, rightGrip = false, leftGrip = false;

        rightController.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger);
        leftController.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger);
        rightController.TryGetFeatureValue(CommonUsages.gripButton, out rightGrip);
        leftController.TryGetFeatureValue(CommonUsages.gripButton, out leftGrip);

        bool legacyInput = Input.GetButtonDown("Fire1") ||
                           Input.GetKeyDown(KeyCode.Space) ||
                           Input.GetMouseButtonDown(0);

        return rightTrigger || leftTrigger || rightGrip || leftGrip || legacyInput;
    }

    IEnumerator LogConnectedDevices()
    {
        yield return new WaitForSeconds(2f);

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        Debug.Log($"=== Connected XR Devices ({devices.Count}) ===");
        foreach (var device in devices)
        {
            Debug.Log($"Device: {device.name} | Role: {device.role} | Valid: {device.isValid}");
        }
    }
}