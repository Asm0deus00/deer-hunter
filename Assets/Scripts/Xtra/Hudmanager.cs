using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// HUDManager — builds and drives the entire in-game HUD at runtime.
/// No manual Canvas setup needed. Just attach this script to any empty
/// GameObject in your scene and press Play.
///
/// What it creates at runtime:
///   Canvas (Screen Space – Overlay)
///   ├─ HitVignette        full-screen red flash on damage
///   ├─ TopBar             timer (centre), score (right), kills (left)
///   ├─ BottomLeft         health bar + HP text
///   ├─ BottomRight        grenade counter
///   ├─ StylePanel         rank letter + word + fill bar (left-centre)
///   ├─ PopupScore         floating "+points" label
///   ├─ DeathScreen        dark overlay + "YOU DIED" + score + restart hint
///   └─ RoundEndScreen     dark overlay + final score + kills + restart button
/// </summary>
public class HUDManager : MonoBehaviour
{
    // ── References wired during Build() ──────────────────────────
    private TMP_Text  scoreText;
    private TMP_Text  killCountText;
    private TMP_Text  timerText;
    private TMP_Text  grenadeText;
    private TMP_Text  popupText;
    private Slider    healthSlider;
    private TMP_Text  healthText;
    private Image     hitVignette;

    // Style bar (also used by StyleBar.cs directly via public refs it finds at runtime)
    [HideInInspector] public TMP_Text  rankLabel;
    [HideInInspector] public TMP_Text  styleWordLabel;
    [HideInInspector] public Slider    styleFillBar;
    [HideInInspector] public Image     rankBackgroundImg;

    private GameObject deathScreen;
    private TMP_Text   deathScoreText;
    private GameObject roundEndScreen;
    private TMP_Text   finalScoreText;
    private TMP_Text   finalKillsText;

    private RectTransform popupAnchor;
    private Coroutine     popupRoutine;

    // ─────────────────────────────────────────────────────────────
    private ScoreManager scoreManager;
    private Canvas       canvas;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        BuildHUD();
    }

    void Start()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();

        // Tell StyleBar about the UI elements we built
        StyleBar bar = FindFirstObjectByType<StyleBar>();
        if (bar != null)
        {
            bar.rankLabel        = rankLabel;
            bar.styleWordLabel   = styleWordLabel;
            bar.styleFillBar     = styleFillBar;
            bar.rankBackgroundImg = rankBackgroundImg;
        }

        // Initial values
        UpdateHealth(1f);
        UpdateScore(0);
        UpdateKillCount(0);
        UpdateGrenades(3, 3);
    }

    void Update()
    {
        if (timerText && scoreManager)
        {
            float t = scoreManager.TimeLeft;
            timerText.text  = string.Format("{0:0}:{1:00}",
                Mathf.FloorToInt(t / 60f), Mathf.FloorToInt(t % 60f));
            timerText.color = t < 10f
                ? Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 4f, 1f))
                : Color.white;
        }
    }

    // ─── Public API (called by other scripts) ─────────────────────

    public void UpdateHealth(float pct)
    {
        if (healthSlider) healthSlider.value = pct;
        if (healthText)   healthText.text    = Mathf.RoundToInt(pct * 100f) + " HP";
        if (healthSlider)
        {
            var fill = healthSlider.fillRect?.GetComponent<Image>();
            if (fill) fill.color = Color.Lerp(Color.red, new Color(0.1f, 0.85f, 0.1f), pct);
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText) scoreText.text = score.ToString("N0");
    }

    public void UpdateKillCount(int kills)
    {
        if (killCountText) killCountText.text = "☠ " + kills;
    }

    public void UpdateGrenades(int current, int max)
    {
        if (grenadeText) grenadeText.text = "💣 " + current + " / " + max;
    }

    public void ShowPopupScore(int points)
    {
        if (!popupText) return;
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(AnimatePopup("+" + points));
    }

    /// <summary>Called by PlayerHealth when the vignette alpha changes.</summary>
    public Image HitVignetteImage => hitVignette;

    public void ShowDeathScreen()
    {
        if (deathScreen) deathScreen.SetActive(true);
        if (deathScoreText && scoreManager)
            deathScoreText.text = "Score: " + scoreManager.Score.ToString("N0");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        StartCoroutine(ReloadAfterDelay(5f));
    }

    public void ShowRoundEnd(int finalScore, int kills)
    {
        if (roundEndScreen)  roundEndScreen.SetActive(true);
        if (finalScoreText)  finalScoreText.text = "Score: " + finalScore.ToString("N0");
        if (finalKillsText)  finalKillsText.text = "Kills: " + kills;
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
        // ── Canvas ──────────────────────────────────────────────
        GameObject cvGO = new GameObject("HUD_Canvas");
        canvas = cvGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        cvGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        ((CanvasScaler)cvGO.GetComponent<CanvasScaler>()).referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(cvGO);

        // ── Hit vignette (full-screen) ───────────────────────────
        hitVignette = MakeImage(cvGO, "HitVignette",
            new Color(0.8f, 0f, 0f, 0f),
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);

        // ── Top bar ─────────────────────────────────────────────
        GameObject topBar = MakePanel(cvGO, "TopBar",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -50), new Vector2(0, 0),
            new Color(0, 0, 0, 0));

        killCountText = MakeLabel(topBar, "KillCount", "☠ 0",
            28, TextAlignmentOptions.Left,
            new Vector2(0, 0), new Vector2(0.33f, 1f), Vector2.zero, Vector2.zero);

        timerText = MakeLabel(topBar, "Timer", "2:00",
            38, TextAlignmentOptions.Center,
            new Vector2(0.33f, 0), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
        timerText.fontStyle = FontStyles.Bold;

        scoreText = MakeLabel(topBar, "Score", "0",
            28, TextAlignmentOptions.Right,
            new Vector2(0.66f, 0), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // ── Bottom-left: health ──────────────────────────────────
        GameObject healthPanel = MakePanel(cvGO, "HealthPanel",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 20), new Vector2(320, 80),
            new Color(0, 0, 0, 0.45f));

        healthText = MakeLabel(healthPanel, "HPText", "100 HP",
            18, TextAlignmentOptions.Left,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(120, 30));

        healthSlider = MakeSlider(healthPanel, "HealthBar",
            new Color(0.1f, 0.85f, 0.1f),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, 10), new Vector2(-10, 30));

        // ── Bottom-right: grenades ───────────────────────────────
        GameObject grenPanel = MakePanel(cvGO, "GrenadePanel",
            new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-220, 20), new Vector2(200, 50),
            new Color(0, 0, 0, 0.45f));

        grenadeText = MakeLabel(grenPanel, "GrenadeText", "💣 3 / 3",
            22, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        // ── Left-centre: style panel ─────────────────────────────
        GameObject stylePanel = MakePanel(cvGO, "StylePanel",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, -90), new Vector2(170, 180),
            new Color(0, 0, 0, 0.0f));

        rankBackgroundImg = MakeImage(stylePanel, "RankBG",
            new Color(1, 1, 1, 0.1f),
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        rankLabel = MakeLabel(stylePanel, "RankLetter", "D",
            72, TextAlignmentOptions.Center,
            new Vector2(0, 0.4f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        rankLabel.fontStyle = FontStyles.Bold;
        rankLabel.color     = new Color(0.55f, 0.55f, 0.55f);

        styleWordLabel = MakeLabel(stylePanel, "StyleWord", "DULL",
            16, TextAlignmentOptions.Center,
            new Vector2(0, 0.25f), new Vector2(1, 0.45f), Vector2.zero, Vector2.zero);
        styleWordLabel.color = Color.white;

        styleFillBar = MakeSlider(stylePanel, "StyleBar",
            new Color(0.55f, 0.55f, 0.55f),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(5, 8), new Vector2(-5, 22));

        // ── Floating score popup ─────────────────────────────────
        GameObject popupGO = new GameObject("PopupScore");
        popupGO.transform.SetParent(cvGO.transform, false);
        popupText = popupGO.AddComponent<TMP_Text>() is TMP_Text t ? t : null;
        // TMP needs the proper component; use AddComponent<TextMeshProUGUI>
        Destroy(popupGO);   // rebuild with correct component
        popupGO = new GameObject("PopupScore");
        popupGO.transform.SetParent(cvGO.transform, false);
        var popupTMP = popupGO.AddComponent<TextMeshProUGUI>();
        popupTMP.fontSize    = 36;
        popupTMP.fontStyle   = FontStyles.Bold;
        popupTMP.color       = Color.yellow;
        popupTMP.alignment   = TextAlignmentOptions.Center;
        popupTMP.text        = "";
        var popupRect        = popupGO.GetComponent<RectTransform>();
        popupRect.anchorMin  = new Vector2(0.5f, 0.4f);
        popupRect.anchorMax  = new Vector2(0.5f, 0.4f);
        popupRect.sizeDelta  = new Vector2(200, 60);
        popupRect.anchoredPosition = Vector2.zero;
        popupText   = popupTMP;
        popupAnchor = popupRect;
        var cg = popupGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // ── Death screen ─────────────────────────────────────────
        deathScreen = MakeDarkOverlay(cvGO, "DeathScreen",
            new Color(0, 0, 0, 0.82f));
        MakeLabel(deathScreen, "DeathTitle", "YOU DIED",
            80, TextAlignmentOptions.Center,
            new Vector2(0, 0.55f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero)
            .color = Color.red;
        deathScoreText = MakeLabel(deathScreen, "DeathScore", "Score: 0",
            36, TextAlignmentOptions.Center,
            new Vector2(0, 0.35f), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
        MakeLabel(deathScreen, "DeathHint", "Restarting…",
            22, TextAlignmentOptions.Center,
            new Vector2(0, 0.2f), new Vector2(1, 0.35f), Vector2.zero, Vector2.zero)
            .color = new Color(1, 1, 1, 0.5f);
        deathScreen.SetActive(false);

        // ── Round end screen ─────────────────────────────────────
        roundEndScreen = MakeDarkOverlay(cvGO, "RoundEndScreen",
            new Color(0, 0, 0, 0.88f));
        MakeLabel(roundEndScreen, "EndTitle", "TIME'S UP",
            72, TextAlignmentOptions.Center,
            new Vector2(0, 0.60f), new Vector2(1, 0.80f), Vector2.zero, Vector2.zero)
            .color = Color.yellow;
        finalScoreText = MakeLabel(roundEndScreen, "FinalScore", "Score: 0",
            40, TextAlignmentOptions.Center,
            new Vector2(0, 0.42f), new Vector2(1, 0.60f), Vector2.zero, Vector2.zero);
        finalKillsText = MakeLabel(roundEndScreen, "FinalKills", "Kills: 0",
            32, TextAlignmentOptions.Center,
            new Vector2(0, 0.28f), new Vector2(1, 0.42f), Vector2.zero, Vector2.zero);
        MakeRestartButton(roundEndScreen, this);
        roundEndScreen.SetActive(false);
    }

    // ─── Popup animation ──────────────────────────────────────────

    IEnumerator AnimatePopup(string msg)
    {
        if (popupText == null || popupAnchor == null) yield break;
        var cg = popupText.GetComponent<CanvasGroup>();
        if (cg == null) cg = popupText.gameObject.AddComponent<CanvasGroup>();

        popupText.text      = msg;
        Vector2 startPos    = Vector2.zero;
        popupAnchor.anchoredPosition = startPos;
        cg.alpha = 1f;

        float dur = 1.2f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / dur;
            popupAnchor.anchoredPosition = startPos + Vector2.up * (90f * t);
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

    // anchorMin/Max are 0-1 fractions of parent; offsetMin/Max are pixel offsets.
    Image MakeImage(GameObject parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        return img;
    }

    GameObject MakePanel(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        Color bg)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        return go;
    }

    TextMeshProUGUI MakeLabel(GameObject parent, string name, string text,
        float fontSize, TextAlignmentOptions align,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = align;
        tmp.color     = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        return tmp;
    }

    Slider MakeSlider(GameObject parent, string name, Color fillColor,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        // Background
        var bg    = new GameObject(name + "_BG");
        bg.transform.SetParent(parent.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        var bgRt  = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = anchorMin;
        bgRt.anchorMax = anchorMax;
        bgRt.offsetMin = offsetMin;
        bgRt.offsetMax = offsetMax;

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(bg.transform, false);
        var faRt     = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(0, 0);
        faRt.offsetMax = new Vector2(0, 0);

        var fill    = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        var fillRt  = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        var slider         = bg.AddComponent<Slider>();
        slider.fillRect    = fillRt;
        slider.direction   = Slider.Direction.LeftToRight;
        slider.minValue    = 0f;
        slider.maxValue    = 1f;
        slider.value       = 1f;
        slider.interactable = false;

        return slider;
    }

    GameObject MakeDarkOverlay(GameObject parent, string name, Color bg)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    void MakeRestartButton(GameObject parent, HUDManager mgr)
    {
        var go  = new GameObject("RestartButton");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.9f, 0.7f, 0.1f);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.10f);
        rt.anchorMax = new Vector2(0.65f, 0.22f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(mgr.RestartScene);

        var lbl = MakeLabel(go, "BtnLabel", "PLAY AGAIN",
            26, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        lbl.color = Color.black;
        lbl.fontStyle = FontStyles.Bold;
    }
}