using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    private enum GameOverButtonSelection
    {
        Retry,
        Title
    }

    [Header("Scene Names")]
    [SerializeField] private string retrySceneName = "SampleScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Button Selection")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private RectTransform retryButtonTransform;
    [SerializeField] private RectTransform titleButtonTransform;
    [SerializeField, Min(1f)] private float selectedScaleMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float scaleChangeSpeed = 8f;

    private GameOverButtonSelection currentSelection;
    private Vector3 retryOriginalScale;
    private Vector3 titleOriginalScale;
    private bool hasCapturedOriginalScales;
    private bool buttonsWereReady;
    private bool allKeyboardInputReleased;
    private bool keyboardInputEnabled;
    private bool hasConfirmed;
    private readonly JoyConMenuInput joyConMenuInput = new JoyConMenuInput();
    private bool joyConInputArmed;

    private void Awake()
    {
        CacheButtonReferences();
        CaptureOriginalScales();
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
            allKeyboardInputReleased = false;
            keyboardInputEnabled = false;
            return;
        }

        if (!buttonsWereReady)
        {
            buttonsWereReady = true;
            allKeyboardInputReleased = false;
            keyboardInputEnabled = false;
            SelectButton(GameOverButtonSelection.Retry);
        }

        Keyboard keyboard = Keyboard.current;
        if (!CanAcceptKeyboardInput(keyboard))
        {
            return;
        }

        JoyConMenuInputFrame joyConInput = ReadJoyConMenuInput();

        if (keyboard.leftArrowKey.wasPressedThisFrame || joyConInput.HorizontalStep < 0)
        {
            SelectButton(GameOverButtonSelection.Title);
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || joyConInput.HorizontalStep > 0)
        {
            SelectButton(GameOverButtonSelection.Retry);
        }

        if (keyboard.spaceKey.wasPressedThisFrame || joyConInput.ConfirmPressed)
        {
            ConfirmSelectedButton();
        }
    }

    public void OnRetryButtonPressed()
    {
        if (!TryBeginConfirmation())
        {
            return;
        }

        Time.timeScale = 1f;
        LoadScene(retrySceneName, "RetryButton");
    }

    public void OnTitleButtonPressed()
    {
        if (!TryBeginConfirmation())
        {
            return;
        }

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

        if (retryButton == null || titleButton == null ||
            retryButtonTransform == null || titleButtonTransform == null)
        {
            Debug.LogWarning("GameOverManager: Retry / Title Buttonの参照が不足しています。", this);
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

    private void ResetSelectionState()
    {
        currentSelection = GameOverButtonSelection.Retry;
        buttonsWereReady = false;
        allKeyboardInputReleased = false;
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

        // 前SceneからのStick/X入力を破棄し、倒しっぱなしならHelper内のLockを維持します。
        joyConInputArmed = true;
        return JoyConMenuInputFrame.None;
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

        bool inputIsPressed = keyboard.leftArrowKey.isPressed ||
                              keyboard.rightArrowKey.isPressed ||
                              keyboard.spaceKey.isPressed;
        if (inputIsPressed)
        {
            allKeyboardInputReleased = false;
            return false;
        }

        if (!allKeyboardInputReleased)
        {
            allKeyboardInputReleased = true;
            return false;
        }

        keyboardInputEnabled = true;
        SelectButton(currentSelection);
        return false;
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

    private void SelectButton(GameOverButtonSelection selection)
    {
        currentSelection = selection;
        Button selectedButton = GetButton(selection);
        if (EventSystem.current != null && selectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }
    }

    private void ConfirmSelectedButton()
    {
        Button selectedButton = GetButton(currentSelection);
        if (IsButtonReady(selectedButton))
        {
            selectedButton.onClick.Invoke();
        }
    }

    private Button GetButton(GameOverButtonSelection selection)
    {
        return selection == GameOverButtonSelection.Retry ? retryButton : titleButton;
    }

    private void UpdateButtonScales()
    {
        if (!hasCapturedOriginalScales)
        {
            return;
        }

        Vector3 retryTarget = buttonsWereReady && currentSelection == GameOverButtonSelection.Retry
            ? retryOriginalScale * selectedScaleMultiplier
            : retryOriginalScale;
        Vector3 titleTarget = buttonsWereReady && currentSelection == GameOverButtonSelection.Title
            ? titleOriginalScale * selectedScaleMultiplier
            : titleOriginalScale;

        retryButtonTransform.localScale = MoveScale(retryButtonTransform.localScale, retryTarget);
        titleButtonTransform.localScale = MoveScale(titleButtonTransform.localScale, titleTarget);
    }

    private Vector3 MoveScale(Vector3 current, Vector3 target)
    {
        if (scaleChangeSpeed <= 0f)
        {
            return target;
        }

        float interpolation = 1f - Mathf.Exp(-scaleChangeSpeed * Time.unscaledDeltaTime);
        Vector3 next = Vector3.Lerp(current, target, interpolation);
        return (next - target).sqrMagnitude < 0.000001f ? target : next;
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

    private void LoadScene(string sceneName, string buttonName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"GameOverManager: Scene name for {buttonName} is empty.", this);
            hasConfirmed = false;
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
