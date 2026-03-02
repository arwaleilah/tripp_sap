using UnityEngine;

public class CoinCollector : MonoBehaviour
{
    [Header("Coin Settings")]
    [Tooltip("Points awarded when this coin is collected")]
    public int pointValue = 1;
    
    [Header("Effects")]
    [Tooltip("Optional particle effect to play when collected")]
    public GameObject collectEffect;
    [Tooltip("Optional sound to play when collected")]
    public AudioClip collectSound;
    
    // Reference to the coin spawner for data tracking
    private SimplifiedCoinSpawner coinSpawner;
    
    private void Start()
    {
        // Find the coin spawner in the scene
        coinSpawner = FindObjectOfType<SimplifiedCoinSpawner>();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the collider is the player's ship
        if (other.CompareTag("Player"))
        {
            // Find the GameManager to add points
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.AddPoints(pointValue);
            }
            
            // Notify the coin spawner for Cognitive3D tracking
            if (coinSpawner != null)
            {
                coinSpawner.MarkCoinAsCollected(gameObject);
            }
            
            // Play collection effect if assigned
            if (collectEffect != null)
            {
                Instantiate(collectEffect, transform.position, Quaternion.identity);
            }
            
            // Play sound if assigned
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, 2.0f);
            }
            
            // Destroy the coin
            Destroy(gameObject);
        }
    }
}