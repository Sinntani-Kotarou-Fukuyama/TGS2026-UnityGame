using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Joy-Con選択時だけ、Neutral確定まで綱渡り開始を待機します。</summary>
public class JoyConStartWaitController : MonoBehaviour
{
    [Header("Joy-Con開始待機")]
    [Tooltip("通常綱渡りを制御しているTrolleyWallを設定します。")]
    [SerializeField] private TrolleyWall trolleyWall;

    [Tooltip("待機中に歩行BoolをOFFにするPlayer Animatorです。")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("開始案内専用のJoyConStartGuidePanelを設定します。")]
    [SerializeField] private GameObject guidePanel;

    [Tooltip("案内文を表示するTextMeshProを設定します。")]
    [SerializeField] private TMP_Text guideText;

    [Tooltip("Neutral確定まで表示する案内文です。")]
    [SerializeField] private string holdMessage = "Joy-Conを水平に持ってください";

    [Tooltip("Neutral確定後に表示する文です。")]
    [SerializeField] private string readyMessage = "準備OK！";

    [Tooltip("準備OKを表示してから綱渡りを始めるまでの実時間です。")]
    [SerializeField, Min(0f)] private float readyMessageDuration = 0.5f;

    private static readonly int WalkAnimationHash = Animator.StringToHash("catwalk");
    private bool isWaiting;

    private void Awake()
    {
        SetGuideVisible(false);

        bool joyConSelected =
            ControlSelectionSession.HasSelection &&
            ControlSelectionSession.SelectedControlType == GameplayControlType.JoyCon;
        if (!joyConSelected)
        {
            return;
        }

        if (trolleyWall == null)
        {
            Debug.LogWarning("JoyConStartWaitController: Trolley Wallが未設定のため、開始待機を行いません。", this);
            return;
        }

        if (playerAnimator == null)
        {
            Debug.LogWarning("JoyConStartWaitController: Player Animatorが未設定のため、待機中の歩行アニメーション停止をスキップします。", this);
        }

        if (guidePanel == null || guideText == null)
        {
            Debug.LogWarning("JoyConStartWaitController: 開始案内UIの参照が不足しています。設定済みのUIだけを使用します。", this);
        }

        isWaiting = true;
        trolleyWall.SetJoyConStartHold(true);
        SetWalkAnimation(false);
        SetGuideText(holdMessage);
        SetGuideVisible(true);
    }

    private IEnumerator Start()
    {
        if (!isWaiting)
        {
            yield break;
        }

        // 別GameObjectのStart順序に依存せず、TrolleyWallのJoy-Con初期化完了後に判定します。
        yield return null;

        if (!trolleyWall.IsJoyConInputActive)
        {
            // Joy-Con未接続時は既存のKeyboard/Mouseフォールバックでそのまま開始します。
            FinishStartHold();
            yield break;
        }

        while (isWaiting && !trolleyWall.IsJoyConReady)
        {
            yield return null;
        }

        if (!isWaiting)
        {
            yield break;
        }

        SetGuideText(readyMessage);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, readyMessageDuration));

        if (isWaiting)
        {
            FinishStartHold();
        }
    }

    private void LateUpdate()
    {
        // RopeWalkManagerの通常アニメーション更新より後でも、待機中は確実に歩行を止めます。
        if (isWaiting)
        {
            SetWalkAnimation(false);
        }
    }

    private void OnDisable()
    {
        if (isWaiting)
        {
            FinishStartHold();
        }
    }

    private void FinishStartHold()
    {
        isWaiting = false;
        if (trolleyWall != null)
        {
            trolleyWall.SetJoyConStartHold(false);
        }

        // 待機中に直接OFFへした歩行Boolを、通常開始時の状態へ戻します。
        SetWalkAnimation(true);
        SetGuideVisible(false);
    }

    private void SetWalkAnimation(bool walking)
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(WalkAnimationHash, walking);
        }
    }

    private void SetGuideVisible(bool visible)
    {
        if (guidePanel != null)
        {
            guidePanel.SetActive(visible);
        }
    }

    private void SetGuideText(string message)
    {
        if (guideText != null)
        {
            guideText.text = message;
        }
    }
}
