using UnityEngine;
using TMPro;
using System.Collections;


public class TutorialUIController : MonoBehaviour
{
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] public TMP_Text nextHintText;
    [SerializeField] private CanvasGroup nextHintCanvasGroup;
    [SerializeField] private float hintFadeDuration = 1.0f;
    [SerializeField,Header("次への点滅スピード")] private float fadeSpeed = 1.0f;

    private string[] currentLines;
    private int index;
    private System.Action onFinished;
    private bool prevJoyconPressed = false;
    private Coroutine hintFadeRoutine;

    private void Start()
    {
        tutorialUI.SetActive(false);
    }

    public void ShowLines(string[] lines, System.Action finishedCallback = null)
    {
        Time.timeScale = 0f;

        currentLines = lines;
        index = 0;
        onFinished = finishedCallback;

        tutorialText.text = currentLines[index];
        tutorialUI.SetActive(true);

        if (hintFadeRoutine != null) StopCoroutine(hintFadeRoutine);
        hintFadeRoutine = StartCoroutine(HintFadeLoop());
    }

    private void Update()
    {

        if (!tutorialUI.activeSelf)
            return;

        var joycons = JoyconManager.Instance.j;

        // Joy-Con が無い場合（または途中で切断された場合）
        if (joycons == null || joycons.Count == 0)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Next();
            }
            return;
        }

        // Joy-Con がある場合だけ joycons[0] を読む
        Joycon jc = joycons[0];
        bool isPressed = jc.GetButton(Joycon.Button.DPAD_UP);

        if (isPressed && !prevJoyconPressed)
        {
            Next();
        }

        prevJoyconPressed = isPressed;
    }


    private void Next()
    {
        index++;

        if (index < currentLines.Length)
        {
            tutorialText.text = currentLines[index];
        }
        else
        {
            tutorialUI.SetActive(false);

            
            if (hintFadeRoutine != null) StopCoroutine(hintFadeRoutine);
            nextHintCanvasGroup.alpha = 1f;

            Time.timeScale = 1f;

            onFinished?.Invoke();
        }
    }
    private IEnumerator HintFadeLoop()
    {
        while (true)
        {
            // フェードアウト
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * fadeSpeed;
                nextHintCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            // フェードイン
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * fadeSpeed;
                nextHintCanvasGroup.alpha = t;
                yield return null;
            }
        }
    }


}