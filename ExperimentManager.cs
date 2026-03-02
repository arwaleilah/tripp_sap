using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance;

    [Header("Participant")]
    public string participantID;

    private List<string> sceneOrder;
    private int currentSceneIndex = 0; //changed this from -1 b/c otherwise Launcher was loading 2x in the beginning
    private string dataPath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            dataPath = Path.Combine(Application.persistentDataPath, "ExperimentData");
            Directory.CreateDirectory(dataPath);

            // ============================================
            // CHANGE SCENE ORDER HERE FOR EACH PARTICIPANT (N 126)
            // ============================================
            sceneOrder = new List<string>
            {
                "Launcher",
                "c4",
                "Launcher",
                "c4",
                "Launcher",
                "c3",
                "BreakScene",
                "Launcher",
                "c1",
                "56Launcher",
                "c5",
                "56Launcher",
                "c5",
                "BreakScene",
                "Launcher",
                "c1",
                "Launcher",
                "c2",
                "Launcher",
                "c6",
                "BreakScene",
                "Launcher",
                "c2",
                "Launcher",
                "c2",
                "56Launcher",
                "c5",
                "BreakScene",
                "Launcher",
                "c1",
                "Launcher",
                "c3",
                "56Launcher",
                "c6",
                "BreakScene",
                "56Launcher",
                "c6",
                "Launcher",
                "c3",
                "Launcher",
                "c4",
                "ThankYou"
            };
            // ============================================
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LogData(string dataLine)
    {
        string filePath = Path.Combine(dataPath, $"{participantID}_data.csv");
        File.AppendAllText(filePath, dataLine + "\n");
    }

    public string GetUpcomingSceneName()
    {
        if (sceneOrder == null || sceneOrder.Count == 0)
            return null;

        int nextIndex = currentSceneIndex + 1;
        if (nextIndex < 0 || nextIndex >= sceneOrder.Count)
            return null;

        return sceneOrder[nextIndex];
    }

    public void LoadNextScene()
    {
        if (sceneOrder == null || sceneOrder.Count == 0)
        {
            Debug.LogError("[ExperimentManager] sceneOrder is empty – cannot load next scene.");
            return;
        }

        currentSceneIndex++;

        if (currentSceneIndex >= 0 && currentSceneIndex < sceneOrder.Count)
        {
            string nextScene = sceneOrder[currentSceneIndex];
            Debug.Log($"[ExperimentManager] Loading sceneOrder[{currentSceneIndex}] = {nextScene}");
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.LogError("[ExperimentManager] End of sceneOrder reached. No more scenes to load.");
        }
    }
}