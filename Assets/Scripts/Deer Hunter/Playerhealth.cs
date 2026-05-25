using UnityEngine;
using System.Collections;

/// <summary>
/// Manages player HP, hit feedback, and death.
/// Attach to the Player root GameObject.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth        = 100f;
    public float healthRegenDelay = 5f;   // seconds after last hit before regen starts
    public float healthRegenRate  = 5f;   // HP per second during regen

    [Header("Screen Hit FX")]
    [Tooltip("UI Image used as a red vignette flash. Assign in Inspector.")]
    public UnityEngine.UI.Image hitVignetteImage;
    public float vignetteFlashSpeed = 8f;

    // ─── Runtime ─────────────────────────────────────────────────
    private float currentHealth;
    private float regenTimer;
    private bool  isDead = false;
    private float vignetteAlpha = 0f;

    public float HealthPercent => currentHealth / maxHealth;
    public bool  IsDead        => isDead;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead) return;

        // Regen
        regenTimer -= Time.deltaTime;
        if (regenTimer <= 0f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(currentHealth + healthRegenRate * Time.deltaTime, maxHealth);
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud) hud.UpdateHealth(HealthPercent);
        }

        // Vignette fade out
        if (vignetteAlpha > 0f)
        {
            vignetteAlpha -= Time.deltaTime * vignetteFlashSpeed;
            vignetteAlpha  = Mathf.Max(vignetteAlpha, 0f);
            ApplyVignette();
        }
    }

    // ─────────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        regenTimer     = healthRegenDelay;

        // Screen flash
        vignetteAlpha = 1f;
        ApplyVignette();

        // HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.UpdateHealth(HealthPercent);

        // Camera shake
        CameraShake shake = Camera.main?.GetComponent<CameraShake>();
        if (shake) shake.Shake(0.2f, 0.25f);

        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.UpdateHealth(HealthPercent);
    }

    void Die()
    {
        isDead = true;
        // Notify HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud) hud.ShowDeathScreen();

        // Disable input
        PlayerMovementScript pms = GetComponent<PlayerMovementScript>();
        if (pms) pms.enabled = false;
        GunInventory gi = GetComponent<GunInventory>();
        if (gi) gi.DeadMethod();
    }

    void ApplyVignette()
    {
        if (hitVignetteImage == null) return;
        Color c = hitVignetteImage.color;
        c.a = vignetteAlpha * 0.6f;
        hitVignetteImage.color = c;
    }
}