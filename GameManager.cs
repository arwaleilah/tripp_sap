using UnityEngine;
using TMPro;
using UnityEngine.XR;

public class GameManager : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long coins spawn (seconds)")]
    public float gameplayDuration = 90f;

    [Tooltip("When to show Game Over UI (seconds)")]
    public float showGameOverAt = 120f;

    [Header("UI References")]
    public GameObject canvasGameOver;
    public TextMeshProUGUI textScore;

    [Header("VR Camera (HMD Camera)")]
    public Camera vrCamera;

    [Header("Gameplay References")]
    public SimplifiedCoinSpawner coinSpawner;
    public GameObject playerShip;

    private float timer = 0f;
    private int score = 0;

    private bool gameOverShown = false;
    private bool waitingForContinue = false;

    void Start()
    {
        timer = 0f;

        if (canvasGameOver != null)
            canvasGameOver.SetActive(false);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= gameplayDuration && coinSpawner != null)
        {
            coinSpawner.SetSpawningActive(false);
        }

        if (timer >= showGameOverAt && !gameOverShown)
        {
            ShowGameOver();
        }

        if (waitingForContinue && CheckAnyTriggerPressed())
        {
            waitingForContinue = false;
            LoadNextScene();
        }
    }

    public void AddPoints(int amount)
    {
        score += amount;
    }

    private void ShowGameOver()
    {
        gameOverShown = true;

        if (playerShip != null)
        {
            Rigidbody rb = playerShip.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            foreach (var mb in playerShip.GetComponents<MonoBehaviour>())
                mb.enabled = false;
        }

        if (textScore != null)
        {
            textScore.text = $"Game Over!\nYou scored {score} points.\n\nPress any trigger to continue.";
        }

        if (canvasGameOver != null)
            canvasGameOver.SetActive(true);

        PositionGameOverCanvas();

        waitingForContinue = true;
    }

    private void PositionGameOverCanvas()
    {
        if (vrCamera == null || canvasGameOver == null)
            return;

        Vector3 targetPos = vrCamera.transform.position + vrCamera.transform.forward * 2f;
        Quaternion targetRot = Quaternion.LookRotation(vrCamera.transform.forward, Vector3.up);

        canvasGameOver.transform.SetPositionAndRotation(targetPos, targetRot);
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

    private void LoadNextScene()
    {
        if (ExperimentManager.Instance != null)
        {
            Debug.Log("[GameManager] Using ExperimentManager.LoadNextScene()");
            ExperimentManager.Instance.LoadNextScene();
        }
        else
        {
            Debug.LogError("[GameManager] ExperimentManager not found! Cannot advance to next scene.");
        }
    }
}