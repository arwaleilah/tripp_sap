using UnityEngine;

public class UniqueCoinSpinner : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 200f;
    
    void Update()
    {
        // Rotate around the FORWARD axis (Z-axis) after the coin is oriented properly
        // Since the coin is already rotated 90 degrees on X in CoinMovement,
        // rotating on Z will make it spin like a coin on a table
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}