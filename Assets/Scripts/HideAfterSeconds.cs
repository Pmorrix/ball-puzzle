using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public sealed class HideAfterSeconds : MonoBehaviour
{
    [SerializeField] private float seconds = 3f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private CanvasGroup presenterCanvasGroup;
    [SerializeField] private Behaviour flowController;
    [SerializeField] private Behaviour[] additionalFlowControllers;
    [SerializeField] private Behaviour inputRaycaster;
    [SerializeField] private GameObject[] revealAfterPresentation;

    private float previousTimeScale = 1f;
    private bool presentationRunning;
    private Graphic[] fadeGraphics;
    private float[] initialGraphicAlphas;

    private void OnEnable()
    {
        previousTimeScale = Time.timeScale;
        presentationRunning = true;

        if (presenterCanvasGroup != null)
        {
            presenterCanvasGroup.alpha = 1f;
        }
        CacheFadeGraphics();
        ApplyFadeAlpha(1f);

        if (flowController != null)
        {
            flowController.enabled = false;
        }
        SetAdditionalFlowControllersEnabled(false);
        if (inputRaycaster != null)
        {
            inputRaycaster.enabled = false;
        }
        SetRevealObjectsActive(false);

        Time.timeScale = 0f;
        StartCoroutine(HideAfterDelay());
    }

    private void Update()
    {
        if (presentationRunning)
        {
            SetRevealObjectsActive(false);
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));

        if (presenterCanvasGroup != null && fadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyFadeAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            ApplyFadeAlpha(0f);
        }

        FinishPresentation();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        FinishPresentation();
    }

    private void FinishPresentation()
    {
        if (!presentationRunning)
        {
            return;
        }

        presentationRunning = false;
        Time.timeScale = previousTimeScale;

        if (flowController != null)
        {
            flowController.enabled = true;
        }
        SetAdditionalFlowControllersEnabled(true);
        if (inputRaycaster != null)
        {
            inputRaycaster.enabled = true;
        }
        SetRevealObjectsActive(true);
    }

    private void SetRevealObjectsActive(bool isActive)
    {
        if (revealAfterPresentation == null)
        {
            return;
        }

        foreach (GameObject revealObject in revealAfterPresentation)
        {
            if (revealObject != null)
            {
                revealObject.SetActive(isActive);
            }
        }
    }

    private void SetAdditionalFlowControllersEnabled(bool isEnabled)
    {
        if (additionalFlowControllers == null)
        {
            return;
        }

        foreach (Behaviour controller in additionalFlowControllers)
        {
            if (controller != null)
            {
                controller.enabled = isEnabled;
            }
        }
    }

    private void CacheFadeGraphics()
    {
        Graphic[] currentGraphics = GetComponentsInChildren<Graphic>(true);
        if (fadeGraphics != null && fadeGraphics.Length == currentGraphics.Length)
        {
            return;
        }

        fadeGraphics = currentGraphics;
        initialGraphicAlphas = new float[fadeGraphics.Length];
        for (int index = 0; index < fadeGraphics.Length; index++)
        {
            initialGraphicAlphas[index] = fadeGraphics[index].color.a;
        }
    }

    private void ApplyFadeAlpha(float alpha)
    {
        if (fadeGraphics == null || initialGraphicAlphas == null)
        {
            return;
        }

        for (int index = 0; index < fadeGraphics.Length; index++)
        {
            Graphic graphic = fadeGraphics[index];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = initialGraphicAlphas[index] * alpha;
            graphic.color = color;
        }
    }
}
