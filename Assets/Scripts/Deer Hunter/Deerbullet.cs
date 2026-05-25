using UnityEngine;

/// <summary>
/// Projectile fired by Gunner Deer.
/// Travels forward, damages player on hit.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DeerBullet : MonoBehaviour
{
    [Header("Settings")]
    public float speed      = 14f;
    public float lifetime   = 3f;
    public float damage     = 10f;   // set externally by DeerAI.FireGun()

    [Header("FX")]
    public GameObject hitFXPrefab;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity  = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            PlayerHealth ph = col.gameObject.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(damage);
        }

        if (hitFXPrefab)
            Instantiate(hitFXPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}