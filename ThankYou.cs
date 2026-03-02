using UnityEngine;
using TMPro;

public class ThankYou : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI displayText;

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
                $"Thank you for participating!\n\n" +
                "You have completed all of the trials for this experiment.\n" +
                "Please remove the headset and let us know that you're done.";
        }

        Debug.Log("[ThankYou] Experiment complete.");
    }
}