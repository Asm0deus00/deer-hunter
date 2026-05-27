using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns deer in waves throughout the round.
/// Escalates difficulty over time: more deer, more Gunners.
///
/// Setup
/// -----
/// 1. Create an empty GameObject "DeerSpawner" in your scene.
/// 2. Attach this script.
/// 3. Assign deerPrefab (a Deer prefab with DeerAI, Rigidbody, collider, "Model" child).
/// 4. Add SpawnPoint transforms around the map in the spawnPoints list, or leave empty
///    to auto-generate random points (see autoSpawnRadius).
/// 5. The script reads ScoreManager.TimeLeft to know when the round is over.
/// </summary>
public class DeerSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Deer prefab (DeerAI). The script will override DeerType per spawn.")]
    public GameObject deerPrefab;

    [Header("Spawn Points")]
    [Tooltip("Leave empty to use random points around autoSpawnCenter within autoSpawnRadius.")]
    public List<Transform> spawnPoints = new List<Transform>();
    public Transform        autoSpawnCenter;   // defaults to this transform if null
    public float            autoSpawnRadius  = 25f;
    public float            minDistToPlayer  = 10f;

    [Header("Wave Settings")]
    [Tooltip("Time between spawn attempts (decreases with time).")]
    public float baseSpawnInterval   = 6f;
    public float minSpawnInterval    = 2.0f;

    [Tooltip("Max deer alive at once (scales up over the round).")]
    public int   baseMaxAlive        = 4;
    public int   maxMaxAlive         = 14;

    [Tooltip("How many deer spawn per wave (increases over time).")]
    public int   basePackSize        = 1;
    public int   maxPackSize         = 4;

    [Tooltip("Probability (0-1) that a spawned deer is a Gunner. Scales to gunnerMaxChance.")]
    [Range(0f, 1f)] public float gunnerStartChance = 0.15f;
    [Range(0f, 1f)] public float gunnerMaxChance   = 0.60f;

    [Header("Round")]
    [Tooltip("Must match ScoreManager.roundDuration (120 s default).")]
    public float roundDuration = 120f;

    // ─── Runtime ─────────────────────────────────────────────────
    private List<DeerAI>   aliveDeer   = new List<DeerAI>();
    private Transform      playerTf;
    private ScoreManager   scoreManager;
    private float          elapsed     = 0f;
    private bool           roundActive = true;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTf = p.transform;

        scoreManager = FindFirstObjectByType<ScoreManager>();

        // Log either way so we can always see this in the Console
        Debug.Log("[DeerSpawner] Start() — deerPrefab=" + (deerPrefab != null ? deerPrefab.name : "NULL")
            + "  playerTf=" + (playerTf != null ? playerTf.name : "NULL")
            + "  scoreManager=" + (scoreManager != null ? "found" : "NULL"));

        if (!deerPrefab)
        {
            Debug.LogError("[DeerSpawner] deerPrefab is NULL — drag your Deer prefab into the DeerSpawner component in the Inspector!");
            return;
        }

        StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        if (!roundActive) return;

        elapsed += Time.deltaTime;

        // Round over when ScoreManager says so (TimeLeft reaches 0)
        if (scoreManager && scoreManager.TimeLeft <= 0f)
            roundActive = false;

        // Clean nulls from dead deer
        aliveDeer.RemoveAll(d => d == null || d.IsDead);
    }

    // ─── Spawn loop ──────────────────────────────────────────────
    IEnumerator SpawnLoop()
    {
        // Short initial delay so the scene finishes loading
        yield return new WaitForSeconds(2f);
        Debug.Log("[DeerSpawner] SpawnLoop started. deerPrefab=" + deerPrefab + " playerTf=" + playerTf);

        while (roundActive)
        {
            float t       = Mathf.Clamp01(elapsed / roundDuration);
            int   maxAlive = Mathf.RoundToInt(Mathf.Lerp(baseMaxAlive, maxMaxAlive, t));
            int   pack     = Mathf.RoundToInt(Mathf.Lerp(basePackSize, maxPackSize, t));
            float interval = Mathf.Lerp(baseSpawnInterval, minSpawnInterval, t);

            int toSpawn = Mathf.Min(pack, maxAlive - aliveDeer.Count);
            for (int i = 0; i < toSpawn; i++)
                SpawnOne(t);

            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnOne(float progressT)
    {
        Vector3 spawnPos = GetSpawnPosition();
        Debug.Log("[DeerSpawner] spawnPos=" + spawnPos);
        if (spawnPos == Vector3.zero) return;

        // Spawn using whatever rotation the prefab was saved with (X=-90 if model is flat)
        // Y is randomised so each deer faces a different direction.
        Quaternion spawnRot = deerPrefab.transform.rotation;
        spawnRot = Quaternion.Euler(spawnRot.eulerAngles.x, Random.Range(0f, 360f), spawnRot.eulerAngles.z);
        GameObject obj = Instantiate(deerPrefab, spawnPos, spawnRot);
        DeerAI deer = obj.GetComponent<DeerAI>();
        if (!deer) { Destroy(obj); return; }

        // Decide type
        float gunnerChance = Mathf.Lerp(gunnerStartChance, gunnerMaxChance, progressT);
        deer.deerType = (Random.value < gunnerChance) ? DeerType.Gunner : DeerType.Normal;

        // Scale stats slightly with progression
        deer.maxHealth  *= Mathf.Lerp(1f, 1.5f, progressT);
        deer.moveSpeed  *= Mathf.Lerp(1f, 1.3f, progressT);

        aliveDeer.Add(deer);
    }

    Vector3 GetSpawnPosition()
    {
        // Try manual spawn points first
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            // Filter for points far enough from the player
            List<Transform> valid = new List<Transform>();
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                if (playerTf == null || Vector3.Distance(sp.position, playerTf.position) >= minDistToPlayer)
                    valid.Add(sp);
            }
            if (valid.Count > 0)
                return valid[Random.Range(0, valid.Count)].position;
        }

        // Fall back to random position around center
        Transform center = autoSpawnCenter ?? transform;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 rand2D    = Random.insideUnitCircle.normalized * autoSpawnRadius;
            // Start candidate at center height so the raycast origin is always above ground
            Vector3 candidate = new Vector3(
                center.position.x + rand2D.x,
                center.position.y,
                center.position.z + rand2D.y);

            if (playerTf && Vector3.Distance(candidate, playerTf.position) < minDistToPlayer)
                continue;

            // Don't spawn on top of existing deer
            bool tooClose = false;
            foreach (var d in aliveDeer)
            {
                if (d != null && Vector3.Distance(candidate, d.transform.position) < 3f)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Cast from well above the candidate down a long distance to hit any floor
            Vector3 rayOrigin = candidate + Vector3.up * 100f;
            Debug.Log("[DeerSpawner] Raycast from " + rayOrigin + " down 200");
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f))
            {
                Debug.Log("[DeerSpawner] Hit " + hit.collider.name + " at " + hit.point);
                return hit.point + Vector3.up * 1.2f;
            }
        }

        Debug.LogWarning("[DeerSpawner] Could not find a valid spawn position. " +
            "Make sure the DeerSpawner GameObject is placed at the same height as your map, " +
            "the map colliders are active, and autoSpawnRadius covers the play area.");

        return Vector3.zero; // failed to find a spot
    }

    void OnDrawGizmosSelected()
    {
        Transform center = autoSpawnCenter ?? transform;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
        Gizmos.DrawWireSphere(center.position, autoSpawnRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(center.position, minDistToPlayer);
    }
}