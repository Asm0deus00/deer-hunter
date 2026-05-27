using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// MainMenuManager — full main-menu built entirely in code.
/// Attach to any GameObject in your MainMenu scene.
///
/// SETUP:
///   1. File → New Scene → Basic (Built-in), save as "MainMenu".
///   2. Empty GameObject → attach this script.
///   3. File → Build Settings → Add Open Scenes:
///        Index 0  MainMenu
///        Index 1  your gameplay scene
///   4. Press Play — everything is built at runtime, no prefabs needed.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene to load when Play is clicked")]
    public int gameSceneIndex = 1;

    [Header("Optional background music")]
    public AudioClip menuMusic;

    // PlayerPrefs keys shared with PauseMenuManager
    internal const string KEY_MASTER = "Vol_Master";
    internal const string KEY_SFX    = "Vol_SFX";
    internal const string KEY_BRIGHT = "Brightness";

    private AudioSource musicSource;
    private GameObject  settingsPanel;
    private Image       brightnessOverlay;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;
        AudioListener.volume = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
    }

    void Start()
    {
        EnsureEventSystem();
        BuildMenuUI();
        SetupMusic();
    }

    // ── EventSystem (required for all UI interaction) ─────────────
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    // ── Music ─────────────────────────────────────────────────────
    void SetupMusic()
    {
        if (!menuMusic) return;
        musicSource        = gameObject.AddComponent<AudioSource>();
        musicSource.clip   = menuMusic;
        musicSource.loop   = true;
        musicSource.volume = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        musicSource.Play();
    }

    // ── Build all UI ──────────────────────────────────────────────
    void BuildMenuUI()
    {
        // Canvas
        var cvGO   = new GameObject("MainMenu_Canvas");
        var canvas = cvGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = cvGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();

        // Brightness overlay — behind everything
        brightnessOverlay = MakeImage(cvGO, "BrightnessOverlay", Color.black);
        brightnessOverlay.raycastTarget = false;
        Stretch(brightnessOverlay.rectTransform);
        brightnessOverlay.transform.SetAsFirstSibling();
        ApplyBrightnessOverlay(PlayerPrefs.GetFloat(KEY_BRIGHT, 1f));

        // Dark background
        var bg = MakeImage(cvGO, "MenuBG", new Color(0.04f, 0.07f, 0.04f, 0.88f));
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);

        // Left accent stripe
        var stripe   = MakeImage(cvGO, "Stripe", new Color(0.55f, 0.30f, 0.05f, 0.7f));
        stripe.raycastTarget = false;
        var stripeRt = stripe.rectTransform;
        stripeRt.anchorMin = Vector2.zero;
        stripeRt.anchorMax = new Vector2(0f, 1f);
        stripeRt.offsetMin = Vector2.zero;
        stripeRt.offsetMax = new Vector2(8f, 0f);

        // Title
        var titleTMP = MakeLabel(cvGO, "DEER\nHUNTER", 120, TextAlignmentOptions.Center,
            new Color(0.95f, 0.75f, 0.15f));
        titleTMP.fontStyle = FontStyles.Bold;
        var titleRt = titleTMP.rectTransform;
        titleRt.anchorMin = new Vector2(0.25f, 0.55f);
        titleRt.anchorMax = new Vector2(0.75f, 0.95f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // Sub-title
        var sub = MakeLabel(cvGO, "Season Open", 32, TextAlignmentOptions.Center,
            new Color(0.80f, 0.60f, 0.30f, 0.85f));
        var subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.25f, 0.50f);
        subRt.anchorMax = new Vector2(0.75f, 0.58f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;

        // Buttons
        MakeButton(cvGO, "PLAY",
            new Vector2(0.35f, 0.38f), new Vector2(0.65f, 0.48f),
            new Color(0.70f, 0.40f, 0.05f), OnPlay);

        MakeButton(cvGO, "SETTINGS",
            new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.35f),
            new Color(0.18f, 0.32f, 0.18f), OnSettings);

        MakeButton(cvGO, "QUIT",
            new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.22f),
            new Color(0.30f, 0.08f, 0.08f), OnQuit);

        // Settings panel (hidden)
        settingsPanel = BuildSettingsPanel(cvGO);
        settingsPanel.SetActive(false);

        // Version watermark
        var ver = MakeLabel(cvGO, "v1.0", 18, TextAlignmentOptions.BottomRight,
            new Color(1, 1, 1, 0.25f));
        ver.raycastTarget = false;
        var verRt = ver.rectTransform;
        verRt.anchorMin = new Vector2(0.85f, 0f);
        verRt.anchorMax = new Vector2(1f, 0.05f);
        verRt.offsetMin = Vector2.zero;
        verRt.offsetMax = new Vector2(-10f, 0f);
    }

    // ── Settings panel ───────────────────────────────────────────
    GameObject BuildSettingsPanel(GameObject canvas)
    {
        // Full-screen dim
        var dimGO  = new GameObject("SettingsRoot");
        dimGO.transform.SetParent(canvas.transform, false);
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.75f);
        Stretch(dimImg.rectTransform);

        // Panel box
        var panelGO  = new GameObject("SettingsPanel");
        panelGO.transform.SetParent(dimGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.12f, 0.08f, 0.97f);
        panelImg.raycastTarget = true;
        var panelRt  = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.28f, 0.20f);
        panelRt.anchorMax = new Vector2(0.72f, 0.82f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        // Title
        var pTitle = MakeLabel(panelGO, "SETTINGS", 48, TextAlignmentOptions.Center,
            new Color(0.95f, 0.75f, 0.15f));
        pTitle.raycastTarget = false;
        Place(pTitle.rectTransform, new Vector2(0f, 0.85f), new Vector2(1f, 1f));

        // Brightness
        MakeSettingRow(panelGO, "BRIGHTNESS", KEY_BRIGHT, new Color(0.95f, 0.75f, 0.15f),
            new Vector2(0f, 0.65f), new Vector2(1f, 0.83f),
            OnBrightnessChanged);

        // Master Volume
        MakeSettingRow(panelGO, "MASTER VOLUME", KEY_MASTER, new Color(0.30f, 0.70f, 0.30f),
            new Vector2(0f, 0.40f), new Vector2(1f, 0.58f),
            OnMasterVolumeChanged);

        // SFX Volume
        MakeSettingRow(panelGO, "SFX VOLUME", KEY_SFX, new Color(0.30f, 0.50f, 0.90f),
            new Vector2(0f, 0.15f), new Vector2(1f, 0.33f),
            OnSFXVolumeChanged);

        // Back button
        MakeButton(dimGO, "BACK",
            new Vector2(0.38f, 0.10f), new Vector2(0.62f, 0.17f),
            new Color(0.30f, 0.08f, 0.08f),
            () => settingsPanel.SetActive(false));

        return dimGO;
    }

    // Builds a label + slider + percentage label as a unit inside a parent rect
    void MakeSettingRow(GameObject parent, string rowLabel, string prefsKey,
        Color fillColor, Vector2 rowMin, Vector2 rowMax,
        UnityEngine.Events.UnityAction<float> callback)
    {
        // Row container (invisible)
        var rowGO = new GameObject("Row_" + rowLabel);
        rowGO.transform.SetParent(parent.transform, false);
        var rowRt = rowGO.AddComponent<RectTransform>();
        rowRt.anchorMin = rowMin;
        rowRt.anchorMax = rowMax;
        rowRt.offsetMin = new Vector2(16f, 0f);
        rowRt.offsetMax = new Vector2(-16f, 0f);

        float saved = PlayerPrefs.GetFloat(prefsKey, 1f);

        // Label (top 35% of row)
        var lbl = MakeLabel(rowGO, rowLabel, 24, TextAlignmentOptions.Left, Color.white);
        lbl.raycastTarget = false;
        Place(lbl.rectTransform, new Vector2(0f, 0.60f), new Vector2(0.75f, 1f));

        // Percentage readout
        var pct = MakeLabel(rowGO, Mathf.RoundToInt(saved * 100) + "%", 22,
            TextAlignmentOptions.Right, new Color(1, 1, 1, 0.7f));
        pct.raycastTarget = false;
        Place(pct.rectTransform, new Vector2(0.75f, 0.60f), new Vector2(1f, 1f));

        // Slider (bottom 50% of row)
        var slider = MakeSlider(rowGO, rowLabel + "_Slider",
            new Vector2(0f, 0f), new Vector2(1f, 0.55f),
            fillColor, saved);

        slider.onValueChanged.AddListener(v =>
        {
            pct.text = Mathf.RoundToInt(v * 100) + "%";
            PlayerPrefs.SetFloat(prefsKey, v);
            PlayerPrefs.Save();
            callback?.Invoke(v);
        });
    }

    // ── Slider callbacks ─────────────────────────────────────────
    void OnMasterVolumeChanged(float val)
    {
        AudioListener.volume = val;
        if (musicSource) musicSource.volume = val;
    }

    void OnSFXVolumeChanged(float val) { /* SFX scale applied via AudioListener */ }

    void OnBrightnessChanged(float val) => ApplyBrightnessOverlay(val);

    void ApplyBrightnessOverlay(float brightness)
    {
        if (!brightnessOverlay) return;
        brightnessOverlay.color = new Color(0, 0, 0, Mathf.Clamp01(1f - brightness) * 0.85f);
    }

    // ── Button callbacks ─────────────────────────────────────────
    void OnPlay()     => StartCoroutine(LoadGame());
    void OnSettings() => settingsPanel.SetActive(true);
    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator LoadGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneIndex);
        yield return null;
    }

    // ── UI factory helpers ────────────────────────────────────────

    Image MakeImage(GameObject parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI MakeLabel(GameObject parent, string text, float size,
        TextAlignmentOptions align, Color color)
    {
        var go  = new GameObject("Lbl_" + text.Replace("\n", ""));
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = color;
        return tmp;
    }

    void MakeButton(GameObject parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Color color, UnityEngine.Events.UnityAction callback)
    {
        var go  = new GameObject("Btn_" + label);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var btn    = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols   = btn.colors;
        cols.normalColor      = color;
        cols.highlightedColor = new Color(Mathf.Min(color.r + 0.25f, 1f),
                                          Mathf.Min(color.g + 0.25f, 1f),
                                          Mathf.Min(color.b + 0.25f, 1f));
        cols.pressedColor     = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f);
        cols.selectedColor    = cols.highlightedColor;
        btn.colors = cols;
        btn.onClick.AddListener(callback);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp   = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.raycastTarget = false;
        Stretch(tmp.rectTransform);
    }

    Slider MakeSlider(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Color fillColor, float startValue)
    {
        // Track
        var trackGO  = new GameObject(name + "_Track");
        trackGO.transform.SetParent(parent.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        var trackRt  = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = anchorMin;
        trackRt.anchorMax = anchorMax;
        trackRt.offsetMin = Vector2.zero;
        trackRt.offsetMax = Vector2.zero;

        // Fill area
        var faGO = new GameObject("Fill Area");
        faGO.transform.SetParent(trackGO.transform, false);
        var faRt = faGO.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.1f);
        faRt.anchorMax = new Vector2(1f, 0.9f);
        faRt.offsetMin = new Vector2(5f, 0f);
        faRt.offsetMax = new Vector2(-15f, 0f);

        // Fill
        var fillGO  = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = fillColor;
        var fillRt  = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        // Handle slide area
        var handleArea   = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(trackGO.transform, false);
        var haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        // Handle
        var handleGO  = new GameObject("Handle");
        handleGO.transform.SetParent(handleArea.transform, false);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRt  = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(24f, 0f);

        // Slider component
        var slider = trackGO.AddComponent<Slider>();
        slider.fillRect   = fillRt;
        slider.handleRect = handleRt;
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = 0f;
        slider.maxValue   = 1f;
        slider.value      = startValue;
        return slider;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void Place(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}