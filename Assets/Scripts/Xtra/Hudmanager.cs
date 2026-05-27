using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// HUDManager — builds and drives the entire in-game HUD at runtime.
/// Attach to any empty GameObject in your scene. No manual Canvas needed.
/// </summary>
public class HUDManager : MonoBehaviour
{
    // ── References built during BuildHUD() ───────────────────────
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI killCountText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI grenadeText;
    private TextMeshProUGUI popupTMP;
    private Slider          healthSlider;
    private TextMeshProUGUI healthText;
    private Image           hitVignetteImg;
    private RectTransform   popupRect;

    // Style panel — injected into StyleBar at Start()
    [HideInInspector] public TextMeshProUGUI rankLabel;
    [HideInInspector] public TextMeshProUGUI styleWordLabel;
    [HideInInspector] public Slider          styleFillBar;
    [HideInInspector] public Image           rankBackgroundImg;

    private GameObject      deathScreen;
    private TextMeshProUGUI deathScoreText;
    private GameObject      roundEndScreen;
    private TextMeshProUGUI finalScoreText;
    private TextMeshProUGUI finalKillsText;

    private Coroutine  popupRoutine;
    private ScoreManager scoreManager;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        BuildHUD();
    }

    void Start()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();

        // Wire StyleBar's UI fields — it finds refs by having them injected here
        StyleBar bar = FindFirstObjectByType<StyleBar>();
        if (bar != null)
        {
            bar.rankLabel        = rankLabel;
            bar.styleWordLabel   = styleWordLabel;
            bar.styleFillBar     = styleFillBar;
            bar.rankBackgroundImg = rankBackgroundImg;
        }

        UpdateHealth(1f);
        UpdateScore(0);
        UpdateKillCount(0);
        UpdateGrenades(3, 3);
    }

    void Update()
    {
        if (timerText == null || scoreManager == null) return;
        float t = scoreManager.TimeLeft;
        timerText.text  = string.Format("{0:0}:{1:00}", Mathf.FloorToInt(t / 60f), Mathf.FloorToInt(t % 60f));
        timerText.color = t < 10f
            ? Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 4f, 1f))
            : Color.white;
    }

    // ─── Public API ───────────────────────────────────────────────

    public void UpdateHealth(float pct)
    {
        if (healthSlider != null) healthSlider.value = pct;
        if (healthText   != null) healthText.text    = Mathf.RoundToInt(pct * 100f) + " HP";
        if (healthSlider != null)
        {
            Image fill = healthSlider.fillRect != null ? healthSlider.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = Color.Lerp(Color.red, new Color(0.1f, 0.85f, 0.1f), pct);
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = score.ToString("N0");
    }

    public void UpdateKillCount(int kills)
    {
        if (killCountText != null) killCountText.text = "Kills: " + kills;
    }

    public void UpdateGrenades(int current, int max)
    {
        if (grenadeText != null) grenadeText.text = "Grenades: " + current + " / " + max;
    }

    public void ShowPopupScore(int points)
    {
        if (popupTMP == null) return;
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(AnimatePopup("+" + points));
    }

    public Image HitVignetteImage => hitVignetteImg;

    public void ShowDeathScreen()
    {
        if (deathScreen != null) deathScreen.SetActive(true);
        if (deathScoreText != null && scoreManager != null)
            deathScoreText.text = "Score: " + scoreManager.Score.ToString("N0");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        StartCoroutine(ReloadAfterDelay(5f));
    }

    public void ShowRoundEnd(int finalScore, int kills)
    {
        if (roundEndScreen != null) roundEndScreen.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = "Score: " + finalScore.ToString("N0");
        if (finalKillsText != null) finalKillsText.text = "Kills: " + kills;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ─── HUD Construction ─────────────────────────────────────────

    void BuildHUD()
    {
        // Canvas
        GameObject cvGO  = new GameObject("HUD_Canvas");
        Canvas canvas    = cvGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = cvGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();

        // ── Hit vignette (full screen red flash) ─────────────────
        hitVignetteImg = NewImage(cvGO, "HitVignette", new Color(0.8f, 0f, 0f, 0f));
        Stretch(hitVignetteImg.rectTransform);

        // ── Top bar ───────────────────────────────────────────────
        GameObject topBar = NewPanel(cvGO, "TopBar", new Color(0, 0, 0, 0));
        RectTransform topRt = topBar.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0, 1); topRt.anchorMax = new Vector2(1, 1);
        topRt.sizeDelta = new Vector2(0, 60); topRt.anchoredPosition = new Vector2(0, -30);

        killCountText = NewLabel(topBar, "KillCount", "Kills: 0", 26, TextAlignmentOptions.Left);
        PlaceInParent(killCountText.rectTransform, new Vector2(0,0), new Vector2(0.33f,1), Vector2.zero, Vector2.zero);

        timerText = NewLabel(topBar, "Timer", "2:00", 38, TextAlignmentOptions.Center);
        timerText.fontStyle = FontStyles.Bold;
        PlaceInParent(timerText.rectTransform, new Vector2(0.33f,0), new Vector2(0.66f,1), Vector2.zero, Vector2.zero);

        scoreText = NewLabel(topBar, "Score", "0", 26, TextAlignmentOptions.Right);
        PlaceInParent(scoreText.rectTransform, new Vector2(0.66f,0), new Vector2(1f,1), Vector2.zero, Vector2.zero);

        // ── Bottom-left: health ───────────────────────────────────
        GameObject hpPanel = NewPanel(cvGO, "HealthPanel", new Color(0,0,0,0.45f));
        RectTransform hpRt = hpPanel.GetComponent<RectTransform>();
        hpRt.anchorMin = new Vector2(0,0); hpRt.anchorMax = new Vector2(0,0);
        hpRt.pivot     = new Vector2(0,0);
        hpRt.anchoredPosition = new Vector2(20, 20);
        hpRt.sizeDelta = new Vector2(300, 70);

        healthText = NewLabel(hpPanel, "HPText", "100 HP", 18, TextAlignmentOptions.Left);
        PlaceInParent(healthText.rectTransform, new Vector2(0,0.5f), new Vector2(1f,1f), new Vector2(8,0), new Vector2(-8,0));

        healthSlider = NewSlider(hpPanel, "HealthBar", new Color(0.1f, 0.85f, 0.1f));
        PlaceInParent(healthSlider.GetComponent<RectTransform>(), new Vector2(0,0), new Vector2(1,0.5f), new Vector2(8,6), new Vector2(-8,0));

        // ── Bottom-right: grenades ────────────────────────────────
        GameObject grenPanel = NewPanel(cvGO, "GrenPanel", new Color(0,0,0,0.45f));
        RectTransform grenRt = grenPanel.GetComponent<RectTransform>();
        grenRt.anchorMin = new Vector2(1,0); grenRt.anchorMax = new Vector2(1,0);
        grenRt.pivot = new Vector2(1,0);
        grenRt.anchoredPosition = new Vector2(-20, 20);
        grenRt.sizeDelta = new Vector2(230, 50);
        grenadeText = NewLabel(grenPanel, "GrenText", "Grenades: 3 / 3", 22, TextAlignmentOptions.Center);
        Stretch(grenadeText.rectTransform);

        // ── Left-centre: style panel ──────────────────────────────
        GameObject stylePanel = NewPanel(cvGO, "StylePanel", new Color(0,0,0,0));
        RectTransform styleRt = stylePanel.GetComponent<RectTransform>();
        styleRt.anchorMin = new Vector2(0,0.5f); styleRt.anchorMax = new Vector2(0,0.5f);
        styleRt.pivot = new Vector2(0, 0.5f);
        styleRt.anchoredPosition = new Vector2(10, 0);
        styleRt.sizeDelta = new Vector2(160, 170);

        rankBackgroundImg = NewImage(stylePanel, "RankBG", new Color(1,1,1,0.08f));
        Stretch(rankBackgroundImg.rectTransform);

        rankLabel = NewLabel(stylePanel, "RankLetter", "D", 72, TextAlignmentOptions.Center);
        rankLabel.fontStyle = FontStyles.Bold;
        rankLabel.color = new Color(0.55f, 0.55f, 0.55f);
        PlaceInParent(rankLabel.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);

        styleWordLabel = NewLabel(stylePanel, "StyleWord", "DULL", 14, TextAlignmentOptions.Center);
        PlaceInParent(styleWordLabel.rectTransform, new Vector2(0, 0.28f), new Vector2(1, 0.48f), Vector2.zero, Vector2.zero);

        styleFillBar = NewSlider(stylePanel, "StyleFill", new Color(0.55f, 0.55f, 0.55f));
        PlaceInParent(styleFillBar.GetComponent<RectTransform>(), new Vector2(0,0), new Vector2(1,0), new Vector2(6,8), new Vector2(-6,22));

        // ── Floating score popup ──────────────────────────────────
        GameObject popupGO = new GameObject("PopupScore");
        popupGO.transform.SetParent(cvGO.transform, false);
        popupTMP = popupGO.AddComponent<TextMeshProUGUI>();
        popupTMP.fontSize  = 36;
        popupTMP.fontStyle = FontStyles.Bold;
        popupTMP.color     = Color.yellow;
        popupTMP.alignment = TextAlignmentOptions.Center;
        popupTMP.text      = "";
        popupRect = popupGO.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.45f);
        popupRect.anchorMax = new Vector2(0.5f, 0.45f);
        popupRect.sizeDelta = new Vector2(200, 60);
        popupRect.anchoredPosition = Vector2.zero;
        CanvasGroup popupCG = popupGO.AddComponent<CanvasGroup>();
        popupCG.alpha = 0f;

        // ── Death screen ──────────────────────────────────────────
        deathScreen = NewOverlay(cvGO, "DeathScreen", new Color(0,0,0,0.82f));
        TextMeshProUGUI dTitle = NewLabel(deathScreen, "DeathTitle", "YOU DIED", 80, TextAlignmentOptions.Center);
        dTitle.color = Color.red;
        PlaceInParent(dTitle.rectTransform, new Vector2(0,0.55f), new Vector2(1,0.78f), Vector2.zero, Vector2.zero);
        deathScoreText = NewLabel(deathScreen, "DeathScore", "Score: 0", 36, TextAlignmentOptions.Center);
        PlaceInParent(deathScoreText.rectTransform, new Vector2(0,0.35f), new Vector2(1,0.55f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI dHint = NewLabel(deathScreen, "DeathHint", "Restarting in 5 seconds…", 22, TextAlignmentOptions.Center);
        dHint.color = new Color(1,1,1,0.5f);
        PlaceInParent(dHint.rectTransform, new Vector2(0,0.20f), new Vector2(1,0.35f), Vector2.zero, Vector2.zero);
        deathScreen.SetActive(false);

        // ── Round end screen ──────────────────────────────────────
        roundEndScreen = NewOverlay(cvGO, "RoundEndScreen", new Color(0,0,0,0.88f));
        TextMeshProUGUI eTitle = NewLabel(roundEndScreen, "EndTitle", "TIME'S UP", 72, TextAlignmentOptions.Center);
        eTitle.color = Color.yellow;
        PlaceInParent(eTitle.rectTransform, new Vector2(0,0.60f), new Vector2(1,0.82f), Vector2.zero, Vector2.zero);
        finalScoreText = NewLabel(roundEndScreen, "FinalScore", "Score: 0", 40, TextAlignmentOptions.Center);
        PlaceInParent(finalScoreText.rectTransform, new Vector2(0,0.42f), new Vector2(1,0.60f), Vector2.zero, Vector2.zero);
        finalKillsText = NewLabel(roundEndScreen, "FinalKills", "Kills: 0", 32, TextAlignmentOptions.Center);
        PlaceInParent(finalKillsText.rectTransform, new Vector2(0,0.28f), new Vector2(1,0.42f), Vector2.zero, Vector2.zero);
        NewRestartButton(roundEndScreen);
        roundEndScreen.SetActive(false);
    }

    // ─── Popup animation ──────────────────────────────────────────
    IEnumerator AnimatePopup(string msg)
    {
        CanvasGroup cg = popupTMP.GetComponent<CanvasGroup>();
        popupTMP.text = msg;
        popupRect.anchoredPosition = Vector2.zero;
        cg.alpha = 1f;
        float dur = 1.2f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / dur;
            popupRect.anchoredPosition = Vector2.up * (90f * t);
            cg.alpha = Mathf.Lerp(1f, 0f, t * t);
            yield return null;
        }
        cg.alpha = 0f;
    }

    IEnumerator ReloadAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ─── UI factory helpers ───────────────────────────────────────

    Image NewImage(GameObject parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    GameObject NewPanel(GameObject parent, string name, Color bg)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        return go;
    }

    TextMeshProUGUI NewLabel(GameObject parent, string name, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = Color.white;
        return tmp;
    }

    Slider NewSlider(GameObject parent, string name, Color fillColor)
    {
        // Track background
        GameObject bg = new GameObject(name + "_Track");
        bg.transform.SetParent(parent.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(bg.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero; faRt.offsetMax = Vector2.zero;

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

        Slider s = bg.AddComponent<Slider>();
        s.fillRect    = fillRt;
        s.direction   = Slider.Direction.LeftToRight;
        s.minValue    = 0f;
        s.maxValue    = 1f;
        s.value       = 1f;
        s.interactable = false;
        return s;
    }

    GameObject NewOverlay(GameObject parent, string name, Color bg)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        RectTransform rt = go.GetComponent<RectTransform>();
        Stretch(rt);
        return go;
    }

    void NewRestartButton(GameObject parent)
    {
        GameObject go = new GameObject("RestartButton");
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.9f, 0.7f, 0.1f);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.10f);
        rt.anchorMax = new Vector2(0.65f, 0.23f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(RestartScene);
        TextMeshProUGUI lbl = NewLabel(go, "Label", "PLAY AGAIN", 26, TextAlignmentOptions.Center);
        lbl.color = Color.black;
        lbl.fontStyle = FontStyles.Bold;
        Stretch(lbl.rectTransform);
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void PlaceInParent(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}