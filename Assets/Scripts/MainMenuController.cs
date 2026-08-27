using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class MainMenuController : MonoBehaviour
{
    private const string LastPlayedLevelKey = "BallPuzzleLastPlayedLevel";

    [Header("Scene")]
    [SerializeField] private string firstLevelScene = "Level01";

    [Header("UI")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button creditsBackButton;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip piecePlaceSound;
    [SerializeField] private AudioClip achievementSound;
    [SerializeField] private AudioClip failureSound;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.75f;

    private bool mainMenuWasShown;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (!HasRequiredUi())
        {
            Debug.LogError("MainMenu: required UI references are missing.", this);
            enabled = false;
            return;
        }

        playButton.onClick.AddListener(Play);
        continueButton.onClick.AddListener(Continue);
        controlsButton.onClick.AddListener(ShowControls);
        creditsButton.onClick.AddListener(ShowCredits);
        quitButton.onClick.AddListener(Quit);
        controlsBackButton.onClick.AddListener(ShowMainMenu);
        creditsBackButton.onClick.AddListener(ShowMainMenu);
        ConfigureAudio();
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(Play);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Continue);
        }

        if (controlsButton != null)
        {
            controlsButton.onClick.RemoveListener(ShowControls);
        }

        if (creditsButton != null)
        {
            creditsButton.onClick.RemoveListener(ShowCredits);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(Quit);
        }

        if (controlsBackButton != null)
        {
            controlsBackButton.onClick.RemoveListener(ShowMainMenu);
        }

        if (creditsBackButton != null)
        {
            creditsBackButton.onClick.RemoveListener(ShowMainMenu);
        }
    }

    private bool HasRequiredUi()
    {
        return playButton != null &&
               continueButton != null &&
               controlsButton != null &&
               creditsButton != null &&
               quitButton != null &&
               controlsBackButton != null &&
               creditsBackButton != null &&
               mainPanel != null &&
               controlsPanel != null &&
               creditsPanel != null;
    }

    private void Play()
    {
        BallPuzzleLevelController.ResetCountdownSession();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        StartCoroutine(PlayAchievementAndLoad(firstLevelScene));
    }

    private void Continue()
    {
        string lastPlayedLevel = PlayerPrefs.GetString(
            LastPlayedLevelKey,
            firstLevelScene);

        if (string.IsNullOrWhiteSpace(lastPlayedLevel) ||
            !Application.CanStreamedLevelBeLoaded(lastPlayedLevel))
        {
            Debug.LogWarning(
                "MainMenu: the saved level is unavailable. Loading '" +
                firstLevelScene + "' instead.",
                this);
            lastPlayedLevel = firstLevelScene;
        }

        StartCoroutine(PlayAchievementAndLoad(lastPlayedLevel));
    }

    private void LoadLevel(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("MainMenu: the target level scene is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "MainMenu: scene '" + sceneName + "' is not available in Build Settings.",
                this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void ShowControls()
    {
        PlayEffect(piecePlaceSound);
        ShowPanel(controlsPanel, controlsBackButton);
    }

    private void ShowCredits()
    {
        PlayEffect(piecePlaceSound);
        ShowPanel(creditsPanel, creditsBackButton);
    }

    private void ShowMainMenu()
    {
        if (mainMenuWasShown && mainPanel != null && !mainPanel.activeSelf)
        {
            PlayEffect(failureSound);
        }
        ShowPanel(mainPanel, playButton);
        mainMenuWasShown = true;
    }

    private void ConfigureAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.volume = musicVolume;
            audioSource.clip = backgroundMusic;
            audioSource.Play();
        }
    }

    private void PlayEffect(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, effectsVolume);
        }
    }

    private IEnumerator PlayAchievementAndLoad(string sceneName)
    {
        PlayEffect(achievementSound);
        float delay = achievementSound != null
            ? Mathf.Min(0.55f, achievementSound.length)
            : 0f;
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        LoadLevel(sceneName);
    }

    private void ShowPanel(GameObject panelToShow, Button selectedButton)
    {
        mainPanel.SetActive(panelToShow == mainPanel);
        controlsPanel.SetActive(panelToShow == controlsPanel);
        creditsPanel.SetActive(panelToShow == creditsPanel);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }
    }

    private void Quit()
    {
        PlayEffect(failureSound);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
