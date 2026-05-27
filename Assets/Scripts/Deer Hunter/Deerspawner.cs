using UnityEngine;

// v3 - complete rewrite using InvokeRepeating, no coroutines
public class DeerSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject deerPrefab;

    [Header("Spawn Settings")]
    public float spawnRadius   = 20f;
    public float minDistPlayer = 8f;
    public float spawnInterval = 3f;
    public int   maxAlive      = 8;

    private int   alive   = 0;
    private float elapsed = 0f;
    public  float roundDuration = 120f;

    void Start()
    {
        Debug.Log("[DeerSpawner v3] Start called. deerPrefab=" +
            (deerPrefab != null ? deerPrefab.name : "NULL"));

        if (deerPrefab == null)
        {
            Debug.LogError("[DeerSpawner v3] deerPrefab is NULL. Assign it in the Inspector.");
            return;
        }

        InvokeRepeating(nameof(SpawnOne), 2f, spawnInterval);
        Debug.Log("[DeerSpawner v3] InvokeRepeating started.");
    }

    void Update()
    {
        elapsed += Time.deltaTime;
    }

    void SpawnOne()
    {
        Debug.Log("[DeerSpawner v3] SpawnOne called. alive=" + alive + "/" + maxAlive);

        if (alive >= maxAlive) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[DeerSpawner v3] No GameObject with tag 'Player' found.");
            return;
        }

        Vector3 pos = FindSpawnPos(player.transform.position);
        if (pos == Vector3.zero)
        {
            Debug.LogWarning("[DeerSpawner v3] Could not find spawn position.");
            return;
        }

        // Use the prefab's saved X/Z rotation (handles X=-90 prefabs), randomise Y
        Vector3 eu  = deerPrefab.transform.eulerAngles;
        var     rot = Quaternion.Euler(eu.x, Random.Range(0f, 360f), eu.z);

        GameObject obj = Instantiate(deerPrefab, pos, rot);
        alive++;
        Debug.Log("[DeerSpawner v3] Spawned deer at " + pos + ". alive=" + alive);

        // Scale difficulty over time
        float t    = Mathf.Clamp01(elapsed / roundDuration);
        DeerAI ai  = obj.GetComponent<DeerAI>();
        if (ai != null)
        {
            ai.deerType  = Random.value < Mathf.Lerp(0.1f, 0.5f, t) ? DeerType.Gunner : DeerType.Normal;
            ai.maxHealth *= Mathf.Lerp(1f, 1.5f, t);
            ai.moveSpeed *= Mathf.Lerp(1f, 1.3f, t);
        }

        // Decrement counter when deer is destroyed
        var tracker = obj.AddComponent<DeerDeathTracker>();
        tracker.OnDied = () => { alive = Mathf.Max(0, alive - 1); };
    }

    Vector3 FindSpawnPos(Vector3 playerPos)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 circle    = Random.insideUnitCircle.normalized * Random.Range(minDistPlayer, spawnRadius);
            Vector3 candidate = playerPos + new Vector3(circle.x, 50f, circle.y);

            if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 100f))
            {
                Debug.Log("[DeerSpawner v3] Raycast hit: " + hit.collider.name + " at " + hit.point);
                return hit.point + Vector3.up * 0.5f;
            }
        }
        return Vector3.zero;
    }
}

// Tiny helper that fires a callback when its GameObject is destroyed
public class DeerDeathTracker : MonoBehaviour
{
    public System.Action OnDied;
    void OnDestroy() { OnDied?.Invoke(); }
}