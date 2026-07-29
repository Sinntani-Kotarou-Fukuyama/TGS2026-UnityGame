using UnityEngine;

// SampleSceneの通常BGMと、スナイパーイベント専用BGMだけを切り替える小さな管理クラスです。
public class GameplayBgmController : MonoBehaviour
{
    [Header("BGM Sources")]
    [Tooltip("通常プレイ中にループ再生するAudioSourceです。Play On AwakeをONにします。")]
    [SerializeField] private AudioSource normalBgmSource;
    [Tooltip("スナイパーイベント中だけループ再生するAudioSourceです。Play On AwakeをOFFにします。")]
    [SerializeField] private AudioSource sniperEventBgmSource;

    private bool isNormalBgmPausedByController;
    private bool isOtherEventPauseActive;
    private bool isSniperEventBgmActive;

    // ポーズイベントなど、専用BGMを流さず通常BGMだけ止める時に使います。
    public void PauseForEvent()
    {
        if (isOtherEventPauseActive)
        {
            return;
        }

        isOtherEventPauseActive = true;
        PauseNormalBgmIfNeeded();
    }

    // PauseForEventで止めた通常BGMを、停止位置から再開します。
    public void ResumeAfterEvent()
    {
        if (!isOtherEventPauseActive)
        {
            return;
        }

        isOtherEventPauseActive = false;
        ResumeNormalBgmIfPossible();
    }

    public void StartSniperEventBgm()
    {
        if (isSniperEventBgmActive)
        {
            return;
        }

        if (normalBgmSource != null && normalBgmSource == sniperEventBgmSource)
        {
            Debug.LogWarning("GameplayBgmController: Normal Bgm SourceとSniper Event Bgm Sourceには別々のAudioSourceを設定してください。", this);
            return;
        }

        isSniperEventBgmActive = true;
        PauseNormalBgmIfNeeded();

        if (sniperEventBgmSource == null)
        {
            Debug.LogWarning("GameplayBgmController: Sniper Event Bgm Sourceが未設定です。通常BGMだけ一時停止します。", this);
            return;
        }

        if (sniperEventBgmSource.clip == null)
        {
            Debug.LogWarning("GameplayBgmController: Sniper Event Bgm SourceにAudioClipが設定されていません。", this);
            return;
        }

        // 必ず曲の先頭から開始できるように、一度停止してから再生します。
        sniperEventBgmSource.Stop();
        sniperEventBgmSource.Play();
    }

    public void EndSniperEventBgm()
    {
        if (!isSniperEventBgmActive)
        {
            return;
        }

        if (sniperEventBgmSource != null)
        {
            sniperEventBgmSource.Stop();
        }

        isSniperEventBgmActive = false;
        ResumeNormalBgmIfPossible();
    }

    private void PauseNormalBgmIfNeeded()
    {
        if (isNormalBgmPausedByController)
        {
            return;
        }

        if (normalBgmSource == null)
        {
            Debug.LogWarning("GameplayBgmController: Normal Bgm Sourceが未設定です。", this);
            return;
        }

        // もともと再生中だった場合だけ、このControllerが再開対象として記録します。
        if (normalBgmSource.isPlaying)
        {
            normalBgmSource.Pause();
            isNormalBgmPausedByController = true;
        }
    }

    private void ResumeNormalBgmIfPossible()
    {
        if (isSniperEventBgmActive || isOtherEventPauseActive || !isNormalBgmPausedByController)
        {
            return;
        }

        if (normalBgmSource != null)
        {
            normalBgmSource.UnPause();
        }
        else
        {
            Debug.LogWarning("GameplayBgmController: Normal Bgm Sourceが見つからないため、通常BGMを再開できません。", this);
        }

        isNormalBgmPausedByController = false;
    }

    private void OnDisable()
    {
        // イベント途中でControllerが無効化されても、専用BGMや一時停止状態を残しません。
        if (isSniperEventBgmActive && sniperEventBgmSource != null)
        {
            sniperEventBgmSource.Stop();
        }

        isSniperEventBgmActive = false;
        isOtherEventPauseActive = false;

        if (isNormalBgmPausedByController && normalBgmSource != null)
        {
            normalBgmSource.UnPause();
        }

        isNormalBgmPausedByController = false;
    }
}
