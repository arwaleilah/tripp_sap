using UnityEngine;

public class CoinMovement : MonoBehaviour
{
    public float moveSpeed = 10f;

    void Start()
    {
        // Set initial orientation (flat facing up)
        transform.rotation = Quaternion.Euler(90, 0, 0);

        // Add the spinner component if needed
        if (GetComponent<UniqueCoinSpinner>() == null)
        {
            gameObject.AddComponent<UniqueCoinSpinner>();
        }
    }

    void Update()
    {
        // Move toward the camera without changing rotation
        transform.position += Vector3.back * moveSpeed * Time.deltaTime;
    }
}
