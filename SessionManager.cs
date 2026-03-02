using UnityEngine;
using Cognitive3D; // Make sure to include the Cognitive3D namespace

public class SessionNameController : MonoBehaviour
{
    [SerializeField] public string sessionName = "KH_100";
    
    void Start()
    {
        // Set the session name from the inspector variable
        Cognitive3D_Manager.SetSessionName(sessionName);
    }
}