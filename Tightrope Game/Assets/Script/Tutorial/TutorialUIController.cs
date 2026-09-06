using UnityEngine;
using TMPro;

public class TutorialUIController : MonoBehaviour
{
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TMP_Text tutorialText;

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

    private void Update()
    {
        if (!tutorialUI.activeSelf)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Next();
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
