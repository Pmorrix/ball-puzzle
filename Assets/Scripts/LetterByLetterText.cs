using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class LetterByLetterText : MonoBehaviour
{
    [SerializeField] private Text targetText;
    [SerializeField, Min(0f)] private float secondsPerLetter = 0.05f;
    [SerializeField] private bool playOnEnable = true;

    private string fullText;
    private Coroutine revealCoroutine;

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponentInChildren<Text>(true);
        }

        if (targetText != null)
        {
            fullText = targetText.text;
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
    }

    public void Play()
    {
        if (targetText == null || string.IsNullOrEmpty(fullText))
        {
            return;
        }

        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
        }

        revealCoroutine = StartCoroutine(RevealText());
    }

    private IEnumerator RevealText()
    {
        targetText.text = string.Empty;

        for (int index = 0; index < fullText.Length; index++)
        {
            targetText.text += fullText[index];

            if (secondsPerLetter > 0f)
            {
                yield return new WaitForSecondsRealtime(secondsPerLetter);
            }
        }

        revealCoroutine = null;
    }
}
