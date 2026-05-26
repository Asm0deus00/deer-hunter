using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to the Main Camera.
/// Called by PlayerHealth.TakeDamage() via Camera.main.GetComponent<CameraShake>().Shake().
/// </summary>
public class CameraShake : MonoBehaviour
{
    // Active shake parameters
    private float shakeDuration  = 0f;
    private float shakeMagnitude = 0.2f;

    private Vector3 originalLocalPos;

    void Awake()
    {
        originalLocalPos = transform.localPosition;
    }

    void Update()
    {
        if (shakeDuration > 0f)
        {
            transform.localPosition = originalLocalPos +
                Random.insideUnitSphere * shakeMagnitude;
            shakeDuration -= Time.deltaTime;
        }
        else
        {
            shakeDuration = 0f;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, originalLocalPos, Time.deltaTime * 12f);
        }
    }

    /// <summary>
    /// Trigger a camera shake.
    /// </summary>
    /// <param name="magnitude">World-unit radius of jitter (e.g. 0.15–0.4).</param>
    /// <param name="duration">How many seconds to shake.</param>
    public void Shake(float magnitude, float duration)
    {
        originalLocalPos = transform.localPosition;   // re-anchor each time
        shakeMagnitude   = magnitude;
        shakeDuration    = duration;
    }
}