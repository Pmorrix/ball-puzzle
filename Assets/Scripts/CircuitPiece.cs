using UnityEngine;

public enum CircuitPieceType
{
    Start,
    Straight,
    Curve45Right
}

[DisallowMultipleComponent]
public sealed class CircuitPiece : MonoBehaviour
{
    [SerializeField] private CircuitPieceType pieceType;
    [SerializeField] private string displayName = "Pieza";
    [SerializeField] private Transform[] connectors;
    [SerializeField, Min(0)] private int incomingConnectorIndex;

    public CircuitPieceType PieceType => pieceType;
    public string DisplayName => displayName;
    public int ConnectorCount => connectors != null ? connectors.Length : 0;
    public int IncomingConnectorIndex => Mathf.Clamp(incomingConnectorIndex, 0, Mathf.Max(0, ConnectorCount - 1));

    public Vector3 GetConnectorPosition(int index)
    {
        return connectors[index].position;
    }

    public Vector3 GetConnectorDirection(int index)
    {
        return connectors[index].forward;
    }

    public Bounds GetRenderBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    public void SetTint(Color color)
    {
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.SetPropertyBlock(properties);
        }
    }

    public void ClearTint()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.SetPropertyBlock(null);
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        CircuitPieceType newPieceType,
        string newDisplayName,
        Transform[] newConnectors,
        int newIncomingConnectorIndex)
    {
        pieceType = newPieceType;
        displayName = newDisplayName;
        connectors = newConnectors;
        incomingConnectorIndex = newIncomingConnectorIndex;
    }
#endif
}
