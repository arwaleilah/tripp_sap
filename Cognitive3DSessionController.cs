using UnityEngine;
using UnityEngine.SceneManagement;
using Cognitive3D;

/// <summary>
/// Controls Cognitive3D session recording for C-scenes only.
/// Session starts on first C-scene and ends on non-C scenes.
/// Handles repeated C-scenes without restarting sessions.
/// </summary>
public class Cognitive3DSessionController : MonoBehaviour
{
    // List all C-scenes here
    public string[] cScenes = { "c1", "c2", "c3", "c4", "c5", "c6" };

    // Tracks if a session is running
    private bool sessionRunning = false;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name.ToLower();

        if (IsCScene(sceneName))
        {
            if (!sessionRunning)
            {
                // Start session on first C-scene
                Cognitive3D_Manager.Instance.BeginSession();
                Cognitive3D_Manager.SetSessionProperty("ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id);
                Cognitive3D_Manager.SetSessionTag(scene.name);
                sessionRunning = true;
                Debug.Log($"Cognitive3D session started on {scene.name}");
            }
            // else: session already running, do nothing
        }
        else
        {
            if (sessionRunning)
            {
                // End session when leaving C-scenes
                Cognitive3D_Manager.Instance.EndSession();
                sessionRunning = false;
                Debug.Log($"Cognitive3D session ended on {scene.name}");
            }
            // else: no session running, do nothing
        }
    }

    // Helper to check if a scene is one of the C-scenes
    bool IsCScene(string sceneName)
    {
        foreach (var s in cScenes)
        {
            if (sceneName.Contains(s.ToLower()))
                return true;
        }
        return false;
    }
}
