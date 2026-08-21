using UnityEngine;

public enum BallPuzzleBallTestResult
{
    InProgress,
    PrizeCollected,
    Fell,
    TimedOut,
    Stopped
}

public sealed class BallPuzzleBallTestController
{
    private const float MinimumMovingSpeedSquared = 0.04f;
    private const float StoppedCheckDelay = 2f;

    private readonly Rigidbody ball;
    private readonly Transform ballSpawnPoint;
    private readonly Transform prize;
    private readonly float prizeCollectionDistance;
    private readonly float fallHeight;
    private readonly float maximumTestDuration;
    private readonly float stoppedDuration;

    private float testStartedAt;
    private float lastTestDuration;
    private float stoppedAt = -1f;
    private bool hasTestDuration;
    private bool isRunning;

    public bool HasTestDuration => hasTestDuration;
    public float LastTestDuration => lastTestDuration;

    public BallPuzzleBallTestController(
        Rigidbody ball,
        Transform ballSpawnPoint,
        Transform prize,
        float prizeCollectionDistance,
        float fallHeight,
        float maximumTestDuration,
        float stoppedDuration)
    {
        this.ball = ball;
        this.ballSpawnPoint = ballSpawnPoint;
        this.prize = prize;
        this.prizeCollectionDistance = prizeCollectionDistance;
        this.fallHeight = fallHeight;
        this.maximumTestDuration = maximumTestDuration;
        this.stoppedDuration = stoppedDuration;
    }

    public void Start(Vector3 launchDirection, float launchSpeed)
    {
        testStartedAt = Time.time;
        lastTestDuration = 0f;
        hasTestDuration = true;
        stoppedAt = -1f;
        isRunning = true;

        ball.gameObject.SetActive(true);
        ball.transform.SetPositionAndRotation(
            ballSpawnPoint.position,
            ballSpawnPoint.rotation);
        ball.isKinematic = false;
        ball.angularVelocity = Vector3.zero;
        ball.linearVelocity = launchDirection * launchSpeed;
        ball.WakeUp();
    }

    public BallPuzzleBallTestResult Evaluate()
    {
        if (!isRunning)
        {
            return BallPuzzleBallTestResult.InProgress;
        }

        if (Vector3.Distance(ball.position, prize.position) <=
            prizeCollectionDistance)
        {
            return BallPuzzleBallTestResult.PrizeCollected;
        }
        if (ball.position.y < fallHeight)
        {
            return BallPuzzleBallTestResult.Fell;
        }
        if (Time.time - testStartedAt >= maximumTestDuration)
        {
            return BallPuzzleBallTestResult.TimedOut;
        }

        if (Time.time - testStartedAt > StoppedCheckDelay &&
            ball.linearVelocity.sqrMagnitude < MinimumMovingSpeedSquared)
        {
            if (stoppedAt < 0f)
            {
                stoppedAt = Time.time;
            }
            else if (Time.time - stoppedAt >= stoppedDuration)
            {
                return BallPuzzleBallTestResult.Stopped;
            }
        }
        else
        {
            stoppedAt = -1f;
        }

        return BallPuzzleBallTestResult.InProgress;
    }

    public void Finish()
    {
        CaptureCurrentDuration();
        FreezeBall();
        isRunning = false;
    }

    private void CaptureCurrentDuration()
    {
        if (!hasTestDuration || !isRunning)
        {
            return;
        }

        lastTestDuration = GetDisplayedDuration();
    }

    public float GetDisplayedDuration()
    {
        if (!hasTestDuration)
        {
            return 0f;
        }
        if (isRunning)
        {
            return Mathf.Max(0f, Time.time - testStartedAt);
        }
        return lastTestDuration;
    }

    public void ClearDuration()
    {
        hasTestDuration = false;
        lastTestDuration = 0f;
    }

    public void ResetBallForBuild()
    {
        if (!ball.isKinematic)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
        }

        ball.isKinematic = true;
        ball.transform.SetPositionAndRotation(
            ballSpawnPoint.position,
            ballSpawnPoint.rotation);
        ball.gameObject.SetActive(true);
        isRunning = false;
    }

    private void FreezeBall()
    {
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
    }
}
