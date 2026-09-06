using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// TitleSceneの「デモ → タイトル → 操作選択」を管理します。
/// ゲーム本編の操作方式はまだ切り替えず、選択後はSampleSceneへ移動します。
/// </summary>
public class TitleSceneUIController : MonoBehaviour
{
    private enum ScreenState
    {
        Demo,
        Title,
        ControlSelect
    }

    private enum TitleSelection
    {
        Start,
        Quit
    }

    private enum ControlSelection
    {
        None,
        Keyboard,
        JoyCon
    }

    private enum DefaultControlType
    {
        Keyboard,
        JoyCon
    }

    [Header("デモフロー")]
    [Tooltip("ONなら既存のデモ映像フローを使用します。OFFならタイトル画面から直接開始します。")]
    [SerializeField] private bool useDemoFlow = false;

    [Header("画面Root")]
    [Tooltip("既存の動画表示Canvasを設定します。")]
    [SerializeField] private GameObject demoRoot;

    [Tooltip("タイトル画面全体の親Objectです。")]
    [SerializeField] private GameObject titleRoot;

    [Tooltip("操作選択画面全体の親Objectです。")]
    [SerializeField] private GameObject controlSelectRoot;

    [Header("既存デモ")]
    [Tooltip("既存のデモ用VideoPlayerです。ループは自動的にOFFになります。")]
    [SerializeField] private VideoPlayer demoVideoPlayer;

    [Tooltip("動画終了通知が使えない場合の予備時間です。0なら時間判定を使いません。")]
    [SerializeField, Min(0f)] private float demoDurationSeconds;

    [Header("タイトル画面")]
    [Tooltip("スタートボタンのRectTransformです。")]
    [SerializeField] private RectTransform startButtonTransform;

    [Tooltip("ゲーム終了ボタンのRectTransformです。")]
    [SerializeField] private RectTransform quitButtonTransform;

    [Tooltip("タイトル画面でこの秒数操作がなければデモへ戻ります。")]
    [SerializeField, Min(0.1f)] private float titleIdleDemoSeconds = 10f;

    [Header("操作選択画面")]
    [Tooltip("キーボードボタンのRectTransformです。")]
    [SerializeField] private RectTransform keyboardButtonTransform;

    [Tooltip("Joy-Con操作ボタンのRectTransformです。")]
    [SerializeField] private RectTransform joyConButtonTransform;

    [Header("操作選択フロー")]
    [Tooltip("ONなら既存の操作選択画面を表示します。OFFなら既定の操作方法でゲームへ直接進みます。")]
    [SerializeField] private bool useControlSelectFlow = false;

    [Tooltip("操作選択画面をスキップした時に使用する操作方法です。")]
    [SerializeField] private DefaultControlType defaultControlType = DefaultControlType.Keyboard;

    [Header("選択中の大きさ")]
    [Tooltip("未選択または通常状態のボタンサイズです。")]
    [SerializeField, Min(0f)] private float normalButtonScale = 1f;

    [Tooltip("選択中のボタンサイズです。")]
    [SerializeField, Min(0f)] private float selectedButtonScale = 1.3f;

    [Header("日本語表示")]
    [Tooltip("日本語を表示するための元フォントです。")]
    [SerializeField] private Font japaneseSourceFont;

    [Tooltip("上の日本語フォントを適用するTextMeshProです。")]
    [SerializeField] private TMP_Text[] japaneseTexts;

    [Header("画面遷移")]
    [Tooltip("操作方法を決定した後に移動するScene名です。")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private ScreenState currentState;
    private TitleSelection titleSelection;
    private ControlSelection controlSelection = ControlSelection.None;
    private float titleIdleSeconds;
    private float demoElapsedSeconds;
    private bool isLoadingScene;

    private Button startButton;
    private Button quitButton;
    private Button keyboardButton;
    private Button joyConButton;
    private TMP_FontAsset runtimeJapaneseFontAsset;

    private void Awake()
    {
        WarnAboutMissingReferences();
        ApplyJapaneseFont();
        RegisterButtonListeners();

        if (demoVideoPlayer != null)
        {
            demoVideoPlayer.loopPointReached += OnDemoFinished;
        }
    }

    private void Start()
    {
        if (useDemoFlow)
        {
            ShowDemo();
        }
        else
        {
            ShowTitle();
        }
    }

    private void Update()
    {
        switch (currentState)
        {
            case ScreenState.Demo:
                UpdateDemo();
                break;

            case ScreenState.Title:
                UpdateTitle();
                break;

            case ScreenState.ControlSelect:
                UpdateControlSelect();
                break;
        }
    }

    private void OnDestroy()
    {
        if (demoVideoPlayer != null)
        {
            demoVideoPlayer.loopPointReached -= OnDemoFinished;
        }

        RemoveButtonListeners();

        if (runtimeJapaneseFontAsset != null)
        {
            Destroy(runtimeJapaneseFontAsset);
        }
    }

    private void UpdateDemo()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            ShowTitle();
            return;
        }

        if (demoDurationSeconds <= 0f)
        {
            return;
        }

        demoElapsedSeconds += Time.unscaledDeltaTime;
        if (demoElapsedSeconds >= demoDurationSeconds)
        {
            ShowTitle();
        }
    }

    private void UpdateTitle()
    {
        if (useDemoFlow)
        {
            bool hasKeyboardInput = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool hasMouseInput = HasMouseActivityThisFrame();

            if (hasKeyboardInput || hasMouseInput)
            {
                titleIdleSeconds = 0f;
            }
            else
            {
                titleIdleSeconds += Time.unscaledDeltaTime;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                SelectTitleButton(TitleSelection.Start);
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                SelectTitleButton(TitleSelection.Quit);
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ConfirmTitleSelection();
                return;
            }
        }

        if (useDemoFlow && titleIdleSeconds >= titleIdleDemoSeconds)
        {
            ShowDemo();
        }
    }

    private void UpdateControlSelect()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            SelectControlButton(ControlSelection.Keyboard);
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            SelectControlButton(ControlSelection.JoyCon);
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ConfirmControlSelection();
        }
    }

    private void ShowDemo()
    {
        currentState = ScreenState.Demo;
        demoElapsedSeconds = 0f;
        ClearEventSystemSelection();
        SetOnlyRootActive(demoRoot);

        if (demoVideoPlayer == null)
        {
            Debug.LogWarning("TitleSceneUIController: Demo Video Playerが未設定です。Demo Duration Secondsでタイトルへ進みます。", this);
            return;
        }

        demoVideoPlayer.Stop();
        demoVideoPlayer.isLooping = false;

        if (demoVideoPlayer.canSetTime)
        {
            demoVideoPlayer.time = 0d;
        }

        demoVideoPlayer.Play();
    }

    private void ShowTitle()
    {
        currentState = ScreenState.Title;
        titleIdleSeconds = 0f;
        StopDemoVideo();
        ClearEventSystemSelection();
        SetOnlyRootActive(titleRoot);
        SelectTitleButton(TitleSelection.Start);
    }

    private void ShowControlSelect()
    {
        currentState = ScreenState.ControlSelect;
        controlSelection = ControlSelection.None;
        ClearEventSystemSelection();
        SetOnlyRootActive(controlSelectRoot);
        SetButtonScale(keyboardButtonTransform, normalButtonScale);
        SetButtonScale(joyConButtonTransform, normalButtonScale);
    }

    private void OnDemoFinished(VideoPlayer finishedPlayer)
    {
        if (currentState == ScreenState.Demo)
        {
            ShowTitle();
        }
    }

    private void StopDemoVideo()
    {
        if (demoVideoPlayer != null)
        {
            demoVideoPlayer.Stop();
        }
    }

    private void SelectTitleButton(TitleSelection selection)
    {
        titleSelection = selection;
        SetButtonScale(startButtonTransform, selection == TitleSelection.Start ? selectedButtonScale : normalButtonScale);
        SetButtonScale(quitButtonTransform, selection == TitleSelection.Quit ? selectedButtonScale : normalButtonScale);
    }

    private void ConfirmTitleSelection()
    {
        if (titleSelection == TitleSelection.Start)
        {
            StartGameFromTitle();
            return;
        }

        QuitGame();
    }

    private void StartGameFromTitle()
    {
        if (useControlSelectFlow)
        {
            ShowControlSelect();
            return;
        }

        ControlSelection defaultSelection = defaultControlType == DefaultControlType.JoyCon
            ? ControlSelection.JoyCon
            : ControlSelection.Keyboard;

        SelectControlButton(defaultSelection);
        ConfirmControlSelection();
    }

    private void SelectControlButton(ControlSelection selection)
    {
        controlSelection = selection;
        SetButtonScale(keyboardButtonTransform, selection == ControlSelection.Keyboard ? selectedButtonScale : normalButtonScale);
        SetButtonScale(joyConButtonTransform, selection == ControlSelection.JoyCon ? selectedButtonScale : normalButtonScale);
    }

    private void ConfirmControlSelection()
    {
        if (controlSelection == ControlSelection.None)
        {
            Debug.Log("操作方法を選択してください", this);
            return;
        }

        GameplayControlType selectedControlType = controlSelection == ControlSelection.JoyCon
            ? GameplayControlType.JoyCon
            : GameplayControlType.Keyboard;
        ControlSelectionSession.SetSelection(selectedControlType);

        LoadGameScene();
    }

    /// <summary>Startボタンをマウスで押した時に呼ばれます。</summary>
    public void OnStartButtonClicked()
    {
        if (currentState == ScreenState.Title)
        {
            StartGameFromTitle();
        }
    }

    /// <summary>終了ボタンをマウスで押した時に呼ばれます。</summary>
    public void OnQuitButtonClicked()
    {
        if (currentState == ScreenState.Title)
        {
            QuitGame();
        }
    }

    /// <summary>キーボードボタンは、マウスクリック時に選択と決定を同時に行います。</summary>
    public void OnKeyboardButtonClicked()
    {
        if (currentState != ScreenState.ControlSelect)
        {
            return;
        }

        SelectControlButton(ControlSelection.Keyboard);
        ConfirmControlSelection();
    }

    /// <summary>Joy-Conボタンは見た目だけ選択し、今回は操作方式を変更せずゲームへ進みます。</summary>
    public void OnJoyConButtonClicked()
    {
        if (currentState != ScreenState.ControlSelect)
        {
            return;
        }

        SelectControlButton(ControlSelection.JoyCon);
        ConfirmControlSelection();
    }

    private void LoadGameScene()
    {
        if (isLoadingScene)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogWarning("TitleSceneUIController: Game Scene Nameが未設定です。", this);
            return;
        }

        isLoadingScene = true;
        SceneManager.LoadScene(gameSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("ゲーム終了", this);
#else
        Application.Quit();
#endif
    }

    private void SetOnlyRootActive(GameObject activeRoot)
    {
        SetRootActive(demoRoot, demoRoot == activeRoot);
        SetRootActive(titleRoot, titleRoot == activeRoot);
        SetRootActive(controlSelectRoot, controlSelectRoot == activeRoot);
    }

    private static void SetRootActive(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }

    private static void SetButtonScale(RectTransform target, float scale)
    {
        if (target != null)
        {
            target.localScale = Vector3.one * scale;
        }
    }

    private static bool HasMouseActivityThisFrame()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        return mouse.delta.ReadValue().sqrMagnitude > 0f ||
               mouse.scroll.ReadValue().sqrMagnitude > 0f ||
               mouse.leftButton.wasPressedThisFrame ||
               mouse.rightButton.wasPressedThisFrame ||
               mouse.middleButton.wasPressedThisFrame;
    }

    private static void ClearEventSystemSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void RegisterButtonListeners()
    {
        startButton = GetButton(startButtonTransform, "Start Button Transform");
        quitButton = GetButton(quitButtonTransform, "Quit Button Transform");
        keyboardButton = GetButton(keyboardButtonTransform, "Keyboard Button Transform");
        joyConButton = GetButton(joyConButtonTransform, "JoyCon Button Transform");

        startButton?.onClick.AddListener(OnStartButtonClicked);
        quitButton?.onClick.AddListener(OnQuitButtonClicked);
        keyboardButton?.onClick.AddListener(OnKeyboardButtonClicked);
        joyConButton?.onClick.AddListener(OnJoyConButtonClicked);
    }

    private void RemoveButtonListeners()
    {
        startButton?.onClick.RemoveListener(OnStartButtonClicked);
        quitButton?.onClick.RemoveListener(OnQuitButtonClicked);
        keyboardButton?.onClick.RemoveListener(OnKeyboardButtonClicked);
        joyConButton?.onClick.RemoveListener(OnJoyConButtonClicked);
    }

    private Button GetButton(RectTransform target, string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"TitleSceneUIController: {fieldName}にButtonコンポーネントがありません。", this);
        }

        return button;
    }

    private void ApplyJapaneseFont()
    {
        if (japaneseSourceFont == null || japaneseTexts == null || japaneseTexts.Length == 0)
        {
            return;
        }

        runtimeJapaneseFontAsset = TMP_FontAsset.CreateFontAsset(japaneseSourceFont);
        if (runtimeJapaneseFontAsset == null)
        {
            Debug.LogWarning("TitleSceneUIController: 日本語用TMP Font Assetを作成できませんでした。", this);
            return;
        }

        foreach (TMP_Text text in japaneseTexts)
        {
            if (text != null)
            {
                text.font = runtimeJapaneseFontAsset;
            }
        }
    }

    private void WarnAboutMissingReferences()
    {
        if (demoRoot == null)
        {
            Debug.LogWarning("TitleSceneUIController: Demo Rootが未設定です。", this);
        }

        if (titleRoot == null)
        {
            Debug.LogWarning("TitleSceneUIController: Title Rootが未設定です。", this);
        }

        if (controlSelectRoot == null)
        {
            Debug.LogWarning("TitleSceneUIController: Control Select Rootが未設定です。", this);
        }

        if (demoVideoPlayer == null)
        {
            Debug.LogWarning("TitleSceneUIController: Demo Video Playerが未設定です。", this);
        }

        if (startButtonTransform == null)
        {
            Debug.LogWarning("TitleSceneUIController: Start Button Transformが未設定です。", this);
        }

        if (quitButtonTransform == null)
        {
            Debug.LogWarning("TitleSceneUIController: Quit Button Transformが未設定です。", this);
        }

        if (keyboardButtonTransform == null)
        {
            Debug.LogWarning("TitleSceneUIController: Keyboard Button Transformが未設定です。", this);
        }

        if (joyConButtonTransform == null)
        {
            Debug.LogWarning("TitleSceneUIController: JoyCon Button Transformが未設定です。", this);
        }

        if (japaneseSourceFont == null)
        {
            Debug.LogWarning("TitleSceneUIController: Japanese Source Fontが未設定です。日本語が正しく表示されない場合があります。", this);
        }
    }
}
