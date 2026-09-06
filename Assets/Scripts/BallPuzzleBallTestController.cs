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
    private readonly bool detectPrize;
    private readonly float prizeCollectionDistance;
    private readonly float fallHeight;
    private readonly float maximumTestDuration;
    private readonly float stoppedDuration;

    private float testStartedAt;
    private float lastTestDuration;
    private float stoppedAt = -1f;
    private bool hasTestDuration;
    private bool isRunning;
    private float driveSpeed;
    private float driveAcceleration;
    private float ballRadius;
    private Vector3 driveDirection;

    public bool HasTestDuration => hasTestDuration;
    public float LastTestDuration => lastTestDuration;

    public BallPuzzleBallTestController(
        Rigidbody ball,
        Transform ballSpawnPoint,
        Transform prize,
        float prizeCollectionDistance,
        float fallHeight,
        float maximumTestDuration,
        float stoppedDuration,
        bool detectPrize = true)
    {
        this.ball = ball;
        this.ballSpawnPoint = ballSpawnPoint;
        this.prize = prize;
        this.detectPrize = detectPrize;
        this.prizeCollectionDistance = prizeCollectionDistance;
        this.fallHeight = fallHeight;
        this.maximumTestDuration = maximumTestDuration;
        this.stoppedDuration = stoppedDuration;
    }

    public void Start(Vector3 launchDirection, float launchSpeed, float continuousAcceleration = 0f)
    {
        testStartedAt = Time.time;
        lastTestDuration = 0f;
        hasTestDuration = true;
        stoppedAt = -1f;
        isRunning = true;
        driveSpeed = launchSpeed;
        driveAcceleration = Mathf.Max(0f, continuousAcceleration);
        driveDirection = Vector3.ProjectOnPlane(launchDirection, Vector3.up).normalized;
        SphereCollider sphere = ball.GetComponent<SphereCollider>();
        Vector3 scale = ball.transform.lossyScale;
        ballRadius = sphere != null
            ? sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))
            : 0.6f;

        ball.gameObject.SetActive(true);
        ball.position = ballSpawnPoint.position;
        ball.rotation = ballSpawnPoint.rotation;
        ball.isKinematic = false;
        ball.angularVelocity = Vector3.zero;
        ball.linearVelocity = launchDirection * launchSpeed;
        ball.WakeUp();
    }

    public void StepPhysics()
    {
        if (!isRunning || ball.isKinematic || driveAcceleration <= 0f)
            return;
        // Propulsion acts only on track contact. The route never supplies a direction.
        if (!Physics.Raycast(ball.position, Vector3.down, out RaycastHit ground,
                ballRadius + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            ground.collider.GetComponentInParent<CircuitPiece>() == null)
            return;
        Vector3 velocity = Vector3.ProjectOnPlane(ball.linearVelocity, Vector3.up);
        float speed = velocity.magnitude;
        if (speed > 0.15f)
            driveDirection = velocity / speed;
        float acceleration = Mathf.Clamp((driveSpeed - speed) * 6f,
            -driveAcceleration, driveAcceleration);
        ball.AddForce(driveDirection * (acceleration * ball.mass), ForceMode.Force);
    }

    public BallPuzzleBallTestResult Evaluate()
    {
        if (!isRunning)
        {
            return BallPuzzleBallTestResult.InProgress;
        }

        if (detectPrize &&
            prize != null &&
            Vector3.Distance(ball.position, prize.position) <=
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
        ball.position = ballSpawnPoint.position;
        ball.rotation = ballSpawnPoint.rotation;
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
