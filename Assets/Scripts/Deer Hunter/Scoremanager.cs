using UnityEngine;
using System.Collections;

/// <summary>
/// Tracks total score, kill count, and elapsed time.
/// Integrates with StyleBar for rank multipliers on final score.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Settings")]
    public float roundDuration = 120f;  // seconds; 0 = infinite

    // ─── Runtime ─────────────────────────────────────────────────
    private int   totalScore  = 0;
    private int   killCount   = 0;
    private float elapsedTime = 0f;
    private bool  roundActive = true;

    private StyleBar styleBar;
    private HUDManager hud;

    void Start()
    {
        styleBar = FindFirstObjectByType<StyleBar>();
        hud      = FindFirstObjectByType<HUDManager>();
        if (hud)
        {
            hud.UpdateScore(totalScore);
            hud.UpdateKillCount(killCount);
        }
    }

    void Update()
    {
        if (!roundActive) return;
        elapsedTime += Time.deltaTime;
        if (roundDuration > 0 && elapsedTime >= roundDuration)
            EndRound();
    }

    // ─────────────────────────────────────────────────────────────
    public void AddScore(int basePoints)
    {
        // Multiply by style rank
        float rankMult = 1f + (styleBar ? styleBar.CurrentRank * 0.25f : 0f);
        int earned     = Mathf.RoundToInt(basePoints * rankMult);
        totalScore    += earned;
        killCount++;

        if (hud)
        {
            hud.UpdateScore(totalScore);
            hud.UpdateKillCount(killCount);
            hud.ShowPopupScore(earned);
        }
    }

    void EndRound()
    {
        roundActive = false;
        if (hud) hud.ShowRoundEnd(totalScore, killCount);
    }

    // ─── Accessors ───────────────────────────────────────────────
    public int   Score     => totalScore;
    public int   Kills     => killCount;
    public float TimeLeft  => Mathf.Max(roundDuration - elapsedTime, 0f);
    public float Elapsed   => elapsedTime;
}