using System.Collections;
using UnityEngine;

public class RopeWalkManager : MonoBehaviour
{
    [Header("共通ロープで使用する移動方式")]
    [Tooltip("ONで新しいTrolley方式、OFFで従来の自動移動方式を使用します。")]
    [SerializeField] private bool useTrolleyWalkSystem = true;

    [Tooltip("共通ロープ終点から、この距離まで近づいたら停止します。")]
    [SerializeField, Min(0f)] private float commonEndStopDistance = 1f;

    [SerializeField] private TrolleyWall trolleyWall;

    [Tooltip("新しいTrolley方式の使用中だけ停止する、SuitManの従来自動移動です。")]
    [SerializeField] private TightropeAutoGoalMover legacyAutoGoalMover;

    [Header("歩行アニメーション")]
    [Tooltip("新しいTrolley方式の通常移動中にcatwalkを切り替えるSuitManのAnimatorです。")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("分岐UIと左右ルートの正式なTransform配列を管理するControllerです。")]
    [SerializeField] private TightropeRouteController routeController;

    [Tooltip("最終ルート終点で、既存のClear処理を1回だけ呼ぶための参照です。")]
    [SerializeField] private PlayerGameFeedbackController clearSceneLoader;

    [Tooltip("イベント中は画面端の傾き表示を止めるための参照です。")]
    [SerializeField] private BlanceBar_Vignette balanceVignette;

    [Header("Recovery")]
    [Tooltip("ロープ復帰後に入力・移動・Damageを止める実時間です。")]
    [SerializeField, Min(0f)] private float recoveryProtectionDuration = 1f;

    [Tooltip("復帰保護中にPlayerの見た目を点滅させる実時間間隔です。")]
    [SerializeField, Min(0.02f)] private float recoveryBlinkInterval = 0.1f;

    [Header("ルートTransform（開始・終了の2個1組）")]
    [Tooltip("例: CommonRope1Start, CommonRope1End")]
    [SerializeField] private Transform[] commonRoutePoints;

    [Tooltip("例: LeftRope2Start, LeftRope2End, LeftRope3Start, LeftRope3End")]
    [SerializeField] private Transform[] leftRoutePoints;

    [Tooltip("例: RightRope2Start, RightRope2End, RightRope3Start, RightRope3End")]
    [SerializeField] private Transform[] rightRoutePoints;

    private Transform CurrentStart, CurrentEnd;
    private bool isSystemInitialized;
    private bool isTemporarilyPaused;
    private bool hasReachedCommonEnd;
    private bool isWaitingForRouteSelection;
    private bool isMovingSelectedRoute;
    private bool hasReachedGoal;
    private bool isPausedForPosingEvent;
    private bool isPausedForSniperEvent;
    private bool isPausedForHelicopterEvent;
    private bool isPausedForFall;
    private bool isRecoveringFromFall;
    private Coroutine managedFallRecoveryCoroutine;
    private Transform savedFallSegmentStart;
    private Transform savedFallSegmentEnd;
    private Transform[] savedFallRoutePoints;
    private int savedFallSegmentStartIndex;
    private bool savedFallWasMovingSelectedRoute;
    private float savedFallSegmentProgress;
    private Vector3 savedFallForward;
    private bool hasPendingSelectedRouteStart;
    private TrolleyWall subscribedFallTrolleyWall;
    private Transform[] activeRoutePoints;
    private int currentSegmentStartIndex;
    private bool legacyMovementStateSaved;
    private bool legacyPlayerStoppingBeforeTrolley;
    private bool hasAppliedWalkAnimationState;
    private bool lastWalkAnimationState;
    private static readonly int WalkAnimationHash = Animator.StringToHash("catwalk");

    private void Awake()
    {
        if (!useTrolleyWalkSystem)
        {
            DisableTrolleyForLegacyMode();
            return;
        }

        if (!TryValidateCommonRoute())
        {
            FallBackToLegacyMode();
            return;
        }

        SaveAndStopLegacyMovement();
        routeController.SetTrolleyMovementActive(true);
        trolleyWall.enabled = true;
        trolleyWall.IsStop(true);
    }

    private void OnEnable()
    {
        SubscribeManagedFallNotifications();
    }

    private void Start()
    {
        if (!useTrolleyWalkSystem)
        {
            gameObject.SetActive(false);
            return;
        }

        SetRoute(commonRoutePoints[0], commonRoutePoints[1]);
        if (!isSystemInitialized)
        {
            FallBackToLegacyMode();
            gameObject.SetActive(false);
            return;
        }

        // CameraSwiichから先に一時停止されていた場合は、カメラ演出終了まで再開しません。
        trolleyWall.IsStop(ShouldKeepTrolleyStopped());
        UpdateWalkAnimationState();
    }

    private void Update()
    {
        if (!useTrolleyWalkSystem || !isSystemInitialized)
        {
            return;
        }

        // 他の既存処理から再開されても、Trolley使用中は旧移動による二重更新を防ぎます。
        if (legacyAutoGoalMover != null)
        {
            legacyAutoGoalMover.PlayerStoping = true;
        }

        UpdateWalkAnimationState();

        if (ShouldKeepTrolleyStopped())
        {
            return;
        }

        float distance = Vector3.Distance(trolleyWall.transform.position, CurrentEnd.position);
        if (distance <= Mathf.Max(0f, commonEndStopDistance))
        {
            trolleyWall.StopRouteMovement();

            if (isMovingSelectedRoute)
            {
                AdvanceSelectedRouteOrCompleteGoal();
            }
            else
            {
                EnterRouteSelection();
            }

            UpdateWalkAnimationState();
        }
    }
    public void SetRoute(Transform start, Transform end)
    {
        isSystemInitialized = false;
        if (trolleyWall == null || start == null || end == null)
        {
            Debug.LogWarning("RopeWalkManager: 共通ロープの開始点・終点・TrolleyWall参照を確認してください。", this);
            return;
        }

        CurrentStart = start;
        CurrentEnd = end;
        hasReachedCommonEnd = false;
        isSystemInitialized = trolleyWall.InitializeForCommonRope(CurrentStart, CurrentEnd);
    }
    private void EnterRouteSelection()
    {
        if (hasReachedCommonEnd)
        {
            return;
        }

        hasReachedCommonEnd = true;
        isWaitingForRouteSelection = true;

        if (!routeController.BeginTrolleyRouteSelection(OnTrolleyRouteSelected))
        {
            Debug.LogWarning("RopeWalkManager: ルート選択を開始できません。安全のため分岐地点で停止を続けます。", this);
        }
    }

    private void OnTrolleyRouteSelected(Transform[] selectedRoutePoints)
    {
        if (!isWaitingForRouteSelection || hasReachedGoal || isMovingSelectedRoute)
        {
            return;
        }

        if (!ValidateSelectedRoutePoints(selectedRoutePoints))
        {
            Debug.LogWarning("RopeWalkManager: 選択ルートのTransform設定が不正なため、移動を開始しません。", this);
            return;
        }

        isWaitingForRouteSelection = false;
        hasReachedCommonEnd = false;
        isMovingSelectedRoute = true;
        activeRoutePoints = selectedRoutePoints;
        currentSegmentStartIndex = 0;
        StartSelectedRouteSegment(true);
    }

    private void StartSelectedRouteSegment(bool waitForRouteInputRelease)
    {
        if (IsPausedForAnyEvent() || isPausedForFall)
        {
            hasPendingSelectedRouteStart = true;
            return;
        }

        hasPendingSelectedRouteStart = false;
        CurrentStart = activeRoutePoints[currentSegmentStartIndex];
        CurrentEnd = activeRoutePoints[currentSegmentStartIndex + 1];

        if (!trolleyWall.PrepareForRouteSegment(CurrentStart, CurrentEnd, commonEndStopDistance))
        {
            Debug.LogWarning("RopeWalkManager: 選択ルート区間を初期化できません。安全のため停止します。", this);
            trolleyWall.StopRouteMovement();
            UpdateWalkAnimationState();
            return;
        }

        trolleyWall.ResumeRouteMovement(waitForRouteInputRelease);
        if (ShouldKeepTrolleyStopped())
        {
            // 他の停止理由が残っている間は、区間だけ切り替えて移動再開を待ちます。
            trolleyWall.StopRouteMovement();
        }

        UpdateWalkAnimationState();

        Debug.Log($"[RopeWalkManager] 区間を開始: {CurrentStart.name} -> {CurrentEnd.name}", this);
    }

    private void AdvanceSelectedRouteOrCompleteGoal()
    {
        currentSegmentStartIndex += 2;
        if (currentSegmentStartIndex < activeRoutePoints.Length)
        {
            StartSelectedRouteSegment(false);
            return;
        }

        CompleteTrolleyRoute();
    }

    private void CompleteTrolleyRoute()
    {
        if (hasReachedGoal)
        {
            return;
        }

        hasReachedGoal = true;
        isMovingSelectedRoute = false;
        trolleyWall.StopRouteMovement();
        UpdateWalkAnimationState();
        routeController.CompleteTrolleyRoute();

        if (clearSceneLoader == null)
        {
            Debug.LogWarning("RopeWalkManager: Clear処理の参照がないため、最終地点で停止します。", this);
            return;
        }

        Debug.Log("[RopeWalkManager] 選択ルートの最終地点に到達しました。既存のClear処理を呼びます。", this);
        clearSceneLoader.LoadClearScene();
    }

    private bool ValidateSelectedRoutePoints(Transform[] routePoints)
    {
        if (routePoints == null || routePoints.Length < 2 || routePoints.Length % 2 != 0)
        {
            return false;
        }

        for (int i = 0; i < routePoints.Length; i++)
        {
            if (routePoints[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    //第一、第二引数から向きを計算し、その方向に真っ直ぐ向くための回転データ（Quaternion）を作り出す
    public Quaternion GetLookAtRotation(Transform currentPosition, Transform targetPosition)
    {
        Vector3 direction = targetPosition.position - currentPosition.position;

        // 完全に同じ座標でなければ、向き（Quaternion）を計算して返す
        if (direction != Vector3.zero)
        {
            return Quaternion.LookRotation(direction);
        }

        // 向きが計算できない場合は、現在のオブジェクトの回転をそのまま返す
        return transform.rotation;
    }

    private void SubscribeManagedFallNotifications()
    {
        if (subscribedFallTrolleyWall == trolleyWall && subscribedFallTrolleyWall != null)
        {
            return;
        }

        UnsubscribeManagedFallNotifications();
        if (!useTrolleyWalkSystem || trolleyWall == null)
        {
            return;
        }

        subscribedFallTrolleyWall = trolleyWall;
        subscribedFallTrolleyWall.ManagedFallStarted += HandleManagedFallStarted;
    }

    private void UnsubscribeManagedFallNotifications()
    {
        if (subscribedFallTrolleyWall == null)
        {
            return;
        }

        subscribedFallTrolleyWall.ManagedFallStarted -= HandleManagedFallStarted;
        subscribedFallTrolleyWall = null;
    }

    private void HandleManagedFallStarted(float _)
    {
        if (!useTrolleyWalkSystem || isPausedForFall || trolleyWall == null || !trolleyWall.UsesManagedFallFlow)
        {
            return;
        }

        // Damageやアニメーションより先に停止理由を確定し、同じ落下から二重処理しません。
        isPausedForFall = true;
        trolleyWall.StopRouteMovement();
        UpdateWalkAnimationState();

        ResolveBalanceVignetteReference();
        if (balanceVignette != null)
        {
            balanceVignette.SetPausedForExternalEvent(true);
        }

        if (clearSceneLoader == null)
        {
            Debug.LogWarning("RopeWalkManager: Player Game Feedback Controllerが未設定のため、落下停止を維持してDamage処理を省略します。", this);
            return;
        }

        bool willReachGameOver = clearSceneLoader.DamageCount + 1 >= clearSceneLoader.MaxDamageCount;
        if (willReachGameOver)
        {
            // 5回目はAddDamage内の既存GameOver落下だけを使用し、通常復帰を開始しません。
            clearSceneLoader.AddDamage();
            return;
        }

        bool hasRecoveryContext = TrySaveManagedFallRecoveryContext();
        clearSceneLoader.PlayFallAnimationVisual();

        // 体幹メーター、赤フラッシュ、Clear評価は既存の正式なDamage入口へ委譲します。
        clearSceneLoader.AddDamage();

        if (!hasRecoveryContext || clearSceneLoader.IsFallingToGameOver)
        {
            return;
        }

        managedFallRecoveryCoroutine = StartCoroutine(ManagedFallRecoveryRoutine());
    }

    private bool TrySaveManagedFallRecoveryContext()
    {
        if (CurrentStart == null || CurrentEnd == null || trolleyWall == null)
        {
            Debug.LogWarning("RopeWalkManager: 落下時のSegment情報を保存できないため、復帰せず停止を維持します。", this);
            return false;
        }

        Vector3 direction = CurrentEnd.position - CurrentStart.position;
        float directionSqrMagnitude = direction.sqrMagnitude;
        if (directionSqrMagnitude <= Mathf.Epsilon)
        {
            Debug.LogWarning("RopeWalkManager: 現在Segmentの長さが0のため、復帰せず停止を維持します。", this);
            return false;
        }

        savedFallSegmentStart = CurrentStart;
        savedFallSegmentEnd = CurrentEnd;
        savedFallRoutePoints = activeRoutePoints;
        savedFallSegmentStartIndex = currentSegmentStartIndex;
        savedFallWasMovingSelectedRoute = isMovingSelectedRoute;
        savedFallForward = direction.normalized;

        Vector3 movementPosition = trolleyWall.transform.position;
        savedFallSegmentProgress = Mathf.Clamp01(
            Vector3.Dot(movementPosition - CurrentStart.position, direction) / directionSqrMagnitude);
        return true;
    }

    private IEnumerator ManagedFallRecoveryRoutine()
    {
        yield return clearSceneLoader.WaitForManagedFallAnimationComplete();

        if (!CanContinueManagedFallRecovery())
        {
            managedFallRecoveryCoroutine = null;
            yield break;
        }

        if (!trolleyWall.RestoreManagedFallAtRouteProgress(
                savedFallSegmentStart,
                savedFallSegmentEnd,
                savedFallSegmentProgress))
        {
            managedFallRecoveryCoroutine = null;
            yield break;
        }

        clearSceneLoader.RestoreAnimatorAfterManagedFall();
        isRecoveringFromFall = true;
        // 既存復帰処理がcatwalkをONにしても、復帰保護中はまだ歩行させません。
        SetPlayerWalkAnimation(false, true);

        // 落下Damageを加算した後、ロープへ戻してから復帰専用の無敵と点滅を開始します。
        clearSceneLoader.SetRecoveryInvulnerable(true);
        clearSceneLoader.BeginRecoveryBlink();

        float duration = Mathf.Max(0f, recoveryProtectionDuration);
        float blinkInterval = Mathf.Max(0.02f, recoveryBlinkInterval);
        float elapsed = 0f;
        float nextBlinkTime = blinkInterval;
        bool isVisible = true;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (blinkInterval > 0f && elapsed >= nextBlinkTime)
            {
                isVisible = !isVisible;
                clearSceneLoader.SetRecoveryBlinkVisible(isVisible);
                nextBlinkTime += blinkInterval;
            }

            yield return null;
        }

        CompleteManagedFallRecovery();
    }

    private bool CanContinueManagedFallRecovery()
    {
        if (!useTrolleyWalkSystem || !isActiveAndEnabled || trolleyWall == null ||
            !trolleyWall.UsesManagedFallFlow || clearSceneLoader == null ||
            clearSceneLoader.IsFallingToGameOver || hasReachedGoal)
        {
            return false;
        }

        bool routeUnchanged = CurrentStart == savedFallSegmentStart &&
            CurrentEnd == savedFallSegmentEnd &&
            isMovingSelectedRoute == savedFallWasMovingSelectedRoute;

        if (savedFallWasMovingSelectedRoute)
        {
            routeUnchanged = routeUnchanged &&
                activeRoutePoints == savedFallRoutePoints &&
                currentSegmentStartIndex == savedFallSegmentStartIndex;
        }

        if (!routeUnchanged)
        {
            Debug.LogWarning("RopeWalkManager: 落下中にSegment状態が変わったため、安全のため復帰せず停止を維持します。", this);
        }

        // 保存した進行方向も保持し、RoutePointが同一なら同じ向きで一式を復帰します。
        return routeUnchanged && savedFallForward.sqrMagnitude > Mathf.Epsilon;
    }

    private void CompleteManagedFallRecovery()
    {
        clearSceneLoader.EndRecoveryBlink();
        clearSceneLoader.SetRecoveryInvulnerable(false);

        trolleyWall.CompleteManagedFallRecovery();
        isPausedForFall = false;
        isRecoveringFromFall = false;
        managedFallRecoveryCoroutine = null;

        ResolveBalanceVignetteReference();
        if (balanceVignette != null && !IsPausedForAnyEvent())
        {
            balanceVignette.SetPausedForExternalEvent(false);
        }

        if (ShouldKeepTrolleyStopped())
        {
            trolleyWall.StopRouteMovement();
            UpdateWalkAnimationState();
            return;
        }

        // 矢印キーの解放待ちとマウス位置再初期化は既存TrolleyWall APIへ任せます。
        trolleyWall.ResumeRouteMovement(true);
        UpdateWalkAnimationState();
    }

    private void CancelManagedFallRecovery()
    {
        if (managedFallRecoveryCoroutine != null)
        {
            StopCoroutine(managedFallRecoveryCoroutine);
            managedFallRecoveryCoroutine = null;
        }

        if (clearSceneLoader != null)
        {
            clearSceneLoader.EndRecoveryBlink();
            clearSceneLoader.SetRecoveryInvulnerable(false);
        }

        isRecoveringFromFall = false;
    }

    public void PauseForPosingEvent()
    {
        PauseForEvent(ref isPausedForPosingEvent);
    }

    public void ResumeAfterPosingEvent()
    {
        ResumeAfterEvent(ref isPausedForPosingEvent);
    }

    public void PauseForSniperEvent()
    {
        PauseForEvent(ref isPausedForSniperEvent);
    }

    public void ResumeAfterSniperEvent()
    {
        ResumeAfterEvent(ref isPausedForSniperEvent);
    }

    public void PauseForHelicopterEvent()
    {
        PauseForEvent(ref isPausedForHelicopterEvent);
    }

    public void ResumeAfterHelicopterEvent()
    {
        ResumeAfterEvent(ref isPausedForHelicopterEvent);
    }

    private void PauseForEvent(ref bool eventPauseFlag)
    {
        if (!useTrolleyWalkSystem || eventPauseFlag)
        {
            return;
        }

        eventPauseFlag = true;
        if (trolleyWall != null)
        {
            trolleyWall.PauseForExternalEvent();
            trolleyWall.StraightenForExternalEvent();
        }

        UpdateWalkAnimationState();

        ResolveBalanceVignetteReference();
        if (balanceVignette != null)
        {
            balanceVignette.SetPausedForExternalEvent(true);
        }
    }

    private void ResumeAfterEvent(ref bool eventPauseFlag)
    {
        if (!useTrolleyWalkSystem || !eventPauseFlag)
        {
            return;
        }

        eventPauseFlag = false;
        if (!isActiveAndEnabled || IsPausedForAnyEvent())
        {
            return;
        }

        FinishExternalEventPause();
    }

    private void FinishExternalEventPause()
    {
        bool shouldResumeMovement = isSystemInitialized && !ShouldKeepTrolleyStopped();

        if (trolleyWall != null)
        {
            trolleyWall.FinishExternalEventPause(false);
        }

        ResolveBalanceVignetteReference();
        if (balanceVignette != null && !isPausedForFall && !isRecoveringFromFall)
        {
            balanceVignette.SetPausedForExternalEvent(false);
        }

        if (hasPendingSelectedRouteStart)
        {
            hasPendingSelectedRouteStart = false;
            StartSelectedRouteSegment(true);
            return;
        }

        if (shouldResumeMovement && trolleyWall != null)
        {
            trolleyWall.IsStop(false);
        }

        UpdateWalkAnimationState();
    }

    private bool IsPausedForAnyEvent()
    {
        return isPausedForPosingEvent || isPausedForSniperEvent || isPausedForHelicopterEvent;
    }

    private bool ShouldKeepTrolleyStopped()
    {
        return isTemporarilyPaused || isWaitingForRouteSelection || hasReachedGoal || isPausedForFall || isRecoveringFromFall || IsPausedForAnyEvent();
    }

    private void UpdateWalkAnimationState()
    {
        bool shouldWalk = useTrolleyWalkSystem &&
            isSystemInitialized &&
            CurrentStart != null &&
            CurrentEnd != null &&
            trolleyWall != null &&
            trolleyWall.IsAutoWalkEnabled &&
            !trolleyWall.IsStop() &&
            !ShouldKeepTrolleyStopped();

        SetPlayerWalkAnimation(shouldWalk);
    }

    // Animatorへの書き込みは状態が変わった時だけ行い、イベント用Animationを毎フレーム上書きしません。
    private void SetPlayerWalkAnimation(bool walking, bool force = false)
    {
        if (playerAnimator == null)
        {
            return;
        }

        if (!force && hasAppliedWalkAnimationState && lastWalkAnimationState == walking)
        {
            return;
        }

        playerAnimator.SetBool(WalkAnimationHash, walking);
        lastWalkAnimationState = walking;
        hasAppliedWalkAnimationState = true;
    }

    private void ResolveBalanceVignetteReference()
    {
        if (balanceVignette == null)
        {
            balanceVignette = FindFirstObjectByType<BlanceBar_Vignette>(FindObjectsInactive.Include);
        }
    }

    public void MovePlayer()
    {
        if (!useTrolleyWalkSystem)
        {
            return;
        }

        isTemporarilyPaused = false;
        if (isSystemInitialized && !ShouldKeepTrolleyStopped())
        {
            trolleyWall.IsStop(false);
        }

        UpdateWalkAnimationState();
    }

    public void StopPlayer()
    {
        if (!useTrolleyWalkSystem)
        {
            return;
        }

        isTemporarilyPaused = true;
        if (trolleyWall != null)
        {
            trolleyWall.IsStop(true);
        }

        UpdateWalkAnimationState();
    }

    public bool IsPlayerStop()
    {
        return useTrolleyWalkSystem && trolleyWall != null && trolleyWall.IsStop();
    }

    private bool TryValidateCommonRoute()
    {
        if (trolleyWall == null)
        {
            Debug.LogWarning("RopeWalkManager: Trolley Wallが未設定のため、従来移動を使用します。", this);
            return false;
        }

        if (commonRoutePoints == null || commonRoutePoints.Length < 2 ||
            commonRoutePoints[0] == null || commonRoutePoints[1] == null)
        {
            Debug.LogWarning("RopeWalkManager: Common Route Pointsの開始点と終点を設定してください。", this);
            return false;
        }

        if (legacyAutoGoalMover == null)
        {
            legacyAutoGoalMover = FindFirstObjectByType<TightropeAutoGoalMover>();
        }

        if (legacyAutoGoalMover == null)
        {
            Debug.LogWarning("RopeWalkManager: 従来移動の参照がないため、二重移動を停止できません。", this);
            return false;
        }

        if (playerAnimator == null)
        {
            playerAnimator = legacyAutoGoalMover.GetComponent<Animator>();
            if (playerAnimator == null)
            {
                playerAnimator = legacyAutoGoalMover.GetComponentInChildren<Animator>();
            }
        }

        if (playerAnimator == null)
        {
            Debug.LogWarning("RopeWalkManager: Player Animatorが未設定のため、Trolley移動中の歩行モーションだけを省略します。", this);
        }

        if (routeController == null)
        {
            routeController = FindFirstObjectByType<TightropeRouteController>();
        }

        if (routeController == null)
        {
            Debug.LogWarning("RopeWalkManager: Tightrope Route Controllerが未設定のため、分岐選択を開始できません。", this);
            return false;
        }

        if (clearSceneLoader == null)
        {
            clearSceneLoader = FindFirstObjectByType<PlayerGameFeedbackController>();
        }

        if (clearSceneLoader == null)
        {
            Debug.LogWarning("RopeWalkManager: Player Game Feedback Controllerが未設定のため、Clear処理を呼べません。", this);
            return false;
        }

        ResolveBalanceVignetteReference();
        if (balanceVignette == null)
        {
            Debug.LogWarning("RopeWalkManager: BlanceBar Vignetteが未設定のため、イベント中の画面端表示停止だけをスキップします。", this);
        }

        return true;
    }

    private void SaveAndStopLegacyMovement()
    {
        if (legacyAutoGoalMover == null || legacyMovementStateSaved)
        {
            return;
        }

        legacyPlayerStoppingBeforeTrolley = legacyAutoGoalMover.PlayerStoping;
        legacyMovementStateSaved = true;
        legacyAutoGoalMover.SetExternalAnimationControl(true);
        legacyAutoGoalMover.PlayerStoping = true;
        SetPlayerWalkAnimation(false, true);
    }

    private void RestoreLegacyMovement()
    {
        if (legacyAutoGoalMover == null)
        {
            return;
        }

        if (legacyMovementStateSaved)
        {
            legacyAutoGoalMover.PlayerStoping = legacyPlayerStoppingBeforeTrolley;
            legacyMovementStateSaved = false;
        }

        legacyAutoGoalMover.SetExternalAnimationControl(false);
        hasAppliedWalkAnimationState = false;
    }

    private void DisableTrolleyForLegacyMode()
    {
        SetPlayerWalkAnimation(false, true);

        if (routeController != null)
        {
            routeController.SetTrolleyMovementActive(false);
        }

        if (trolleyWall != null)
        {
            trolleyWall.IsStop(true);
            trolleyWall.enabled = false;
        }

        RestoreLegacyMovement();
    }

    private void FallBackToLegacyMode()
    {
        CancelManagedFallRecovery();
        useTrolleyWalkSystem = false;
        isSystemInitialized = false;
        isWaitingForRouteSelection = false;
        isMovingSelectedRoute = false;
        hasReachedGoal = false;
        isPausedForPosingEvent = false;
        isPausedForSniperEvent = false;
        isPausedForHelicopterEvent = false;
        isPausedForFall = false;
        hasPendingSelectedRouteStart = false;
        DisableTrolleyForLegacyMode();
    }

    private void OnDisable()
    {
        SetPlayerWalkAnimation(false, true);
        CancelManagedFallRecovery();
        UnsubscribeManagedFallNotifications();
        bool wasPausedForEvent = IsPausedForAnyEvent();
        isPausedForPosingEvent = false;
        isPausedForSniperEvent = false;
        isPausedForHelicopterEvent = false;
        hasPendingSelectedRouteStart = false;

        if (trolleyWall != null)
        {
            if (wasPausedForEvent)
            {
                trolleyWall.FinishExternalEventPause(false);
            }
            else
            {
                trolleyWall.IsStop(true);
            }
        }

        if (balanceVignette != null && !isPausedForFall)
        {
            balanceVignette.SetPausedForExternalEvent(false);
        }

        RestoreLegacyMovement();
        if (useTrolleyWalkSystem)
        {
            // Scene遷移などでManagerが無効になる時は、最後にIdleへ戻します。
            SetPlayerWalkAnimation(false, true);
        }
    }
}
