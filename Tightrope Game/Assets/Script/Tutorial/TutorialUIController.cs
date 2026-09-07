using UnityEngine;
using TMPro;

public class TutorialUIController : MonoBehaviour
{
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] public TMP_Text nextHintText;

    private string[] currentLines;
    private int index;
    private System.Action onFinished;

    private void Start()
    {
        tutorialUI.SetActive(false);
    }

    public void ShowLines(string[] lines, System.Action finishedCallback = null)
    {
        currentLines = lines;
        index = 0;
        onFinished = finishedCallback;

        tutorialText.text = currentLines[index];
        tutorialUI.SetActive(true);
    }

    private void LateUpdate()
    {
        if (!tutorialUI.activeSelf)
            return;

        // キーボード（クリック）
        if (Input.GetMouseButtonDown(0))
        {
            Next();
        }

        // Joy-Con（Xボタン）
        var joycons = JoyconManager.Instance.j;
        if (joycons != null && joycons.Count > 0)
        {
            Joycon jc = joycons[0];
            if (jc != null && jc.GetButtonDown(Joycon.Button.DPAD_UP))
            {
                Next();
            }

        }


        if (joycons == null)
        {
            Debug.Log("JoyconManager.Instance.j が null");
        }
        else if (joycons.Count == 0)
        {
            Debug.Log("Joy-Con が見つかっていない（Count=0）");
        }
        else
        {
            Joycon jc = joycons[0];
            Debug.Log("Joy-Con state = " + jc.state);
        }

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

            if (onFinished != null)
            {
                onFinished.Invoke();
            }
        }
    }
}
