using UnityEngine;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }

    public Vector3 Offset { get; private set; } = Vector3.zero;
    public bool HasCalibration { get; private set; } = false;

    void Awake()
    {
        // keep one copy alive across scene loads
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetOffset(Vector3 offset)
    {
        Offset = offset;
        HasCalibration = true;
    }
}
