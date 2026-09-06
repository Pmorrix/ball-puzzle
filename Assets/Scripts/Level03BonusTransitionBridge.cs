using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Level03BonusTransitionBridge : MonoBehaviour
{
    private const string SourceScene = "Level03";
    private const string TargetScene = "Bonus01Test";

    private BallPuzzleLevelController levelController;
    private bool levelCompleted;
    private bool transitionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLevel03()
    {
        if (SceneManager.GetActiveScene().name != SourceScene)
        {
            return;
        }

        BallPuzzleLevelController[] controllers =
            FindObjectsByType<BallPuzzleLevelController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (controllers.Length == 0)
        {
            Debug.LogError(
                "Level03 transition: BallPuzzleLevelController not found.");
            return;
        }

        GameObject bridgeObject = new GameObject(nameof(Level03BonusTransitionBridge));
        bridgeObject.transform.SetParent(controllers[0].transform, false);

        Level03BonusTransitionBridge bridge =
            bridgeObject.AddComponent<Level03BonusTransitionBridge>();
        bridge.Initialize(controllers[0]);
    }

    private void Initialize(BallPuzzleLevelController controller)
    {
        levelController = controller;
        levelController.LevelCompleted += HandleLevelCompleted;
    }

    private void OnDestroy()
    {
        if (levelController != null)
        {
            levelController.LevelCompleted -= HandleLevelCompleted;
        }
    }

    private void Update()
    {
        if (!levelCompleted || transitionStarted || levelController == null ||
            !levelController.IsBuilding)
        {
            return;
        }

        transitionStarted = true;

        if (!Application.CanStreamedLevelBeLoaded(TargetScene))
        {
            Debug.LogError(
                "Level03 transition: scene '" + TargetScene +
                "' is not available in Build Settings.",
                this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(TargetScene);
    }

    private void HandleLevelCompleted()
    {
        levelCompleted = true;
    }
}
