using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Reflection;

public static class BonusSolutionAudit
{
    [Serializable] public class PieceData {
        public string name, path; public Vector3 position, scale, center, size;
        public Quaternion rotation; public Vector3[] connectors, directions, points;
    }
    [Serializable] public class Audit { public Vector3 start; public PieceData[] prefabs, targets; }
    [Serializable] public class SolutionPiece { public int type,entry; public float[] root; public float yaw; }
    [Serializable] public class Solution { public SolutionPiece[] pieces; }
    [Serializable] public class Solutions { public Solution[] models; }
    static readonly BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
    static int forcedModel=-1;
    [MenuItem("Ball Puzzle/Bonus01Test/Construir circuito FIJO")]
    public static void BuildFixedCircuit()
    {
        const string scenePath = "Assets/Scenes/Bonus01Test.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != scenePath)
            throw new InvalidOperationException("Abrir Bonus01Test fuera de Play Mode.");
        if (GameObject.Find("Circuito fijo - montaje real") != null)
            throw new InvalidOperationException("El circuito fijo ya existe; no se duplicara.");
        string backup = "BonusSolutions/Bonus01Test.before-fixed-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("No se pudo guardar la copia de seguridad.");
        var controller = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>(FindObjectsInactive.Include);
        var so = new SerializedObject(controller);
        var challenge = (BonusPieceChallengeController)so.FindProperty("bonusChallenge").objectReferenceValue;
        var refs = new SerializedObject(challenge).FindProperty("targetVisuals");
        var targets = Enumerable.Range(0, refs.arraySize).Select(i => (CircuitPiece)refs.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var model = JsonUtility.FromJson<Solutions>("{\"models\":" + File.ReadAllText("BonusSolutions/strict-candidates.json") + "}").models[1];
        var root = new GameObject("Circuito fijo - montaje real");
        Undo.RegisterCreatedObjectUndo(root, "Construir circuito fijo");
        var previous = new GameObject("Piezas anteriores - conservadas (inactivas)");
        Undo.RegisterCreatedObjectUndo(previous, "Conservar piezas anteriores");
        foreach (var piece in UnityEngine.Object.FindObjectsByType<CircuitPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (targets.Contains(piece) || piece.transform.parent != null) continue;
            if (!new[] { "StraightPiece", "HalfStraightPiece", "Curve45RightPiece" }.Any(n => piece.name.StartsWith(n, StringComparison.Ordinal))) continue;
            Undo.SetTransformParent(piece.transform, previous.transform, "Conservar pieza anterior");
        }
        previous.SetActive(false);
        Undo.RecordObject(challenge, "Pausar animacion bonus");
        challenge.enabled = false;
        var built = new List<CircuitPiece>();
        var entries = new List<int>();
        var report = new System.Text.StringBuilder("MONTAJE FIJO. Prefabs reales, escala original. Sin prueba de gameplay.\n");
        var center = so.FindProperty("buildAreaCenter").vector2Value;
        float half = so.FindProperty("buildHalfSize").floatValue;
        for (int i = 0; i < model.pieces.Length; i++)
        {
            // Two consecutive half straights become one full straight, with identical endpoints.
            if (i == 15) continue;
            var spec = model.pieces[i];
            int type = Math.Abs(spec.type);
            CircuitPiece piece;
            Vector3 position = new Vector3(spec.root[0], 1, spec.root[1]);
            if (type == 90 || type == 180 || type == 8)
            {
                piece = targets[type == 90 ? 0 : type == 180 ? 1 : 2];
                Undo.RecordObject(piece.transform, "Colocar especial fija");
                for (Transform parent = piece.transform; parent != null; parent = parent.parent)
                {
                    Undo.RecordObject(parent.gameObject, "Mostrar pieza especial");
                    parent.gameObject.SetActive(true);
                }
                var mover = piece.GetComponent<BonusRotatingPieceConnector>();
                if (mover != null) { Undo.RecordObject(mover, "Fijar especial"); mover.enabled = false; }
            }
            else
            {
                string prefab = i == 14 ? "StraightPiece" : type == 0 ? "HalfStraightPiece" : "Curve45RightPiece";
                piece = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircuitEditor/" + prefab + ".prefab"), root.transform)).GetComponent<CircuitPiece>();
                Undo.RegisterCreatedObjectUndo(piece.gameObject, "Colocar pieza fija");
                piece.name = (built.Count + 1).ToString("D2") + " - " + prefab;
                if (i == 14) position = (position + new Vector3(model.pieces[15].root[0], 1, model.pieces[15].root[1])) * .5f;
            }
            piece.transform.SetPositionAndRotation(position, Quaternion.Euler(0, spec.yaw, 0));
            piece.gameObject.SetActive(true);
            SceneVisibilityManager.instance.Show(piece.gameObject, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(piece.transform);
            var connector = piece.GetComponent<BonusRotatingPieceConnector>();
            if (connector != null) PrefabUtility.RecordPrefabInstancePropertyModifications(connector);
            var b = piece.GetRenderBounds();
            if (b.min.x < center.x-half || b.max.x > center.x+half || b.min.z < center.y-half || b.max.z > center.y+half)
                throw new InvalidOperationException("Fuera del tablero: " + piece.name);
            built.Add(piece); entries.Add(spec.entry);
            report.AppendLine($"{built.Count}: {piece.name}; root {position.ToString("F6")}; yaw {spec.yaw}; scale {piece.transform.lossyScale}; inside=True");
        }
        float maxGap = 0;
        for (int i = 0; i < built.Count; i++)
        {
            int j = (i+1)%built.Count;
            float gap = Vector3.Distance(built[i].GetConnectorPosition(1-entries[i]), built[j].GetConnectorPosition(entries[j]));
            float dot = Vector3.Dot(built[i].GetConnectorDirection(1-entries[i]).normalized, built[j].GetConnectorDirection(entries[j]).normalized);
            if (gap > .001f || dot > -.9999f) throw new InvalidOperationException($"Union {i+1}: gap={gap}, dot={dot}");
            maxGap = Mathf.Max(maxGap, gap);
        }
        var start = (Transform)so.FindProperty("startAnchor").objectReferenceValue;
        Vector3 startDelta = built[0].GetConnectorPosition(entries[0]) - start.position;
        if (new Vector2(startDelta.x, startDelta.z).magnitude > .001f) throw new InvalidOperationException("START no coincide con el circuito.");
        report.AppendLine($"PASS: {built.Count} piezas; cierre y todas las uniones; max gap={maxGap:F9}; dentro del tablero; 3 especiales deshabilitadas para giro. Backup: {backup}");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar Bonus01Test.");
        File.WriteAllText("BonusSolutions/fixed-circuit-report.txt", report.ToString());
        Selection.activeGameObject = root;
        SceneView.RepaintAll();
        Debug.Log(report.ToString());
    }
    public static void ValidateStrict() {
        for(int i=0;i<2;i++){forcedModel=i;Validate();}
        forcedModel=-1;
        CheckStartClearance();
    }
    public static void CheckStartClearance() {
        EditorSceneManager.OpenScene("Assets/Scenes/Bonus01Test.unity");
        var controller=UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var so=new SerializedObject(controller);
        var start=((Transform)so.FindProperty("startAnchor").objectReferenceValue).position;
        var center=so.FindProperty("buildAreaCenter").vector2Value;
        float half=so.FindProperty("buildHalfSize").floatValue;
        var records=new List<(int angle,string name,int connector,Bounds bounds)>();
        foreach(string name in new[]{"StraightPiece","HalfStraightPiece","Curve45RightPiece"}) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircuitEditor/"+name+".prefab"));
            var p=go.GetComponent<CircuitPiece>();
            for(int angle=0;angle<360;angle+=45) for(int connector=0;connector<2;connector++) {
                go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                float localYaw=Vector3.SignedAngle(Vector3.forward,p.GetConnectorDirection(connector),Vector3.up);
                go.transform.rotation=Quaternion.Euler(0,angle-localYaw,0);
                go.transform.position=start-p.GetConnectorPosition(connector);
                records.Add((angle,name,connector,p.GetRenderBounds()));
            }
            UnityEngine.Object.DestroyImmediate(go);
        }
        bool Inside(Bounds b)=>b.min.x>=center.x-half && b.max.x<=center.x+half && b.min.z>=center.y-half && b.max.z<=center.y+half;
        var report=new System.Text.StringBuilder();
        report.AppendLine("Current scene; real prefab Renderer.bounds; no scene saved and no runtime rules changed.");
        report.AppendLine($"START Z {start.z:F9}; board minimum Z {center.y-half:F9}; margin {start.z-center.y+half:F9}");
        int valid=0;float minimumShift=float.PositiveInfinity;
        foreach(var first in records) foreach(var last in records) {
            if((first.angle+180)%360!=last.angle)continue;
            if(Inside(first.bounds)&&Inside(last.bounds))valid++;
            var both=first.bounds;both.Encapsulate(last.bounds);
            if(both.min.x<center.x-half || both.max.x>center.x+half)continue;
            float shift=Mathf.Max(0,center.y-half-both.min.z);
            if(both.max.z+shift<=center.y+half)minimumShift=Mathf.Min(minimumShift,shift);
        }
        foreach(int angle in Enumerable.Range(0,8).Select(i=>i*45))
            report.AppendLine($"Outward connector yaw {angle}: {records.Count(r=>r.angle==angle&&Inside(r.bounds))} fitting options");
        report.AppendLine($"Compatible first/last pairs fully within current board: {valid}");
        report.AppendLine($"Minimum northward START shift permitting at least one local pair: {minimumShift:F9}. Necessary local clearance only; not a full circuit solution.");
        File.WriteAllText("BonusSolutions/start-clearance.txt",report.ToString());
    }
    public static void Validate() {
        EditorSceneManager.OpenScene("Assets/Scenes/Bonus01Test.unity");
        var controller=UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var so=new SerializedObject(controller);
        var start=((Transform)so.FindProperty("startAnchor").objectReferenceValue).position;
        var center=so.FindProperty("buildAreaCenter").vector2Value;
        float half=so.FindProperty("buildHalfSize").floatValue;
        var report=new System.Text.StringBuilder();
        report.AppendLine("Real Unity renderer bounds and connector audit; Edit Mode, not a physical Play Mode run.");
        report.AppendLine($"START {start.ToString("F6")}; board X [{center.x-half},{center.x+half}], Z [{center.y-half},{center.y+half}]");
        bool Inside(CircuitPiece p) {var b=p.GetRenderBounds();return b.min.x>=center.x-half && b.max.x<=center.x+half && b.min.z>=center.y-half && b.max.z<=center.y+half;}
        string[] names={"StraightPiece","HalfStraightPiece","Curve45RightPiece"};
        for(int angle=0;angle<360;angle+=45) {
            var options=new List<string>();
            foreach(string name in names) for(int exit=0;exit<2;exit++) {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircuitEditor/"+name+".prefab"));
                var p=go.GetComponent<CircuitPiece>();
                float localAngle=Vector3.SignedAngle(Vector3.forward,p.GetConnectorDirection(exit),Vector3.up);
                go.transform.rotation=Quaternion.Euler(0,angle-localAngle,0);
                go.transform.position+=start-p.GetConnectorPosition(exit);
                if(Inside(p)) options.Add(name+" exit "+exit);
                UnityEngine.Object.DestroyImmediate(go);
            }
            report.AppendLine($"Arrival yaw {angle}: legal final pieces = {string.Join(", ",options)}");
        }
        var args=Environment.GetCommandLineArgs();int modelIndex=0;
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-bonusModel")modelIndex=int.Parse(args[i+1]);
        if(forcedModel>=0)modelIndex=forcedModel;
        var models=JsonUtility.FromJson<Solutions>("{\"models\":"+File.ReadAllText("BonusSolutions/strict-candidates.json")+"}").models;
        var bonus=new SerializedObject(so.FindProperty("bonusChallenge").objectReferenceValue);
        var refs=bonus.FindProperty("targetVisuals");
        var targets=Enumerable.Range(0,refs.arraySize).Select(i=>(CircuitPiece)refs.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var route=(List<CircuitPiece>)typeof(BallPuzzleLevelController).GetField("routePieces",Private).GetValue(controller);
        var model=models[modelIndex];
        for(int i=0;i<model.pieces.Length;i++) {
            var spec=model.pieces[i];int type=Math.Abs(spec.type);CircuitPiece p;
            if(type==90 || type==180 || type==8) p=targets[type==90?0:type==180?1:2];
            else {
                string name=type==0?"HalfStraightPiece":"Curve45RightPiece";
                p=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircuitEditor/"+name+".prefab"))).GetComponent<CircuitPiece>();
            }
            p.transform.SetPositionAndRotation(new Vector3(spec.root[0],1,spec.root[1]),Quaternion.Euler(0,spec.yaw,0));
            p.gameObject.SetActive(true);p.ClearTint();route.Add(p);
            var b=p.GetRenderBounds();
            report.AppendLine($"Piece {i+1} type {spec.type}, root {p.transform.position.ToString("F6")}, yaw {spec.yaw:F6}, inside {Inside(p)}, min {b.min.ToString("F6")}, max {b.max.ToString("F6")}");
        }
        float maxGap=0,minDot=1,maxDot=-1;
        for(int i=0;i<route.Count;i++) {
            int next=(i+1)%route.Count;
            Vector3 delta=route[i].GetConnectorPosition(1-model.pieces[i].entry)-route[next].GetConnectorPosition(model.pieces[next].entry);
            float dot=Vector3.Dot(route[i].GetConnectorDirection(1-model.pieces[i].entry).normalized,route[next].GetConnectorDirection(model.pieces[next].entry).normalized);
            maxGap=Mathf.Max(maxGap,new Vector2(delta.x,delta.z).magnitude);minDot=Mathf.Min(minDot,dot);maxDot=Mathf.Max(maxDot,dot);
        }
        var closed=typeof(BallPuzzleLevelController).GetMethod("HasClosedBonusCircuit",Private).Invoke(controller,new object[]{false});
        report.AppendLine($"Max XZ connector gap {maxGap:F9}; direction dot [{minDot:F9},{maxDot:F9}]; actual HasClosedBonusCircuit = {closed}");
        File.WriteAllText($"BonusSolutions/unity-validation-{modelIndex+1}.txt",report.ToString());
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
        var camera=new GameObject("Audit Camera").AddComponent<Camera>();camera.transform.SetPositionAndRotation(new Vector3(0,50,4),Quaternion.Euler(90,0,0));
        camera.orthographic=true;camera.orthographicSize=18;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.09f,.11f);
        var rt=new RenderTexture(1280,1280,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1280,1280,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,1280),0,0);texture.Apply();
        File.WriteAllBytes($"BonusSolutions/candidate-unity-{modelIndex+1}.png",texture.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    static PieceData Read(CircuitPiece p, string path) {
        var t=p.transform;
        var b=p.GetRenderBounds();
        return new PieceData { name=p.name,path=path,position=t.position,rotation=t.rotation,
            scale=t.lossyScale,center=b.center,size=b.size,
            connectors=Enumerable.Range(0,p.ConnectorCount).Select(i=>t.InverseTransformPoint(p.GetConnectorPosition(i))).ToArray(),
            directions=Enumerable.Range(0,p.ConnectorCount).Select(i=>t.InverseTransformDirection(p.GetConnectorDirection(i))).ToArray(),
            points=Enumerable.Range(0,p.BallPathPointCount).Select(i=>t.InverseTransformPoint(p.GetBallPathPointPosition(i))).ToArray() };
    }
    public static void Export() {
        EditorSceneManager.OpenScene("Assets/Scenes/Bonus01Test.unity");
        var controller=UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var so=new SerializedObject(controller);
        var bonus=new SerializedObject(so.FindProperty("bonusChallenge").objectReferenceValue);
        var targets=bonus.FindProperty("targetVisuals");
        var data=new Audit {start=((Transform)so.FindProperty("startAnchor").objectReferenceValue).position,
            targets=Enumerable.Range(0,targets.arraySize).Select(i=>Read((CircuitPiece)targets.GetArrayElementAtIndex(i).objectReferenceValue,"scene")).ToArray(),
            prefabs=new[]{"StraightPiece","HalfStraightPiece","Curve45RightPiece","Curve90Piece","Curve180Piece","HumpStraightPiece"}.Select(n=> {
                string path="Assets/Prefabs/CircuitEditor/"+n+".prefab";
                return Read(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CircuitPiece>(),path);
            }).ToArray()};
        Directory.CreateDirectory("BonusSolutions");
        File.WriteAllText("BonusSolutions/geometry.json",JsonUtility.ToJson(data,true));
    }
}
