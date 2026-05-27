using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// PauseMenuManager — ESC toggles an in-game pause menu.
/// Sliders for Brightness, Master Volume, and SFX Volume.
/// Settings persist via PlayerPrefs (shared with MainMenuManager).
///
/// SETUP:
///   1. Open your gameplay scene.
///   2. Create an empty GameObject → attach this script.
///   3. Press ESC in Play mode to open/close the pause menu.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("Scene indices")]
    public int mainMenuSceneIndex = 0;

    private const string KEY_MASTER = "Vol_Master";
    private const string KEY_SFX    = "Vol_SFX";
    private const string KEY_BRIGHT = "Brightness";

    private bool      isPaused = false;
    private GameObject pauseRoot;
    private Image     brightnessOverlay;

    private PlayerMovementScript playerMovement;
    private MouseLookScript      mouseLook;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovementScript>();
            mouseLook      = player.GetComponent<MouseLookScript>();
        }
    }

    void Start()
    {
        EnsureEventSystem();
        BuildPauseUI();
        // Apply saved brightness immediately (overlay stays visible always)
        ApplyBrightnessOverlay(PlayerPrefs.GetFloat(KEY_BRIGHT, 1f));
        SetPaused(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetPaused(!isPaused);
    }

    // ── EventSystem ───────────────────────────────────────────────
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    // ── Pause / resume ────────────────────────────────────────────
    void SetPaused(bool paused)
    {
        isPaused = paused;
        pauseRoot.SetActive(paused);

        if (paused)
        {
            Time.timeScale   = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            if (playerMovement) playerMovement.enabled = false;
            if (mouseLook)      mouseLook.enabled      = false;
        }
        else
        {
            Time.timeScale   = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            if (playerMovement) playerMovement.enabled = true;
            if (mouseLook)      mouseLook.enabled      = true;
        }
    }

    void Resume()        => SetPaused(false);
    void RestartLevel()  { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    void GoToMainMenu()  { Time.timeScale = 1f; SceneManager.LoadScene(mainMenuSceneIndex); }

    // ── Build UI ──────────────────────────────────────────────────
    void BuildPauseUI()
    {
        // Canvas — high sort order so it's above HUD
        var cvGO   = new GameObject("PauseMenu_Canvas");
        var canvas = cvGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cvGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();

        // Brightness overlay — always visible, not part of pauseRoot
        brightnessOverlay = PM_MakeImage(cvGO, "BrightnessOverlay", Color.black);
        brightnessOverlay.raycastTarget = false;
        PM_Stretch(brightnessOverlay.rectTransform);
        brightnessOverlay.transform.SetAsFirstSibling();

        // ── Pause root (toggled on/off) ───────────────────────────
        pauseRoot = new GameObject("PauseRoot");
        pauseRoot.transform.SetParent(cvGO.transform, false);
        // Transparent blocker so clicks don't fall through to game
        var rootImg = pauseRoot.AddComponent<Image>();
        rootImg.color = new Color(0, 0, 0, 0);
        rootImg.raycastTarget = true;
        PM_Stretch(rootImg.rectTransform);

        // Dim backdrop
        var backdrop = PM_MakeImage(pauseRoot, "Backdrop", new Color(0.03f, 0.06f, 0.03f, 0.82f));
        backdrop.raycastTarget = false;
        PM_Stretch(backdrop.rectTransform);

        // Panel box
        var panelGO  = new GameObject("PausePanel");
        panelGO.transform.SetParent(pauseRoot.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.07f, 0.11f, 0.07f, 0.97f);
        var panelRt  = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.30f, 0.10f);
        panelRt.anchorMax = new Vector2(0.70f, 0.90f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        // Accent stripe
        var stripe = PM_MakeImage(panelGO, "Stripe", new Color(0.55f, 0.30f, 0.05f, 0.8f));
        stripe.raycastTarget = false;
        var sRt = stripe.rectTransform;
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = new Vector2(0f, 1f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = new Vector2(7f, 0f);

        // PAUSED label
        var pausedLbl = PM_MakeLabel(panelGO, "PAUSED", 64, TextAlignmentOptions.Center,
            new Color(0.95f, 0.75f, 0.15f));
        pausedLbl.fontStyle   = FontStyles.Bold;
        pausedLbl.raycastTarget = false;
        PM_Place(pausedLbl.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 1f));

        // Settings rows
        PM_MakeSettingRow(panelGO, "BRIGHTNESS",    KEY_BRIGHT, new Color(0.95f, 0.75f, 0.15f),
            new Vector2(0f, 0.65f), new Vector2(1f, 0.83f), OnBrightnessChanged);

        PM_MakeSettingRow(panelGO, "MASTER VOLUME", KEY_MASTER, new Color(0.30f, 0.70f, 0.30f),
            new Vector2(0f, 0.42f), new Vector2(1f, 0.60f), OnMasterVolumeChanged);

        PM_MakeSettingRow(panelGO, "SFX VOLUME",    KEY_SFX,    new Color(0.30f, 0.50f, 0.90f),
            new Vector2(0f, 0.19f), new Vector2(1f, 0.37f), OnSFXVolumeChanged);

        // Buttons
        PM_MakeButton(panelGO, "RESUME",
            new Vector2(0.06f, 0.08f), new Vector2(0.46f, 0.17f),
            new Color(0.20f, 0.50f, 0.15f), Resume);

        PM_MakeButton(panelGO, "RESTART",
            new Vector2(0.54f, 0.08f), new Vector2(0.94f, 0.17f),
            new Color(0.50f, 0.35f, 0.05f), RestartLevel);

        PM_MakeButton(panelGO, "MAIN MENU",
            new Vector2(0.06f, 0.01f), new Vector2(0.94f, 0.07f),
            new Color(0.30f, 0.08f, 0.08f), GoToMainMenu);
    }

    // ── Setting row (label + slider + pct) ───────────────────────
    void PM_MakeSettingRow(GameObject parent, string rowLabel, string prefsKey,
        Color fillColor, Vector2 rowMin, Vector2 rowMax,
        UnityEngine.Events.UnityAction<float> callback)
    {
        var rowGO = new GameObject("Row_" + rowLabel);
        rowGO.transform.SetParent(parent.transform, false);
        var rowRt = rowGO.AddComponent<RectTransform>();
        rowRt.anchorMin = rowMin;
        rowRt.anchorMax = rowMax;
        rowRt.offsetMin = new Vector2(16f, 0f);
        rowRt.offsetMax = new Vector2(-16f, 0f);

        float saved = PlayerPrefs.GetFloat(prefsKey, 1f);

        var lbl = PM_MakeLabel(rowGO, rowLabel, 24, TextAlignmentOptions.Left, Color.white);
        lbl.raycastTarget = false;
        PM_Place(lbl.rectTransform, new Vector2(0f, 0.55f), new Vector2(0.75f, 1f));

        var pct = PM_MakeLabel(rowGO, Mathf.RoundToInt(saved * 100) + "%",
            22, TextAlignmentOptions.Right, new Color(1, 1, 1, 0.7f));
        pct.raycastTarget = false;
        PM_Place(pct.rectTransform, new Vector2(0.75f, 0.55f), new Vector2(1f, 1f));

        var slider = PM_MakeSlider(rowGO, rowLabel + "_Slider",
            new Vector2(0f, 0f), new Vector2(1f, 0.50f),
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
    void OnMasterVolumeChanged(float val) => AudioListener.volume = val;
    void OnSFXVolumeChanged(float val)    { /* stored in prefs; apply per-source if needed */ }
    void OnBrightnessChanged(float val)   => ApplyBrightnessOverlay(val);

    void ApplyBrightnessOverlay(float brightness)
    {
        if (!brightnessOverlay) return;
        brightnessOverlay.color = new Color(0, 0, 0, Mathf.Clamp01(1f - brightness) * 0.85f);
    }

    // ── UI helpers ────────────────────────────────────────────────

    Image PM_MakeImage(GameObject parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI PM_MakeLabel(GameObject parent, string text, float size,
        TextAlignmentOptions align, Color color)
    {
        var go  = new GameObject("Lbl_" + text);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = color;
        return tmp;
    }

    void PM_MakeButton(GameObject parent, string label,
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

        var btn  = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
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
        tmp.text          = label;
        tmp.fontSize      = 24;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        PM_Stretch(tmp.rectTransform);
    }

    Slider PM_MakeSlider(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Color fillColor, float startValue)
    {
        var trackGO  = new GameObject(name + "_Track");
        trackGO.transform.SetParent(parent.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        var trackRt  = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = anchorMin;
        trackRt.anchorMax = anchorMax;
        trackRt.offsetMin = Vector2.zero;
        trackRt.offsetMax = Vector2.zero;

        var faGO = new GameObject("Fill Area");
        faGO.transform.SetParent(trackGO.transform, false);
        var faRt = faGO.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.1f);
        faRt.anchorMax = new Vector2(1f, 0.9f);
        faRt.offsetMin = new Vector2(5f, 0f);
        faRt.offsetMax = new Vector2(-15f, 0f);

        var fillGO  = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = fillColor;
        var fillRt  = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(trackGO.transform, false);
        var haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        var handleGO  = new GameObject("Handle");
        handleGO.transform.SetParent(handleArea.transform, false);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRt  = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(24f, 0f);

        var slider = trackGO.AddComponent<Slider>();
        slider.fillRect   = fillRt;
        slider.handleRect = handleRt;
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = 0f;
        slider.maxValue   = 1f;
        slider.value      = startValue;
        return slider;
    }

    void PM_Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void PM_Place(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}