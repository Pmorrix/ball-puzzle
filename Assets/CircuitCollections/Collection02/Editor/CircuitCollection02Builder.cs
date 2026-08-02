using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CircuitCollection02Builder
{
    private const string RootFolder = "Assets/CircuitCollections/Collection02";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string PrefabFolder = RootFolder + "/Prefabs";
    private const string PreviewFolder = RootFolder + "/Previews";
    private const string ShowcaseFolder = RootFolder + "/Showcase";
    private const string ShowcaseScenePath = ShowcaseFolder + "/Collection02_Showcase.unity";
    private const string ShowcaseFloorMaterialPath = ShowcaseFolder + "/Collection02_ShowcaseFloor.mat";
    private const string LegacyRailMountMeshPath = MeshFolder + "/Shared_RailMount.mesh";
    private const string RailSeatBuildMarkerPath =
        MeshFolder + "/Collection02_CompactCurve45_Compact_Curve_45_LeftRim.mesh";
    private const string ShowcasePreviewSessionKey =
        "BallPuzzle.Collection02.ShowcasePreview.OriginalRailsV2";

    private const string WoodMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceMetal.mat";
    private const string SourceYJunctionPath =
        "Assets/Prefabs/CircuitEditor/YJunctionPiece.prefab";
    private const string OriginalRailMountMeshPath =
        "Assets/Art/ClassicReferenceTrack/Curve90/Meshes/OuterStartRailMount.mesh";

    private const float SurfaceBaseHeight = 0.38f;
    private const float TrackHalfWidth = 0.94f;
    private const float BodyThickness = 0.38f;
    private const float ChannelRise = 0.18f;
    private const float RimCenterOffset = 1.02f;
    private const float RimBottomHalfWidth = 0.15f;
    private const float RimTopHalfWidth = 0.135f;
    private const float RimHeight = 0.52f;
    private const float RailOffset = 0.98f;
    private const float RailMountInset = 0.55f;
    private const float RailMountSurfaceOffset = 0.52f;
    private const float SocketRailSurfaceOffset = 0.665f;
    private const float BendStartRailSurfaceOffset = 0.76f;
    private const float MainRailSurfaceOffset = 0.92f;
    private const float RailBendRadius = 0.16f;
    private const float RailRadius = 0.075f;
    private const int CrossSectionColumns = 9;
    private const int TubeSides = 14;
    private const int RailBendSegments = 12;

    private static readonly string[] PrefabPaths =
    {
        PrefabFolder + "/Collection02_CompactCurve45.prefab",
        PrefabFolder + "/Collection02_SCurve.prefab",
        PrefabFolder + "/Collection02_InclineRamp.prefab",
        PrefabFolder + "/Collection02_IntegratedOverpass.prefab",
        PrefabFolder + "/Collection02_JumpModule.prefab",
        PrefabFolder + "/Collection02_SwitchJunction.prefab"
    };

    static CircuitCollection02Builder()
    {
        EditorApplication.delayCall += CreateMissingCollection;
        EditorApplication.delayCall += RenderLoadedShowcasePreviewOnce;
    }

    [MenuItem("Tools/Ball Puzzle/Create Circuit Collection 02")]
    public static void CreateCollection02()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(PreviewFolder);
        EnsureFolder(ShowcaseFolder);

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        Mesh mountMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OriginalRailMountMeshPath);
        if (woodMaterial == null || metalMaterial == null || mountMesh == null)
        {
            Debug.LogError(
                "Collection02: the original Classic materials or rail mount mesh could not be loaded.");
            return;
        }

        CreateCompactCurve45(woodMaterial, metalMaterial, mountMesh);
        CreateSCurve(woodMaterial, metalMaterial, mountMesh);
        CreateInclineRamp(woodMaterial, metalMaterial, mountMesh);
        CreateIntegratedOverpass(woodMaterial, metalMaterial, mountMesh);
        CreateJumpModule(woodMaterial, metalMaterial, mountMesh);
        CreateSwitchJunction(metalMaterial);
        CreateShowcaseScene();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(LegacyRailMountMeshPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyRailMountMeshPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorApplication.delayCall += RenderAllPreviews;

        Debug.Log("Collection02: six isolated circuit prefabs were created in " + PrefabFolder + ".");
    }

    private static void CreateMissingCollection()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += CreateMissingCollection;
            return;
        }

        foreach (string prefabPath in PrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                CreateCollection02();
                return;
            }
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ShowcaseScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<Mesh>(RailSeatBuildMarkerPath) == null ||
            AssetDatabase.LoadAssetAtPath<Mesh>(LegacyRailMountMeshPath) != null)
        {
            CreateCollection02();
        }
    }

    private static void CreateCompactCurve45(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        List<Vector3> path = CreateArcPath(2.6f, -90f, -45f, 28);
        GameObject root = new GameObject("Collection02_CompactCurve45");
        CreateTrackSection(root.transform, "Compact Curve 45", path, woodMaterial, metalMaterial, mountMesh);
        CreateEndpointConnectors(root.transform, path, "Start Connection", "End Connection");
        SavePrefabAndDestroy(root, PrefabPaths[0]);
    }

    private static void CreateSCurve(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        Vector3 start = new Vector3(-1.3f, SurfaceBaseHeight, -4f);
        Vector3 controlA = new Vector3(-1.3f, SurfaceBaseHeight, -1.35f);
        Vector3 controlB = new Vector3(1.3f, SurfaceBaseHeight, 1.35f);
        Vector3 end = new Vector3(1.3f, SurfaceBaseHeight, 4f);
        List<Vector3> path = CreateBezierPath(start, controlA, controlB, end, 40);

        GameObject root = new GameObject("Collection02_SCurve");
        CreateTrackSection(root.transform, "S Curve", path, woodMaterial, metalMaterial, mountMesh);
        CreateEndpointConnectors(root.transform, path, "Start Connection", "End Connection");
        SavePrefabAndDestroy(root, PrefabPaths[1]);
    }

    private static void CreateInclineRamp(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        List<Vector3> path = new List<Vector3>();
        const int segments = 36;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float eased = t * t * (3f - 2f * t);
            path.Add(new Vector3(0f, SurfaceBaseHeight + 2.2f * eased, Mathf.Lerp(-4f, 4f, t)));
        }

        GameObject root = new GameObject("Collection02_InclineRamp");
        CreateTrackSection(root.transform, "Incline Ramp", path, woodMaterial, metalMaterial, mountMesh);
        CreateEndpointConnectors(root.transform, path, "Low Connection", "High Connection");
        SavePrefabAndDestroy(root, PrefabPaths[2]);
    }

    private static void CreateIntegratedOverpass(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        GameObject root = new GameObject("Collection02_IntegratedOverpass");

        List<Vector3> lowerPath = CreateStraightPath(
            new Vector3(0f, SurfaceBaseHeight, -4.5f),
            new Vector3(0f, SurfaceBaseHeight, 4.5f),
            36);
        CreateTrackSection(root.transform, "Lower Track", lowerPath, woodMaterial, metalMaterial, mountMesh);
        CreateEndpointConnectors(root.transform, lowerPath, "Lower Start Connection", "Lower End Connection");

        List<Vector3> bridgePath = new List<Vector3>();
        const int bridgeSegments = 48;
        for (int i = 0; i <= bridgeSegments; i++)
        {
            float t = i / (float)bridgeSegments;
            float lift = 2.55f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2f);
            bridgePath.Add(new Vector3(Mathf.Lerp(-4.5f, 4.5f, t), SurfaceBaseHeight + lift, 0f));
        }

        CreateTrackSection(root.transform, "Upper Bridge", bridgePath, woodMaterial, metalMaterial, mountMesh);
        CreateEndpointConnectors(root.transform, bridgePath, "Bridge Start Connection", "Bridge End Connection");
        SavePrefabAndDestroy(root, PrefabPaths[3]);
    }

    private static void CreateJumpModule(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        GameObject root = new GameObject("Collection02_JumpModule");

        List<Vector3> launchPath = new List<Vector3>();
        const int halfSegments = 24;
        for (int i = 0; i <= halfSegments; i++)
        {
            float t = i / (float)halfSegments;
            float lift = 1.05f * t * t;
            launchPath.Add(new Vector3(0f, SurfaceBaseHeight + lift, Mathf.Lerp(-4f, -0.85f, t)));
        }

        List<Vector3> landingPath = new List<Vector3>();
        for (int i = 0; i <= halfSegments; i++)
        {
            float t = i / (float)halfSegments;
            float lift = 0.95f * (1f - t) * (1f - t);
            landingPath.Add(new Vector3(0f, SurfaceBaseHeight + lift, Mathf.Lerp(0.85f, 4f, t)));
        }

        CreateTrackSection(root.transform, "Launch Ramp", launchPath, woodMaterial, metalMaterial, mountMesh);
        CreateTrackSection(root.transform, "Landing Ramp", landingPath, woodMaterial, metalMaterial, mountMesh);
        CreateConnector(root.transform, "Start Connection", ConnectorBasePosition(launchPath[0]), -PathTangent(launchPath, 0));
        CreateConnector(
            root.transform,
            "End Connection",
            ConnectorBasePosition(landingPath[landingPath.Count - 1]),
            PathTangent(landingPath, landingPath.Count - 1));
        SavePrefabAndDestroy(root, PrefabPaths[4]);
    }

    private static void CreateSwitchJunction(Material metalMaterial)
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceYJunctionPath);
        if (sourcePrefab == null)
        {
            Debug.LogError("Collection02: the existing Y junction prefab could not be loaded.");
            return;
        }

        GameObject root = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
        if (root == null)
        {
            Debug.LogError("Collection02: the switch junction base could not be instantiated.");
            return;
        }

        PrefabUtility.UnpackPrefabInstance(
            root,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
        root.name = "Collection02_SwitchJunction";

        GameObject pivot = new GameObject("Switch Pivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0.86f, 0.35f);
        pivot.transform.localRotation = Quaternion.Euler(0f, -22f, 0f);

        GameObject tongue = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tongue.name = "Selector Tongue";
        tongue.transform.SetParent(pivot.transform, false);
        tongue.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        tongue.transform.localScale = new Vector3(0.15f, 0.14f, 1.45f);
        tongue.GetComponent<MeshRenderer>().sharedMaterial = metalMaterial;

        SavePrefabAndDestroy(root, PrefabPaths[5]);
    }

    private static void CreateTrackSection(
        Transform parent,
        string sectionName,
        List<Vector3> path,
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        string assetPrefix = Sanitize(parent.name + "_" + sectionName);
        GameObject sectionRoot = new GameObject(sectionName);
        sectionRoot.transform.SetParent(parent, false);

        Mesh bodyMesh = SaveOrUpdateMesh(
            MeshFolder + "/" + assetPrefix + "_Body.mesh",
            CreateTrackBodyMesh(sectionName + " Body", path));
        CreateMeshPart(sectionRoot.transform, "Wooden Track Body", bodyMesh, woodMaterial, true);

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float side = sideIndex == 0 ? -1f : 1f;
            string sideName = side < 0f ? "Left" : "Right";
            Mesh rimMesh = SaveOrUpdateMesh(
                MeshFolder + "/" + assetPrefix + "_" + sideName + "Rim.mesh",
                CreateWoodenRimMesh(sectionName + " " + sideName + " Rim", path, side));
            CreateMeshPart(
                sectionRoot.transform,
                sideName + " Wooden Rail Seat",
                rimMesh,
                woodMaterial,
                true);

            Mesh railMesh = SaveOrUpdateMesh(
                MeshFolder + "/" + assetPrefix + "_" + sideName + "Rail.mesh",
                CreateRailMesh(sectionName + " " + sideName + " Rail", path, side));
            CreateMeshPart(sectionRoot.transform, sideName + " Metal Guard", railMesh, metalMaterial, true);
            CreateRailMounts(sectionRoot.transform, path, side, sideName, mountMesh, metalMaterial);
        }
    }

    private static void CreateMeshPart(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        bool addCollider)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        if (addCollider)
        {
            MeshCollider collider = part.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }

    private static void CreateRailMounts(
        Transform parent,
        List<Vector3> path,
        float railSide,
        string sideName,
        Mesh mountMesh,
        Material material)
    {
        float[] distances = BuildPathDistances(path);
        float pathLength = distances[distances.Length - 1];
        float mountInset = Mathf.Min(RailMountInset, pathLength * 0.35f);
        float[] mountDistances = { mountInset, pathLength - mountInset };

        for (int i = 0; i < mountDistances.Length; i++)
        {
            SamplePathFrame(
                path,
                distances,
                mountDistances[i],
                out Vector3 surfacePosition,
                out _,
                out Vector3 lateral,
                out Vector3 normal);
            Vector3 position =
                surfacePosition +
                lateral * (railSide * RailOffset) +
                normal * RailMountSurfaceOffset;

            GameObject mount = new GameObject(sideName + " Rail Mount " + (i + 1));
            mount.transform.SetParent(parent, false);
            mount.transform.localPosition = position;
            mount.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
            MeshFilter filter = mount.AddComponent<MeshFilter>();
            filter.sharedMesh = mountMesh;
            MeshRenderer renderer = mount.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshCollider collider = mount.AddComponent<MeshCollider>();
            collider.sharedMesh = mountMesh;
        }
    }

    private static Mesh CreateTrackBodyMesh(string name, List<Vector3> path)
    {
        int columns = CrossSectionColumns;
        int sectionVertexCount = columns * 2;
        Vector3[] vertices = new Vector3[path.Count * sectionVertexCount];
        Vector2[] uvs = new Vector2[vertices.Length];
        List<int> triangles = new List<int>();
        float accumulatedDistance = 0f;

        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                accumulatedDistance += Vector3.Distance(path[i - 1], path[i]);
            }

            GetFrame(path, i, out _, out Vector3 lateral, out Vector3 normal);
            for (int column = 0; column < columns; column++)
            {
                float normalized = column / (float)(columns - 1);
                float cross = Mathf.Lerp(-TrackHalfWidth, TrackHalfWidth, normalized);
                float curve = ChannelRise * Mathf.Pow(Mathf.Abs(cross) / TrackHalfWidth, 2f);
                int topIndex = i * sectionVertexCount + column;
                int bottomIndex = topIndex + columns;
                vertices[topIndex] = path[i] + lateral * cross + normal * curve;
                vertices[bottomIndex] = path[i] + lateral * cross - normal * BodyThickness;
                uvs[topIndex] = new Vector2(normalized, accumulatedDistance * 0.25f);
                uvs[bottomIndex] = new Vector2(normalized, accumulatedDistance * 0.25f);
            }
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            int current = i * sectionVertexCount;
            int next = (i + 1) * sectionVertexCount;
            for (int column = 0; column < columns - 1; column++)
            {
                AddQuad(triangles, current + column, next + column, next + column + 1, current + column + 1);
                AddQuad(
                    triangles,
                    current + columns + column + 1,
                    next + columns + column + 1,
                    next + columns + column,
                    current + columns + column);
            }

            AddQuad(
                triangles,
                current,
                current + columns,
                next + columns,
                next);
            AddQuad(
                triangles,
                current + columns - 1,
                next + columns - 1,
                next + sectionVertexCount - 1,
                current + sectionVertexCount - 1);
        }

        int last = (path.Count - 1) * sectionVertexCount;
        for (int column = 0; column < columns - 1; column++)
        {
            AddQuad(
                triangles,
                column,
                column + 1,
                columns + column + 1,
                columns + column);
            AddQuad(
                triangles,
                last + column + 1,
                last + column,
                last + columns + column,
                last + columns + column + 1);
        }

        return BuildMesh(name, vertices, triangles.ToArray(), uvs);
    }

    private static Mesh CreateWoodenRimMesh(
        string name,
        List<Vector3> path,
        float rimSide)
    {
        const int sectionVertexCount = 4;
        Vector3[] vertices = new Vector3[path.Count * sectionVertexCount];
        Vector2[] uvs = new Vector2[vertices.Length];
        List<int> triangles = new List<int>();
        float accumulatedDistance = 0f;

        for (int index = 0; index < path.Count; index++)
        {
            if (index > 0)
            {
                accumulatedDistance += Vector3.Distance(path[index - 1], path[index]);
            }

            GetFrame(path, index, out _, out Vector3 lateral, out Vector3 normal);
            Vector3 center = path[index] + lateral * (rimSide * RimCenterOffset);
            int current = index * sectionVertexCount;
            vertices[current] = center - lateral * RimBottomHalfWidth;
            vertices[current + 1] = center + lateral * RimBottomHalfWidth;
            vertices[current + 2] =
                center - lateral * RimTopHalfWidth + normal * RimHeight;
            vertices[current + 3] =
                center + lateral * RimTopHalfWidth + normal * RimHeight;

            float v = accumulatedDistance * 0.25f;
            uvs[current] = new Vector2(0f, v);
            uvs[current + 1] = new Vector2(1f, v);
            uvs[current + 2] = new Vector2(0.05f, v);
            uvs[current + 3] = new Vector2(0.95f, v);
        }

        for (int index = 0; index < path.Count - 1; index++)
        {
            int current = index * sectionVertexCount;
            int next = current + sectionVertexCount;
            AddQuad(triangles, current, current + 1, next + 1, next);
            AddQuad(triangles, current, next, next + 2, current + 2);
            AddQuad(triangles, current + 1, current + 3, next + 3, next + 1);
            AddQuad(triangles, current + 2, next + 2, next + 3, current + 3);
        }

        int last = (path.Count - 1) * sectionVertexCount;
        AddQuad(triangles, 0, 2, 3, 1);
        AddQuad(triangles, last, last + 1, last + 3, last + 2);
        return BuildMesh(name, vertices, triangles.ToArray(), uvs);
    }

    private static Mesh CreateRailMesh(string name, List<Vector3> path, float railSide)
    {
        List<Vector3> railPath = CreateOriginalStyleRailPath(path, railSide);
        float[] pathDistances = BuildPathDistances(path);
        SamplePathFrame(
            path,
            pathDistances,
            0f,
            out _,
            out _,
            out Vector3 firstNormal,
            out _);
        Mesh mesh = CreateTubeMesh(name, railPath, RailRadius, TubeSides, firstNormal);
        return mesh;
    }

    private static List<Vector3> CreateOriginalStyleRailPath(
        List<Vector3> surfacePath,
        float railSide)
    {
        float[] distances = BuildPathDistances(surfacePath);
        float pathLength = distances[distances.Length - 1];
        float mountInset = Mathf.Min(RailMountInset, pathLength * 0.35f);
        float supportStart = mountInset;
        float supportEnd = pathLength - mountInset;
        float bendRadius = Mathf.Min(
            RailBendRadius,
            Mathf.Max(0.02f, (supportEnd - supportStart) * 0.25f));
        float mainStart = supportStart + bendRadius;
        float mainEnd = supportEnd - bendRadius;
        List<Vector3> points = new List<Vector3>();

        AppendRailRange(
            points,
            surfacePath,
            distances,
            0f,
            supportStart,
            railSide,
            SocketRailSurfaceOffset);
        AppendRailPoint(
            points,
            surfacePath,
            distances,
            supportStart,
            railSide,
            BendStartRailSurfaceOffset);

        for (int segment = 1; segment <= RailBendSegments; segment++)
        {
            float bend = segment / (float)RailBendSegments * Mathf.PI * 0.5f;
            float travelled = bendRadius * (1f - Mathf.Cos(bend));
            float height = BendStartRailSurfaceOffset + bendRadius * Mathf.Sin(bend);
            AppendRailPoint(
                points,
                surfacePath,
                distances,
                supportStart + travelled,
                railSide,
                height);
        }

        AppendRailRange(
            points,
            surfacePath,
            distances,
            mainStart,
            mainEnd,
            railSide,
            MainRailSurfaceOffset);

        for (int segment = 1; segment <= RailBendSegments; segment++)
        {
            float bend = segment / (float)RailBendSegments * Mathf.PI * 0.5f;
            float travelled = bendRadius * Mathf.Sin(bend);
            float height = BendStartRailSurfaceOffset + bendRadius * Mathf.Cos(bend);
            AppendRailPoint(
                points,
                surfacePath,
                distances,
                mainEnd + travelled,
                railSide,
                height);
        }

        AppendRailPoint(
            points,
            surfacePath,
            distances,
            supportEnd,
            railSide,
            SocketRailSurfaceOffset);
        AppendRailRange(
            points,
            surfacePath,
            distances,
            supportEnd,
            pathLength,
            railSide,
            SocketRailSurfaceOffset);
        return points;
    }

    private static void AppendRailRange(
        List<Vector3> railPath,
        List<Vector3> surfacePath,
        float[] distances,
        float startDistance,
        float endDistance,
        float railSide,
        float surfaceOffset)
    {
        AppendRailPoint(
            railPath,
            surfacePath,
            distances,
            startDistance,
            railSide,
            surfaceOffset);

        for (int index = 1; index < distances.Length - 1; index++)
        {
            if (distances[index] > startDistance + 0.0001f &&
                distances[index] < endDistance - 0.0001f)
            {
                AppendRailPoint(
                    railPath,
                    surfacePath,
                    distances,
                    distances[index],
                    railSide,
                    surfaceOffset);
            }
        }

        AppendRailPoint(
            railPath,
            surfacePath,
            distances,
            endDistance,
            railSide,
            surfaceOffset);
    }

    private static void AppendRailPoint(
        List<Vector3> railPath,
        List<Vector3> surfacePath,
        float[] distances,
        float distance,
        float railSide,
        float surfaceOffset)
    {
        SamplePathFrame(
            surfacePath,
            distances,
            distance,
            out Vector3 surfacePosition,
            out _,
            out Vector3 lateral,
            out Vector3 normal);
        Vector3 point =
            surfacePosition +
            lateral * (railSide * RailOffset) +
            normal * surfaceOffset;

        if (railPath.Count == 0 ||
            (railPath[railPath.Count - 1] - point).sqrMagnitude > 0.00000001f)
        {
            railPath.Add(point);
        }
    }

    private static float[] BuildPathDistances(List<Vector3> path)
    {
        float[] distances = new float[path.Count];
        for (int index = 1; index < path.Count; index++)
        {
            distances[index] =
                distances[index - 1] + Vector3.Distance(path[index - 1], path[index]);
        }

        return distances;
    }

    private static void SamplePathFrame(
        List<Vector3> path,
        float[] distances,
        float distance,
        out Vector3 position,
        out Vector3 tangent,
        out Vector3 lateral,
        out Vector3 normal)
    {
        float clampedDistance = Mathf.Clamp(distance, 0f, distances[distances.Length - 1]);
        int segment = distances.Length - 2;
        for (int index = 0; index < distances.Length - 1; index++)
        {
            if (clampedDistance <= distances[index + 1])
            {
                segment = index;
                break;
            }
        }

        float segmentLength = distances[segment + 1] - distances[segment];
        float t = segmentLength > Mathf.Epsilon
            ? (clampedDistance - distances[segment]) / segmentLength
            : 0f;
        position = Vector3.Lerp(path[segment], path[segment + 1], t);
        tangent = (path[segment + 1] - path[segment]).normalized;
        lateral = Vector3.Cross(Vector3.up, tangent).normalized;
        if (lateral.sqrMagnitude < Mathf.Epsilon)
        {
            lateral = Vector3.right;
        }

        normal = Vector3.Cross(tangent, lateral).normalized;
        if (normal.y < 0f)
        {
            lateral = -lateral;
            normal = -normal;
        }
    }

    private static Mesh CreateTubeMesh(
        string name,
        List<Vector3> path,
        float radius,
        int sides,
        Vector3 firstNormal)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        Vector3 previousTangent = Vector3.zero;
        Vector3 normal = firstNormal.normalized;
        for (int index = 0; index < path.Count; index++)
        {
            Vector3 tangent = index == 0
                ? path[1] - path[0]
                : index == path.Count - 1
                    ? path[index] - path[index - 1]
                    : path[index + 1] - path[index - 1];
            tangent.Normalize();
            if (index > 0)
            {
                normal = Quaternion.FromToRotation(previousTangent, tangent) * normal;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                if (normal.sqrMagnitude < 0.5f)
                {
                    normal = Vector3.Cross(tangent, Vector3.forward).normalized;
                }
            }

            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            for (int side = 0; side < sides; side++)
            {
                float angle = side / (float)sides * Mathf.PI * 2f;
                vertices.Add(
                    path[index] +
                    (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius);
                uvs.Add(new Vector2(side / (float)sides, index / (float)(path.Count - 1)));
            }

            previousTangent = tangent;
        }

        for (int ring = 0; ring < path.Count - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + nextSide;
                int c = (ring + 1) * sides + nextSide;
                int d = (ring + 1) * sides + side;
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        return BuildMesh(name, vertices.ToArray(), triangles.ToArray(), uvs.ToArray());
    }

    private static void CreateEndpointConnectors(
        Transform root,
        List<Vector3> path,
        string startName,
        string endName)
    {
        CreateConnector(root, startName, ConnectorBasePosition(path[0]), -PathTangent(path, 0));
        CreateConnector(
            root,
            endName,
            ConnectorBasePosition(path[path.Count - 1]),
            PathTangent(path, path.Count - 1));
    }

    private static void CreateConnector(Transform root, string name, Vector3 position, Vector3 direction)
    {
        GameObject connector = new GameObject(name);
        connector.transform.SetParent(root, false);
        connector.transform.localPosition = position;
        if (direction.sqrMagnitude > Mathf.Epsilon)
        {
            connector.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private static Vector3 ConnectorBasePosition(Vector3 surfacePosition)
    {
        return new Vector3(
            surfacePosition.x,
            surfacePosition.y - SurfaceBaseHeight,
            surfacePosition.z);
    }

    private static Vector3 PathTangent(List<Vector3> path, int index)
    {
        if (index <= 0)
        {
            return (path[1] - path[0]).normalized;
        }

        if (index >= path.Count - 1)
        {
            return (path[path.Count - 1] - path[path.Count - 2]).normalized;
        }

        return (path[index + 1] - path[index - 1]).normalized;
    }

    private static void GetFrame(
        List<Vector3> path,
        int index,
        out Vector3 tangent,
        out Vector3 lateral,
        out Vector3 normal)
    {
        tangent = PathTangent(path, index);
        lateral = Vector3.Cross(Vector3.up, tangent).normalized;
        if (lateral.sqrMagnitude < Mathf.Epsilon)
        {
            lateral = Vector3.right;
        }

        normal = Vector3.Cross(tangent, lateral).normalized;
        if (normal.y < 0f)
        {
            lateral = -lateral;
            normal = -normal;
        }
    }

    private static List<Vector3> CreateArcPath(
        float radius,
        float startDegrees,
        float endDegrees,
        int segments)
    {
        List<Vector3> path = new List<Vector3>();
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
            path.Add(new Vector3(
                Mathf.Cos(angle) * radius,
                SurfaceBaseHeight,
                Mathf.Sin(angle) * radius));
        }

        Vector3 centerOffset = (path[0] + path[path.Count - 1]) * 0.5f;
        centerOffset.y = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            path[i] -= centerOffset;
        }

        return path;
    }

    private static List<Vector3> CreateBezierPath(
        Vector3 start,
        Vector3 controlA,
        Vector3 controlB,
        Vector3 end,
        int segments)
    {
        List<Vector3> path = new List<Vector3>();
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float inverse = 1f - t;
            path.Add(
                inverse * inverse * inverse * start +
                3f * inverse * inverse * t * controlA +
                3f * inverse * t * t * controlB +
                t * t * t * end);
        }

        return path;
    }

    private static List<Vector3> CreateStraightPath(Vector3 start, Vector3 end, int segments)
    {
        List<Vector3> path = new List<Vector3>();
        for (int i = 0; i <= segments; i++)
        {
            path.Add(Vector3.Lerp(start, end, i / (float)segments));
        }

        return path;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    private static Mesh BuildMesh(string name, Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = vertices.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveOrUpdateMesh(string path, Mesh generatedMesh)
    {
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(generatedMesh, path);
            return generatedMesh;
        }

        EditorUtility.CopySerialized(generatedMesh, existingMesh);
        existingMesh.name = generatedMesh.name;
        EditorUtility.SetDirty(existingMesh);
        UnityEngine.Object.DestroyImmediate(generatedMesh);
        return existingMesh;
    }

    private static void SavePrefabAndDestroy(GameObject root, string path)
    {
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void CreateShowcaseScene()
    {
        Scene loadedShowcaseScene = SceneManager.GetSceneByPath(ShowcaseScenePath);
        if (loadedShowcaseScene.IsValid() && loadedShowcaseScene.isLoaded)
        {
            foreach (GameObject root in loadedShowcaseScene.GetRootGameObjects())
            {
                Camera loadedCamera = root.GetComponentInChildren<Camera>(true);
                if (loadedCamera != null)
                {
                    RenderShowcaseCameraToPng(
                        loadedCamera,
                        loadedShowcaseScene,
                        PreviewFolder + "/Collection02_Overview.png",
                        1400,
                        900);
                    break;
                }
            }

            return;
        }

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene showcaseScene = default;

        try
        {
            showcaseScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            SceneManager.SetActiveScene(showcaseScene);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f, 1f);
            RenderSettings.fog = false;

            GameObject showcaseRoot = new GameObject("Collection 02 Showcase");
            GameObject environmentRoot = new GameObject("Environment");
            environmentRoot.transform.SetParent(showcaseRoot.transform, false);
            GameObject piecesRoot = new GameObject("Pieces");
            piecesRoot.transform.SetParent(showcaseRoot.transform, false);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Showcase Floor";
            floor.transform.SetParent(environmentRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(32f, 0.3f, 24f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateShowcaseFloorMaterial();

            PlaceShowcasePrefab(piecesRoot.transform, 0, new Vector3(-10f, 0f, 5.5f), 18f);
            PlaceShowcasePrefab(piecesRoot.transform, 1, new Vector3(-3.5f, 0f, 5.2f), 88f);
            PlaceShowcasePrefab(piecesRoot.transform, 2, new Vector3(5.5f, 0f, 5.2f), 88f);
            PlaceShowcasePrefab(piecesRoot.transform, 3, new Vector3(-8.5f, 0f, -5.3f), 0f);
            PlaceShowcasePrefab(piecesRoot.transform, 4, new Vector3(2.1f, 0f, -5.3f), 0f);
            PlaceShowcasePrefab(piecesRoot.transform, 5, new Vector3(10.5f, 0f, -5.1f), 0f);

            GameObject cameraObject = new GameObject("Showcase Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(showcaseRoot.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.20f, 0.34f, 1f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.allowHDR = true;
            cameraObject.transform.position = new Vector3(23f, 23f, -31f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.1f, 0f));

            GameObject lightObject = new GameObject("Key Light");
            lightObject.transform.SetParent(environmentRoot.transform, false);
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.color = new Color(1f, 0.91f, 0.82f);
            keyLight.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            EditorSceneManager.SaveScene(showcaseScene, ShowcaseScenePath);
            RenderShowcaseCameraToPng(
                camera,
                showcaseScene,
                PreviewFolder + "/Collection02_Overview.png",
                1400,
                900);
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (showcaseScene.IsValid() && showcaseScene.isLoaded)
            {
                EditorSceneManager.CloseScene(showcaseScene, true);
            }
        }
    }

    private static void PlaceShowcasePrefab(
        Transform parent,
        int prefabIndex,
        Vector3 position,
        float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[prefabIndex]);
        if (prefab == null)
        {
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
        if (instance == null)
        {
            return;
        }

        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static Material GetOrCreateShowcaseFloorMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ShowcaseFloorMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Collection02 Showcase Floor" };
            AssetDatabase.CreateAsset(material, ShowcaseFloorMaterialPath);
        }

        Color floorColor = new Color(0.48f, 0.46f, 0.45f, 1f);
        material.color = floorColor;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", floorColor);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.25f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RenderCameraToPng(
        Camera camera,
        string assetPath,
        int width,
        int height)
    {
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousTarget = RenderTexture.active;

        try
        {
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previousTarget;
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    private static void RenderShowcaseCameraToPng(
        Camera camera,
        Scene showcaseScene,
        string assetPath,
        int width,
        int height)
    {
        List<GameObject> sceneObjects = new List<GameObject>();
        List<int> originalLayers = new List<int>();
        int originalCullingMask = camera.cullingMask;

        try
        {
            foreach (GameObject root in showcaseScene.GetRootGameObjects())
            {
                CollectAndSetLayer(root, 31, sceneObjects, originalLayers);
            }

            camera.cullingMask = 1 << 31;
            RenderCameraToPng(camera, assetPath, width, height);
        }
        finally
        {
            camera.cullingMask = originalCullingMask;
            for (int index = 0; index < sceneObjects.Count; index++)
            {
                if (sceneObjects[index] != null)
                {
                    sceneObjects[index].layer = originalLayers[index];
                }
            }
        }
    }

    private static void CollectAndSetLayer(
        GameObject root,
        int layer,
        List<GameObject> objects,
        List<int> originalLayers)
    {
        objects.Add(root);
        originalLayers.Add(root.layer);
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            CollectAndSetLayer(child.gameObject, layer, objects, originalLayers);
        }
    }

    private static void RenderLoadedShowcasePreviewOnce()
    {
        if (SessionState.GetBool(ShowcasePreviewSessionKey, false))
        {
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RenderLoadedShowcasePreviewOnce;
            return;
        }

        Scene loadedShowcaseScene = SceneManager.GetSceneByPath(ShowcaseScenePath);
        if (!loadedShowcaseScene.IsValid() || !loadedShowcaseScene.isLoaded)
        {
            return;
        }

        foreach (GameObject root in loadedShowcaseScene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                continue;
            }

            RenderShowcaseCameraToPng(
                camera,
                loadedShowcaseScene,
                PreviewFolder + "/Collection02_Overview.png",
                1400,
                900);
            AssetDatabase.Refresh();
            SessionState.SetBool(ShowcasePreviewSessionKey, true);
            break;
        }
    }

    private static void RenderAllPreviews()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RenderAllPreviews;
            return;
        }

        foreach (string prefabPath in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                RenderPreview(prefab, PreviewFolder + "/" + prefab.name + ".png");
            }
        }

        AssetDatabase.Refresh();
    }

    private static void RenderPreview(GameObject prefab, string assetPath)
    {
        GameObject previewRoot = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousTarget = RenderTexture.active;

        try
        {
            previewRoot = UnityEngine.Object.Instantiate(prefab);
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            previewRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, -24f, 0f));
            SetLayerRecursively(previewRoot, 31);
            Bounds bounds = GetRenderBounds(previewRoot);

            cameraObject = new GameObject("Collection02 Preview Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = 31
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.35f + bounds.extents.y * 0.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.03f, 0.045f, 1f);
            camera.cullingMask = 1 << 31;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;

            Vector3 viewDirection = new Vector3(1.2f, 1f, -1.35f).normalized;
            float distance = Mathf.Max(10f, bounds.extents.magnitude * 3f);
            cameraObject.transform.position = bounds.center + viewDirection * distance;
            cameraObject.transform.LookAt(bounds.center);

            lightObject = new GameObject("Collection02 Preview Light")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = 31
            };
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.91f, 0.82f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            renderTexture = new RenderTexture(768, 512, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(768, 512, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, 768f, 512f), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
        }
        finally
        {
            if (cameraObject != null)
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }
            }

            RenderTexture.active = previousTarget;
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            if (previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(previewRoot);
            }

            if (cameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            if (lightObject != null)
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }
    }

    private static Bounds GetRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Replace(' ', '_');
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
