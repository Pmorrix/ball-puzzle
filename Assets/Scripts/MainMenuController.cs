using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private const string LastPlayedLevelKey = "BallPuzzleLastPlayedLevel";

    [Header("Scene")]
    [SerializeField] private string firstLevelScene = "Level01video";

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
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        LoadLevel(firstLevelScene);
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

        LoadLevel(lastPlayedLevel);
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
        ShowPanel(controlsPanel, controlsBackButton);
    }

    private void ShowCredits()
    {
        ShowPanel(creditsPanel, creditsBackButton);
    }

    private void ShowMainMenu()
    {
        ShowPanel(mainPanel, playButton);
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
