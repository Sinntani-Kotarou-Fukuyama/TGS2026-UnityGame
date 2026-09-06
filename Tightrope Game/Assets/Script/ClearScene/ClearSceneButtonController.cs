using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClearSceneButtonController : MonoBehaviour
{
    private enum ClearButtonSelection
    {
        Retry,
        Title
    }

    private static readonly string[] ResetTargetTypeNames =
    {
        "PlayerGameFeedback",
        "PlayerGameFeedbackController",
        "GameManager",
        "BalanceManager"
    };

    private static readonly string[] ResetMethodNames =
    {
        "ResetDamage",
        "ResetBalance",
        "ResetMissCount",
        "ResetMistakeCount",
        "ResetGameState"
    };

    [Header("Scene Names")]
    [SerializeField] private string retrySceneName = "SampleScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Optional Retry Reset Targets")]
    [SerializeField] private MonoBehaviour[] retryResetTargets;

    [Header("Keyboard Button Selection")]
    [Tooltip("既存のRetryボタンです。Space決定時もこのButtonのonClickを再利用します。")]
    [SerializeField] private Button retryButton;

    [Tooltip("既存のTitleボタンです。Space決定時もこのButtonのonClickを再利用します。")]
    [SerializeField] private Button titleButton;

    [Tooltip("Retryボタン全体を拡大するRectTransformです。")]
    [SerializeField] private RectTransform retryButtonTransform;

    [Tooltip("Titleボタン全体を拡大するRectTransformです。")]
    [SerializeField] private RectTransform titleButtonTransform;

    [Tooltip("選択中のボタンを元のScaleの何倍にするかを指定します。")]
    [SerializeField, Min(1f)] private float selectedScaleMultiplier = 1.15f;

    [Tooltip("選択時の拡大・縮小速度です。Time.timeScaleの影響は受けません。")]
    [SerializeField, Min(0f)] private float scaleChangeSpeed = 8f;

    [Tooltip("ClearScene表示時に最初から選択しておくボタンです。")]
    [SerializeField] private ClearButtonSelection defaultSelectedButton = ClearButtonSelection.Retry;

    private ClearButtonSelection currentSelection;
    private ClearButtonSelection leftSelection;
    private ClearButtonSelection rightSelection;
    private Vector3 retryOriginalScale;
    private Vector3 titleOriginalScale;
    private bool hasCapturedOriginalScales;
    private bool buttonsWereReady;
    private bool allInputReleased;
    private bool keyboardInputEnabled;
    private bool hasConfirmed;
    private readonly JoyConMenuInput joyConMenuInput = new JoyConMenuInput();
    private bool joyConInputArmed;

    private void Awake()
    {
        CacheButtonReferences();
        CaptureOriginalScales();
        ResolveHorizontalOrder();
    }

    private void OnEnable()
    {
        ResetSelectionState();
    }

    private void Update()
    {
        UpdateButtonScales();

        if (hasConfirmed)
        {
            return;
        }

        if (!AreButtonsReady())
        {
            buttonsWereReady = false;
            allInputReleased = false;
            keyboardInputEnabled = false;
            return;
        }

        if (!buttonsWereReady)
        {
            buttonsWereReady = true;
            allInputReleased = false;
            keyboardInputEnabled = false;
            SelectButton(defaultSelectedButton);
        }

        Keyboard keyboard = Keyboard.current;
        if (!CanAcceptKeyboardInput(keyboard))
        {
            return;
        }

        JoyConMenuInputFrame joyConInput = ReadJoyConMenuInput();

        if (keyboard.leftArrowKey.wasPressedThisFrame || joyConInput.HorizontalStep < 0)
        {
            SelectButton(leftSelection);
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || joyConInput.HorizontalStep > 0)
        {
            SelectButton(rightSelection);
        }

        if (keyboard.spaceKey.wasPressedThisFrame || joyConInput.ConfirmPressed)
        {
            Button selectedButton = GetButton(currentSelection);
            if (IsButtonReady(selectedButton))
            {
                selectedButton.onClick.Invoke();
            }
        }
    }

    public void OnRetryButtonPressed()
    {
        if (!TryBeginConfirmation())
        {
            return;
        }

        Debug.Log($"{nameof(ClearSceneButtonController)}: RetryButton pressed.", this);
        LoadRetryScene();
    }

    public void OnTitleButtonPressed()
    {
        if (!TryBeginConfirmation())
        {
            return;
        }

        Debug.Log($"{nameof(ClearSceneButtonController)}: TitleButton pressed.", this);
        Time.timeScale = 1f;
        LoadScene(titleSceneName, "TitleButton");
    }

    private void CacheButtonReferences()
    {
        if (retryButton == null && retryButtonTransform != null)
        {
            retryButton = retryButtonTransform.GetComponent<Button>();
        }

        if (titleButton == null && titleButtonTransform != null)
        {
            titleButton = titleButtonTransform.GetComponent<Button>();
        }

        if (retryButtonTransform == null && retryButton != null)
        {
            retryButtonTransform = retryButton.transform as RectTransform;
        }

        if (titleButtonTransform == null && titleButton != null)
        {
            titleButtonTransform = titleButton.transform as RectTransform;
        }

        if (retryButton == null || titleButton == null || retryButtonTransform == null || titleButtonTransform == null)
        {
            Debug.LogWarning($"{nameof(ClearSceneButtonController)}: Retry / TitleのButtonまたはRectTransform参照が不足しています。キーボード操作を無効にします。", this);
        }
    }

    private void CaptureOriginalScales()
    {
        if (retryButtonTransform == null || titleButtonTransform == null)
        {
            hasCapturedOriginalScales = false;
            return;
        }

        retryOriginalScale = retryButtonTransform.localScale;
        titleOriginalScale = titleButtonTransform.localScale;
        hasCapturedOriginalScales = true;
    }

    private void ResolveHorizontalOrder()
    {
        if (retryButtonTransform == null || titleButtonTransform == null)
        {
            leftSelection = ClearButtonSelection.Title;
            rightSelection = ClearButtonSelection.Retry;
            return;
        }

        bool retryIsLeft = retryButtonTransform.position.x <= titleButtonTransform.position.x;
        leftSelection = retryIsLeft ? ClearButtonSelection.Retry : ClearButtonSelection.Title;
        rightSelection = retryIsLeft ? ClearButtonSelection.Title : ClearButtonSelection.Retry;
    }

    private void ResetSelectionState()
    {
        currentSelection = defaultSelectedButton;
        buttonsWereReady = false;
        allInputReleased = false;
        keyboardInputEnabled = false;
        hasConfirmed = false;
        joyConMenuInput.Reset();
        joyConInputArmed = false;

        if (hasCapturedOriginalScales)
        {
            retryButtonTransform.localScale = retryOriginalScale;
            titleButtonTransform.localScale = titleOriginalScale;
        }
    }

    private JoyConMenuInputFrame ReadJoyConMenuInput()
    {
        if (!ControlSelectionSession.HasSelection ||
            ControlSelectionSession.SelectedControlType != GameplayControlType.JoyCon)
        {
            return JoyConMenuInputFrame.None;
        }

        JoyConMenuInputFrame input = joyConMenuInput.Read();
        if (joyConInputArmed)
        {
            return input;
        }

        // 最初のReadを破棄して、前Sceneから倒しっぱなしのStickをHelper内でLockします。
        // XのGetButtonDownも同じフレームでは決定に使用しません。
        joyConInputArmed = true;
        return JoyConMenuInputFrame.None;
    }

    private bool AreButtonsReady()
    {
        return hasCapturedOriginalScales &&
               IsButtonReady(retryButton) &&
               IsButtonReady(titleButton);
    }

    private static bool IsButtonReady(Button button)
    {
        return button != null &&
               button.gameObject.activeInHierarchy &&
               button.isActiveAndEnabled &&
               button.IsInteractable();
    }

    private bool CanAcceptKeyboardInput(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return false;
        }

        if (keyboardInputEnabled)
        {
            return true;
        }

        bool selectionInputIsPressed = keyboard.leftArrowKey.isPressed ||
                                       keyboard.rightArrowKey.isPressed ||
                                       keyboard.spaceKey.isPressed;

        if (selectionInputIsPressed)
        {
            allInputReleased = false;
            return false;
        }

        if (!allInputReleased)
        {
            allInputReleased = true;
            return false;
        }

        // 全キー解放を確認した次のフレームも待ってから、持ち越し入力を受け付けます。
        keyboardInputEnabled = true;
        SelectButton(currentSelection);
        return false;
    }

    private void SelectButton(ClearButtonSelection selection)
    {
        currentSelection = selection;
        Button selectedButton = GetButton(selection);

        if (keyboardInputEnabled && EventSystem.current != null && selectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }
    }

    private Button GetButton(ClearButtonSelection selection)
    {
        return selection == ClearButtonSelection.Retry ? retryButton : titleButton;
    }

    private void UpdateButtonScales()
    {
        if (!hasCapturedOriginalScales)
        {
            return;
        }

        bool showSelection = buttonsWereReady;
        Vector3 retryTargetScale = showSelection && currentSelection == ClearButtonSelection.Retry
            ? retryOriginalScale * selectedScaleMultiplier
            : retryOriginalScale;
        Vector3 titleTargetScale = showSelection && currentSelection == ClearButtonSelection.Title
            ? titleOriginalScale * selectedScaleMultiplier
            : titleOriginalScale;

        retryButtonTransform.localScale = MoveScale(retryButtonTransform.localScale, retryTargetScale);
        titleButtonTransform.localScale = MoveScale(titleButtonTransform.localScale, titleTargetScale);
    }

    private Vector3 MoveScale(Vector3 currentScale, Vector3 targetScale)
    {
        if (scaleChangeSpeed <= 0f)
        {
            return targetScale;
        }

        float interpolation = 1f - Mathf.Exp(-scaleChangeSpeed * Time.unscaledDeltaTime);
        Vector3 nextScale = Vector3.Lerp(currentScale, targetScale, interpolation);
        return (nextScale - targetScale).sqrMagnitude < 0.000001f ? targetScale : nextScale;
    }

    private bool TryBeginConfirmation()
    {
        if (hasConfirmed)
        {
            return false;
        }

        hasConfirmed = true;
        keyboardInputEnabled = false;
        return true;
    }

    private void LoadRetryScene()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(retrySceneName))
        {
            Debug.LogWarning($"{nameof(ClearSceneButtonController)}: Scene name for RetryButton is empty.", this);
            return;
        }

        SceneManager.sceneLoaded += OnRetrySceneLoaded;
        DontDestroyOnLoad(gameObject);
        SceneManager.LoadScene(retrySceneName);
    }

    private void OnRetrySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != retrySceneName)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnRetrySceneLoaded;
        ResetRunStateForRetry();
        Destroy(gameObject);
    }

    private void ResetRunStateForRetry()
    {
        Time.timeScale = 1f;

        bool resetApplied = TryResetAssignedTargets();

        if (!resetApplied)
        {
            resetApplied = TryResetFoundTargets();
        }

        if (!resetApplied)
        {
            Debug.LogWarning($"{nameof(ClearSceneButtonController)}: reset target not found.", this);
        }
    }

    private bool TryResetAssignedTargets()
    {
        if (retryResetTargets == null || retryResetTargets.Length == 0)
        {
            return false;
        }

        bool resetApplied = false;

        foreach (MonoBehaviour resetTarget in retryResetTargets)
        {
            if (resetTarget == null)
            {
                continue;
            }

            resetApplied |= TryInvokeResetMethod(resetTarget);
        }

        return resetApplied;
    }

    private bool TryResetFoundTargets()
    {
        bool resetApplied = false;
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || !IsResetTargetType(behaviour.GetType()))
            {
                continue;
            }

            resetApplied |= TryInvokeResetMethod(behaviour);
        }

        return resetApplied;
    }

    private bool IsResetTargetType(System.Type type)
    {
        string typeName = type.Name;

        foreach (string resetTargetTypeName in ResetTargetTypeNames)
        {
            if (typeName == resetTargetTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryInvokeResetMethod(MonoBehaviour resetTarget)
    {
        System.Type targetType = resetTarget.GetType();

        foreach (string resetMethodName in ResetMethodNames)
        {
            MethodInfo method = targetType.GetMethod(
                resetMethodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                System.Type.EmptyTypes,
                null);

            if (method == null)
            {
                continue;
            }

            try
            {
                method.Invoke(resetTarget, null);
                Debug.Log($"{nameof(ClearSceneButtonController)}: Invoked {targetType.Name}.{resetMethodName}().", resetTarget);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"{nameof(ClearSceneButtonController)}: Failed to invoke {targetType.Name}.{resetMethodName}(). {exception.Message}", resetTarget);
            }
        }

        return false;
    }

    private void LoadScene(string sceneName, string buttonName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"{nameof(ClearSceneButtonController)}: Scene name for {buttonName} is empty.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
