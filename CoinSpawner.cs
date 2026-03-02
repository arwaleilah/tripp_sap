
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class SimplifiedCoinSpawner : MonoBehaviour
{
    [System.Serializable]
    public class CoinType
    {
        public string name;
        public GameObject coinObject;
        public float sizeMultiplier = 1f;

        private static readonly Vector3 baseScale = new Vector3(0.1737f, 0.006f, 0.1737f);

        public Vector3 CalculateScale()
        {
            return Vector3.Scale(baseScale, new Vector3(sizeMultiplier, 1f, sizeMultiplier));
        }
    }

    [Header("Coin Types")]
    public CoinType smallCoin; 
    public CoinType largeCoin; 

    [Header("References")]
    public Camera mainCamera;
    public Transform player;
    public v3SpaceshipController spaceshipController; // Reference to get the offset
    public string conditionId;



    [Header("Gaze Tracking")]
    public Transform gazeCenter; // Reference to the player's gaze direction (camera or eye tracker)
    [Tooltip("If true, logs real-time eccentricity measurements to the console")]
    public bool logEccentricityValues = true;
    [Range(0.1f, 2f)]
    public float logInterval = 0.5f; // How often to log values (in seconds)

    [Header("Offset Compensation")]
    [Tooltip("If true, automatically uses the spaceship's offset correction")]
    public bool useSpaceshipOffset = true;
    [Tooltip("Manual offset override if not using spaceship's offset")]
    public Vector3 manualOffsetCorrection = Vector3.zero;

    [Header("Data Logging")]
    [Tooltip("If true, saves eccentricity data to CSV files")]
    public bool saveDataToFile = true;
    [Tooltip("Base directory path where data will be saved")]
    public string dataBasePath = "";
    [Tooltip("If true, will include scene name in session folder")]
    public bool includeSceneName = true;

    [Header("Cognitive3D Integration")]
    [Tooltip("If true, sends eccentricity data to Cognitive3D")]
    public bool sendToCognitive3D = true;

    [Header("Spawn Settings")]
    public float spawnAheadDistance = 40f; // Distance ahead of the player to spawn coins
    public float spawnInterval = 3f;       // Time in seconds between spawn waves
    [Range(1, 8)]
    public int coinsPerWave = 4;          // Number of coins per wave
    [Tooltip("Duration of the trial in seconds")]
    public float trialDuration = 90f;     // Default to 90 seconds

    [Header("Eccentricity Settings")]
    public float eccentricityScaleFactor = 0.09f; // Converts degrees to Unity units

    [Header("Spawn Boundaries")]
    public float maxHorizontalSpawn = 5f; // Horizontal spawn boundary
    public float maxVerticalSpawn = 3f;   // Vertical spawn boundary
    public float minCoinDistance = 1.5f;  // Minimum distance between coins

    [Header("Z & Time Staggering (per wave)")]
    [Tooltip("Max Z offset (+/-) applied within a wave to break the flat wall of coins")]
    public float zStaggerAmount = 2f;
    [Tooltip("Minimum delay between coins in the same wave")]
    public float minStaggerDelay = 0.1f;
    [Tooltip("Maximum delay between coins in the same wave")]
    public float maxStaggerDelay = 0.25f;

    // Eccentricity ranges for research
    [Header("Eccentricity Ranges (degrees)")]
    public float[][] eccentricityRanges = new float[][]
    {
        new float[] { 0f, 5f },    
        new float[] { 5f, 10f }, 
        new float[] { 10f, 15f }, 
        new float[] { 15f, 20f },  
        new float[] { 20f, 25f }   
    };

    // Store coin data for eccentricity tracking
    [System.Serializable]
    public class TrackedCoin
    {
        public GameObject coinObject;
        public string coinTypeName;
        public float initialEccentricity; // Eccentricity from initial fixed point
        public float currentEccentricity; // Current eccentricity from gaze
        public Vector3 position;          // World position
        public int eccentricityRangeIndex; // Which range it was from
        public string uniqueId;           // Unique identifier for this coin
        public bool isLarge;         // true = big coin, false = small coin
        public bool isFast;          // true = fast movement script attached
        public int pointValue;       // value from the coin’s own CoinCollector script
        public string conditionId;      // optional, set from GameManager

        // For data collection
        public bool hasBeenObserved = false;
        public float minEccentricityReached = float.MaxValue;
        public float timeSpentInFocus = 0f;
        public System.DateTime spawnTime;
        public List<EccentricityDataPoint> dataPoints = new List<EccentricityDataPoint>();
    }

    // For recording individual data points over time
    public class EccentricityDataPoint
    {
        public float time;               // Time since spawn (approx)
        public float eccentricity;       // Current eccentricity
        public bool isBeingObserved;     // Whether being directly observed
        public Vector3 playerPosition;   // Player position
        public Vector3 gazeDirection;    // Gaze direction
    }

    private List<TrackedCoin> spawnedCoins = new List<TrackedCoin>();
    private List<TrackedCoin> destroyedCoins = new List<TrackedCoin>();
    private bool isSpawningActive = true;
    private float nextSpawnTime;
    private float nextLogTime;
    private float nextSpawnZ;
    private float trialStartTime;
    private string sessionId;
    private string sessionDataPath;
    private StreamWriter summaryWriter;

    // For tracking currently observed coin
    private TrackedCoin currentlyObservedCoin = null;
    private float observationStartTime = 0f;

    // Current offset being used
    private Vector3 currentOffset = Vector3.zero;

    // Helper struct for staggered wave planning
    private struct CoinSpawnInfo
    {
        public Vector3 position;
        public int rangeIndex;

        public CoinSpawnInfo(Vector3 pos, int rangeIdx)
        {
            position = pos;
            rangeIndex = rangeIdx;
        }
    }

    private bool IsCoinFast(GameObject coinPrefab)
    {
        if (coinPrefab == null) return false;

        string name = coinPrefab.name.ToUpper();

        if (name.Length < 1) return false;

        char lastChar = name[name.Length - 1];
        return lastChar == 'F';
    }

    void SendToCognitive3D(string eventName, TrackedCoin coin)
    {
        new Cognitive3D.CustomEvent(eventName)
            .SetProperty("coinId", coin.uniqueId)
            .SetProperty("coinType", coin.coinTypeName)
            .SetProperty("isLarge", coin.isLarge)
            .SetProperty("isFast", coin.isFast)
            .SetProperty("pointValue", coin.pointValue)
            .SetProperty("eccentricityRange", coin.eccentricityRangeIndex + 1)
            .SetProperty("initialEccentricity", coin.initialEccentricity)
            .SetProperty("currentEccentricity", coin.currentEccentricity)
            .Send(coin.position);
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gazeCenter == null)
            gazeCenter = mainCamera.transform;

        if (mainCamera == null || player == null)
        {
            Debug.LogError("Required references not set!");
            enabled = false;
            return;
        }

        // Get initial offset
        UpdateOffset();

        // Initialize data logging
        if (saveDataToFile)
        {
            InitializeDataLogging();
        }

        trialStartTime = Time.time;
        Debug.Log($"Trial started at time: {trialStartTime}");
        Debug.Log($"Using offset: {currentOffset}");

        // Start spawning immediately (staggered)
        StartCoroutine(SpawnWaveStaggered());
        nextSpawnTime = Time.time + spawnInterval;
        nextLogTime = Time.time + logInterval;
    }

    void UpdateOffset()
    {
        if (useSpaceshipOffset && spaceshipController != null)
        {
            currentOffset = spaceshipController.offsetCorrection;
        }
        else
        {
            currentOffset = manualOffsetCorrection;
        }
    }

    void InitializeDataLogging()
    {
        try
        {
            string basePath = Path.Combine(Application.persistentDataPath, "ArwaSAP", "CoinSpawnLog");
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (includeSceneName && !string.IsNullOrEmpty(sceneName))
            {
                sessionId = $"Session_{sceneName}_{timestamp}";
            }
            else
            {
                sessionId = $"Session_{timestamp}";
            }

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            sessionDataPath = Path.Combine(basePath, sessionId);
            Directory.CreateDirectory(sessionDataPath);

            string coinDataFolder = Path.Combine(sessionDataPath, "CoinData");
            Directory.CreateDirectory(coinDataFolder);

            string summaryPath = Path.Combine(sessionDataPath, "summary.csv");
            summaryWriter = new StreamWriter(summaryPath, false, Encoding.UTF8);
            summaryWriter.WriteLine(
    "CoinID,CoinType,IsLarge,IsFast,PointValue,ConditionID,EccentricityRange,InitialEccentricity,MinEccentricityReached,TimeSpentInFocus,WasObserved,SpawnTime,DestroyTime"
);
            summaryWriter.Flush();

            Debug.Log($"Initialized data logging to: {sessionDataPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize data logging: {e.Message}");
            saveDataToFile = false;
        }
    }

    void OnDestroy()
    {
        if (saveDataToFile && summaryWriter != null)
        {
            Debug.Log("Saving all coin data and closing files due to component destruction.");

            foreach (TrackedCoin coin in spawnedCoins)
            {
                SaveCoinData(coin, true);
            }

            summaryWriter.Flush();
            summaryWriter.Close();
            summaryWriter = null;

            Debug.Log($"Closed data logging files. Data saved to: {sessionDataPath}");
        }
    }

    void Update()
    {
        // Update offset in case it changed
        UpdateOffset();

        CleanupOldCoins();
        UpdateEccentricityValues();

        if (Time.time - trialStartTime >= trialDuration)
        {
            if (isSpawningActive)
            {
                Debug.Log("Trial duration reached. Stopping coin spawning.");
                EndTrial();
            }
            return;
        }

        if (logEccentricityValues && Time.time >= nextLogTime)
        {
            LogEccentricityValues();
            nextLogTime = Time.time + logInterval;
        }

        if (Time.time >= nextSpawnTime && isSpawningActive)
        {
            StartCoroutine(SpawnWaveStaggered());
            nextSpawnTime = Time.time + spawnInterval;
            Debug.Log($"Scheduled next staggered wave at time: {Time.time}");
        }
    }

    public float CalculateEccentricity(Vector3 position, Transform reference)
    {
        if (reference == null) return 0f;

        Vector3 referenceForward = reference.forward;
        Vector3 directionToPosition = (position - reference.position).normalized;
        float angleDegrees = Vector3.Angle(referenceForward, directionToPosition);

        return angleDegrees;
    }

    void UpdateEccentricityValues()
    {
        TrackedCoin previouslyObservedCoin = currentlyObservedCoin;
        currentlyObservedCoin = null;

        foreach (TrackedCoin coin in spawnedCoins)
        {
            if (coin.coinObject == null) continue;

            coin.position = coin.coinObject.transform.position;
            coin.currentEccentricity = CalculateEccentricity(coin.position, gazeCenter);

            bool isBeingObserved = coin.currentEccentricity < 5f; // Foveal threshold

            if (saveDataToFile)
            {
                RecordDataPoint(coin, isBeingObserved);
            }

            if (isBeingObserved)
            {
                currentlyObservedCoin = coin;

                if (coin.currentEccentricity < coin.minEccentricityReached)
                {
                    coin.minEccentricityReached = coin.currentEccentricity;
                }

                if (!coin.hasBeenObserved)
                {
                    coin.hasBeenObserved = true;
                    observationStartTime = Time.time;
                    Debug.Log($"Coin now being observed: {coin.coinTypeName} from range {coin.eccentricityRangeIndex + 1}");

                    if (sendToCognitive3D)
                    {
                        SendToCognitive3D("CoinObserved", coin);
                    }
                }
            }
        }

        if (previouslyObservedCoin != null && currentlyObservedCoin != previouslyObservedCoin)
        {
            previouslyObservedCoin.timeSpentInFocus += (Time.time - observationStartTime);
            Debug.Log($"Observation ended: Spent {previouslyObservedCoin.timeSpentInFocus:F2}s looking at {previouslyObservedCoin.coinTypeName}");

            if (sendToCognitive3D)
            {
                SendToCognitive3D("CoinObservationEnded", previouslyObservedCoin);
            }
        }
    }

    void RecordDataPoint(TrackedCoin coin, bool isBeingObserved)
    {
        if (player == null || gazeCenter == null) return;

        EccentricityDataPoint dataPoint = new EccentricityDataPoint
        {
            time = Time.time - coin.spawnTime.Ticks / 10000000f, // preserves your original logic
            eccentricity = coin.currentEccentricity,
            isBeingObserved = isBeingObserved,
            playerPosition = player.position,
            gazeDirection = gazeCenter.forward
        };

        coin.dataPoints.Add(dataPoint);
    }

    void LogEccentricityValues()
    {
        foreach (TrackedCoin coin in spawnedCoins)
        {
            if (coin.coinObject == null) continue;

            string logMessage = $"Coin {coin.coinTypeName} (Range {coin.eccentricityRangeIndex + 1}): " +
                                $"Initial={coin.initialEccentricity:F1}°, " +
                                $"Current={coin.currentEccentricity:F1}°";

            if (coin == currentlyObservedCoin)
            {
                logMessage += " [CURRENTLY OBSERVED]";
            }

            Debug.Log(logMessage);
        }

        Debug.Log($"Current offset: {currentOffset}");
        Debug.Log("------------------------------");
    }

    Vector3 GetPositionWithEccentricity(float[] range, List<Vector3> existingPositions)
    {
        const int maxAttempts = 10;

        // Get the center position including offset
        Vector3 centerWithOffset = new Vector3(currentOffset.x, currentOffset.y, nextSpawnZ);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float eccentricityDegrees = Random.Range(range[0], range[1]);
            float eccentricityRadial = eccentricityDegrees * eccentricityScaleFactor;
            float angleRadians = Random.Range(0f, Mathf.PI * 2f);

            // Calculate position relative to offset center
            float x = centerWithOffset.x + Mathf.Cos(angleRadians) * eccentricityRadial;
            float y = centerWithOffset.y + Mathf.Sin(angleRadians) * eccentricityRadial;

            Vector3 spawnPos = new Vector3(x, y, nextSpawnZ);

            // Check bounds relative to offset center
            bool inBounds = Mathf.Abs(x - currentOffset.x) <= maxHorizontalSpawn &&
                           Mathf.Abs(y - currentOffset.y) <= maxVerticalSpawn;

            if (!inBounds)
                continue;

            bool isTooClose = false;
            foreach (Vector3 existingPos in existingPositions)
            {
                if (Vector3.Distance(new Vector2(spawnPos.x, spawnPos.y),
                                    new Vector2(existingPos.x, existingPos.y)) < minCoinDistance)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (!isTooClose)
            {
                float actualEccentricity = CalculateEccentricity(spawnPos, gazeCenter);
                Debug.Log($"Spawning coin at {eccentricityDegrees:F1}° intended, {actualEccentricity:F1}° actual, position: {spawnPos}");
                return spawnPos;
            }
        }

        // Fallback with offset
        float fallbackEccentricity = (range[0] + range[1]) / 2;
        float fallbackRadius = fallbackEccentricity * eccentricityScaleFactor;
        float fallbackAngle = Random.Range(0f, Mathf.PI * 2f);
        float fallbackX = centerWithOffset.x + Mathf.Clamp(Mathf.Cos(fallbackAngle) * fallbackRadius,
                                                           -maxHorizontalSpawn, maxHorizontalSpawn);
        float fallbackY = centerWithOffset.y + Mathf.Clamp(Mathf.Sin(fallbackAngle) * fallbackRadius,
                                                           -maxVerticalSpawn, maxVerticalSpawn);

        return new Vector3(fallbackX, fallbackY, nextSpawnZ);
    }

    IEnumerator SpawnWaveStaggered()
    {
        if (!isSpawningActive) yield break;

        nextSpawnZ = player.position.z + spawnAheadDistance;

        List<Vector3> wavePositions = new List<Vector3>();
        List<CoinSpawnInfo> plannedSpawns = new List<CoinSpawnInfo>();

        // 1) Try to spawn one coin per eccentricity range for balanced research data
        for (int i = 0; i < eccentricityRanges.Length && plannedSpawns.Count < coinsPerWave; i++)
        {
            Vector3 pos = GetPositionWithEccentricity(eccentricityRanges[i], wavePositions);
            if (pos != Vector3.zero)
            {
                // Add Z-stagger within the wave
                float zOffset = (zStaggerAmount > 0f) ? Random.Range(-zStaggerAmount, zStaggerAmount) : 0f;
                pos.z += zOffset;

                wavePositions.Add(pos);
                plannedSpawns.Add(new CoinSpawnInfo(pos, i));
            }
        }

        // 2) Fill remaining slots if needed
        while (plannedSpawns.Count < coinsPerWave)
        {
            int rangeIndex = Random.Range(0, eccentricityRanges.Length);
            Vector3 pos = GetPositionWithEccentricity(eccentricityRanges[rangeIndex], wavePositions);
            if (pos != Vector3.zero)
            {
                float zOffset = (zStaggerAmount > 0f) ? Random.Range(-zStaggerAmount, zStaggerAmount) : 0f;
                pos.z += zOffset;

                wavePositions.Add(pos);
                plannedSpawns.Add(new CoinSpawnInfo(pos, rangeIndex));
            }
        }

        // 3) Actually spawn coins with temporal staggering
        for (int i = 0; i < plannedSpawns.Count; i++)
        {
            Vector3 pos = plannedSpawns[i].position;
            int rangeIndex = plannedSpawns[i].rangeIndex;

            // Preserve your original size alternation logic:
            // originally used (wavePositions.Count % 2 == 0) with Count after Add.
            // That makes coin #1 (index 0) = large, #2 = small, etc.
            int coinNumber = i + 1; // convert 0-based index to 1-based order in wave
            CoinType coinType = (coinNumber % 2 == 0) ? smallCoin : largeCoin;

            SpawnCoin(coinType, pos, rangeIndex);

            // Stagger time between coins in this wave (but not after the last one)
            if (i < plannedSpawns.Count - 1 && maxStaggerDelay > 0f)
            {
                float delay = Random.Range(minStaggerDelay, maxStaggerDelay);
                yield return new WaitForSeconds(delay);
            }
        }

        Debug.Log($"Spawned staggered wave at time: {Time.time} with offset: {currentOffset}");
    }

    void SpawnCoin(CoinType coinType, Vector3 position, int rangeIndex)
{
    // Instantiate the coin
    GameObject newCoin = Instantiate(coinType.coinObject, position, Quaternion.identity);
    newCoin.transform.localScale = coinType.CalculateScale();

    float initialEccentricity = CalculateEccentricity(position, gazeCenter);

    // --- Extract point value from CoinCollector script ---
    var collector = newCoin.GetComponent<CoinCollector>();
    int pointValue = collector != null ? collector.pointValue : 1;

    // Determine size based on your existing logic
    bool isLarge = coinType == largeCoin;  // preserves your current size selection

    // Determine speed using helper
    bool isFast = IsCoinFast(coinType.coinObject);


    // --- Create tracked coin entry ---
    TrackedCoin coinData = new TrackedCoin
    {
        coinObject = newCoin,
        coinTypeName = coinType.name,
        initialEccentricity = initialEccentricity,
        currentEccentricity = initialEccentricity,
        position = position,
        eccentricityRangeIndex = rangeIndex,
        minEccentricityReached = initialEccentricity,
        uniqueId = System.Guid.NewGuid().ToString(),
        spawnTime = System.DateTime.Now,
        isLarge = isLarge,
        isFast = isFast,
        pointValue = pointValue,
    };
    coinData.conditionId =
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;


    spawnedCoins.Add(coinData);

    Debug.Log(
        $"Spawned {(isLarge ? "Large" : "Small")} " +
        $"{(isFast ? "Fast" : "Slow")} coin | " +
        $"Value={pointValue} | Range={rangeIndex+1} | Ecc={initialEccentricity:F1}°"
    );

    if (sendToCognitive3D)
        SendToCognitive3D("CoinSpawned", coinData);
}



    void CleanupOldCoins()
    {
        float cleanupDistance = mainCamera.transform.position.z - 10f;
        spawnedCoins.RemoveAll(coin =>
        {
            if (coin.coinObject != null && coin.coinObject.transform.position.z < cleanupDistance)
            {
                SaveCoinData(coin);

                if (coin.hasBeenObserved)
                {
                    Debug.Log($"Coin removed: {coin.coinTypeName}, Range: {coin.eccentricityRangeIndex + 1}, " +
                              $"Initial ecc: {coin.initialEccentricity:F1}°, " +
                              $"Min ecc: {coin.minEccentricityReached:F1}°, " +
                              $"Focus time: {coin.timeSpentInFocus:F2}s");
                }

                if (sendToCognitive3D)
                {
                    SendToCognitive3D("CoinDestroyed", coin);
                }

                Destroy(coin.coinObject);
                return true;
            }
            return coin.coinObject == null;
        });
    }

    void SaveCoinData(TrackedCoin coin, bool isSessionEnding = false)
    {
        if (!saveDataToFile || summaryWriter == null) return;

        try
        {
            if (!destroyedCoins.Contains(coin))
            {
                destroyedCoins.Add(coin);
            }

            System.DateTime destroyTime = System.DateTime.Now;
            summaryWriter.WriteLine(
    $"{coin.uniqueId}," +
    $"{coin.coinTypeName}," +
    $"{coin.isLarge}," +
    $"{coin.isFast}," +
    $"{coin.pointValue}," +
    $"{coin.conditionId}," +
    $"{coin.eccentricityRangeIndex + 1}," +
    $"{coin.initialEccentricity:F1}," +
    $"{coin.minEccentricityReached:F1}," +
    $"{coin.timeSpentInFocus:F2}," +
    $"{coin.hasBeenObserved}," +
    $"{coin.spawnTime}," +
    $"{destroyTime}"
);

            summaryWriter.Flush();

            if (coin.dataPoints.Count > 0)
            {
                string coinDataFolder = Path.Combine(sessionDataPath, "CoinData");

                if (!Directory.Exists(coinDataFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(coinDataFolder);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to create coin data directory: {e.Message}");
                        return;
                    }
                }

                string coinTypePrefix;
                string coinObjectName = coin.coinObject != null ? coin.coinObject.name : "";

                if (coinObjectName.Contains("coinSS"))
                {
                    coinTypePrefix = "small";
                }
                else if (coinObjectName.Contains("coinBS"))
                {
                    coinTypePrefix = "large";
                }
                else if (coin.coinTypeName.ToLower().Contains("small"))
                {
                    coinTypePrefix = "small";
                }
                else if (coin.coinTypeName.ToLower().Contains("large"))
                {
                    coinTypePrefix = "large";
                }
                else
                {
                    coinTypePrefix = "coin";
                }

                string coinDataPath = Path.Combine(coinDataFolder, $"{coinTypePrefix}_coin_{coin.uniqueId}.csv");

                using (StreamWriter coinWriter = new StreamWriter(coinDataPath, false, Encoding.UTF8))
                {
                    coinWriter.WriteLine("Time,Eccentricity,IsBeingObserved,PlayerX,PlayerY,PlayerZ,GazeX,GazeY,GazeZ");

                    foreach (EccentricityDataPoint point in coin.dataPoints)
                    {
                        coinWriter.WriteLine($"{point.time:F3},{point.eccentricity:F3},{point.isBeingObserved}," +
                                          $"{point.playerPosition.x:F3},{point.playerPosition.y:F3},{point.playerPosition.z:F3}," +
                                          $"{point.gazeDirection.x:F3},{point.gazeDirection.y:F3},{point.gazeDirection.z:F3}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving coin data: {e.Message}");
        }
    }

    public void SetSpawningActive(bool active)
    {
        isSpawningActive = active;
    }

    public void MarkCoinAsCollected(GameObject coinObject)
    {
        if (coinObject == null) return;

        foreach (TrackedCoin coin in spawnedCoins)
        {
            if (coin.coinObject == coinObject)
            {
                Debug.Log($"Coin {coin.coinTypeName} (ID: {coin.uniqueId}) was collected!");

                if (sendToCognitive3D)
                {
                    SendToCognitive3D("CoinCollected", coin);
                }

                break;
            }
        }
    }

    public void EndTrial()
    {
        if (!isSpawningActive) return;

        Debug.Log("Trial ended manually.");
        isSpawningActive = false;

        if (saveDataToFile && summaryWriter != null)
        {
            foreach (TrackedCoin coin in spawnedCoins)
            {
                SaveCoinData(coin, true);
            }

            summaryWriter.Flush();
        }
    }

    public void ResetTrialTimer()
    {
        trialStartTime = Time.time;
        isSpawningActive = true;
        Debug.Log("Trial timer reset.");
    }
}
