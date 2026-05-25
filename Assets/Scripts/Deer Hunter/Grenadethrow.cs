using UnityEngine;

/// <summary>
/// Handles grenade throwing input.
/// Attach to the Player. Assign grenadePrefab in the Inspector.
/// Press G to throw, hold G for longer charge (more force).
/// </summary>
public class GrenadeThrow : MonoBehaviour
{
    [Header("Grenade")]
    public GameObject grenadePrefab;
    public int        maxGrenades      = 3;
    public float      throwForce       = 18f;
    public float      throwUpAngle     = 15f;  // degrees above camera look

    [Header("Cooldown")]
    public float throwCooldown         = 1.5f;

    [Header("Spawn Offset")]
    public Vector3 spawnOffset         = new Vector3(0.4f, -0.1f, 0.6f);

    // ─── Runtime ─────────────────────────────────────────────────
    private int   grenadeCount;
    private float cooldownTimer = 0f;
    private Transform mainCamera;

    void Start()
    {
        grenadeCount = maxGrenades;
        mainCamera   = Camera.main?.transform;

        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.UpdateGrenades(grenadeCount, maxGrenades);
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.G))
            TryThrow();
    }

    void TryThrow()
    {
        if (grenadePrefab == null) { Debug.LogWarning("GrenadeThrow: no grenade prefab assigned."); return; }
        if (grenadeCount <= 0)    { Debug.Log("Out of grenades!"); return; }
        if (cooldownTimer > 0f)   return;

        grenadeCount--;
        cooldownTimer = throwCooldown;

        // Spawn position = camera position offset into screen-space
        Vector3 spawnPos = mainCamera.TransformPoint(spawnOffset);

        // Direction: slightly above look direction
        Vector3 throwDir = Quaternion.AngleAxis(-throwUpAngle, mainCamera.right) * mainCamera.forward;

        GameObject gren = Instantiate(grenadePrefab, spawnPos, Random.rotation);
        Rigidbody grenRb = gren.GetComponent<Rigidbody>();
        if (grenRb) grenRb.AddForce(throwDir * throwForce, ForceMode.Impulse);

        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.UpdateGrenades(grenadeCount, maxGrenades);
    }

    /// <summary>Called by pickup items to refill grenades.</summary>
    public void RefillGrenades(int amount)
    {
        grenadeCount = Mathf.Min(grenadeCount + amount, maxGrenades);
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.UpdateGrenades(grenadeCount, maxGrenades);
    }
}