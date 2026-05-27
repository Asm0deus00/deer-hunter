using UnityEngine;
using System.Collections;

public enum DeerType { Normal, Gunner }

/// <summary>
/// Deer AI. Kinematic movement (no NavMesh needed).
/// Rigidbody is kept for collision detection only (isKinematic = true).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class DeerAI : MonoBehaviour
{
    [Header("Type")]
    public DeerType deerType = DeerType.Normal;

    [Header("Stats")]
    public float maxHealth   = 100f;
    public float moveSpeed   = 5f;
    public float chaseRange  = 50f;
    public float attackRange = 2.2f;
    public int   scoreValue  = 100;
    public int   stylePoints = 10;

    [Header("Tackle (Normal)")]
    public float tackleDamage   = 20f;
    public float tackleCooldown = 2f;

    [Header("Gun (Gunner)")]
    public GameObject deerBulletPrefab;
    public Transform  gunMuzzle;
    public float      gunDamage   = 10f;
    public float      gunFireRate = 1.2f;

    [Header("Toy Locomotion")]
    public float wobbleSpeed  = 8f;
    public float wobbleAmount = 0.08f;
    public float tiltAmount   = 12f;

    [Header("FX")]
    public GameObject deathFX;
    public GameObject hitFX;

    // ── Runtime ──────────────────────────────────────────────────
    private float     hp;
    private bool      isDead      = false;
    private float     attackTimer = 0f;
    private float     shootTimer  = 0f;
    private float     wobblePhase = 0f;
    private bool      isMoving    = false;

    private Transform  player;
    private Rigidbody  rb;
    private Transform  modelRoot;
    private Vector3    modelStartLocalPos;
    private Quaternion modelStartLocalRot;

    private enum State { Idle, Chase, Attack, Dead }
    private State state = State.Idle;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Kinematic: we move via transform, Rigidbody is only for OnCollisionEnter
        rb.isKinematic = true;

        hp = maxHealth;

        modelRoot = transform.Find("Model");
        if (modelRoot == null) modelRoot = transform;
        modelStartLocalPos = modelRoot.localPosition;
        modelStartLocalRot = modelRoot.localRotation;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    void Update()
    {
        if (isDead) return;
        attackTimer -= Time.deltaTime;
        shootTimer  -= Time.deltaTime;
        Tick();
        ToyLoco();
    }

    // ── State machine ─────────────────────────────────────────────
    void Tick()
    {
        if (!player) { state = State.Idle; return; }

        float dist = Vector3.Distance(transform.position, player.position);

        if      (dist > chaseRange)  state = State.Idle;
        else if (dist > attackRange) state = State.Chase;
        else                         state = State.Attack;

        switch (state)
        {
            case State.Idle:
                isMoving = false;
                break;

            case State.Chase:
                isMoving = true;
                Vector3 dir = player.position - transform.position;
                dir.y = 0f;
                dir.Normalize();
                // Move
                transform.position += dir * moveSpeed * Time.deltaTime;
                // Turn — only Y axis
                float targetY = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Vector3 eu = transform.eulerAngles;
                eu.y = Mathf.LerpAngle(eu.y, targetY, Time.deltaTime * 10f);
                transform.eulerAngles = eu;
                break;

            case State.Attack:
                isMoving = false;
                // Always face player while attacking
                Vector3 adir = player.position - transform.position;
                adir.y = 0f;
                if (adir.sqrMagnitude > 0.001f)
                {
                    float ay = Mathf.Atan2(adir.x, adir.z) * Mathf.Rad2Deg;
                    Vector3 aeu = transform.eulerAngles;
                    aeu.y = Mathf.LerpAngle(aeu.y, ay, Time.deltaTime * 10f);
                    transform.eulerAngles = aeu;
                }

                if (deerType == DeerType.Normal)
                {
                    if (attackTimer <= 0f) StartCoroutine(Tackle());
                }
                else
                {
                    if (shootTimer <= 0f) Shoot();
                }
                break;
        }
    }

    // ── Attacks ───────────────────────────────────────────────────
    IEnumerator Tackle()
    {
        attackTimer = tackleCooldown;
        isMoving    = true;

        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;
        dir.Normalize();

        float elapsed = 0f;
        while (elapsed < 0.25f && !isDead)
        {
            elapsed += Time.deltaTime;
            transform.position += dir * 14f * Time.deltaTime;
            yield return null;
        }

        isMoving = false;

        if (player && Vector3.Distance(transform.position, player.position) < attackRange + 1.5f)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(tackleDamage);
        }
    }

    void Shoot()
    {
        if (!deerBulletPrefab || !gunMuzzle) return;
        shootTimer = 1f / gunFireRate;
        Vector3 dir = (player.position + Vector3.up - gunMuzzle.position).normalized;
        var proj = Instantiate(deerBulletPrefab, gunMuzzle.position, Quaternion.LookRotation(dir));
        var db   = proj.GetComponent<DeerBullet>();
        if (db) db.damage = gunDamage;
    }

    // ── Toy Locomotion ────────────────────────────────────────────
    void ToyLoco()
    {
        if (modelRoot == null || modelRoot == transform) return;
        float speed = isMoving ? 1f : 0.15f;
        wobblePhase += Time.deltaTime * wobbleSpeed * speed;
        float s = Mathf.Sin(wobblePhase);
        modelRoot.localPosition = modelStartLocalPos + new Vector3(s * wobbleAmount * (isMoving ? 1 : 0), Mathf.Abs(s) * 0.04f * (isMoving ? 1 : 0), 0);
        modelRoot.localRotation = Quaternion.Euler(
            isMoving ? -10f : 0f,
            0f,
            s * tiltAmount * (isMoving ? 1 : 0));
    }

    // ── Damage / Death ────────────────────────────────────────────
    public void TakeDamage(float amount, Vector3 hitDir = default)
    {
        if (isDead) return;
        hp -= amount;
        if (hitFX) Instantiate(hitFX, transform.position + Vector3.up, Quaternion.identity);

        StyleBar sb = FindFirstObjectByType<StyleBar>();
        if (sb) sb.AddStylePoints(stylePoints);

        if (hp <= 0f) Die();
    }

    void Die()
    {
        isDead = true;
        state  = State.Dead;
        StopAllCoroutines();
        StartCoroutine(DeathTumble());

        ScoreManager sm = FindFirstObjectByType<ScoreManager>();
        if (sm) sm.AddScore(scoreValue);

        StyleBar bar = FindFirstObjectByType<StyleBar>();
        if (bar) bar.OnEnemyKilled();
    }

    IEnumerator DeathTumble()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion deadRot  = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y + Random.Range(-40f, 40f),
            90f);

        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, deadRot, elapsed / 0.4f);
            yield return null;
        }
        if (deathFX) Instantiate(deathFX, transform.position + Vector3.up, Quaternion.identity);
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision col)
    {
        if (isDead || deerType != DeerType.Normal) return;
        if (col.gameObject.CompareTag("Player") && attackTimer <= 0f)
        {
            PlayerHealth ph = col.gameObject.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(tackleDamage * 0.5f);
            attackTimer = tackleCooldown * 0.5f;
        }
    }

    public bool  IsDead        => isDead;
    public float HealthPercent => hp / maxHealth;
}