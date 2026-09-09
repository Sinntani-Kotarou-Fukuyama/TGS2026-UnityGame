using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 最終ゴール到達時のUI表示と花火風Effect再生だけを担当します。
/// </summary>
public class GoalCelebrationController : MonoBehaviour
{
    [Header("Goal UI")]
    [SerializeField] private GameObject goalEffectUI;

    [Header("Effect")]
    [SerializeField] private Camera effectCamera;
    [SerializeField] private GameObject[] effectPrefabs;
    [SerializeField, Min(1)] private int spawnCount = 12;
    [SerializeField, Min(0f)] private float effectScale = 1.6f;
    [SerializeField, Min(0f)] private float viewportDepth = 4f;
    [SerializeField] private Vector2[] viewportSpawnPositions =
    {
        new Vector2(0.15f, 0.75f),
        new Vector2(0.85f, 0.75f),
        new Vector2(0.22f, 0.52f),
        new Vector2(0.78f, 0.52f),
        new Vector2(0.50f, 0.68f),
        new Vector2(0.30f, 0.35f),
        new Vector2(0.70f, 0.35f),
        new Vector2(0.50f, 0.48f)
    };

    [Header("Timing")]
    [SerializeField, Min(0f)] private float celebrationDuration = 3f;
    [SerializeField, Min(0f)] private float firstSpawnTime = 0.2f;
    [SerializeField, Min(0f)] private float lastSpawnTime = 2.2f;

    private bool isPlaying;

    public bool PlayCelebration(Action onCompleted)
    {
        if (isPlaying)
        {
            return true;
        }

        if (!isActiveAndEnabled)
        {
            return false;
        }

        isPlaying = true;
        StartCoroutine(PlayCelebrationRoutine(onCompleted));
        return true;
    }

    private IEnumerator PlayCelebrationRoutine(Action onCompleted)
    {
        if (goalEffectUI != null)
        {
            goalEffectUI.SetActive(true);
            goalEffectUI.transform.SetAsLastSibling();
        }

        int safeSpawnCount = Mathf.Max(1, spawnCount);
        float safeDuration = Mathf.Max(0f, celebrationDuration);
        float safeFirstSpawnTime = Mathf.Clamp(firstSpawnTime, 0f, safeDuration);
        float safeLastSpawnTime = Mathf.Clamp(lastSpawnTime, safeFirstSpawnTime, safeDuration);
        int firstColorIndex = GetFirstColorIndex();
        float elapsed = 0f;

        for (int i = 0; i < safeSpawnCount; i++)
        {
            float normalizedIndex = safeSpawnCount <= 1 ? 0f : i / (float)(safeSpawnCount - 1);
            float spawnTime = Mathf.Lerp(safeFirstSpawnTime, safeLastSpawnTime, normalizedIndex);

            while (elapsed < spawnTime)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SpawnEffect(i, firstColorIndex);
        }

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (goalEffectUI != null)
        {
            goalEffectUI.SetActive(false);
        }

        isPlaying = false;
        onCompleted?.Invoke();
    }

    private int GetFirstColorIndex()
    {
        if (effectPrefabs == null || effectPrefabs.Length == 0)
        {
            return 0;
        }

        return UnityEngine.Random.Range(0, effectPrefabs.Length);
    }

    private void SpawnEffect(int spawnIndex, int firstColorIndex)
    {
        if (effectCamera == null || effectPrefabs == null || effectPrefabs.Length == 0 ||
            viewportSpawnPositions == null || viewportSpawnPositions.Length == 0)
        {
            return;
        }

        GameObject effectPrefab = effectPrefabs[(firstColorIndex + spawnIndex) % effectPrefabs.Length];
        if (effectPrefab == null)
        {
            return;
        }

        Vector2 viewportPosition = viewportSpawnPositions[spawnIndex % viewportSpawnPositions.Length];
        float depth = Mathf.Max(effectCamera.nearClipPlane, viewportDepth);
        Vector3 worldPosition = effectCamera.ViewportToWorldPoint(
            new Vector3(viewportPosition.x, viewportPosition.y, depth));
        GameObject effectInstance = Instantiate(effectPrefab, worldPosition, effectCamera.transform.rotation);
        effectInstance.transform.localScale *= Mathf.Max(0f, effectScale);

        Destroy(effectInstance, GetEffectLifetime(effectInstance));
    }

    private static float GetEffectLifetime(GameObject effectInstance)
    {
        const float cleanupMargin = 0.25f;
        float maximumLifetime = 0.1f;
        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            float particleLifetime = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            maximumLifetime = Mathf.Max(maximumLifetime, particleLifetime);
        }

        return maximumLifetime + cleanupMargin;
    }
}
