using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class Level01VideoGoalFlag : MonoBehaviour
{
    private const float FlagLength = 1.4f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");
    [Header("References")]
    [SerializeField] private MeshFilter flagMeshFilter;

    [Header("Flag Wave")]
    [SerializeField] private bool waveEnabled = true;
    [SerializeField, Range(0f, 0.25f)]
    [Tooltip("Maximum sideways movement at the tip of the flag.")]
    private float waveAmplitude = 0.08f;
    [SerializeField, Range(0f, 8f)]
    [Tooltip("Speed of the looping wave animation.")]
    private float waveSpeed = 2.2f;
    [FormerlySerializedAs("waveLength")]
    [SerializeField, Range(0f, 12f)]
    [Tooltip("Number and tightness of waves across the triangle.")]
    private float waveDensity = 4.5f;

    [Header("Flag Border")]
    [SerializeField, Range(0f, 0.08f)]
    [Tooltip("Width of the trim in local units. It tapers at the tip.")]
    private float borderThickness = 0.034f;
    [SerializeField]
    [Tooltip("Color of the trim around the green triangle.")]
    private Color borderColor = new Color(0.05f, 0.42f, 1f, 1f);

    [Header("Colors")]
    [SerializeField] private Color poleColor =
        new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color flagColor =
        new Color(0.08f, 0.78f, 0.24f, 1f);

    private Mesh animatedMesh;
    private Material borderMaterial;
    private Material sourceFlagMaterial;
    private Vector3[] restVertices;
    private Vector3[] animatedVertices;
    private Quaternion fixedWorldRotation;
    private float minimumX;
    private float inverseFlagLength;
    private Renderer poleRenderer;
    private Renderer flagRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        fixedWorldRotation = transform.rotation;
        InitializeVisuals();
    }

    private void OnEnable()
    {
        fixedWorldRotation = transform.rotation;
        InitializeVisuals();
    }

    private void OnValidate()
    {
        InitializeVisuals();
        UpdateBorderGeometry();
        UpdateFlagMesh(0f);
        ApplyColors();
    }

    private void LateUpdate()
    {
        InitializeVisuals();

        if (Application.isPlaying)
        {
            // BallPuzzleLevelController rotates its prize in Update. Resetting
            // it here keeps this Level01video flag and pole facing still.
            transform.rotation = fixedWorldRotation;
        }

        UpdateFlagMesh(
            Application.isPlaying ? Time.time * waveSpeed : 0f);
        ApplyColors();
    }

    private void InitializeVisuals()
    {
        if (flagMeshFilter == null)
        {
            return;
        }

        if (animatedMesh == null)
        {
            animatedMesh = CreateFlagMesh(borderThickness);
            animatedMesh.name = "Goal Flag Triangle (Animated)";
            animatedMesh.hideFlags = HideFlags.HideAndDontSave;
            animatedMesh.MarkDynamic();
            flagMeshFilter.sharedMesh = animatedMesh;

            restVertices = animatedMesh.vertices;
            animatedVertices = new Vector3[restVertices.Length];
            CacheFlagLength();
        }
        else if (flagMeshFilter.sharedMesh != animatedMesh)
        {
            flagMeshFilter.sharedMesh = animatedMesh;
        }

        flagRenderer ??= flagMeshFilter.GetComponent<Renderer>();
        EnsureFlagMaterials();
        Transform pole = transform.Find("Goal Flag Pole");
        if (pole != null)
        {
            poleRenderer ??= pole.GetComponent<Renderer>();
        }

        propertyBlock ??= new MaterialPropertyBlock();
        ApplyColors();
    }

    private static Mesh CreateFlagMesh(float trimWidth)
    {
        const float fillSurfaceOffset = 0.006f;
        Vector3[] baseVertices =
        {
            new Vector3(0f, 0.42f, 0f),
            new Vector3(0f, -0.42f, 0f),
            new Vector3(0.35f, 0.315f, 0f),
            new Vector3(0.35f, -0.315f, 0f),
            new Vector3(0.7f, 0.21f, 0f),
            new Vector3(0.7f, -0.21f, 0f),
            new Vector3(1.05f, 0.105f, 0f),
            new Vector3(1.05f, -0.105f, 0f),
            new Vector3(1.4f, 0f, 0f)
        };

        int sectionLength = baseVertices.Length;
        int fillBackOffset = sectionLength;
        int borderFrontOffset = sectionLength * 2;
        int borderBackOffset = sectionLength * 3;
        Vector3[] vertices = new Vector3[sectionLength * 4];

        for (int i = 0; i < sectionLength; i++)
        {
            Vector3 fillVertex = baseVertices[i];
            vertices[i] = fillVertex + Vector3.forward * fillSurfaceOffset;
            vertices[i + fillBackOffset] =
                fillVertex - Vector3.forward * fillSurfaceOffset;

            Vector3 borderVertex = CreateBorderVertex(
                fillVertex,
                trimWidth);
            vertices[i + borderFrontOffset] = borderVertex;
            vertices[i + borderBackOffset] = borderVertex;
        }

        int[] frontTriangles =
        {
            0, 1, 2,
            1, 3, 2,
            2, 3, 4,
            3, 5, 4,
            4, 5, 6,
            5, 7, 6,
            6, 7, 8
        };

        int[] fillTriangles = CreateDoubleSidedTriangles(
            frontTriangles,
            0,
            fillBackOffset);
        int[] borderTriangles = CreateBorderBandTriangles(
            0,
            fillBackOffset,
            borderFrontOffset,
            borderBackOffset);

        Mesh mesh = new Mesh { vertices = vertices };
        mesh.subMeshCount = 2;
        mesh.SetTriangles(fillTriangles, 0);
        mesh.SetTriangles(borderTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 CreateBorderVertex(
        Vector3 fillVertex,
        float trimWidth)
    {
        float progress = Mathf.Clamp01(fillVertex.x / FlagLength);
        float taperedWidth = Mathf.Max(0f, trimWidth) * (1f - progress);
        Vector3 borderVertex = new Vector3(fillVertex.x, fillVertex.y, 0f);
        borderVertex.x -= taperedWidth;
        borderVertex.y += Mathf.Sign(fillVertex.y) * taperedWidth;
        return borderVertex;
    }

    private void UpdateBorderGeometry()
    {
        if (animatedMesh == null || restVertices == null ||
            restVertices.Length % 4 != 0)
        {
            return;
        }

        int sectionLength = restVertices.Length / 4;
        int borderFrontOffset = sectionLength * 2;
        int borderBackOffset = sectionLength * 3;
        for (int i = 0; i < sectionLength; i++)
        {
            Vector3 fillVertex = restVertices[i];
            Vector3 borderVertex = CreateBorderVertex(
                fillVertex,
                borderThickness);
            restVertices[i + borderFrontOffset] = borderVertex;
            restVertices[i + borderBackOffset] = borderVertex;
        }

        animatedMesh.vertices = restVertices;
        animatedMesh.RecalculateNormals();
        animatedMesh.RecalculateBounds();
    }

    private static int[] CreateDoubleSidedTriangles(
        int[] frontTriangles,
        int frontOffset,
        int backOffset)
    {
        int[] triangles = new int[frontTriangles.Length * 2];
        for (int i = 0; i < frontTriangles.Length; i += 3)
        {
            triangles[i] = frontTriangles[i] + frontOffset;
            triangles[i + 1] = frontTriangles[i + 1] + frontOffset;
            triangles[i + 2] = frontTriangles[i + 2] + frontOffset;

            int backIndex = frontTriangles.Length + i;
            triangles[backIndex] = frontTriangles[i + 2] + backOffset;
            triangles[backIndex + 1] =
                frontTriangles[i + 1] + backOffset;
            triangles[backIndex + 2] = frontTriangles[i] + backOffset;
        }

        return triangles;
    }

    private static int[] CreateBorderBandTriangles(
        int fillFrontOffset,
        int fillBackOffset,
        int borderFrontOffset,
        int borderBackOffset)
    {
        int[] perimeter = { 0, 2, 4, 6, 8, 7, 5, 3, 1, 0 };
        int segmentCount = perimeter.Length - 1;
        int[] triangles = new int[segmentCount * 12];
        int backStart = segmentCount * 6;

        for (int i = 0; i < segmentCount; i++)
        {
            int vertexA = perimeter[i];
            int vertexB = perimeter[i + 1];
            int frontIndex = i * 6;

            int innerA = vertexA + fillFrontOffset;
            int innerB = vertexB + fillFrontOffset;
            int outerA = vertexA + borderFrontOffset;
            int outerB = vertexB + borderFrontOffset;
            triangles[frontIndex] = innerA;
            triangles[frontIndex + 1] = innerB;
            triangles[frontIndex + 2] = outerB;
            triangles[frontIndex + 3] = innerA;
            triangles[frontIndex + 4] = outerB;
            triangles[frontIndex + 5] = outerA;

            int backIndex = backStart + frontIndex;
            innerA = vertexA + fillBackOffset;
            innerB = vertexB + fillBackOffset;
            outerA = vertexA + borderBackOffset;
            outerB = vertexB + borderBackOffset;
            triangles[backIndex] = innerA;
            triangles[backIndex + 1] = outerB;
            triangles[backIndex + 2] = innerB;
            triangles[backIndex + 3] = innerA;
            triangles[backIndex + 4] = outerA;
            triangles[backIndex + 5] = outerB;
        }

        return triangles;
    }

    private void CacheFlagLength()
    {
        minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;

        int fillVertexCount = restVertices.Length / 4;
        for (int i = 0; i < fillVertexCount; i++)
        {
            minimumX = Mathf.Min(minimumX, restVertices[i].x);
            maximumX = Mathf.Max(maximumX, restVertices[i].x);
        }

        inverseFlagLength = 1f / Mathf.Max(0.001f, maximumX - minimumX);
    }

    private void UpdateFlagMesh(float time)
    {
        if (animatedMesh == null || restVertices == null)
        {
            return;
        }

        int sectionLength = restVertices.Length / 4;
        for (int i = 0; i < restVertices.Length; i++)
        {
            Vector3 vertex = restVertices[i];
            int baseVertexIndex = i % sectionLength;
            float progress = Mathf.Clamp01(
                (restVertices[baseVertexIndex].x - minimumX) *
                inverseFlagLength);
            float amplitude = waveEnabled ? waveAmplitude : 0f;
            vertex.z += Mathf.Sin(time - progress * waveDensity) *
                amplitude * progress;
            animatedVertices[i] = vertex;
        }

        animatedMesh.vertices = animatedVertices;
        animatedMesh.RecalculateNormals();
        animatedMesh.RecalculateBounds();
    }

    private void ApplyColors()
    {
        ApplyColor(poleRenderer, poleColor, Color.black);
        ApplyColor(flagRenderer, flagColor, flagColor * 0.35f, 0);
        ApplyMaterialColor(
            borderMaterial,
            borderColor,
            borderColor * 0.35f);
    }

    private void EnsureFlagMaterials()
    {
        if (flagRenderer == null)
        {
            return;
        }

        Material[] materials = flagRenderer.sharedMaterials;
        if (materials.Length == 0 || materials[0] == null)
        {
            return;
        }

        Material sourceMaterial = materials[0];
        if (borderMaterial == null || sourceFlagMaterial != sourceMaterial)
        {
            ReleaseBorderMaterial();
            sourceFlagMaterial = sourceMaterial;
            borderMaterial = new Material(sourceMaterial)
            {
                name = "Goal Flag Blue Border (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (materials.Length != 2 || materials[1] != borderMaterial)
        {
            flagRenderer.sharedMaterials =
                new[] { sourceMaterial, borderMaterial };
            flagRenderer.SetPropertyBlock(null, 1);
        }
    }

    private static void ApplyMaterialColor(
        Material material,
        Color baseColor,
        Color emissionColor)
    {
        if (material == null)
        {
            return;
        }

        material.SetColor(BaseColorId, baseColor);
        material.SetColor(ColorId, baseColor);
        material.SetColor(EmissionColorId, emissionColor);
    }

    private void ApplyColor(
        Renderer targetRenderer,
        Color baseColor,
        Color emissionColor,
        int materialIndex = 0)
    {
        if (targetRenderer == null || propertyBlock == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(ColorId, baseColor);
        propertyBlock.SetColor(EmissionColorId, emissionColor);
        targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        propertyBlock.Clear();
    }

    private void ReleaseAnimatedMesh()
    {
        if (animatedMesh == null)
        {
            return;
        }

        if (flagMeshFilter != null &&
            flagMeshFilter.sharedMesh == animatedMesh)
        {
            flagMeshFilter.sharedMesh = null;
        }

        Mesh meshToDestroy = animatedMesh;
        animatedMesh = null;
        restVertices = null;
        animatedVertices = null;

        if (Application.isPlaying)
        {
            Destroy(meshToDestroy);
        }
        else
        {
            DestroyImmediate(meshToDestroy);
        }
    }

    private void ReleaseBorderMaterial()
    {
        if (borderMaterial == null)
        {
            sourceFlagMaterial = null;
            return;
        }

        Material materialToDestroy = borderMaterial;
        borderMaterial = null;
        sourceFlagMaterial = null;

        if (Application.isPlaying)
        {
            Destroy(materialToDestroy);
        }
        else
        {
            DestroyImmediate(materialToDestroy);
        }
    }

    private void OnDestroy()
    {
        ReleaseAnimatedMesh();
        ReleaseBorderMaterial();
    }
}
