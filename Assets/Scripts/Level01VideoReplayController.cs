using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class Level01VideoReplayController : MonoBehaviour
{
    private const int RequiredCinematicCameraCount = 5;
    private const int CameraCollisionHitBufferSize = 32;
    private const int StoredReplayVersion = 1;
    private const int PiecePreviewLayer = 30;
    private const string StoredReplayFileName = "LastLevel01ResultReplay.json";

    private enum CinematicShot
    {
        Aerial = 0,
        Chase = 1,
        Curve = 2,
        HeadOn = 3,
        Hero = 4
    }

    private struct ReplaySample
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public ReplaySample(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    private struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public Vector3 LookTarget;

        public CameraPose(
            Vector3 position,
            Quaternion rotation,
            float fieldOfView,
            Vector3 lookTarget)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            LookTarget = lookTarget;
        }
    }

    [Serializable]
    private sealed class StoredReplayData
    {
        public int Version;
        public StoredReplaySample[] Samples;
        public StoredReplayPiece[] Pieces;
    }

    [Serializable]
    private struct StoredReplaySample
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    private struct StoredReplayPiece
    {
        public CircuitPieceType PieceType;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    [Header("Scene references")]
    [SerializeField] private BallPuzzleLevelController levelController;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera piecePreviewCamera;
    [SerializeField] private GameObject levelUi;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private SpriteRenderer workshopBackground;

    [Header("Cinematic cameras")]
    [SerializeField] private Camera aerialCamera;
    [SerializeField] private Camera chaseCamera;
    [SerializeField] private Camera curveCamera;
    [SerializeField] private Camera headOnCamera;
    [SerializeField] private Camera heroCamera;

    [Header("Shot sequence")]
    [Tooltip("Shot used from the start to 14% of the replay.")]
    [SerializeField] private CinematicShot openingPhaseShot = CinematicShot.Aerial;
    [Tooltip("Shot used after the opening and before the detected curve window.")]
    [SerializeField] private CinematicShot trackingPhaseShot = CinematicShot.Chase;
    [Tooltip("Shot used around the strongest detected curve.")]
    [SerializeField] private CinematicShot curvePhaseShot = CinematicShot.Curve;
    [Tooltip("Shot used after the curve window and before the final 14%.")]
    [SerializeField] private CinematicShot exitPhaseShot = CinematicShot.HeadOn;
    [Tooltip("Shot used during the final 14% and the final hold.")]
    [SerializeField] private CinematicShot finalPhaseShot = CinematicShot.Hero;

    [Header("Replay timing")]
    [SerializeField, Min(4f)] private float replayDuration = 8.5f;
    [SerializeField, Min(0f)] private float finalHeroHold = 0.55f;
    [SerializeField, Min(0f)] private float returnBlendDuration = 0.65f;
    [SerializeField, Range(0.01f, 0.1f)] private float sampleInterval = 0.025f;
    [SerializeField, Min(0.001f)] private float minimumSampleDistance = 0.015f;
    [SerializeField, Range(2, 4)] private int minimumSamples = 2;
    [SerializeField, Range(0.05f, 0.25f)] private float minimumReplayDistance = 0.25f;

    [Header("Camera movement")]
    [SerializeField, Min(0.1f)] private float positionSharpness = 11f;
    [SerializeField, Min(0.1f)] private float rotationSharpness = 13f;
    [SerializeField, Min(0.1f)] private float tangentLookDistance = 0.65f;
    [SerializeField, Min(0f)] private float cameraCollisionPadding = 0.22f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;

    [Header("Overview shot")]
    [SerializeField] private bool autoFrameOverview = true;
    [SerializeField] private Transform overviewTarget;
    [SerializeField] private Vector3 overviewCameraOffset = new Vector3(0f, 49f, -6f);
    [SerializeField, Range(30f, 60f)] private float overviewFieldOfView = 44f;

    [Header("Curve drama")]
    [SerializeField, Min(0.1f)] private float turnDetectionDistance = 0.65f;
    [SerializeField, Range(0f, 4f)] private float curveSlowdownBoost = 2.1f;
    [SerializeField, Range(0f, 15f)] private float curveCameraBank = 8f;
    [SerializeField, Range(0f, 0.25f)] private float curveCameraShake = 0.065f;

    [Header("Cinematic effects")]
    [SerializeField] private bool enablePostProcessing = true;
    [SerializeField] private bool enableBallParticles = true;
    [SerializeField] private bool showRecordedPathGlow;
    [SerializeField, Range(10f, 180f)] private float particleEmissionRate = 160f;
    [SerializeField, Range(0.2f, 1.5f)] private float particleLifetime = 1.15f;
    [SerializeField, Range(0.03f, 0.25f)] private float particleSize = 0.16f;
    [SerializeField, Range(0f, 1.5f)] private float particleSpeed = 0.42f;
    [SerializeField, Range(0.01f, 0.3f)] private float particleSpread = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float particleRiseSpeed = 0.18f;
    [SerializeField, Range(0f, 2f)] private float particleTurnBoost = 1f;
    [SerializeField, ColorUsage(true, true)] private Color particleHotColor =
        new Color(3.2f, 2.2f, 0.35f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color particleFlameColor =
        new Color(3f, 0.55f, 0.04f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color particleEmberColor =
        new Color(0.55f, 0.015f, 0.005f, 0f);
    [SerializeField] private Color ballLightColor = new Color(1f, 0.26f, 0.035f);
    [SerializeField, Min(0f)] private float ballLightIntensity = 8.5f;
    [SerializeField, Range(0f, 2f)] private float particleBloomIntensity = 0.95f;
    [SerializeField, Range(0f, 3f)] private float particleCurveBloomIntensity = 1.5f;

    [Header("Temporary last result viewer")]
    [SerializeField] private bool playStoredReplayOnStart;
    [SerializeField] private bool loopStoredReplay = true;
    [SerializeField, Min(0f)] private float storedReplayLoopDelay = 1f;
    [SerializeField] private Transform storedReplayPiecesRoot;
    [SerializeField] private CircuitPiece storedStraightPiecePrefab;
    [SerializeField] private CircuitPiece storedCurve45RightPiecePrefab;
    [SerializeField] private CircuitPiece storedHalfStraightPiecePrefab;

    private readonly List<ReplaySample> samples = new List<ReplaySample>();
    private readonly List<float> sampleDistances = new List<float>();
    private readonly List<float> playbackTimes = new List<float>();
    private readonly List<float> turnStrengths = new List<float>();
    private readonly List<float> turnSigns = new List<float>();
    private readonly RaycastHit[] cameraCollisionHits =
        new RaycastHit[CameraCollisionHitBufferSize];

    private Camera[] cinematicCameras;
    private Camera activeCamera;
    private Transform resolvedOverviewTarget;
    private Coroutine replayRoutine;
    private Coroutine storedReplayLoopRoutine;
    private readonly List<CircuitPiece> storedReplayPieceInstances =
        new List<CircuitPiece>();
    private bool recording;
    private bool replaying;
    private bool hasReplaySnapshot;
    private float nextSampleAt;
    private float totalDistance;
    private float strongestTurnProgress = 0.5f;
    private float strongestTurnSign = 1f;
    private float curveShotStart = 0.4f;
    private float curveShotEnd = 0.68f;
    private Bounds pathBounds;
    private Vector3 overallForward = Vector3.forward;
    private Vector3 headOnAnchorPosition;
    private Vector3 headOnAnchorForward = Vector3.forward;
    private Vector3 headOnAnchorRight = Vector3.right;

    private bool gameplayCameraWasEnabled;
    private bool previewCameraWasEnabled;
    private bool levelUiWasActive;
    private bool resultPanelWasActive;
    private bool ballDetectedCollisions;
    private RigidbodyInterpolation ballInterpolation;
    private Vector3 finalBallPosition;
    private Quaternion finalBallRotation;
    private Transform workshopBackgroundOriginalParent;
    private int workshopBackgroundOriginalSiblingIndex;
    private Vector3 workshopBackgroundOriginalLocalPosition;
    private Quaternion workshopBackgroundOriginalLocalRotation;
    private Vector3 workshopBackgroundOriginalLocalScale;
    private bool workshopBackgroundWasActive;
    private bool workshopBackgroundWasEnabled;
    private bool hasWorkshopBackgroundSnapshot;

    private Material cinematicMaterial;
    private GameObject particleObject;
    private ParticleSystem ballParticles;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private GameObject pathLineObject;
    private LineRenderer pathLine;
    private GameObject ballLightObject;
    private Light ballLight;
    private GameObject volumeObject;
    private VolumeProfile volumeProfile;
    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private DepthOfField depthOfField;

    private void Awake()
    {
        SanitizeReplaySettings();

        cinematicCameras = new[]
        {
            aerialCamera,
            chaseCamera,
            curveCamera,
            headOnCamera,
            heroCamera
        };

        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "Level01video: cinematic replay references are incomplete.",
                this);
            enabled = false;
            return;
        }

        SetAllCinematicCameras(false);
    }

    private void OnValidate()
    {
        SanitizeReplaySettings();
    }

    private void SanitizeReplaySettings()
    {
        minimumSamples = Mathf.Clamp(minimumSamples, 2, 4);
        minimumReplayDistance = Mathf.Clamp(minimumReplayDistance, 0.05f, 0.25f);
        particleEmissionRate = Mathf.Clamp(particleEmissionRate, 10f, 180f);
        particleLifetime = Mathf.Clamp(particleLifetime, 0.2f, 1.5f);
        particleSize = Mathf.Clamp(particleSize, 0.03f, 0.25f);
        particleSpeed = Mathf.Clamp(particleSpeed, 0f, 1.5f);
        particleSpread = Mathf.Clamp(particleSpread, 0.01f, 0.3f);
        particleRiseSpeed = Mathf.Clamp(particleRiseSpeed, 0f, 0.5f);
        particleTurnBoost = Mathf.Clamp(particleTurnBoost, 0f, 2f);
        particleBloomIntensity = Mathf.Clamp(particleBloomIntensity, 0f, 2f);
        particleCurveBloomIntensity = Mathf.Clamp(
            particleCurveBloomIntensity,
            particleBloomIntensity,
            3f);
    }

    private void Start()
    {
        if (playStoredReplayOnStart)
        {
            StartStoredReplayViewer();
        }
    }

    private void OnEnable()
    {
        if (levelController == null)
        {
            return;
        }

        levelController.TestStarted += HandleTestStarted;
        levelController.LevelCompleted += HandleLevelCompleted;
        levelController.LayoutReset += HandleLayoutReset;
    }

    private void OnDisable()
    {
        if (levelController != null)
        {
            levelController.TestStarted -= HandleTestStarted;
            levelController.LevelCompleted -= HandleLevelCompleted;
            levelController.LayoutReset -= HandleLayoutReset;
        }

        if (storedReplayLoopRoutine != null)
        {
            StopCoroutine(storedReplayLoopRoutine);
            storedReplayLoopRoutine = null;
        }

        StopReplayAndRestore(false, true);
        DestroyStoredReplayPieces();
    }

    private void FixedUpdate()
    {
        if (!recording || replaying || Time.fixedUnscaledTime < nextSampleAt)
        {
            return;
        }

        RecordBallSample(false);
        nextSampleAt = Time.fixedUnscaledTime + sampleInterval;
    }

    private void LateUpdate()
    {
        if (recording && !replaying)
        {
            if (resultPanel.activeSelf && ball.isKinematic)
            {
                TryStartReplay();
            }
        }

        if (!replaying)
        {
            return;
        }

        if (levelUi.activeSelf)
        {
            levelUi.SetActive(false);
        }

    }

    private bool HasRequiredReferences()
    {
        if (levelController == null || ball == null || gameplayCamera == null ||
            piecePreviewCamera == null || levelUi == null || resultPanel == null ||
            workshopBackground == null ||
            cinematicCameras == null ||
            cinematicCameras.Length != RequiredCinematicCameraCount)
        {
            return false;
        }

        foreach (Camera cinematicCamera in cinematicCameras)
        {
            if (cinematicCamera == null)
            {
                return false;
            }

            UniversalAdditionalCameraData cameraData =
                cinematicCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = enablePostProcessing;
        }

        return true;
    }

    private void HandleTestStarted()
    {
        BeginRecording();
    }

    private void BeginRecording()
    {
        StopReplayAndRestore(false, false);
        samples.Clear();
        sampleDistances.Clear();
        playbackTimes.Clear();
        turnStrengths.Clear();
        turnSigns.Clear();
        recording = true;
        nextSampleAt = Time.fixedUnscaledTime;
        RecordBallSample(true);
    }

    private void HandleLevelCompleted()
    {
        TryStartReplay();
    }

    private void TryStartReplay()
    {
        if (!recording || replayRoutine != null)
        {
            return;
        }

        RecordBallSample(true);
        recording = false;

        if (!BuildReplayData())
        {
            Debug.LogWarning(
                "Level01video: the finished run was too short to create a replay.",
                this);
            return;
        }

        replayRoutine = StartCoroutine(PlayReplay());
    }

    private void HandleLayoutReset()
    {
        recording = false;
        samples.Clear();
        StopReplayAndRestore(false, false);
    }

    private void RecordBallSample(bool force)
    {
        Vector3 position = ball.position;
        Quaternion rotation = ball.rotation;

        if (samples.Count > 0)
        {
            ReplaySample previous = samples[samples.Count - 1];
            float distance = Vector3.Distance(previous.Position, position);
            float rotationDelta = Quaternion.Angle(previous.Rotation, rotation);

            if (distance < 0.001f && rotationDelta < 0.5f)
            {
                if (force)
                {
                    samples[samples.Count - 1] = new ReplaySample(position, rotation);
                }
                return;
            }

            if (!force && distance < minimumSampleDistance)
            {
                return;
            }
        }

        samples.Add(new ReplaySample(position, rotation));
    }

    private bool BuildReplayData()
    {
        if (samples.Count < minimumSamples)
        {
            return false;
        }

        BuildDistancesAndBounds();
        if (totalDistance < minimumReplayDistance)
        {
            return false;
        }

        BuildTurnData();
        BuildPlaybackTiming();
        ConfigureShotTiming();
        ConfigureHeadOnAnchor();
        return true;
    }

    private void BuildDistancesAndBounds()
    {
        sampleDistances.Clear();
        totalDistance = 0f;
        sampleDistances.Add(0f);
        pathBounds = new Bounds(samples[0].Position, Vector3.zero);

        for (int i = 1; i < samples.Count; i++)
        {
            totalDistance += Vector3.Distance(
                samples[i - 1].Position,
                samples[i].Position);
            sampleDistances.Add(totalDistance);
            pathBounds.Encapsulate(samples[i].Position);
        }

        overallForward = Vector3.ProjectOnPlane(
            samples[samples.Count - 1].Position - samples[0].Position,
            Vector3.up).normalized;
        if (overallForward.sqrMagnitude < 0.01f)
        {
            overallForward = Vector3.forward;
        }
    }

    private void BuildTurnData()
    {
        turnStrengths.Clear();
        turnSigns.Clear();

        float strongestTurn = 0f;
        strongestTurnProgress = 0.5f;
        strongestTurnSign = 1f;

        for (int i = 0; i < samples.Count; i++)
        {
            float distance = sampleDistances[i];
            Vector3 previous = GetPositionAtDistance(distance - turnDetectionDistance);
            Vector3 next = GetPositionAtDistance(distance + turnDetectionDistance);
            Vector3 incoming = Vector3.ProjectOnPlane(
                samples[i].Position - previous,
                Vector3.up).normalized;
            Vector3 outgoing = Vector3.ProjectOnPlane(
                next - samples[i].Position,
                Vector3.up).normalized;

            float strength = 0f;
            float sign = 0f;
            if (incoming.sqrMagnitude > 0.01f && outgoing.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(incoming, outgoing);
                strength = Mathf.InverseLerp(4f, 28f, angle);
                sign = Mathf.Sign(Vector3.Dot(
                    Vector3.Cross(incoming, outgoing),
                    Vector3.up));
            }

            turnStrengths.Add(strength);
            turnSigns.Add(sign);

            if (strength <= strongestTurn)
            {
                continue;
            }

            strongestTurn = strength;
            strongestTurnProgress = distance / totalDistance;
            strongestTurnSign = Mathf.Approximately(sign, 0f) ? 1f : sign;
        }
    }

    private void BuildPlaybackTiming()
    {
        playbackTimes.Clear();
        playbackTimes.Add(0f);
        float weightedTotal = 0f;

        for (int i = 1; i < samples.Count; i++)
        {
            float distance = sampleDistances[i] - sampleDistances[i - 1];
            float turnStrength = (turnStrengths[i - 1] + turnStrengths[i]) * 0.5f;
            float weight = 1f + curveSlowdownBoost * turnStrength * turnStrength;
            weightedTotal += distance * weight;
            playbackTimes.Add(weightedTotal);
        }

        if (weightedTotal <= Mathf.Epsilon)
        {
            return;
        }

        for (int i = 1; i < playbackTimes.Count; i++)
        {
            playbackTimes[i] /= weightedTotal;
        }
    }

    private void ConfigureShotTiming()
    {
        float curveReplayTime = GetReplayTimeAtProgress(strongestTurnProgress);
        curveShotStart = Mathf.Clamp(curveReplayTime - 0.14f, 0.30f, 0.48f);
        curveShotEnd = Mathf.Clamp(curveReplayTime + 0.17f, 0.56f, 0.74f);
        curveShotEnd = Mathf.Max(curveShotEnd, curveShotStart + 0.12f);
    }

    private void ConfigureHeadOnAnchor()
    {
        float anchorProgress = GetPathProgressAtReplayTime(0.86f);
        EvaluatePath(anchorProgress, out headOnAnchorPosition, out _, out headOnAnchorForward);
        headOnAnchorRight = Vector3.Cross(Vector3.up, headOnAnchorForward).normalized;
        if (headOnAnchorRight.sqrMagnitude < 0.01f)
        {
            headOnAnchorRight = Vector3.right;
        }
    }

    private IEnumerator PlayReplay()
    {
        CaptureReplaySnapshot();
        PrepareReplayState();
        CreateCinematicEffects();
        replaying = true;

        ApplyReplayFrame(0f, true);
        if (ballParticles != null)
        {
            ballParticles.Clear(true);
            ballParticles.Play(true);
        }
        yield return null;

        float elapsed = 0f;
        while (elapsed < replayDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / replayDuration);
            ApplyReplayFrame(normalizedTime, false);
            yield return null;
        }

        float holdElapsed = 0f;
        while (holdElapsed < finalHeroHold)
        {
            holdElapsed += Time.unscaledDeltaTime;
            ApplyFinalShotHold(holdElapsed);
            yield return null;
        }

        yield return BlendBackToGameplayCamera();
        if (!playStoredReplayOnStart)
        {
            SaveStoredReplay();
        }
        RestoreReplayState(true);
        replayRoutine = null;
    }

    private void StartStoredReplayViewer()
    {
        piecePreviewCamera.enabled = false;
        levelUi.SetActive(false);
        resultPanel.SetActive(false);

        if (!TryLoadStoredReplay(out StoredReplayData storedReplay))
        {
            Debug.LogWarning(
                "LastReplayViewer: no valid result animation has been stored yet.",
                this);
            return;
        }

        SpawnStoredReplayPieces(storedReplay.Pieces);
        ball.isKinematic = true;
        ball.detectCollisions = false;
        ball.transform.SetPositionAndRotation(
            samples[0].Position,
            samples[0].Rotation);
        storedReplayLoopRoutine = StartCoroutine(PlayStoredReplayLoop());
    }

    private IEnumerator PlayStoredReplayLoop()
    {
        do
        {
            resultPanel.SetActive(false);
            ball.transform.SetPositionAndRotation(
                samples[0].Position,
                samples[0].Rotation);
            replayRoutine = StartCoroutine(PlayReplay());
            yield return replayRoutine;

            if (!loopStoredReplay)
            {
                break;
            }

            float elapsed = 0f;
            while (elapsed < storedReplayLoopDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        while (enabled && playStoredReplayOnStart);

        storedReplayLoopRoutine = null;
    }

    private void SaveStoredReplay()
    {
        StoredReplaySample[] storedSamples = new StoredReplaySample[samples.Count];
        for (int i = 0; i < samples.Count; i++)
        {
            storedSamples[i] = new StoredReplaySample
            {
                Position = samples[i].Position,
                Rotation = samples[i].Rotation
            };
        }

        StoredReplayData storedReplay = new StoredReplayData
        {
            Version = StoredReplayVersion,
            Samples = storedSamples,
            Pieces = CaptureStoredReplayPieces()
        };

        try
        {
            File.WriteAllText(
                GetStoredReplayPath(),
                JsonUtility.ToJson(storedReplay));
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            Debug.LogWarning(
                $"Level01video: the last result animation could not be stored. {exception.Message}",
                this);
        }
    }

    private StoredReplayPiece[] CaptureStoredReplayPieces()
    {
        CircuitPiece[] scenePieces = FindObjectsByType<CircuitPiece>(
            FindObjectsInactive.Exclude);
        List<StoredReplayPiece> storedPieces = new List<StoredReplayPiece>();

        foreach (CircuitPiece piece in scenePieces)
        {
            if (piece == null || piece.gameObject.scene != gameObject.scene ||
                piece.gameObject.layer == PiecePreviewLayer ||
                piece.name.StartsWith("Preview", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            storedPieces.Add(new StoredReplayPiece
            {
                PieceType = piece.PieceType,
                Position = piece.transform.position,
                Rotation = piece.transform.rotation,
                Scale = piece.transform.lossyScale
            });
        }

        return storedPieces.ToArray();
    }

    private bool TryLoadStoredReplay(out StoredReplayData storedReplay)
    {
        storedReplay = null;
        string storedReplayPath = GetStoredReplayPath();
        if (!File.Exists(storedReplayPath))
        {
            return false;
        }

        try
        {
            storedReplay = JsonUtility.FromJson<StoredReplayData>(
                File.ReadAllText(storedReplayPath));
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException)
        {
            Debug.LogWarning(
                $"LastReplayViewer: the stored animation could not be read. {exception.Message}",
                this);
            return false;
        }

        if (storedReplay == null || storedReplay.Version != StoredReplayVersion ||
            storedReplay.Samples == null || storedReplay.Samples.Length < minimumSamples)
        {
            return false;
        }

        samples.Clear();
        foreach (StoredReplaySample storedSample in storedReplay.Samples)
        {
            samples.Add(new ReplaySample(
                storedSample.Position,
                storedSample.Rotation));
        }

        return BuildReplayData();
    }

    private void SpawnStoredReplayPieces(StoredReplayPiece[] storedPieces)
    {
        DestroyStoredReplayPieces();
        if (storedPieces == null)
        {
            return;
        }

        foreach (StoredReplayPiece storedPiece in storedPieces)
        {
            CircuitPiece prefab = GetStoredPiecePrefab(storedPiece.PieceType);
            if (prefab == null)
            {
                continue;
            }

            CircuitPiece instance = Instantiate(
                prefab,
                storedPiece.Position,
                storedPiece.Rotation,
                storedReplayPiecesRoot);
            instance.name = $"Stored Replay {prefab.DisplayName}";
            instance.transform.localScale = storedPiece.Scale;
            instance.ClearTint();
            storedReplayPieceInstances.Add(instance);
        }
    }

    private CircuitPiece GetStoredPiecePrefab(CircuitPieceType pieceType)
    {
        switch (pieceType)
        {
            case CircuitPieceType.Straight:
                return storedStraightPiecePrefab;
            case CircuitPieceType.Curve45Right:
                return storedCurve45RightPiecePrefab;
            case CircuitPieceType.HalfStraight:
                return storedHalfStraightPiecePrefab;
            default:
                return null;
        }
    }

    private void DestroyStoredReplayPieces()
    {
        foreach (CircuitPiece instance in storedReplayPieceInstances)
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
            }
        }

        storedReplayPieceInstances.Clear();
    }

    private static string GetStoredReplayPath()
    {
        return Path.Combine(Application.persistentDataPath, StoredReplayFileName);
    }

    private void CaptureReplaySnapshot()
    {
        gameplayCameraWasEnabled = gameplayCamera.enabled;
        previewCameraWasEnabled = piecePreviewCamera.enabled;
        levelUiWasActive = levelUi.activeSelf;
        resultPanelWasActive = resultPanel.activeSelf;
        ballDetectedCollisions = ball.detectCollisions;
        ballInterpolation = ball.interpolation;
        finalBallPosition = ball.position;
        finalBallRotation = ball.rotation;
        CaptureWorkshopBackgroundSnapshot();
        hasReplaySnapshot = true;
    }

    private void PrepareReplayState()
    {
        gameplayCamera.enabled = false;
        piecePreviewCamera.enabled = false;
        PrepareWorkshopBackground();
        levelUi.SetActive(false);
        ball.detectCollisions = false;
        ball.interpolation = RigidbodyInterpolation.None;
        SetAllCinematicCameras(false);
        activeCamera = null;
    }

    private void ApplyReplayFrame(float normalizedTime, bool forceCameraCut)
    {
        float pathProgress = GetPathProgressAtReplayTime(normalizedTime);
        EvaluatePath(pathProgress, out Vector3 position, out Quaternion rotation, out Vector3 forward);
        ball.transform.SetPositionAndRotation(position, rotation);

        float turnStrength = GetTurnStrength(pathProgress);
        float turnSign = GetTurnSign(pathProgress);
        CinematicShot shot = GetShot(normalizedTime);
        bool cameraCut = ActivateShotCamera(shot) || forceCameraCut;
        CameraPose pose = GetCameraPose(
            shot,
            normalizedTime,
            pathProgress,
            position,
            forward,
            turnStrength,
            turnSign);
        ApplyCameraPose(pose, cameraCut);
        UpdateWorkshopBackground();
        UpdateCinematicEffects(turnStrength, pose.LookTarget, pathProgress);
    }

    private void ApplyFinalShotHold(float holdElapsed)
    {
        EvaluatePath(1f, out Vector3 position, out Quaternion rotation, out Vector3 forward);
        ball.transform.SetPositionAndRotation(position, rotation);
        CinematicShot finalShot = GetShot(1f);
        bool cameraCut = ActivateShotCamera(finalShot);
        CameraPose pose;
        if (finalShot == CinematicShot.Hero)
        {
            float orbitTime = 1f + holdElapsed / Mathf.Max(0.01f, finalHeroHold);
            pose = GetHeroPose(orbitTime, position, forward);
        }
        else
        {
            pose = GetCameraPose(
                finalShot,
                1f,
                1f,
                position,
                forward,
                GetTurnStrength(1f),
                GetTurnSign(1f));
        }
        ApplyCameraPose(pose, cameraCut);
        UpdateWorkshopBackground();
        UpdateCinematicEffects(0f, pose.LookTarget, 1f);
    }

    private CinematicShot GetShot(float normalizedTime)
    {
        if (normalizedTime < 0.14f)
        {
            return ResolveConfiguredShot(openingPhaseShot, CinematicShot.Aerial);
        }
        if (normalizedTime < curveShotStart)
        {
            return ResolveConfiguredShot(trackingPhaseShot, CinematicShot.Chase);
        }
        if (normalizedTime < curveShotEnd)
        {
            return ResolveConfiguredShot(curvePhaseShot, CinematicShot.Curve);
        }
        if (normalizedTime < 0.86f)
        {
            return ResolveConfiguredShot(exitPhaseShot, CinematicShot.HeadOn);
        }
        return ResolveConfiguredShot(finalPhaseShot, CinematicShot.Hero);
    }

    private static CinematicShot ResolveConfiguredShot(
        CinematicShot configuredShot,
        CinematicShot fallbackShot)
    {
        int shotIndex = (int)configuredShot;
        return shotIndex >= 0 && shotIndex < RequiredCinematicCameraCount
            ? configuredShot
            : fallbackShot;
    }

    private bool ActivateShotCamera(CinematicShot shot)
    {
        Camera nextCamera = cinematicCameras[(int)shot];
        if (activeCamera == nextCamera && activeCamera.enabled)
        {
            return false;
        }

        if (activeCamera != null)
        {
            activeCamera.enabled = false;
        }

        activeCamera = nextCamera;
        activeCamera.enabled = true;
        return true;
    }

    private CameraPose GetCameraPose(
        CinematicShot shot,
        float normalizedTime,
        float pathProgress,
        Vector3 ballPosition,
        Vector3 forward,
        float turnStrength,
        float turnSign)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        switch (shot)
        {
            case CinematicShot.Aerial:
                return GetAerialPose(ballPosition);
            case CinematicShot.Chase:
                return GetChasePose(normalizedTime, ballPosition, forward, right);
            case CinematicShot.Curve:
                return GetCurvePose(
                    normalizedTime,
                    ballPosition,
                    forward,
                    right,
                    turnStrength,
                    turnSign);
            case CinematicShot.HeadOn:
                return GetHeadOnPose(ballPosition);
            default:
                float heroTime = Mathf.InverseLerp(0.86f, 1f, normalizedTime);
                return GetHeroPose(heroTime, ballPosition, forward);
        }
    }

    private CameraPose GetAerialPose(Vector3 ballPosition)
    {
        if (autoFrameOverview)
        {
            Vector3 overviewCenter = GetOverviewCenter();
            return CreateCameraPose(
                overviewCenter + overviewCameraOffset,
                overviewCenter,
                overviewFieldOfView,
                0f,
                false);
        }

        float radius = Mathf.Max(5f, pathBounds.extents.magnitude);
        Vector3 right = Vector3.Cross(Vector3.up, overallForward).normalized;
        Vector3 position = pathBounds.center - overallForward * (radius * 0.75f + 2f) +
                           right * (radius * 0.55f + 1.5f) +
                           Vector3.up * (radius * 0.8f + 5f);
        Vector3 lookTarget = Vector3.Lerp(pathBounds.center, ballPosition, 0.55f);
        return CreateCameraPose(position, lookTarget, 44f, 0f, false);
    }

    private Vector3 GetOverviewCenter()
    {
        Transform target = ResolveOverviewTarget();
        return target != null ? target.position : pathBounds.center;
    }

    private Transform ResolveOverviewTarget()
    {
        if (overviewTarget != null)
        {
            return overviewTarget;
        }
        if (resolvedOverviewTarget != null)
        {
            return resolvedOverviewTarget;
        }

        GameObject board = GameObject.Find("Board");
        resolvedOverviewTarget = board != null ? board.transform : null;
        return resolvedOverviewTarget;
    }

    private CameraPose GetChasePose(
        float normalizedTime,
        Vector3 ballPosition,
        Vector3 forward,
        Vector3 right)
    {
        float sideDrift = Mathf.Sin(normalizedTime * Mathf.PI * 5f) * 0.35f;
        Vector3 position = ballPosition - forward * 3.25f +
                           right * (0.55f + sideDrift) +
                           Vector3.up * 1.35f;
        Vector3 lookTarget = ballPosition + forward * 1.45f + Vector3.up * 0.18f;
        return CreateCameraPose(position, lookTarget, 52f, sideDrift * 2f, true);
    }

    private CameraPose GetCurvePose(
        float normalizedTime,
        Vector3 ballPosition,
        Vector3 forward,
        Vector3 right,
        float turnStrength,
        float turnSign)
    {
        float sideSign = Mathf.Approximately(turnSign, 0f)
            ? -strongestTurnSign
            : -turnSign;
        float shakeX = (Mathf.PerlinNoise(normalizedTime * 31f, 0.17f) - 0.5f) * 2f;
        float shakeY = (Mathf.PerlinNoise(0.53f, normalizedTime * 37f) - 0.5f) * 2f;
        Vector3 shake = (right * shakeX + Vector3.up * shakeY) *
                        curveCameraShake * turnStrength;
        Vector3 position = ballPosition + right * sideSign * 3.5f -
                           forward * 0.45f + Vector3.up * 1.15f + shake;
        Vector3 lookTarget = ballPosition + forward * 0.9f + Vector3.up * 0.16f;
        float fieldOfView = Mathf.Lerp(43f, 35f, turnStrength);
        float bank = -sideSign * curveCameraBank * turnStrength;
        return CreateCameraPose(position, lookTarget, fieldOfView, bank, true);
    }

    private CameraPose GetHeadOnPose(Vector3 ballPosition)
    {
        Vector3 position = headOnAnchorPosition + headOnAnchorForward * 3.4f +
                           headOnAnchorRight * 0.3f + Vector3.up * 0.9f;
        Vector3 lookTarget = ballPosition + Vector3.up * 0.15f;
        return CreateCameraPose(position, lookTarget, 48f, -2f, true);
    }

    private CameraPose GetHeroPose(float heroTime, Vector3 ballPosition, Vector3 forward)
    {
        float angle = Mathf.Lerp(-35f, 95f, Mathf.Clamp01(heroTime));
        if (heroTime > 1f)
        {
            angle += (heroTime - 1f) * 35f;
        }
        Vector3 orbitDirection = Quaternion.AngleAxis(angle, Vector3.up) * -forward;
        Vector3 position = ballPosition + orbitDirection * 3.8f +
                           Vector3.up * Mathf.Lerp(1.25f, 2.05f, Mathf.Clamp01(heroTime));
        Vector3 lookTarget = ballPosition + Vector3.up * 0.22f;
        float fieldOfView = Mathf.Lerp(40f, 34f, Mathf.Clamp01(heroTime));
        return CreateCameraPose(position, lookTarget, fieldOfView, 0f, true);
    }

    private CameraPose CreateCameraPose(
        Vector3 desiredPosition,
        Vector3 lookTarget,
        float fieldOfView,
        float roll,
        bool avoidCollisions)
    {
        Vector3 resolvedPosition = avoidCollisions
            ? ResolveCameraCollision(lookTarget, desiredPosition)
            : desiredPosition;
        Vector3 lookDirection = lookTarget - resolvedPosition;
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            lookDirection = Vector3.forward;
        }

        Quaternion rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up) *
                              Quaternion.AngleAxis(roll, Vector3.forward);
        return new CameraPose(resolvedPosition, rotation, fieldOfView, lookTarget);
    }

    private Vector3 ResolveCameraCollision(Vector3 lookTarget, Vector3 desiredPosition)
    {
        Vector3 origin = lookTarget + Vector3.up * 0.55f;
        Vector3 offset = desiredPosition - origin;
        float distance = offset.magnitude;
        if (distance < 0.1f)
        {
            return desiredPosition;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            offset / distance,
            cameraCollisionHits,
            distance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);
        float closestDistance = distance;
        RaycastHit closestHit = default;
        bool foundBlockingHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraCollisionHits[i];
            if (hit.collider == null ||
                hit.collider.transform == ball.transform ||
                hit.collider.transform.IsChildOf(ball.transform) ||
                hit.collider.bounds.SqrDistance(lookTarget) < 0.5f)
            {
                continue;
            }

            if (hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestHit = hit;
            foundBlockingHit = true;
        }

        return foundBlockingHit
            ? closestHit.point + closestHit.normal * cameraCollisionPadding
            : desiredPosition;
    }

    private void ApplyCameraPose(CameraPose pose, bool immediate)
    {
        if (activeCamera == null)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        float positionBlend = immediate
            ? 1f
            : 1f - Mathf.Exp(-positionSharpness * deltaTime);
        float rotationBlend = immediate
            ? 1f
            : 1f - Mathf.Exp(-rotationSharpness * deltaTime);

        activeCamera.transform.position = Vector3.Lerp(
            activeCamera.transform.position,
            pose.Position,
            positionBlend);
        activeCamera.transform.rotation = Quaternion.Slerp(
            activeCamera.transform.rotation,
            pose.Rotation,
            rotationBlend);
        activeCamera.fieldOfView = Mathf.Lerp(
            activeCamera.fieldOfView,
            pose.FieldOfView,
            positionBlend);
    }

    private IEnumerator BlendBackToGameplayCamera()
    {
        if (activeCamera == null || returnBlendDuration <= 0f)
        {
            yield break;
        }

        Vector3 startPosition = activeCamera.transform.position;
        Quaternion startRotation = activeCamera.transform.rotation;
        float startFieldOfView = activeCamera.fieldOfView;
        Vector3 targetPosition = gameplayCamera.transform.position;
        Quaternion targetRotation = gameplayCamera.transform.rotation;
        float targetFieldOfView = gameplayCamera.fieldOfView;
        float elapsed = 0f;

        while (elapsed < returnBlendDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / returnBlendDuration));
            activeCamera.transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t);
            activeCamera.transform.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t);
            activeCamera.fieldOfView = Mathf.Lerp(
                startFieldOfView,
                targetFieldOfView,
                t);
            UpdateWorkshopBackground();
            UpdateCinematicEffects(0f, finalBallPosition, 1f);
            yield return null;
        }
    }

    private void CaptureWorkshopBackgroundSnapshot()
    {
        Transform backgroundTransform = workshopBackground.transform;
        workshopBackgroundOriginalParent = backgroundTransform.parent;
        workshopBackgroundOriginalSiblingIndex = backgroundTransform.GetSiblingIndex();
        workshopBackgroundOriginalLocalPosition = backgroundTransform.localPosition;
        workshopBackgroundOriginalLocalRotation = backgroundTransform.localRotation;
        workshopBackgroundOriginalLocalScale = backgroundTransform.localScale;
        workshopBackgroundWasActive = workshopBackground.gameObject.activeSelf;
        workshopBackgroundWasEnabled = workshopBackground.enabled;
        hasWorkshopBackgroundSnapshot = true;
    }

    private void PrepareWorkshopBackground()
    {
        if (!hasWorkshopBackgroundSnapshot || workshopBackground == null)
        {
            return;
        }

        workshopBackground.transform.SetParent(null, false);
        workshopBackground.gameObject.SetActive(true);
        workshopBackground.enabled = true;
    }

    private void UpdateWorkshopBackground()
    {
        if (!hasWorkshopBackgroundSnapshot || workshopBackground == null ||
            activeCamera == null || workshopBackground.sprite == null)
        {
            return;
        }

        float distance = Mathf.Min(60f, activeCamera.farClipPlane * 0.8f);
        distance = Mathf.Max(distance, activeCamera.nearClipPlane + 1f);
        float verticalSize = 2f * distance * Mathf.Tan(
            activeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float horizontalSize = verticalSize * activeCamera.aspect;
        Vector2 spriteSize = workshopBackground.sprite.bounds.size;
        float scale = Mathf.Max(
            horizontalSize / Mathf.Max(0.01f, spriteSize.x),
            verticalSize / Mathf.Max(0.01f, spriteSize.y)) * 1.06f;

        Transform backgroundTransform = workshopBackground.transform;
        backgroundTransform.SetPositionAndRotation(
            activeCamera.transform.position + activeCamera.transform.forward * distance,
            activeCamera.transform.rotation);
        backgroundTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private void RestoreWorkshopBackground()
    {
        if (!hasWorkshopBackgroundSnapshot)
        {
            return;
        }

        if (workshopBackground != null)
        {
            Transform backgroundTransform = workshopBackground.transform;
            backgroundTransform.SetParent(workshopBackgroundOriginalParent, false);
            if (workshopBackgroundOriginalParent != null)
            {
                backgroundTransform.SetSiblingIndex(Mathf.Clamp(
                    workshopBackgroundOriginalSiblingIndex,
                    0,
                    workshopBackgroundOriginalParent.childCount - 1));
            }
            backgroundTransform.localPosition = workshopBackgroundOriginalLocalPosition;
            backgroundTransform.localRotation = workshopBackgroundOriginalLocalRotation;
            backgroundTransform.localScale = workshopBackgroundOriginalLocalScale;
            workshopBackground.enabled = workshopBackgroundWasEnabled;
            workshopBackground.gameObject.SetActive(workshopBackgroundWasActive);
        }

        workshopBackgroundOriginalParent = null;
        hasWorkshopBackgroundSnapshot = false;
    }

    private void EvaluatePath(
        float progress,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 forward)
    {
        float distance = Mathf.Clamp01(progress) * totalDistance;
        int upperIndex = FindUpperIndex(sampleDistances, distance);
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        float lowerDistance = sampleDistances[lowerIndex];
        float upperDistance = sampleDistances[upperIndex];
        float segmentProgress = Mathf.InverseLerp(lowerDistance, upperDistance, distance);

        position = Vector3.Lerp(
            samples[lowerIndex].Position,
            samples[upperIndex].Position,
            segmentProgress);
        rotation = Quaternion.Slerp(
            samples[lowerIndex].Rotation,
            samples[upperIndex].Rotation,
            segmentProgress);

        Vector3 previous = GetPositionAtDistance(distance - tangentLookDistance);
        Vector3 next = GetPositionAtDistance(distance + tangentLookDistance);
        forward = (next - previous).normalized;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = overallForward;
        }
    }

    private Vector3 GetPositionAtDistance(float distance)
    {
        float clampedDistance = Mathf.Clamp(distance, 0f, totalDistance);
        int upperIndex = FindUpperIndex(sampleDistances, clampedDistance);
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        float t = Mathf.InverseLerp(
            sampleDistances[lowerIndex],
            sampleDistances[upperIndex],
            clampedDistance);
        return Vector3.Lerp(
            samples[lowerIndex].Position,
            samples[upperIndex].Position,
            t);
    }

    private float GetPathProgressAtReplayTime(float replayTime)
    {
        int upperIndex = FindUpperIndex(playbackTimes, Mathf.Clamp01(replayTime));
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        float t = Mathf.InverseLerp(
            playbackTimes[lowerIndex],
            playbackTimes[upperIndex],
            replayTime);
        float distance = Mathf.Lerp(
            sampleDistances[lowerIndex],
            sampleDistances[upperIndex],
            t);
        return distance / totalDistance;
    }

    private float GetReplayTimeAtProgress(float progress)
    {
        float distance = Mathf.Clamp01(progress) * totalDistance;
        int upperIndex = FindUpperIndex(sampleDistances, distance);
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        float t = Mathf.InverseLerp(
            sampleDistances[lowerIndex],
            sampleDistances[upperIndex],
            distance);
        return Mathf.Lerp(playbackTimes[lowerIndex], playbackTimes[upperIndex], t);
    }

    private float GetTurnStrength(float progress)
    {
        return InterpolatePathValue(turnStrengths, progress);
    }

    private float GetTurnSign(float progress)
    {
        float sign = InterpolatePathValue(turnSigns, progress);
        return Mathf.Abs(sign) < 0.1f ? 0f : Mathf.Sign(sign);
    }

    private float InterpolatePathValue(List<float> values, float progress)
    {
        float distance = Mathf.Clamp01(progress) * totalDistance;
        int upperIndex = FindUpperIndex(sampleDistances, distance);
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        float t = Mathf.InverseLerp(
            sampleDistances[lowerIndex],
            sampleDistances[upperIndex],
            distance);
        return Mathf.Lerp(values[lowerIndex], values[upperIndex], t);
    }

    private static int FindUpperIndex(List<float> values, float target)
    {
        int low = 0;
        int high = values.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (values[middle] < target)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    private void CreateCinematicEffects()
    {
        CreateCinematicMaterial();
        if (enableBallParticles)
        {
            CreateBallParticles();
        }
        if (showRecordedPathGlow)
        {
            CreatePathLine();
        }
        CreateBallLight();
        CreatePostProcessing();
    }

    private void CreateCinematicMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            return;
        }

        cinematicMaterial = new Material(shader)
        {
            name = "Level01video Cinematic Glow"
        };
    }

    private void CreateBallParticles()
    {
        CreateParticleMaterial();
        if (particleMaterial == null)
        {
            return;
        }

        particleObject = new GameObject("Cinematic Fire Particles");
        particleObject.transform.SetParent(ball.transform, false);
        ballParticles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ballParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            particleLifetime * 0.55f,
            particleLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            particleSpeed * 0.2f,
            particleSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(
            particleSize * 0.28f,
            particleSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.CeilToInt(
            particleEmissionRate * particleLifetime * (2.5f + particleTurnBoost));
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = ballParticles.emission;
        emission.rateOverTime = particleEmissionRate;

        ParticleSystem.ShapeModule shape = ballParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = particleSpread;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            ballParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFireParticleGradient();

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            ballParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.62f, 0.55f),
                new Keyframe(1f, 0f)));

        ParticleSystem.VelocityOverLifetimeModule velocity =
            ballParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = particleRiseSpeed;

        ParticleSystem.NoiseModule noise = ballParticles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = particleSpread * 1.6f;
        noise.frequency = 1.35f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;
        noise.octaveCount = 2;

        ParticleSystem.RotationOverLifetimeModule rotation =
            ballParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.sortingOrder = 2;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        ballParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            return;
        }

        particleTexture = CreateSoftParticleTexture();
        particleMaterial = new Material(shader)
        {
            name = "Level01video Fire Particles",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (particleMaterial.HasProperty("_BaseMap"))
        {
            particleMaterial.SetTexture("_BaseMap", particleTexture);
        }
        if (particleMaterial.HasProperty("_MainTex"))
        {
            particleMaterial.SetTexture("_MainTex", particleTexture);
        }
        if (particleMaterial.HasProperty("_Surface"))
        {
            particleMaterial.SetFloat("_Surface", 1f);
        }
        if (particleMaterial.HasProperty("_Blend"))
        {
            particleMaterial.SetFloat("_Blend", 2f);
        }
        if (particleMaterial.HasProperty("_Mode"))
        {
            particleMaterial.SetFloat("_Mode", 4f);
        }
        if (particleMaterial.HasProperty("_SrcBlend"))
        {
            particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (particleMaterial.HasProperty("_DstBlend"))
        {
            particleMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        if (particleMaterial.HasProperty("_ZWrite"))
        {
            particleMaterial.SetFloat("_ZWrite", 0f);
        }
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMaterial.EnableKeyword("_ALPHABLEND_ON");
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Level01video Soft Fire Particle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(
                    (x + 0.5f) / textureSize * 2f - 1f,
                    (y + 0.5f) / textureSize * 2f - 1f);
                float alpha = Mathf.Pow(
                    Mathf.Clamp01(1f - point.magnitude),
                    1.35f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Gradient CreateFireParticleGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(particleHotColor, 0f),
                new GradientColorKey(particleHotColor, 0.18f),
                new GradientColorKey(particleFlameColor, 0.48f),
                new GradientColorKey(particleEmberColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(particleHotColor.a, 0f),
                new GradientAlphaKey(0.95f, 0.25f),
                new GradientAlphaKey(0.62f, 0.62f),
                new GradientAlphaKey(0.16f, 0.86f),
                new GradientAlphaKey(particleEmberColor.a, 1f)
            });
        return gradient;
    }

    private void CreatePathLine()
    {
        if (cinematicMaterial == null)
        {
            return;
        }

        pathLineObject = new GameObject("Recorded Route Glow");
        pathLineObject.transform.SetParent(transform, false);
        pathLine = pathLineObject.AddComponent<LineRenderer>();
        pathLine.sharedMaterial = cinematicMaterial;
        pathLine.useWorldSpace = true;
        pathLine.loop = false;
        pathLine.widthMultiplier = 0.02f;
        pathLine.positionCount = 0;
        pathLine.shadowCastingMode = ShadowCastingMode.Off;
        pathLine.receiveShadows = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.35f, 1f), 0f),
                new GradientColorKey(new Color(0.1f, 1f, 0.85f), 0.65f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.03f, 0f),
                new GradientAlphaKey(0.12f, 0.75f),
                new GradientAlphaKey(0.05f, 1f)
            });
        pathLine.colorGradient = gradient;
    }

    private void UpdatePathLine(float pathProgress)
    {
        if (pathLine == null || samples.Count < 2 || totalDistance <= 0f)
        {
            return;
        }

        float visibleDistance = Mathf.Clamp01(pathProgress) * totalDistance;
        if (visibleDistance <= 0.001f)
        {
            pathLine.positionCount = 0;
            return;
        }

        int upperIndex = FindUpperIndex(sampleDistances, visibleDistance);
        int lowerIndex = Mathf.Max(0, upperIndex - 1);
        int visiblePointCount = lowerIndex + 2;
        pathLine.positionCount = visiblePointCount;

        for (int i = 0; i <= lowerIndex; i++)
        {
            pathLine.SetPosition(i, samples[i].Position + Vector3.up * 0.07f);
        }

        pathLine.SetPosition(
            visiblePointCount - 1,
            GetPositionAtDistance(visibleDistance) + Vector3.up * 0.07f);
    }

    private void CreateBallLight()
    {
        if (ballLightIntensity <= 0f)
        {
            return;
        }

        ballLightObject = new GameObject("Cinematic Ball Light");
        ballLightObject.transform.SetParent(ball.transform, false);
        ballLight = ballLightObject.AddComponent<Light>();
        ballLight.type = LightType.Point;
        ballLight.color = ballLightColor;
        ballLight.intensity = ballLightIntensity;
        ballLight.range = 6f;
        ballLight.shadows = LightShadows.None;
    }

    private void CreatePostProcessing()
    {
        if (!enablePostProcessing)
        {
            return;
        }

        volumeObject = new GameObject("Level01video Cinematic Volume");
        volumeObject.transform.SetParent(transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.weight = 1f;

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volumeProfile.name = "Level01video Runtime Cinematic Profile";
        volume.sharedProfile = volumeProfile;

        bloom = volumeProfile.Add<Bloom>(true);
        bloom.threshold.Override(0.5f);
        bloom.intensity.Override(particleBloomIntensity);
        bloom.scatter.Override(0.76f);

        vignette = volumeProfile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.005f, 0.012f, 0.035f));
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.5f);

        ColorAdjustments color = volumeProfile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.08f);
        color.contrast.Override(14f);
        color.saturation.Override(9f);
        color.colorFilter.Override(new Color(0.92f, 0.98f, 1f));

        chromaticAberration = volumeProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.Override(0.035f);

        lensDistortion = volumeProfile.Add<LensDistortion>(true);
        lensDistortion.intensity.Override(-0.035f);

        depthOfField = volumeProfile.Add<DepthOfField>(true);
        depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
        depthOfField.focusDistance.Override(5f);
        depthOfField.aperture.Override(8f);
        depthOfField.focalLength.Override(42f);
        depthOfField.bladeCount.Override(7);
    }

    private void UpdateCinematicEffects(
        float turnStrength,
        Vector3 lookTarget,
        float pathProgress)
    {
        UpdatePathLine(pathProgress);

        if (ballLight != null)
        {
            ballLight.intensity = ballLightIntensity * Mathf.Lerp(1f, 1.65f, turnStrength);
            ballLight.range = Mathf.Lerp(6f, 8f, turnStrength);
        }

        if (ballParticles != null)
        {
            ParticleSystem.EmissionModule emission = ballParticles.emission;
            emission.rateOverTime = particleEmissionRate * Mathf.Lerp(
                1f,
                1f + particleTurnBoost,
                turnStrength);

            ParticleSystem.MainModule main = ballParticles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                particleSpeed * 0.2f,
                particleSpeed * Mathf.Lerp(1f, 1.45f, turnStrength));
        }

        if (bloom != null)
        {
            bloom.intensity.value = Mathf.Lerp(
                particleBloomIntensity,
                particleCurveBloomIntensity,
                turnStrength);
        }
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Lerp(0.035f, 0.13f, turnStrength);
        }
        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(-0.035f, -0.11f, turnStrength);
        }
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.3f, 0.38f, turnStrength);
        }
        if (depthOfField != null && activeCamera != null)
        {
            depthOfField.focusDistance.value = Mathf.Max(
                0.1f,
                Vector3.Distance(activeCamera.transform.position, lookTarget));
        }
    }

    private void StopReplayAndRestore(bool revealResult, bool restoreBallPose = true)
    {
        recording = false;
        if (replayRoutine != null)
        {
            StopCoroutine(replayRoutine);
            replayRoutine = null;
        }

        if (hasReplaySnapshot)
        {
            RestoreReplayState(revealResult, restoreBallPose);
        }
        else
        {
            SetAllCinematicCameras(false);
        }
    }

    private void RestoreReplayState(bool revealResult, bool restoreBallPose = true)
    {
        replaying = false;
        RestoreWorkshopBackground();
        if (ball != null)
        {
            if (restoreBallPose)
            {
                ball.transform.SetPositionAndRotation(finalBallPosition, finalBallRotation);
            }
            ball.detectCollisions = ballDetectedCollisions;
            ball.interpolation = ballInterpolation;
        }

        SetAllCinematicCameras(false);
        activeCamera = null;
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = gameplayCameraWasEnabled;
        }
        if (piecePreviewCamera != null)
        {
            piecePreviewCamera.enabled = previewCameraWasEnabled;
        }
        if (resultPanel != null)
        {
            resultPanel.SetActive(revealResult || resultPanelWasActive);
        }
        if (levelUi != null)
        {
            levelUi.SetActive(levelUiWasActive);
        }

        DestroyCinematicEffects();
        hasReplaySnapshot = false;
    }

    private void SetAllCinematicCameras(bool enabledState)
    {
        if (cinematicCameras == null)
        {
            return;
        }

        foreach (Camera cinematicCamera in cinematicCameras)
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.enabled = enabledState;
            }
        }
    }

    private void DestroyCinematicEffects()
    {
        if (particleObject != null) Destroy(particleObject);
        if (pathLineObject != null) Destroy(pathLineObject);
        if (ballLightObject != null) Destroy(ballLightObject);
        if (volumeObject != null) Destroy(volumeObject);
        if (volumeProfile != null) Destroy(volumeProfile);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (particleTexture != null) Destroy(particleTexture);
        if (cinematicMaterial != null) Destroy(cinematicMaterial);

        particleObject = null;
        ballParticles = null;
        particleMaterial = null;
        particleTexture = null;
        pathLineObject = null;
        pathLine = null;
        ballLightObject = null;
        ballLight = null;
        volumeObject = null;
        volumeProfile = null;
        cinematicMaterial = null;
        bloom = null;
        vignette = null;
        chromaticAberration = null;
        lensDistortion = null;
        depthOfField = null;
    }
}
