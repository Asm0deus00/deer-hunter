using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Style Bar — DMC-inspired rank system.
/// All UI is created in code. No manual setup needed.
/// HUDManager injects its built elements; if they're missing this script builds its own.
/// Ranks: D → C → B → A → S → SS → SSS
/// </summary>
public class StyleBar : MonoBehaviour
{
    // ── Rank data ────────────────────────────────────────────────
    static readonly string[] RankLabels     = { "D",    "C",    "B",      "A",        "S",       "SS",                "SSS"        };
    static readonly string[] StyleWords     = { "DULL", "COOL", "BRUTAL", "ANARCHIC", "STYLISH!", "SMOKIN' SEXY STYLE!!", "** BUCK WILD **" };
    static readonly Color[]  RankColors     = {
        new Color(0.55f, 0.55f, 0.55f),  // D  grey
        new Color(0.25f, 0.55f, 1.00f),  // C  blue
        new Color(0.10f, 0.85f, 0.35f),  // B  green
        new Color(1.00f, 0.80f, 0.10f),  // A  yellow
        new Color(1.00f, 0.40f, 0.05f),  // S  orange
        new Color(1.00f, 0.10f, 0.10f),  // SS red
        new Color(0.95f, 0.10f, 0.90f),  // SSS magenta
    };
    static readonly float[] RankThresholds = { 0, 100, 250, 500, 900, 1400, 2000 };

    // ── UI refs (injected by HUDManager or self-built) ───────────
    [HideInInspector] public TextMeshProUGUI rankLabel;
    [HideInInspector] public TextMeshProUGUI styleWordLabel;
    [HideInInspector] public Slider          styleFillBar;
    [HideInInspector] public Image           rankBackgroundImg;
    [HideInInspector] public Image           styleFillImage;

    // ── Tuning ───────────────────────────────────────────────────
    [Header("Tuning")]
    public float drainRate        = 40f;
    public float drainDelay       = 2.5f;
    public float maxPoints        = 2500f;
    public float multiplierMax    = 4f;

    // ── Runtime ──────────────────────────────────────────────────
    private float currentPoints   = 0f;
    private int   currentRank     = 0;
    private float drainTimer      = 0f;
    private float comboMultiplier = 1f;
    private float multiplierTimer = 0f;
    private int   killStreak      = 0;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        // If HUDManager hasn't injected UI yet, build our own
        if (rankLabel == null) BuildOwnUI();
        UpdateUI();
    }

    void Update()
    {
        drainTimer      -= Time.deltaTime;
        multiplierTimer -= Time.deltaTime;

        if (drainTimer <= 0f && currentPoints > 0f)
        {
            currentPoints = Mathf.Max(currentPoints - drainRate * Time.deltaTime, 0f);
            UpdateUI();
        }
        if (multiplierTimer <= 0f && comboMultiplier > 1f)
            comboMultiplier = Mathf.MoveTowards(comboMultiplier, 1f, Time.deltaTime * 0.5f);
    }

    // ── Public API ───────────────────────────────────────────────

    public void AddStylePoints(float basePoints)
    {
        drainTimer      = drainDelay;
        comboMultiplier = Mathf.Min(comboMultiplier + 0.1f, multiplierMax);
        multiplierTimer = 1.5f;
        currentPoints   = Mathf.Min(currentPoints + basePoints * comboMultiplier, maxPoints);
        UpdateUI();
        CheckRankUp();
    }

    public void OnEnemyKilled()
    {
        killStreak++;
        AddStylePoints(150f * killStreak * comboMultiplier);
        StartCoroutine(KillAnnounce(killStreak));
    }

    public int   CurrentRank => currentRank;
    public float Points      => currentPoints;

    // ── UI update ────────────────────────────────────────────────

    void UpdateUI()
    {
        Color col = RankColors[currentRank];

        if (rankLabel != null)
        {
            rankLabel.text  = RankLabels[currentRank];
            rankLabel.color = col;
        }

        if (styleWordLabel != null)
            styleWordLabel.text = StyleWords[currentRank];

        if (rankBackgroundImg != null)
            rankBackgroundImg.color = new Color(col.r, col.g, col.b, 0.18f);

        // Fill bar: value = progress between current and next threshold
        if (styleFillBar != null)
        {
            float lo = RankThresholds[currentRank];
            float hi = currentRank < RankThresholds.Length - 1
                       ? RankThresholds[currentRank + 1] : maxPoints;
            styleFillBar.value = Mathf.InverseLerp(lo, hi, currentPoints);
        }

        // Fill image color matches rank color
        if (styleFillImage != null)
            styleFillImage.color = col;
    }

    void CheckRankUp()
    {
        int newRank = 0;
        for (int i = RankThresholds.Length - 1; i >= 0; i--)
            if (currentPoints >= RankThresholds[i]) { newRank = i; break; }

        if (newRank != currentRank)
        {
            currentRank = newRank;
            StartCoroutine(RankBump());
        }
    }

    IEnumerator RankBump()
    {
        if (rankLabel == null) yield break;
        Vector3 origin = rankLabel.transform.localScale;
        Vector3 big    = origin * 1.6f;
        float   dur    = 0.1f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            rankLabel.transform.localScale = Vector3.Lerp(origin, big, t / dur);
            yield return null;
        }
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            rankLabel.transform.localScale = Vector3.Lerp(big, origin, t / dur);
            yield return null;
        }
        rankLabel.transform.localScale = origin;
    }

    IEnumerator KillAnnounce(int streak)
    {
        if (styleWordLabel == null || streak < 3) yield break;
        string msg  = streak >= 6 ? "BUCK RAMPAGE!!!" : streak >= 4 ? "HERD SLAUGHTER!" : "MULTI-KILL!";
        string prev = styleWordLabel.text;
        styleWordLabel.text  = msg;
        styleWordLabel.color = Color.white;
        yield return new WaitForSeconds(1.2f);
        styleWordLabel.text  = StyleWords[currentRank];
        styleWordLabel.color = Color.white;
        yield return new WaitForSeconds(drainDelay + 0.5f);
        if (currentPoints < RankThresholds[1]) killStreak = 0;
    }

    // ── Self-build UI (fallback if HUDManager isn't in scene) ────

    void BuildOwnUI()
    {
        // Find or create a canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        GameObject cvGO;
        if (canvas == null)
        {
            cvGO   = new GameObject("StyleBar_Canvas");
            canvas = cvGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cvGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ((CanvasScaler)cvGO.GetComponent<CanvasScaler>()).referenceResolution = new Vector2(1920, 1080);
            cvGO.AddComponent<GraphicRaycaster>();
        }
        else cvGO = canvas.gameObject;

        // Panel
        GameObject panel = new GameObject("StylePanel");
        panel.transform.SetParent(cvGO.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0, 0.5f); panelRt.anchorMax = new Vector2(0, 0.5f);
        panelRt.pivot     = new Vector2(0, 0.5f);
        panelRt.anchoredPosition = new Vector2(10, 0);
        panelRt.sizeDelta = new Vector2(160, 170);

        // BG
        var bgGO  = new GameObject("RankBG");
        bgGO.transform.SetParent(panel.transform, false);
        rankBackgroundImg = bgGO.AddComponent<Image>();
        rankBackgroundImg.color = new Color(1, 1, 1, 0.08f);
        Stretch(bgGO.GetComponent<RectTransform>());

        // Rank letter
        var rlGO = new GameObject("RankLetter");
        rlGO.transform.SetParent(panel.transform, false);
        rankLabel = rlGO.AddComponent<TextMeshProUGUI>();
        rankLabel.text      = "D";
        rankLabel.fontSize  = 72;
        rankLabel.fontStyle = FontStyles.Bold;
        rankLabel.alignment = TextAlignmentOptions.Center;
        rankLabel.color     = RankColors[0];
        var rlRt = rlGO.GetComponent<RectTransform>();
        rlRt.anchorMin = new Vector2(0, 0.45f); rlRt.anchorMax = new Vector2(1, 1f);
        rlRt.offsetMin = Vector2.zero;          rlRt.offsetMax = Vector2.zero;

        // Style word
        var swGO = new GameObject("StyleWord");
        swGO.transform.SetParent(panel.transform, false);
        styleWordLabel = swGO.AddComponent<TextMeshProUGUI>();
        styleWordLabel.text      = "DULL";
        styleWordLabel.fontSize  = 14;
        styleWordLabel.alignment = TextAlignmentOptions.Center;
        styleWordLabel.color     = Color.white;
        var swRt = swGO.GetComponent<RectTransform>();
        swRt.anchorMin = new Vector2(0, 0.28f); swRt.anchorMax = new Vector2(1, 0.48f);
        swRt.offsetMin = Vector2.zero;           swRt.offsetMax = Vector2.zero;

        // Slider track
        var trackGO  = new GameObject("StyleFill_Track");
        trackGO.transform.SetParent(panel.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        var trackRt  = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0, 0); trackRt.anchorMax = new Vector2(1, 0);
        trackRt.offsetMin = new Vector2(6, 8);  trackRt.offsetMax = new Vector2(-6, 22);

        // Fill area
        var faGO = new GameObject("Fill Area");
        faGO.transform.SetParent(trackGO.transform, false);
        var faRt = faGO.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero; faRt.offsetMax = Vector2.zero;

        // Fill
        var fillGO  = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        styleFillImage = fillGO.AddComponent<Image>();
        styleFillImage.color = RankColors[0];
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

        styleFillBar = trackGO.AddComponent<Slider>();
        styleFillBar.fillRect   = fillRt;
        styleFillBar.direction  = Slider.Direction.LeftToRight;
        styleFillBar.minValue   = 0f;
        styleFillBar.maxValue   = 1f;
        styleFillBar.value      = 0f;
        styleFillBar.interactable = false;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}