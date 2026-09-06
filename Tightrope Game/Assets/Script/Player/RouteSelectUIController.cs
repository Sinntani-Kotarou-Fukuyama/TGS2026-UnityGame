using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分岐地点で表示するルート選択UIの見た目だけを管理します。
/// ルート移動や決定入力はTightropeRouteControllerが担当します。
/// </summary>
public class RouteSelectUIController : MonoBehaviour
{
    [Header("ルート選択UI")]
    [Tooltip("RouteSelectPanelを設定します。このスクリプトをPanel自身に付けた場合は未設定でも自動取得します。")]
    [SerializeField] private GameObject routeSelectPanel;

    [Tooltip("『どちらの綱を渡る？』を表示するTextMeshProです。")]
    [SerializeField] private TMP_Text questionText;

    [Tooltip("左矢印ImageのRectTransformです。")]
    [SerializeField] private RectTransform leftArrowTransform;

    [Tooltip("右矢印ImageのRectTransformです。")]
    [SerializeField] private RectTransform rightArrowTransform;

    [Tooltip("『Spaceキーで決定』を表示するTextMeshProです。")]
    [SerializeField] private TMP_Text decideText;

    [Tooltip("左ルートをHold中に表示する円ゲージです。")]
    [SerializeField] private Image leftHoldCircleImage;

    [Tooltip("右ルートをHold中に表示する円ゲージです。")]
    [SerializeField] private Image rightHoldCircleImage;

    [Header("矢印サイズ")]
    [Tooltip("未選択時の矢印サイズです。")]
    [SerializeField, Min(0f)] private float normalArrowScale = 1.0f;

    [Tooltip("選択中の矢印サイズです。")]
    [SerializeField, Min(0f)] private float selectedArrowScale = 1.3f;

    [Header("バランスゲージ")]
    [Tooltip("分岐選択中だけ非表示にするBalanceGaugeの親Objectです。")]
    [SerializeField] private GameObject balanceGaugeRoot;

    [Header("数字カウント表示")]
    [Tooltip("分岐選択中だけ非表示にする数字カウント表示の親Objectです。")]
    [SerializeField] private GameObject countDisplayRoot;

    private string originalDecideText;
    private bool hasCapturedOriginalDecideText;
    private bool keepNormalBalanceUiHidden;
    private bool hasSavedTrolleyBalanceUiState;
    private bool balanceGaugeWasActiveBeforeTrolley;
    private bool countDisplayWasActiveBeforeTrolley;
    private bool immediateSelectionMode;
    private bool joyConHoldMode;

    private const string JoyConHoldGuideText = "行きたい方向にJoy-Conを傾け続けよう！";

    private void Awake()
    {
        if (routeSelectPanel == null)
        {
            routeSelectPanel = gameObject;
        }

        CaptureOriginalDecideText();
        WarnAboutMissingReferences();
        ResetArrowScales();
        SetJoyConHoldProgress(0f, 0f);
        SetBalanceGaugeVisible(true);
        SetCountDisplayVisible(true);
    }

    /// <summary>Trolley方式では左右キー自体が決定入力であることを表示します。</summary>
    public void SetImmediateSelectionMode(bool immediateSelection)
    {
        CaptureOriginalDecideText();
        immediateSelectionMode = immediateSelection;
        UpdateDecideText();
    }

    /// <summary>Joy-Con選択時だけHold操作の案内へ切り替えます。</summary>
    public void SetJoyConHoldMode(bool enabled)
    {
        joyConHoldMode = enabled;
        UpdateDecideText();
        SetJoyConHoldProgress(0f, 0f);
    }

    /// <summary>左右のHold円ゲージを0～1で更新します。</summary>
    public void SetJoyConHoldProgress(float leftProgress, float rightProgress)
    {
        if (leftHoldCircleImage != null)
        {
            leftHoldCircleImage.fillAmount = Mathf.Clamp01(leftProgress);
        }

        if (rightHoldCircleImage != null)
        {
            rightHoldCircleImage.fillAmount = Mathf.Clamp01(rightProgress);
        }
    }

    /// <summary>Trolley方式の間、旧BalanceGaugeと通常カウントを非表示のまま保ちます。</summary>
    public void SetTrolleyNormalBalanceUiHidden(bool hidden)
    {
        if (keepNormalBalanceUiHidden == hidden)
        {
            return;
        }

        if (hidden)
        {
            balanceGaugeWasActiveBeforeTrolley = balanceGaugeRoot != null && balanceGaugeRoot.activeSelf;
            countDisplayWasActiveBeforeTrolley = countDisplayRoot != null && countDisplayRoot.activeSelf;
            hasSavedTrolleyBalanceUiState = true;
            keepNormalBalanceUiHidden = true;

            if (balanceGaugeRoot != null)
            {
                balanceGaugeRoot.SetActive(false);
            }

            if (countDisplayRoot != null)
            {
                countDisplayRoot.SetActive(false);
            }

            return;
        }

        keepNormalBalanceUiHidden = false;
        if (!hasSavedTrolleyBalanceUiState)
        {
            return;
        }

        if (balanceGaugeRoot != null)
        {
            balanceGaugeRoot.SetActive(balanceGaugeWasActiveBeforeTrolley);
        }

        if (countDisplayRoot != null)
        {
            countDisplayRoot.SetActive(countDisplayWasActiveBeforeTrolley);
        }

        hasSavedTrolleyBalanceUiState = false;
    }

    /// <summary>分岐地点でUIを表示し、左右とも未選択状態に戻します。</summary>
    public void ShowRouteSelection()
    {
        ResetArrowScales();
        SetJoyConHoldProgress(0f, 0f);
        SetRouteSelectVisible(true);
        SetBalanceGaugeVisible(false);
        SetCountDisplayVisible(false);
    }

    /// <summary>左矢印だけを選択サイズにします。</summary>
    public void ShowLeftSelected()
    {
        SetArrowScales(selectedArrowScale, normalArrowScale);
    }

    /// <summary>右矢印だけを選択サイズにします。</summary>
    public void ShowRightSelected()
    {
        SetArrowScales(normalArrowScale, selectedArrowScale);
    }

    /// <summary>決定後にUIを隠し、バランスゲージと数字カウントを再表示します。</summary>
    public void HideRouteSelection()
    {
        ResetArrowScales();
        SetJoyConHoldProgress(0f, 0f);
        SetRouteSelectVisible(false);
        SetBalanceGaugeVisible(true);
        SetCountDisplayVisible(true);
    }

    private void ResetArrowScales()
    {
        SetArrowScales(normalArrowScale, normalArrowScale);
    }

    private void SetArrowScales(float leftScale, float rightScale)
    {
        if (leftArrowTransform != null)
        {
            leftArrowTransform.localScale = Vector3.one * leftScale;
        }

        if (rightArrowTransform != null)
        {
            rightArrowTransform.localScale = Vector3.one * rightScale;
        }
    }

    private void SetRouteSelectVisible(bool visible)
    {
        if (routeSelectPanel != null)
        {
            routeSelectPanel.SetActive(visible);
        }
    }

    private void SetBalanceGaugeVisible(bool visible)
    {
        if (keepNormalBalanceUiHidden && visible)
        {
            return;
        }

        if (balanceGaugeRoot == null)
        {
            return;
        }

        balanceGaugeRoot.SetActive(visible);
    }

    private void SetCountDisplayVisible(bool visible)
    {
        if (keepNormalBalanceUiHidden && visible)
        {
            return;
        }

        if (countDisplayRoot == null)
        {
            return;
        }

        countDisplayRoot.SetActive(visible);
    }

    private void CaptureOriginalDecideText()
    {
        if (hasCapturedOriginalDecideText || decideText == null)
        {
            return;
        }

        originalDecideText = decideText.text;
        hasCapturedOriginalDecideText = true;
    }

    private void UpdateDecideText()
    {
        CaptureOriginalDecideText();
        if (decideText == null)
        {
            return;
        }

        if (joyConHoldMode)
        {
            decideText.text = JoyConHoldGuideText;
            return;
        }

        decideText.text = immediateSelectionMode ? "左右キーで決定" : originalDecideText;
    }

    private void WarnAboutMissingReferences()
    {
        if (questionText == null)
        {
            Debug.LogWarning("RouteSelectUIController: Question Textが未設定です。", this);
        }

        if (leftArrowTransform == null)
        {
            Debug.LogWarning("RouteSelectUIController: Left Arrow Transformが未設定です。", this);
        }

        if (rightArrowTransform == null)
        {
            Debug.LogWarning("RouteSelectUIController: Right Arrow Transformが未設定です。", this);
        }

        if (decideText == null)
        {
            Debug.LogWarning("RouteSelectUIController: Decide Textが未設定です。", this);
        }

        if (leftHoldCircleImage == null)
        {
            Debug.LogWarning("RouteSelectUIController: Left Hold Circle Imageが未設定です。", this);
        }

        if (rightHoldCircleImage == null)
        {
            Debug.LogWarning("RouteSelectUIController: Right Hold Circle Imageが未設定です。", this);
        }

        if (balanceGaugeRoot == null)
        {
            Debug.LogWarning("RouteSelectUIController: Balance Gauge Rootが未設定です。分岐中もゲージを表示します。", this);
        }

        if (countDisplayRoot == null)
        {
            Debug.LogWarning("RouteSelectUIController: Count Display Rootが未設定です。分岐中も数字カウントを表示します。", this);
        }
    }
}
