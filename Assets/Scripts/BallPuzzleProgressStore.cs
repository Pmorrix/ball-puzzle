using UnityEngine;

public static class BallPuzzleProgressStore
{
    private const string LastPlayedLevelKey = "BallPuzzleLastPlayedLevel";
    private const string TotalCompletionTimeKey = "BallPuzzleTotalCompletionTime";
    private const string LevelCompletionTimeKeyPrefix = "BallPuzzleCompletionTime.";
    private const string UnlockedPieceKeyPrefix = "BallPuzzleUnlockedPiece.";

    public static void RememberCurrentLevel(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        PlayerPrefs.SetString(LastPlayedLevelKey, sceneName);
        PlayerPrefs.Save();
    }

    public static void RegisterCompletedLevelTime(
        string sceneName,
        float completionTime)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string levelTimeKey = LevelCompletionTimeKeyPrefix + sceneName;
        float totalTime = GetTotalCompletionTime();

        if (!PlayerPrefs.HasKey(levelTimeKey))
        {
            PlayerPrefs.SetFloat(levelTimeKey, completionTime);
            PlayerPrefs.SetFloat(
                TotalCompletionTimeKey,
                totalTime + completionTime);
            PlayerPrefs.Save();
            return;
        }

        float previousLevelTime = Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(levelTimeKey, completionTime));
        if (completionTime >= previousLevelTime)
        {
            return;
        }

        PlayerPrefs.SetFloat(levelTimeKey, completionTime);
        PlayerPrefs.SetFloat(
            TotalCompletionTimeKey,
            Mathf.Max(0f, totalTime - previousLevelTime + completionTime));
        PlayerPrefs.Save();
    }

    public static void UnlockPiece(CircuitPieceType pieceType)
    {
        if (pieceType == CircuitPieceType.Start)
        {
            return;
        }

        PlayerPrefs.SetInt(GetUnlockedPieceKey(pieceType), 1);
        PlayerPrefs.Save();
    }

    public static bool IsPieceUnlocked(CircuitPieceType pieceType)
    {
        return pieceType != CircuitPieceType.Start &&
               PlayerPrefs.GetInt(GetUnlockedPieceKey(pieceType), 0) != 0;
    }

    private static string GetUnlockedPieceKey(CircuitPieceType pieceType)
    {
        return UnlockedPieceKeyPrefix + pieceType;
    }

    private static float GetTotalCompletionTime()
    {
        return Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(TotalCompletionTimeKey, 0f));
    }
}
