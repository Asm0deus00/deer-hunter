using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Style Bar — Devil May Cry / Ultrakill-inspired combo rank system.
///
/// Ranks:  D → C → B → A → S → SS → SSS
/// Points accumulate on hits/kills, drain over time when idle.
///
/// Wire up the UI elements in the Inspector.
/// </summary>
public class StyleBar : MonoBehaviour
{
    // ─── Ranks ───────────────────────────────────────────────────
    private static readonly string[] RankLabels     = { "D", "C", "B", "A", "S", "SS", "SSS" };
    private static readonly Color[]  RankColors     = {
        new Color(0.55f, 0.55f, 0.55f),  // D - grey
        new Color(0.25f, 0.55f, 1.00f),  // C - blue
        new Color(0.10f, 0.85f, 0.35f),  // B - green
        new Color(1.00f, 0.80f, 0.10f),  // A - yellow
        new Color(1.00f, 0.40f, 0.05f),  // S - orange
        new Color(1.00f, 0.10f, 0.10f),  // SS - red
        new Color(0.95f, 0.10f, 0.90f),  // SSS - magenta
    };

    // Minimum points required to reach each rank (index = rank index)
    private static readonly float[] RankThresholds = { 0, 100, 250, 500, 900, 1400, 2000 };

    // ─── Inspector ───────────────────────────────────────────────
    [Header("UI References")]
    public TMP_Text   rankLabel;         // Large letter label, e.g. "SSS"
    public Slider     styleFillBar;      // Fills between current rank thresholds
    public TMP_Text   styleWordLabel;    // "STYLISH", "SMOKING SEXY STYLE", etc.
    public Image      rankBackgroundImg; // Tinted per rank
    public Image      styleFillImage;    // Fill image of the slider bar — tinted per rank

    [Header("Tuning")]
    public float drainRate        = 40f;   // points drained per second when idle
    public float drainDelay       = 2.5f;  // seconds after last action before drain begins
    public float maxPoints        = 2500f;

    [Header("Multiplier")]
    public float comboMultiplier      = 1f;
    public float multiplierIncrement  = 0.1f;   // per hit
    public float multiplierDecayDelay = 1.5f;
    public float multiplierMax        = 4f;

    // ─── Runtime ─────────────────────────────────────────────────
    private float currentPoints  = 0f;
    private int   currentRank    = 0;
    private float drainTimer     = 0f;
    private float multiplierTimer= 0f;
    private int   killStreak     = 0;

    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        drainTimer     -= Time.deltaTime;
        multiplierTimer -= Time.deltaTime;

        // Drain style
        if (drainTimer <= 0f && currentPoints > 0f)
        {
            currentPoints -= drainRate * Time.deltaTime;
            currentPoints  = Mathf.Max(currentPoints, 0f);
            UpdateUI();
        }

        // Decay combo multiplier
        if (multiplierTimer <= 0f && comboMultiplier > 1f)
        {
            comboMultiplier = Mathf.MoveTowards(comboMultiplier, 1f, Time.deltaTime * 0.5f);
        }
    }

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>Add style points (called by DeerAI when hit).</summary>
    public void AddStylePoints(float basePoints)
    {
        drainTimer = drainDelay;

        // Multiplier ramp
        comboMultiplier   = Mathf.Min(comboMultiplier + multiplierIncrement, multiplierMax);
        multiplierTimer   = multiplierDecayDelay;

        float earned = basePoints * comboMultiplier;
        currentPoints = Mathf.Min(currentPoints + earned, maxPoints);

        UpdateUI();
        CheckRankUp();
    }

    /// <summary>Call when an enemy is killed for big bonus.</summary>
    public void OnEnemyKilled()
    {
        killStreak++;
        float bonus = 150f * killStreak * comboMultiplier;
        AddStylePoints(bonus);
        StartCoroutine(KillAnnounce(killStreak));
    }

    // ─── Private ─────────────────────────────────────────────────
    void CheckRankUp()
    {
        int newRank = 0;
        for (int i = RankThresholds.Length - 1; i >= 0; i--)
        {
            if (currentPoints >= RankThresholds[i]) { newRank = i; break; }
        }

        if (newRank != currentRank)
        {
            currentRank = newRank;
            StartCoroutine(RankBump());
        }
    }

    void UpdateUI()
    {
        if (rankLabel)
        {
            rankLabel.text  = RankLabels[currentRank];
            rankLabel.color = RankColors[currentRank];
        }

        if (rankBackgroundImg)
        {
            Color c = RankColors[currentRank];
            c.a = 0.18f;
            rankBackgroundImg.color = c;
        }

        if (styleFillImage)
            styleFillImage.color = RankColors[currentRank];

        if (styleFillBar)
        {
            float lo = RankThresholds[currentRank];
            float hi = currentRank < RankThresholds.Length - 1
                       ? RankThresholds[currentRank + 1]
                       : maxPoints;
            styleFillBar.value = Mathf.InverseLerp(lo, hi, currentPoints);
        }

        if (styleWordLabel)
            styleWordLabel.text = GetStyleWord();
    }

    string GetStyleWord()
    {
        switch (currentRank)
        {
            case 0: return "DULL";
            case 1: return "COOL";
            case 2: return "BRUTAL";
            case 3: return "ANARCHIC";
            case 4: return "STYLISH!";
            case 5: return "SMOKIN' SEXY STYLE!!";
            case 6: return "☆ BUCK WILD ☆";
            default: return "";
        }
    }

    IEnumerator RankBump()
    {
        if (rankLabel == null) yield break;
        // Scale punch
        Vector3 origin = rankLabel.transform.localScale;
        Vector3 big    = origin * 1.5f;
        float   t      = 0f;
        float   dur    = 0.12f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rankLabel.transform.localScale = Vector3.Lerp(origin, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rankLabel.transform.localScale = Vector3.Lerp(big, origin, t / dur);
            yield return null;
        }
        rankLabel.transform.localScale = origin;
    }

    IEnumerator KillAnnounce(int streak)
    {
        if (styleWordLabel == null) yield break;
        if (streak >= 3)
        {
            string msg = streak >= 6 ? "BUCK RAMPAGE!!!" :
                         streak >= 4 ? "HERD SLAUGHTER!" : "MULTI-KILL!";
            string prev = styleWordLabel.text;
            styleWordLabel.text  = msg;
            styleWordLabel.color = Color.white;
            yield return new WaitForSeconds(1.2f);
            styleWordLabel.text  = GetStyleWord();
            styleWordLabel.color = Color.white;
        }
        // Reset streak after short pause
        yield return new WaitForSeconds(drainDelay + 0.5f);
        if (currentPoints < RankThresholds[1]) killStreak = 0;
    }

    // ─── Score passthrough ───────────────────────────────────────
    public int CurrentRank  => currentRank;
    public float Points     => currentPoints;
}