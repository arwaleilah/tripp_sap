using UnityEngine;
using TMPro;
using UnityEngine.XR;

public class BreakSceneController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI displayText;

    [Header("Flow Settings")]
    [Tooltip("Minimum break duration before the player is allowed to continue (seconds)")]
    public float minBreakDuration = 0f;

    private float timer = 0f;

    void Start()
    {
        string id = "Participant";

        if (ExperimentManager.Instance != null && 
            !string.IsNullOrEmpty(ExperimentManager.Instance.participantID))
        {
            id = ExperimentManager.Instance.participantID;
        }

        if (displayText != null)
        {
            displayText.text =
                $"Great job!\n\n" +
                "You can take a short break now.\n" +
                "Please keep your headset on.\n\n" +
                "When you're ready to continue, press any trigger.";
        }

        Debug.Log("[BreakScene] Break scene ready – waiting for input to continue.");
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= minBreakDuration && CheckAnyTriggerPressed())
        {
            if (ExperimentManager.Instance != null)
            {
                Debug.Log("[BreakScene] Input detected – advancing to next scene.");
                ExperimentManager.Instance.LoadNextScene();
            }
            else
            {
                Debug.LogError("[BreakScene] ExperimentManager not found! Cannot continue experiment.");
            }
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
}