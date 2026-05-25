using UnityEngine;
using System.Collections;

public enum DeerType
{
    Normal,   // Tackles the player
    Gunner    // Shoots at the player
}

/// <summary>
/// Deer AI — toy-like locomotion (whole-model wobble, no skeleton animations).
/// Handles: chasing, tackle attack, gun attack, health, and death.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DeerAI : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────
    [Header("Deer Type")]
    public DeerType deerType = DeerType.Normal;

    [Header("Stats")]
    public float maxHealth        = 100f;
    public float moveSpeed        = 4f;
    public float chaseRange       = 20f;
    public float attackRange      = 2.5f;
    public int   scoreValue       = 100;       // base score for killing this deer
    public int   stylePoints      = 10;        // style-bar contribution per hit received

    [Header("Tackle Attack (Normal Deer)")]
    public float tackleDamage     = 20f;
    public float tackleCooldown   = 2f;
    public float tackleForce      = 600f;

    [Header("Gun Attack (Gunner Deer)")]
    public GameObject deerBulletPrefab;
    public Transform  gunMuzzle;
    public float      gunDamage       = 10f;
    public float      gunFireRate     = 1.5f;   // shots per second
    public float      gunRange        = 18f;

    [Header("Toy Locomotion")]
    public float wobbleSpeed      = 8f;         // body bob Hz
    public float wobbleAmount     = 0.08f;
    public float tiltAmount       = 12f;        // side tilt when strafing
    public float modelBounce      = 0.04f;      // vertical bounce amplitude

    [Header("Knockback")]
    public float hitKnockback     = 3f;

    [Header("FX Prefabs")]
    public GameObject deathFX;                  // particle burst on death
    public GameObject hitFX;                    // small spark/flash on hit

    // ─── Runtime ─────────────────────────────────────────────────
    private float      currentHealth;
    private Transform  player;
    private Rigidbody  rb;
    private bool       isDead          = false;
    private bool       isAttacking     = false;
    private float      attackTimer     = 0f;
    private float      shootTimer      = 0f;

    // Toy locomotion
    private Vector3    modelStartLocalPos;
    private Quaternion modelStartLocalRot;
    private Transform  modelRoot;           // child "model" transform to wobble
    private float      wobblePhase;

    // ─── State ───────────────────────────────────────────────────
    private enum State { Idle, Chase, Attack, Dead }
    private State state = State.Idle;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        currentHealth = maxHealth;

        // Expect a child named "Model" to wobble (the entire visible mesh)
        modelRoot = transform.Find("Model");
        if (modelRoot == null) modelRoot = transform; // fallback

        modelStartLocalPos = modelRoot.localPosition;
        modelStartLocalRot = modelRoot.localRotation;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
    }

    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (isDead) return;

        attackTimer -= Time.deltaTime;
        shootTimer  -= Time.deltaTime;

        UpdateState();
        ExecuteState();
        ToyLoco();
    }

    // ── State machine ────────────────────────────────────────────
    void UpdateState()
    {
        if (!player) return;
        float dist = Vector3.Distance(transform.position, player.position);

        if      (dist > chaseRange)  state = State.Idle;
        else if (dist > attackRange) state = State.Chase;
        else                         state = State.Attack;
    }

    void ExecuteState()
    {
        switch (state)
        {
            case State.Idle:   IdleBehavior();   break;
            case State.Chase:  ChaseBehavior();  break;
            case State.Attack: AttackBehavior(); break;
        }
    }

    void IdleBehavior()
    {
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    void ChaseBehavior()
    {
        if (!player) return;
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        rb.linearVelocity = new Vector3(dir.x * moveSpeed, rb.linearVelocity.y, dir.z * moveSpeed);
        // Face player
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    void AttackBehavior()
    {
        // Stop moving while attacking
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        if (deerType == DeerType.Normal)
        {
            if (attackTimer <= 0f)
                StartCoroutine(TackleAttack());
        }
        else // Gunner
        {
            // Face player
            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), Time.deltaTime * 6f);

            if (shootTimer <= 0f)
                FireGun();
        }
    }

    // ── Attacks ──────────────────────────────────────────────────
    IEnumerator TackleAttack()
    {
        isAttacking = true;
        attackTimer = tackleCooldown;

        // Wind-up: exaggerated tilt backward (toy spring effect)
        float elapsed = 0f;
        float windupTime = 0.25f;
        while (elapsed < windupTime)
        {
            elapsed += Time.deltaTime;
            if (modelRoot != null)
            {
                float t = elapsed / windupTime;
                modelRoot.localRotation = Quaternion.Euler(-30f * t, 0, 0);
            }
            yield return null;
        }

        // Lunge forward
        if (player)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            rb.AddForce(dir * tackleForce, ForceMode.Impulse);
        }

        // Reset model rotation
        yield return new WaitForSeconds(0.1f);
        if (modelRoot != null)
            modelRoot.localRotation = modelStartLocalRot;

        // Deal damage if player still in range
        if (player)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < attackRange + 1f)
            {
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph) ph.TakeDamage(tackleDamage);
            }
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    void FireGun()
    {
        if (!deerBulletPrefab || !gunMuzzle) return;
        shootTimer = 1f / gunFireRate;

        // Wobble/recoil on model
        StartCoroutine(GunRecoilAnim());

        Vector3 dir = (player.position + Vector3.up * 1f - gunMuzzle.position).normalized;
        GameObject proj = Instantiate(deerBulletPrefab, gunMuzzle.position, Quaternion.LookRotation(dir));
        DeerBullet db = proj.GetComponent<DeerBullet>();
        if (db) db.damage = gunDamage;
    }

    IEnumerator GunRecoilAnim()
    {
        if (modelRoot == null) yield break;
        modelRoot.localPosition += Vector3.back * 0.15f;
        yield return new WaitForSeconds(0.06f);
        modelRoot.localPosition = modelStartLocalPos;
    }

    // ── Toy Locomotion ───────────────────────────────────────────
    void ToyLoco()
    {
        if (modelRoot == null) return;

        wobblePhase += Time.deltaTime * wobbleSpeed *
                       (rb.linearVelocity.magnitude > 0.5f ? 1f : 0.2f);

        float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / moveSpeed);

        // Vertical bounce
        float bounce = Mathf.Sin(wobblePhase * 2f) * modelBounce * speedRatio;

        // Side-to-side rock
        float rock = Mathf.Sin(wobblePhase) * wobbleAmount * speedRatio;

        // Forward tilt based on speed
        float forwardTilt = -speedRatio * 15f;

        modelRoot.localPosition = modelStartLocalPos + new Vector3(rock, Mathf.Abs(bounce), 0);
        modelRoot.localRotation = Quaternion.Euler(
            forwardTilt,
            0,
            Mathf.Sin(wobblePhase) * tiltAmount * speedRatio);
    }

    // ── Damage & Death ───────────────────────────────────────────
    public void TakeDamage(float amount, Vector3 hitDirection = default)
    {
        if (isDead) return;

        currentHealth -= amount;

        // Knockback
        if (hitDirection != Vector3.zero)
            rb.AddForce(hitDirection.normalized * hitKnockback, ForceMode.Impulse);

        // Hit flash
        if (hitFX)
            Instantiate(hitFX, transform.position + Vector3.up, Quaternion.identity);

        // Notify style system
        StyleBar styleBar = FindFirstObjectByType<StyleBar>();
        if (styleBar) styleBar.AddStylePoints(stylePoints);

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        isDead = true;
        state  = State.Dead;
        rb.linearVelocity = Vector3.zero;

        // Death: flip the whole model upside-down (toy topple)
        StartCoroutine(DeathTumble());

        // Score
        ScoreManager sm = FindFirstObjectByType<ScoreManager>();
        if (sm) sm.AddScore(scoreValue);

        // Style
        StyleBar bar = FindFirstObjectByType<StyleBar>();
        if (bar) bar.OnEnemyKilled();
    }

    IEnumerator DeathTumble()
    {
        float elapsed = 0f;
        float duration = 0.5f;
        Quaternion startRot = transform.rotation;
        Quaternion deadRot  = startRot * Quaternion.Euler(90f, Random.Range(-40f, 40f), 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, deadRot, elapsed / duration);
            yield return null;
        }

        if (deathFX)
            Instantiate(deathFX, transform.position + Vector3.up, Quaternion.identity);

        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    // Collision with player for tackle damage (fallback)
    void OnCollisionEnter(Collision col)
    {
        if (isDead) return;
        if (col.gameObject.CompareTag("Player") && deerType == DeerType.Normal)
        {
            PlayerHealth ph = col.gameObject.GetComponent<PlayerHealth>();
            if (ph && attackTimer <= 0f)
            {
                ph.TakeDamage(tackleDamage * 0.5f);
                attackTimer = tackleCooldown * 0.5f;
            }
        }
    }

    // ── Public helpers ───────────────────────────────────────────
    public float HealthPercent => currentHealth / maxHealth;
    public bool  IsDead        => isDead;
}