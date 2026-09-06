using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/* マトリックス回避フェーズで、4発の弾を同時に飛ばすための発射管理です。 */
public class MatrixAvoidBulletShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BalanceManager balanceManager;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform[] bulletSpawnPoints = new Transform[4];
    [SerializeField] private Transform passCheckTarget;
    [SerializeField] private Transform slowTarget;

    [Header("Bullet Movement")]
    /* 右から左へ飛ばす想定なので初期値は左方向です。Sceneに合わせてInspectorで調整できます。 */
    [SerializeField] private Vector3 moveDirection = Vector3.left;
    [SerializeField] private float normalSpeed = 12f;
    [SerializeField] private float slowSpeed = 2f;
    [SerializeField] private float slowStartDistance = 4f;
    [SerializeField] private float passDistance = 1f;
    [SerializeField] private float bulletLifeTime = 6f;

    [Header("Matrix Mash UI")]
    [Tooltip("『下キー連打！！』をまとめたUIの親Objectです。通常時は非表示にします。")]
    [SerializeField] private GameObject matrixMashUiRoot;
    [Tooltip("『下キー連打！！』と結果メッセージを表示するTextMeshProです。")]
    [SerializeField] private TMP_Text matrixMashMessageText;
    [Tooltip("必要であれば『▼ ▼ ▼』を表示するTextMeshProです。未設定でも動作します。")]
    [SerializeField] private TMP_Text matrixMashArrowText;

    [Header("Matrix Mash Settings")]
    [Tooltip("成功に必要な下キーの入力回数です。")]
    [SerializeField, Min(1)] private int requiredPressCount = 10;
    [Tooltip("連打入力を受け付ける実時間です。")]
    [SerializeField, Min(0.01f)] private float mashDuration = 2.5f;
    [Tooltip("UI表示後、連打入力を受け付け始めるまでの実時間です。")]
    [SerializeField, Min(0f)] private float inputStartDelay = 0.3f;
    [Tooltip("成功・失敗メッセージを表示する実時間です。")]
    [SerializeField, Min(0f)] private float resultMessageDuration = 3f;
    [Tooltip("連打中の文字が最も小さくなる倍率です。")]
    [SerializeField, Min(0f)] private float pulseMinScale = 0.95f;
    [Tooltip("連打中の文字が最も大きくなる倍率です。")]
    [SerializeField, Min(0f)] private float pulseMaxScale = 1.10f;
    [Tooltip("下キーを1回押した瞬間の文字倍率です。")]
    [SerializeField, Min(0f)] private float inputPunchScale = 1.20f;

    [Header("Matrix Shot Sound")]
    [SerializeField] private AudioSource matrixShotAudioSource;
    [SerializeField] private AudioClip matrixShotSound;
    [SerializeField] private float matrixShotSoundVolume = 1.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onAllMatrixBulletsPassed;

    private readonly List<GameObject> spawnedBullets = new List<GameObject>();
    private int firedBulletCount;
    private int passedBulletCount;
    private bool isShooting;
    private Coroutine matrixMashCoroutine;
    private int currentPressCount;
    private bool isMatrixMashResolved;
    private float inputPunchRemainingTime;
    private Vector3 matrixMashMessageBaseScale = Vector3.one;
    private bool hasSavedMatrixMashMessageScale;
    private bool useJoyConMashInput;
    private Joycon matrixMashJoyCon;

    public event Action AllMatrixBulletsPassed;

    private void Reset()
    {
        AutoFindBalanceManager();
    }

    private void Awake()
    {
        if (balanceManager == null)
        {
            AutoFindBalanceManager();
        }

        CleanupMatrixMashUi();
    }

    private void OnDisable()
    {
        StopShooting();
    }

    public void StartShooting()
    {
        FireMatrixBullets();

        if (isShooting)
        {
            StartMatrixMashInput();
        }
    }

    public void FireMatrixBullets()
    {
        StopShooting();

        if (bulletPrefab == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Bullet Prefabが設定されていません。マトリックス弾を発射しません。", this);
            return;
        }

        if (passCheckTarget == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Pass Check Targetが設定されていません。弾の通過判定ができないため発射しません。", this);
            return;
        }

        if (slowTarget == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Slow Targetが設定されていません。弾は通常速度のまま飛びます。", this);
        }

        if (bulletSpawnPoints == null || bulletSpawnPoints.Length == 0)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Bullet Spawn Pointsが設定されていません。", this);
            return;
        }

        Vector3 direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector3.left;
        isShooting = true;
        firedBulletCount = 0;
        passedBulletCount = 0;

        for (int i = 0; i < bulletSpawnPoints.Length; i++)
        {
            Transform spawnPoint = bulletSpawnPoints[i];
            if (spawnPoint == null)
            {
                Debug.LogWarning($"MatrixAvoidBulletShooter: Bullet Spawn Points の Element {i} が未設定です。", this);
                continue;
            }

            GameObject bulletObject = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);
            MatrixAvoidBullet bullet = bulletObject.GetComponent<MatrixAvoidBullet>();
            if (bullet == null)
            {
                bullet = bulletObject.AddComponent<MatrixAvoidBullet>();
            }

            spawnedBullets.Add(bulletObject);
            firedBulletCount++;
            bullet.Initialize(direction, normalSpeed, slowSpeed, slowStartDistance, passDistance, bulletLifeTime, slowTarget, passCheckTarget, OnBulletPassed);
        }

        RemoveDestroyedBulletReferences();

        if (firedBulletCount == 0)
        {
            isShooting = false;
            Debug.LogWarning("MatrixAvoidBulletShooter: 有効なSpawn Pointがないため、弾を発射できませんでした。", this);
            return;
        }

        PlayMatrixShotSound();
        Debug.Log($"MatrixAvoidBulletShooter: マトリックス弾を{firedBulletCount}発同時に発射しました。", this);
    }

    private void PlayMatrixShotSound()
    {
        bool hasMissingAudioSetting = false;

        if (matrixShotAudioSource == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: matrixShotAudioSource not assigned.", this);
            hasMissingAudioSetting = true;
        }

        if (matrixShotSound == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: matrixShotSound not assigned.", this);
            hasMissingAudioSetting = true;
        }

        if (hasMissingAudioSetting)
        {
            return;
        }

        matrixShotAudioSource.PlayOneShot(matrixShotSound, matrixShotSoundVolume);
        Debug.Log("MatrixAvoidBulletShooter: Matrix avoid shot sound played.", this);
    }

    public void StopShooting()
    {
        StopMatrixMashInput();
        DestroySpawnedBullets();
        firedBulletCount = 0;
        passedBulletCount = 0;
        isShooting = false;
        currentPressCount = 0;
        inputPunchRemainingTime = 0f;
        isMatrixMashResolved = false;
    }

    private void OnBulletPassed(MatrixAvoidBullet bullet)
    {
        if (bullet != null)
        {
            spawnedBullets.Remove(bullet.gameObject);
        }

        if (!isShooting)
        {
            return;
        }

        passedBulletCount++;
        Debug.Log($"MatrixAvoidBulletShooter: 弾が通過しました。{passedBulletCount}/{firedBulletCount}", this);

        if (passedBulletCount >= firedBulletCount)
        {
            // 弾は演出として通過させます。Matrix回避の成否は下キー連打だけで確定します。
            Debug.Log("MatrixAvoidBulletShooter: すべての弾が通過しました。連打結果の確定を待ちます。", this);
        }
    }

    private void NotifyMatrixMashCompleted()
    {
        if (!isShooting)
        {
            return;
        }

        isShooting = false;
        RemoveDestroyedBulletReferences();
        Debug.Log("MatrixAvoidBulletShooter: Matrix連打判定が完了しました。", this);
        onAllMatrixBulletsPassed?.Invoke();
        AllMatrixBulletsPassed?.Invoke();
    }

    private void StartMatrixMashInput()
    {
        StopMatrixMashInput();
        currentPressCount = 0;
        isMatrixMashResolved = false;
        inputPunchRemainingTime = 0f;
        SelectMatrixMashInput();
        matrixMashCoroutine = StartCoroutine(MatrixMashRoutine());
    }

    private void SelectMatrixMashInput()
    {
        useJoyConMashInput = false;
        matrixMashJoyCon = null;

        if (ControlSelectionSession.HasSelection
            && ControlSelectionSession.SelectedControlType == GameplayControlType.JoyCon)
        {
            JoyconManager joyconManager = JoyconManager.Instance;
            if (joyconManager != null
                && joyconManager.j != null
                && joyconManager.j.Count > 0
                && joyconManager.j[0] != null)
            {
                matrixMashJoyCon = joyconManager.j[0];
                useJoyConMashInput = true;
            }
            else
            {
                Debug.LogWarning("MatrixAvoidBulletShooter: Joy-Conを利用できないため、Matrix連打入力をKeyboardへFallbackします。", this);
            }
        }

        Debug.Log($"MatrixAvoidBulletShooter: MatrixMashInput={(useJoyConMashInput ? "JoyCon" : "Keyboard")}", this);
    }

    private bool WasMatrixMashInputPressedThisFrame()
    {
        if (useJoyConMashInput)
        {
            return matrixMashJoyCon != null
                && matrixMashJoyCon.GetButtonDown(Joycon.Button.DPAD_UP);
        }

        return Keyboard.current != null
            && Keyboard.current.downArrowKey.wasPressedThisFrame;
    }

    private void StopMatrixMashInput()
    {
        if (matrixMashCoroutine != null)
        {
            StopCoroutine(matrixMashCoroutine);
            matrixMashCoroutine = null;
        }

        isMatrixMashResolved = true;
        CleanupMatrixMashUi();
    }

    private IEnumerator MatrixMashRoutine()
    {
        SetupMatrixMashUi();

        // スローモーションに影響されない実時間で、入力開始まで待ちます。
        float delayElapsed = 0f;
        while (delayElapsed < Mathf.Max(0f, inputStartDelay))
        {
            UpdateMashMessagePulse();
            delayElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float mashElapsed = 0f;
        float duration = Mathf.Max(0.01f, mashDuration);
        int requiredCount = Mathf.Max(1, requiredPressCount);

        while (!isMatrixMashResolved && mashElapsed < duration)
        {
            if (WasMatrixMashInputPressedThisFrame())
            {
                currentPressCount++;
                inputPunchRemainingTime = 0.12f;
                Debug.Log($"MatrixAvoidBulletShooter: 連打入力 {currentPressCount}/{requiredCount}", this);

                if (currentPressCount >= requiredCount)
                {
                    isMatrixMashResolved = true;
                    break;
                }
            }

            UpdateMashMessagePulse();
            mashElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        bool success = currentPressCount >= requiredCount;
        isMatrixMashResolved = true;
        yield return ShowMatrixMashResult(success);

        CleanupMatrixMashUi();

        if (!success && balanceManager != null)
        {
            balanceManager.FailMatrixAvoidByMash();
        }
        else if (!success)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Balance Manager が未設定のため、Matrix連打失敗時のダメージ処理を呼べません。", this);
        }

        // 既存通知を使い、SniperEventManager側で成功処理またはイベント終了へ進みます。
        matrixMashCoroutine = null;
        NotifyMatrixMashCompleted();
    }

    private void SetupMatrixMashUi()
    {
        if (matrixMashUiRoot == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Matrix Mash UI Root が未設定です。連打判定はUIなしで続行します。", this);
        }
        else if (matrixMashUiRoot == gameObject || transform.IsChildOf(matrixMashUiRoot.transform))
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Matrix Mash UI Root にShooter自身が含まれるため、UI表示切り替えをスキップします。", this);
        }
        else
        {
            matrixMashUiRoot.SetActive(true);
        }

        if (matrixMashMessageText == null)
        {
            Debug.LogWarning("MatrixAvoidBulletShooter: Matrix Mash Message Text が未設定です。連打判定は文字表示なしで続行します。", this);
        }
        else
        {
            matrixMashMessageBaseScale = matrixMashMessageText.rectTransform.localScale;
            hasSavedMatrixMashMessageScale = true;
            matrixMashMessageText.text = useJoyConMashInput
                ? "Xボタン連打！！"
                : "下キー連打！！";
        }

        if (matrixMashArrowText != null)
        {
            matrixMashArrowText.text = "▼ ▼ ▼";
            matrixMashArrowText.gameObject.SetActive(true);
        }
    }

    private void UpdateMashMessagePulse()
    {
        if (matrixMashMessageText == null)
        {
            return;
        }

        float minScale = Mathf.Min(pulseMinScale, pulseMaxScale);
        float maxScale = Mathf.Max(pulseMinScale, pulseMaxScale);
        float pulseRate = (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f;
        float pulseScale = Mathf.Lerp(minScale, maxScale, pulseRate);
        float progress = Mathf.Clamp01((float)currentPressCount / Mathf.Max(1, requiredPressCount));
        float growingBaseScale = 1f + progress * 0.08f;
        float displayScale = pulseScale * growingBaseScale;

        if (inputPunchRemainingTime > 0f)
        {
            displayScale = Mathf.Max(displayScale, inputPunchScale);
            inputPunchRemainingTime -= Time.unscaledDeltaTime;
        }

        SetMashMessageScale(displayScale);
    }

    private IEnumerator ShowMatrixMashResult(bool success)
    {
        if (matrixMashMessageText != null)
        {
            matrixMashMessageText.text = success ? "素晴らしい！" : "間に合わないっ！";
        }

        if (matrixMashArrowText != null)
        {
            matrixMashArrowText.gameObject.SetActive(false);
        }

        float duration = Mathf.Max(0f, resultMessageDuration);
        float elapsedTime = 0f;
        float startScale = success
            ? Mathf.Max(1f, inputPunchScale)
            : Mathf.Max(1.05f, pulseMaxScale);

        while (elapsedTime < duration)
        {
            float rate = duration > 0f ? elapsedTime / duration : 1f;
            SetMashMessageScale(Mathf.Lerp(startScale, 1f, rate));
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        SetMashMessageScale(1f);
    }

    private void SetMashMessageScale(float scale)
    {
        if (matrixMashMessageText == null)
        {
            return;
        }

        Vector3 baseScale = hasSavedMatrixMashMessageScale ? matrixMashMessageBaseScale : Vector3.one;
        matrixMashMessageText.rectTransform.localScale = baseScale * Mathf.Max(0f, scale);
    }

    private void CleanupMatrixMashUi()
    {
        if (matrixMashMessageText != null)
        {
            if (hasSavedMatrixMashMessageScale)
            {
                matrixMashMessageText.rectTransform.localScale = matrixMashMessageBaseScale;
            }

            matrixMashMessageText.text = "下キー連打！！";
        }

        if (matrixMashArrowText != null)
        {
            matrixMashArrowText.text = "▼ ▼ ▼";
            matrixMashArrowText.gameObject.SetActive(true);
        }

        if (matrixMashUiRoot != null
            && matrixMashUiRoot != gameObject
            && !transform.IsChildOf(matrixMashUiRoot.transform))
        {
            matrixMashUiRoot.SetActive(false);
        }

        hasSavedMatrixMashMessageScale = false;
    }

    private void DestroySpawnedBullets()
    {
        for (int i = spawnedBullets.Count - 1; i >= 0; i--)
        {
            if (spawnedBullets[i] != null)
            {
                Destroy(spawnedBullets[i]);
            }
        }

        spawnedBullets.Clear();
    }

    private void RemoveDestroyedBulletReferences()
    {
        for (int i = spawnedBullets.Count - 1; i >= 0; i--)
        {
            if (spawnedBullets[i] == null)
            {
                spawnedBullets.RemoveAt(i);
            }
        }
    }

    private void AutoFindBalanceManager()
    {
        balanceManager = FindFirstObjectByType<BalanceManager>();
    }
}
