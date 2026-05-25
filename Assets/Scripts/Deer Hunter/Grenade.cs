using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Grenade — AOE explosive. Press G to throw.
/// Bounces physically (toy vibe), then explodes after fuseTime.
/// Attach this script to the Grenade PREFAB.
/// GrenadeThrow.cs handles input and instantiation.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Grenade : MonoBehaviour
{
    [Header("Explosion")]
    public float blastRadius   = 6f;
    public float blastDamage   = 80f;
    public float fuseTime      = 2.5f;
    public float upwardForce   = 4f;     // adds upward component to knockback

    [Header("Toy Spin")]
    public float spinSpeed     = 720f;   // degrees per second while in flight

    [Header("FX")]
    public GameObject explosionVFX;

    [Header("Sound")]
    public AudioClip bounceClip;
    public AudioClip explodeClip;

    private Rigidbody  rb;
    private AudioSource audioSrc;
    private bool       hasExploded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 1f;
    }

    void Start()
    {
        StartCoroutine(Fuse());
    }

    void Update()
    {
        // Toy-style constant spin while airborne
        transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);
    }

    void OnCollisionEnter(Collision col)
    {
        if (bounceClip && !hasExploded)
            audioSrc.PlayOneShot(bounceClip, 0.5f);
    }

    IEnumerator Fuse()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // VFX
        if (explosionVFX)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        // SFX
        if (explodeClip)
            AudioSource.PlayClipAtPoint(explodeClip, transform.position, 1f);

        // Blast all deers in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
        HashSet<DeerAI> damaged = new HashSet<DeerAI>();

        foreach (Collider hit in hits)
        {
            // Damage player (self-damage)
            if (hit.CompareTag("Player"))
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                if (ph)
                {
                    float falloff = 1f - (Vector3.Distance(transform.position, hit.transform.position) / blastRadius);
                    ph.TakeDamage(blastDamage * falloff * 0.3f); // reduced self-damage
                }
            }

            // Damage deers
            DeerAI deer = hit.GetComponent<DeerAI>();
            if (deer == null) deer = hit.GetComponentInParent<DeerAI>();
            if (deer != null && !damaged.Contains(deer))
            {
                damaged.Add(deer);
                float dist    = Vector3.Distance(transform.position, deer.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / blastRadius);
                Vector3 dir   = (deer.transform.position - transform.position).normalized;
                dir.y        += upwardForce;
                deer.TakeDamage(blastDamage * falloff, dir);
            }
        }

        // Big style bonus for multi-kill
        if (damaged.Count >= 2)
        {
            StyleBar bar = FindFirstObjectByType<StyleBar>();
            if (bar) bar.AddStylePoints(damaged.Count * 25);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.3f, 0, 0.35f);
        Gizmos.DrawSphere(transform.position, blastRadius);
    }
}