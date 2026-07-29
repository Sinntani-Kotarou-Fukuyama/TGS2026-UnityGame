using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 横視点レーザーに沿って、スナイパーの弾を1発ずつ発射するスクリプトです。
public class SniperBulletShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SniperSideViewLaserController sideViewLaserController;
    [SerializeField] private BalanceManager balanceManager;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Bullet Settings")]
    [SerializeField] private float bulletSpeed = 8.0f;
    [SerializeField] private float bulletLifeTime = 3.0f;
    [SerializeField] private int shotCount = 4;
    [SerializeField] private float shotInterval = 0.6f;

    [Header("Pre-Fire Laser Warning")]
    [Tooltip("弾を発射する前に、次の弾と同じ赤線を点滅させる時間です。")]
    [SerializeField, Min(0f)] private float preFireBlink = 0.6f;
    [Tooltip("赤線を点滅させるON/OFFの間隔です。")]
    [SerializeField, Min(0.02f)] private float preFireBlinkInterval = 0.12f;

    [Header("Shot Sound")]
    [SerializeField] private AudioSource sniperShotAudioSource;
    [SerializeField] private AudioClip sniperShotSound;
    [SerializeField] private float sniperShotSoundVolume = 1.0f;

    [Header("Sniper Target Settings")]
    // 弾を撃つレーザーに合わせて、縦ゲージ内の黄色ターゲット位置を変えます。
    [SerializeField, Range(0f, 1f)] private float upperLaserTargetPosition = 0.75f;
    [SerializeField, Range(0f, 1f)] private float lowerLaserTargetPosition = 0.25f;

    [Header("Events")]
    // 4発すべての防御判定が終わった時に、次の演出へ進めるための通知です。
    [SerializeField] private UnityEvent onAllShotsResolved;

    private readonly List<GameObject> spawnedBullets = new List<GameObject>();
    private Coroutine shootingCoroutine;
    private bool currentShotFinished;
    private bool currentShotReachedTarget;
    private bool currentShotResolved;
    private int currentLaserIndex = -1;
    private float currentLaserWorldY;
    private bool enabledSniperDefenseMode;

    public event Action AllShotsResolved;

    private void Reset()
    {
        AutoFindSideViewLaserController();
        AutoFindBalanceManager();
    }

    private void Awake()
    {
        if (sideViewLaserController == null)
        {
            AutoFindSideViewLaserController();
        }

        if (balanceManager == null)
        {
            AutoFindBalanceManager();
        }
    }

    private void OnDisable()
    {
        StopShooting();
    }

    public void StartShooting()
    {
        StopShooting();

        if (bulletPrefab == null)
        {
            Debug.LogWarning("SniperBulletShooter: Bullet Prefab が設定されていません。", this);
            return;
        }

        if (sideViewLaserController == null)
        {
            Debug.LogWarning("SniperBulletShooter: Side View Laser Controller が設定されていません。", this);
            return;
        }

        if (sideViewLaserController.GetLaserCount() <= 0)
        {
            Debug.LogWarning("SniperBulletShooter: 発射に使える横視点レーザーがありません。", this);
            return;
        }

        if (balanceManager == null)
        {
            Debug.LogWarning("SniperBulletShooter: Balance Manager が設定されていません。黄色ターゲット位置は変更せずに弾を発射します。", this);
        }

        shootingCoroutine = StartCoroutine(ShootRoutine());
    }

    public void StopShooting()
    {
        if (shootingCoroutine != null)
        {
            StopCoroutine(shootingCoroutine);
            shootingCoroutine = null;
        }

        if (sideViewLaserController != null)
        {
            sideViewLaserController.RestoreSideViewLaserVisibility();
        }

        DestroySpawnedBullets();
        DisableSniperDefenseModeIfNeeded();
        currentShotFinished = true;
        currentShotResolved = true;
        currentLaserIndex = -1;
    }

    private IEnumerator ShootRoutine()
    {
        EnableSniperDefenseModeIfNeeded();

        int totalShots = Mathf.Max(0, shotCount);

        for (int shotIndex = 0; shotIndex < totalShots; shotIndex++)
        {
            currentShotFinished = false;
            currentShotReachedTarget = false;
            currentShotResolved = false;
            currentLaserIndex = GetLaserIndexForShot(shotIndex);

            yield return BlinkLaserBeforeShot(currentLaserIndex);
            FireShot(currentLaserIndex);
            yield return new WaitUntil(() => currentShotFinished);

            ResolveCurrentShotIfNeeded();

            if (shotIndex < totalShots - 1)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, shotInterval));
            }
        }

        sideViewLaserController.RestoreSideViewLaserVisibility();
        shootingCoroutine = null;
        DisableSniperDefenseModeIfNeeded();
        NotifyAllShotsResolved();
    }

    private void FireShot(int laserIndex)
    {
        SetSniperTargetPositionForLaser(laserIndex);

        Vector3 startPosition = sideViewLaserController.GetLaserFirePosition(laserIndex);
        Vector3 targetPosition = sideViewLaserController.GetLaserEndPosition(laserIndex);
        currentLaserWorldY = targetPosition.y;

        GameObject bulletObject = Instantiate(bulletPrefab, startPosition, Quaternion.identity);
        SniperBullet bullet = bulletObject.GetComponent<SniperBullet>();
        if (bullet == null)
        {
            bullet = bulletObject.AddComponent<SniperBullet>();
        }

        spawnedBullets.Add(bulletObject);
        bullet.Initialize(startPosition, targetPosition, bulletSpeed, bulletLifeTime, OnBulletFinished);
        PlaySniperShotSound();
        RemoveDestroyedBulletReferences();
    }

    private void PlaySniperShotSound()
    {
        bool hasMissingAudioSetting = false;

        if (sniperShotAudioSource == null)
        {
            Debug.LogWarning("SniperBulletShooter: sniperShotAudioSource not assigned.", this);
            hasMissingAudioSetting = true;
        }

        if (sniperShotSound == null)
        {
            Debug.LogWarning("SniperBulletShooter: sniperShotSound not assigned.", this);
            hasMissingAudioSetting = true;
        }

        if (hasMissingAudioSetting)
        {
            return;
        }

        sniperShotAudioSource.PlayOneShot(sniperShotSound, sniperShotSoundVolume);
        Debug.Log("SniperBulletShooter: Sniper shot sound played.", this);
    }

    // 弾が判定位置へ届いた時、または寿命切れで消える時に呼ばれます。
    private void OnBulletFinished(SniperBullet bullet, bool reachedTarget)
    {
        if (bullet != null)
        {
            spawnedBullets.Remove(bullet.gameObject);
        }

        // 停止後に破棄された弾から通知されても、防御判定は行いません。
        if (shootingCoroutine == null)
        {
            return;
        }

        currentShotReachedTarget = reachedTarget;
        ResolveCurrentShotIfNeeded();
        currentShotFinished = true;
    }

    // 今回は上、下、上、下の交互発射です。後でここを変えると発射順を変更できます。
    private int GetLaserIndexForShot(int shotIndex)
    {
        int laserCount = Mathf.Max(1, sideViewLaserController.GetLaserCount());
        return shotIndex % laserCount;
    }

    // レーザー番号に合わせて、黄色ターゲットを上側/下側へ移動します。
    private void SetSniperTargetPositionForLaser(int laserIndex)
    {
        if (balanceManager == null)
        {
            return;
        }

        balanceManager.SetSniperTargetPosition(GetTargetPositionForLaserIndex(laserIndex));
    }

    // 弾が判定位置に到達した瞬間に、防御成功/失敗を決めます。
    private void ResolveCurrentShotIfNeeded()
    {
        if (currentShotResolved)
        {
            return;
        }

        currentShotResolved = true;

        if (!currentShotReachedTarget)
        {
            Debug.LogWarning("SniperBulletShooter: 弾が判定位置に届く前に消えました。次の弾へ進みます。", this);
            return;
        }

        if (balanceManager == null)
        {
            return;
        }

        balanceManager.ResolveSniperDefenseShot(currentLaserWorldY);
    }

    /// <summary>
    /// 次に発射するレーンだけを、時間倍率の影響を受けずに点滅させます。
    /// </summary>
    private IEnumerator BlinkLaserBeforeShot(int laserIndex)
    {
        if (sideViewLaserController == null)
        {
            Debug.LogWarning("SniperBulletShooter: Side View Laser Controller が未設定のため、発射前点滅をスキップします。", this);
            yield break;
        }

        float blinkDuration = Mathf.Max(0f, preFireBlink);
        float blinkInterval = Mathf.Max(0.02f, preFireBlinkInterval);
        float elapsed = 0f;
        float intervalTimer = 0f;
        bool visible = true;

        while (elapsed < blinkDuration)
        {
            if (intervalTimer <= 0f)
            {
                sideViewLaserController.SetOnlySideViewLaserVisible(laserIndex, visible);
                visible = !visible;
                intervalTimer = blinkInterval;
            }

            float unscaledDeltaTime = Time.unscaledDeltaTime;
            elapsed += unscaledDeltaTime;
            intervalTimer -= unscaledDeltaTime;
            yield return null;
        }

        // 点滅後は、実際に弾が飛ぶレーンだけを表示します。
        sideViewLaserController.SetOnlySideViewLaserVisible(laserIndex, true);
    }

    private void NotifyAllShotsResolved()
    {
        // SniperEventManagerへ、4発分の判定がすべて終わったことを知らせます。
        onAllShotsResolved?.Invoke();
        AllShotsResolved?.Invoke();
    }

    private void EnableSniperDefenseModeIfNeeded()
    {
        if (balanceManager == null)
        {
            return;
        }

        balanceManager.EnableSniperDefenseMode();
        enabledSniperDefenseMode = true;
    }

    private void DisableSniperDefenseModeIfNeeded()
    {
        if (!enabledSniperDefenseMode)
        {
            return;
        }

        if (balanceManager != null)
        {
            balanceManager.DisableSniperDefenseMode();
        }

        enabledSniperDefenseMode = false;
    }

    // 現在は2本レーザー前提です。レーザー数を増やした時も偶数/奇数で仮対応できます。
    private float GetTargetPositionForLaserIndex(int laserIndex)
    {
        if (laserIndex == 0)
        {
            return Mathf.Clamp01(upperLaserTargetPosition);
        }

        if (laserIndex == 1)
        {
            return Mathf.Clamp01(lowerLaserTargetPosition);
        }

        return Mathf.Clamp01(laserIndex % 2 == 0 ? upperLaserTargetPosition : lowerLaserTargetPosition);
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

    private void AutoFindSideViewLaserController()
    {
        sideViewLaserController = FindFirstObjectByType<SniperSideViewLaserController>();
    }

    private void AutoFindBalanceManager()
    {
        balanceManager = FindFirstObjectByType<BalanceManager>();
    }
}
