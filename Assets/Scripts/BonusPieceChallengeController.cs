using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BonusPieceChallengeController : MonoBehaviour
{
    private enum TargetState
    {
        Pending,
        Secured,
        Missed
    }

    [Header("Scene")]
    [SerializeField] private Rigidbody ball;
    [SerializeField] private TMP_Text progressLabel;

    [Header("Bonus pieces")]
    [SerializeField] private CircuitPiece[] targetVisuals =
        new CircuitPiece[0];
    [SerializeField] private SpriteRenderer[] targetRings =
        new SpriteRenderer[0];
    [SerializeField] private string[] targetLabels = new string[0];

    [Header("Collection")]
    [SerializeField, Min(0.25f)] private float collectionRadius = 1.25f;
    [SerializeField, Min(0.25f)] private float exitRadius = 1.6f;
    [SerializeField, Min(0.1f)] private float minimumTraversalDistance = 1.25f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 18f;

    [Header("Available")]
    [SerializeField, Range(0.05f, 1f)]
    private float availablePieceAlpha = 0.55f;

    private TargetState[] targetStates = new TargetState[0];
    private bool[] targetEntered = new bool[0];
    private int[] targetEntryConnectors = new int[0];
    private Vector3[] targetEntryPositions = new Vector3[0];
    private Quaternion[] targetBaseLocalRotations = new Quaternion[0];
    private Vector3[] targetBaseWorldPositions = new Vector3[0];
    private Quaternion[] targetBaseWorldRotations = new Quaternion[0];
    private Vector3[] targetRotationCenters = new Vector3[0];
    private Color[] targetRingBaseColors = new Color[0];
    private CircuitPiece[] placedTargetPieces = new CircuitPiece[0];
    private float targetRotationAngle;
    private bool runActive;

    public int TargetCount => targetStates.Length;
    public int SecuredCount { get; private set; }
    public bool AreAllTargetsConnected
    {
        get
        {
            if (targetVisuals == null || targetVisuals.Length == 0)
                return false;
            foreach (CircuitPiece target in targetVisuals)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                    return false;
                BonusRotatingPieceConnector connector =
                    target.GetComponent<BonusRotatingPieceConnector>();
                if (connector == null || !connector.IsConnected)
                    return false;
            }
            return true;
        }
    }
    public string ProgressText =>
        "BONUS PIECES: " + SecuredCount + " / " + TargetCount;

    private void Awake()
    {
        if (ball == null)
        {
            Debug.LogError(
                "BonusPieceChallengeController: falta la referencia de la bola.",
                this);
            enabled = false;
            return;
        }

        InitializeTargets();
        ResetChallenge();
    }

    private void Update()
    {
        AnimateTargets();
    }

    public void BeginRun()
    {
        SecuredCount = 0;
        for (int i = 0; i < targetStates.Length; i++)
        {
            targetStates[i] = TargetState.Pending;
            ApplyTargetPresentation(i);
        }
        ResetTraversal();
        RefreshProgressLabel();
        runActive = true;
    }

    public void AbortRun()
    {
        runActive = false;
    }

    public void RegisterPlacedPiece(CircuitPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        for (int i = 0; i < targetVisuals.Length; i++)
        {
            CircuitPiece targetVisual = targetVisuals[i];
            if (targetVisual == null ||
                targetVisual.GetComponent<BonusRotatingPieceConnector>() != null ||
                targetVisual.PieceType != piece.PieceType ||
                targetStates[i] != TargetState.Pending)
            {
                continue;
            }

            placedTargetPieces[i] = piece;
            targetEntered[i] = false;
            targetEntryPositions[i] = Vector3.zero;
            return;
        }
    }

    public void RegisterConnectedTargetPiece(CircuitPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        for (int i = 0; i < targetVisuals.Length; i++)
        {
            if (targetVisuals[i] != piece ||
                targetStates[i] != TargetState.Pending)
            {
                continue;
            }

            placedTargetPieces[i] = piece;
            targetEntered[i] = false;
            targetEntryPositions[i] = Vector3.zero;
            return;
        }
    }

    public void UnregisterPiece(CircuitPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        for (int i = 0; i < placedTargetPieces.Length; i++)
        {
            if (placedTargetPieces[i] != piece)
            {
                continue;
            }

            placedTargetPieces[i] = null;
            targetEntered[i] = false;
            targetEntryPositions[i] = Vector3.zero;
        }
    }

    public void TrackBall()
    {
        if (!runActive || ball == null)
        {
            return;
        }

        for (int i = 0; i < targetStates.Length; i++)
        {
            if (targetStates[i] != TargetState.Pending)
            {
                continue;
            }

            CircuitPiece placedPiece = placedTargetPieces[i];
            if (placedPiece == null)
            {
                continue;
            }

            if (placedPiece.ConnectorCount != 2)
                continue;
            Bounds bounds = placedPiece.GetRenderBounds();
            if (ball.position.y < bounds.min.y - 0.25f ||
                ball.position.y > bounds.max.y + collectionRadius)
                continue;
            if (!targetEntered[i])
            {
                for (int connector = 0; connector < 2; connector++)
                {
                    if (HorizontalDistance(ball.position,
                            placedPiece.GetConnectorPosition(connector)) > collectionRadius)
                        continue;
                    targetEntered[i] = true;
                    targetEntryConnectors[i] = connector;
                    targetEntryPositions[i] = ball.position;
                    break;
                }
                continue;
            }

            int exitConnector = 1 - targetEntryConnectors[i];
            if (HorizontalDistance(ball.position,
                    placedPiece.GetConnectorPosition(exitConnector)) > collectionRadius)
            {
                continue;
            }

            float traversalDistance = HorizontalDistance(
                ball.position,
                targetEntryPositions[i]);
            if (traversalDistance >= minimumTraversalDistance)
            {
                SecureTarget(i);
            }
        }
    }

    public void CompleteRun()
    {
        runActive = false;
        for (int i = 0; i < targetStates.Length; i++)
        {
            if (targetStates[i] != TargetState.Pending)
            {
                continue;
            }

            targetStates[i] = TargetState.Missed;
            ApplyTargetPresentation(i);
        }

        for (int i = 0; i < targetStates.Length; i++)
        {
            if (targetStates[i] == TargetState.Secured &&
                i < targetVisuals.Length && targetVisuals[i] != null)
            {
                BallPuzzleProgressStore.UnlockPiece(targetVisuals[i].PieceType);
            }
        }

        RefreshProgressLabel();
    }

    public void ResetChallenge()
    {
        runActive = false;
        SecuredCount = 0;

        for (int i = 0; i < targetStates.Length; i++)
        {
            targetStates[i] = TargetState.Pending;
            ApplyTargetPresentation(i);
        }

        for (int i = 0; i < placedTargetPieces.Length; i++)
        {
            placedTargetPieces[i] = null;
        }

        ResetTraversal();

        RefreshProgressLabel();
    }

    public string CreateResultMessage()
    {
        StringBuilder result = new StringBuilder();
        result.Append(SecuredCount)
              .Append(" OF ")
              .Append(TargetCount)
              .Append(" SECURED");

        for (int i = 0; i < targetStates.Length; i++)
        {
            result.Append('\n')
                  .Append(GetTargetLabel(i))
                  .Append("  ")
                  .Append(targetStates[i] == TargetState.Secured
                      ? "SECURED"
                      : "MISSED");
        }

        return result.ToString();
    }

    private void InitializeTargets()
    {
        int count = targetVisuals != null ? targetVisuals.Length : 0;
        targetStates = new TargetState[count];
        targetEntered = new bool[count];
        targetEntryConnectors = new int[count];
        targetEntryPositions = new Vector3[count];
        targetBaseLocalRotations = new Quaternion[count];
        targetBaseWorldPositions = new Vector3[count];
        targetBaseWorldRotations = new Quaternion[count];
        targetRotationCenters = new Vector3[count];
        targetRingBaseColors = new Color[count];
        placedTargetPieces = new CircuitPiece[count];

        for (int i = 0; i < count; i++)
        {
            CircuitPiece visual = targetVisuals[i];
            if (visual != null)
            {
                Transform visualTransform = visual.transform;
                targetBaseLocalRotations[i] = visualTransform.localRotation;
                targetBaseWorldPositions[i] = visualTransform.position;
                targetBaseWorldRotations[i] = visualTransform.rotation;
                targetRotationCenters[i] = GetVisualCenter(visualTransform);
                visual.enabled = false;
                foreach (Collider targetCollider in
                         visual.GetComponentsInChildren<Collider>(true))
                {
                    targetCollider.enabled = true;
                }
            }

            SpriteRenderer ring = GetTargetRing(i);
            targetRingBaseColors[i] = ring != null
                ? ring.color
                : Color.white;
        }
    }

    private void AnimateTargets()
    {
        float time = Time.unscaledTime;
        targetRotationAngle += rotationSpeed * Time.unscaledDeltaTime;
        for (int i = 0; i < targetVisuals.Length; i++)
        {
            CircuitPiece visual = targetVisuals[i];
            if (visual != null &&
                visual.GetComponent<BonusRotatingPieceConnector>() == null)
            {
                Transform visualTransform = visual.transform;
                if (visual.PieceType == CircuitPieceType.Curve45Right ||
                    visual.PieceType == CircuitPieceType.Curve90 ||
                    visual.PieceType == CircuitPieceType.Curve180)
                {
                    Quaternion rotation = Quaternion.AngleAxis(
                        targetRotationAngle,
                        Vector3.up);
                    visualTransform.SetPositionAndRotation(
                        targetRotationCenters[i] +
                        rotation * (targetBaseWorldPositions[i] -
                                    targetRotationCenters[i]),
                        rotation * targetBaseWorldRotations[i]);
                }
                else
                {
                    Quaternion animatedRotation =
                        targetBaseLocalRotations[i] *
                        Quaternion.Euler(0f, targetRotationAngle, 0f);
                    visualTransform.localRotation = animatedRotation;
                }
            }

            SpriteRenderer ring = GetTargetRing(i);
            if (ring == null)
            {
                continue;
            }

            Color color = GetRingBaseColor(i);
            float pulse = 0.55f +
                          (Mathf.Sin(time * 2.2f + i) + 1f) * 0.225f;
            color.a *= pulse;
            ring.color = color;
        }
    }

    private static Vector3 GetVisualCenter(Transform targetTransform)
    {
        Renderer[] renderers =
            targetTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return targetTransform.position;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    private void SecureTarget(int index)
    {
        targetStates[index] = TargetState.Secured;
        SecuredCount++;
        ApplyTargetPresentation(index);
        RefreshProgressLabel();
    }

    private void ApplyTargetPresentation(int index)
    {
        if (index >= 0 &&
            index < targetVisuals.Length &&
            targetVisuals[index] != null)
        {
            CircuitPiece visual = targetVisuals[index];
            visual.ClearTint();
            SetPieceAlpha(
                visual,
                targetStates[index] == TargetState.Pending
                    ? availablePieceAlpha
                    : 1f);
        }

        SpriteRenderer ring = GetTargetRing(index);
        if (ring != null)
        {
            ring.color = GetRingBaseColor(index);
        }
    }

    private static void SetPieceAlpha(CircuitPiece piece, float alpha)
    {
        foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
            {
                SetTransparentSurface(material);
                SetMaterialAlpha(material, alpha);
            }
        }
    }

    private static void SetTransparentSurface(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }

    private void RefreshProgressLabel()
    {
        if (progressLabel != null)
        {
            progressLabel.text = ProgressText;
        }
    }

    private void ResetTraversal()
    {
        for (int i = 0; i < targetEntered.Length; i++)
        {
            targetEntered[i] = false;
            targetEntryConnectors[i] = -1;
            targetEntryPositions[i] = Vector3.zero;
        }
    }

    private static float GetDistanceToPiece(Vector3 point, CircuitPiece piece)
    {
        float closestDistance = float.PositiveInfinity;
        foreach (Collider pieceCollider in
                 piece.GetComponentsInChildren<Collider>(false))
        {
            if (pieceCollider == null || !pieceCollider.enabled)
            {
                continue;
            }

            Vector3 closestPoint = pieceCollider.ClosestPoint(point);
            closestDistance = Mathf.Min(
                closestDistance,
                HorizontalDistance(point, closestPoint));
        }

        return closestDistance;
    }

    private string GetTargetLabel(int index)
    {
        if (targetLabels != null &&
            index >= 0 &&
            index < targetLabels.Length &&
            !string.IsNullOrWhiteSpace(targetLabels[index]))
        {
            return targetLabels[index].Trim();
        }

        return "BONUS PIECE " + (index + 1);
    }

    private SpriteRenderer GetTargetRing(int index)
    {
        return targetRings != null && index >= 0 && index < targetRings.Length
            ? targetRings[index]
            : null;
    }

    private Color GetRingBaseColor(int index)
    {
        if (index < 0 || index >= targetRingBaseColors.Length)
        {
            return Color.white;
        }

        return targetRingBaseColors[index];
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

}
