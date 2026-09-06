using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only regression: real placement events, natural rotating capture, real Rigidbody run.
// Never pre-solves the saved puzzle or substitutes a simulated ball for gameplay physics.
[InitializeOnLoad]
public static class Bonus01TestPlaytest
{
    const string ScenePath = "Assets/Scenes/Bonus01Test.unity";
    const string ActiveKey = "Bonus01Test.Playtest.Active";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static BallPuzzleLevelController controller;
    static BonusPieceChallengeController bonus;
    static CircuitPiece[] targets;
    static BonusSolutionAudit.Solution model;
    static readonly List<CircuitPiece> built = new List<CircuitPiece>();
    static readonly List<int> entries = new List<int>();
    static readonly StringBuilder report = new StringBuilder();
    static readonly StringBuilder trace = new StringBuilder("time,x,y,z,speed,secured,routeExit\n");
    static int next, passed, completions;
    static double deadline, nextSample;
    static bool started, initialized;
    static float runStarted;
    static int physicsTrial;
    static readonly float[] trialSpeeds = { 4.5f, 5f, 4f };
    static readonly float[] trialStaticFrictions = { 0f, 0f, .01f };
    static readonly float[] trialFrictions = { 0f, 0f, 0f };
    static readonly float[] trialBounces = { 0f, 0f, 0f };
    [Serializable] class VerifiedPhysics { public float speed, friction, staticFriction, bounce; }
    [Serializable] class Pref { public string key, kind, value; public bool exists; }
    [Serializable] class Prefs { public Pref[] items; }

    static Bonus01TestPlaytest()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey + ".SavePhysics", false))
            {
                SessionState.SetBool(ActiveKey + ".SavePhysics", false);
                var verified = JsonUtility.FromJson<VerifiedPhysics>(File.ReadAllText("BonusSolutions/verified-physics.json"));
                EditorSceneManager.OpenScene(ScenePath);
                var level = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
                var so = new SerializedObject(level);
                so.FindProperty("launchSpeed").floatValue = verified.speed;
                var body = (Rigidbody)so.FindProperty("ball").objectReferenceValue;
                const string materialPath = "Assets/Settings/Bonus01TestBall.physicsMaterial";
                var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);
                if (material == null) { material = new PhysicsMaterial("Bonus01TestBall"); AssetDatabase.CreateAsset(material, materialPath); }
                material.staticFriction = verified.staticFriction; material.dynamicFriction = verified.friction;
                material.frictionCombine = PhysicsMaterialCombine.Minimum;
                material.bounciness = verified.bounce; material.bounceCombine = PhysicsMaterialCombine.Maximum;
                EditorUtility.SetDirty(material);
                body.GetComponent<SphereCollider>().sharedMaterial = material;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
                EditorSceneManager.SaveScene(level.gameObject.scene); AssetDatabase.SaveAssets();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void TunePhysics()
    {
        SessionState.SetBool(ActiveKey + ".Tune", true);
        Run();
    }

    public static void RepairCurve180CapsAndTest()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var level = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var challenge = (BonusPieceChallengeController)Field(level, "bonusChallenge");
        var target = ((CircuitPiece[])Field(challenge, "targetVisuals"))[1];
        const string folder = "Assets/Art/ClassicReferenceTrack/Bonus01Test";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/ClassicReferenceTrack", "Bonus01Test");
        foreach (string name in new[] { "Inner Wooden Rim", "Outer Wooden Rim" })
        {
            var child = target.transform.Find(name);
            var filter = child.GetComponent<MeshFilter>();
            string path = folder + "/" + name.Replace(" ", "") + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = name + " Correct End Caps";
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = mesh.vertices;
            Require(vertices.Length == 518, "Unexpected Curve180 rim topology");
            Vector3 first = Vector3.zero, last = Vector3.zero;
            for (int i = 0; i < 4; i++) { first += vertices[i]; last += vertices[vertices.Length - 6 + i]; }
            vertices[vertices.Length - 2] = first / 4;
            vertices[vertices.Length - 1] = last / 4;
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            filter.sharedMesh = mesh;
            var collider = child.GetComponent<MeshCollider>(); collider.sharedMesh = mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        EditorSceneManager.SaveScene(level.gameObject.scene); AssetDatabase.SaveAssets();
        TunePhysics();
    }

    public static void AuditColliders()
    {
        BonusSolutionAudit.Validate();
        var level = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var route = (List<CircuitPiece>)Field(level, "routePieces");
        var selected = JsonUtility.FromJson<BonusSolutionAudit.Solutions>("{\"models\":" + File.ReadAllText("BonusSolutions/strict-candidates.json") + "}").models[1];
        Physics.SyncTransforms();
        var result = new StringBuilder();
        for (int i = 0; i < route.Count; i++)
        {
            var piece = route[i]; int entry = selected.pieces[i].entry;
            Vector3 endpoint = piece.GetConnectorPosition(entry);
            Vector3 inward = -piece.GetConnectorDirection(entry); inward.y = 0; inward.Normalize();
            result.AppendLine($"PIECE {i+1} {piece.name} root={piece.transform.position:F4}");
            for (float offset = -.2f; offset < .61f; offset += .2f)
            {
                var point = endpoint + inward * offset; point.y = 6;
                foreach (var hit in Physics.RaycastAll(point, Vector3.down, 7).OrderBy(h => h.distance).Take(4))
                    result.AppendLine($" FLOOR offset={offset:F2} y={hit.point.y:F4} collider={hit.collider.name} normal={hit.normal:F3}");
            }
            var origin = endpoint - inward * .8f; origin.y = 2.09f;
            foreach (var hit in Physics.SphereCastAll(origin, .6f, inward, 1.6f).OrderBy(h => h.distance))
                if (Vector3.Dot(hit.normal, inward) < -.2f)
                    result.AppendLine($" BLOCK distance={hit.distance:F4} collider={hit.collider.name} point={hit.point:F3} normal={hit.normal:F3}");
        }
        File.WriteAllText("BonusSolutions/collider-entrances.txt", result.ToString());
    }
    static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Log(string message)
    {
        report.AppendLine(message);
        Debug.Log("[BonusPlaytest] " + message);
        File.WriteAllText("BonusSolutions/playtest-report.txt", report.ToString());
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void IntegrateAndTest()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (!File.Exists("BonusSolutions/Bonus01Test.before-final-integration.unity"))
            File.Copy(ScenePath, "BonusSolutions/Bonus01Test.before-final-integration.unity");
        var level = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
        var so = new SerializedObject(level);
        var challenge = new SerializedObject(so.FindProperty("bonusChallenge").objectReferenceValue);
        var refs = challenge.FindProperty("targetVisuals");
        var baseline = JsonUtility.FromJson<BonusSolutionAudit.Audit>(File.ReadAllText("BonusSolutions/geometry.json"));
        Vector3[] positions = {
            baseline.targets[0].position + new Vector3(.338376382f, 0, -.372770617f),
            baseline.targets[1].position + new Vector3(.780443568f, 0, 1.214945485f),
            new Vector3(-3.8f, 1, 9.97f)
        };
        for (int i = 0; i < 3; i++)
        {
            var piece = (CircuitPiece)refs.GetArrayElementAtIndex(i).objectReferenceValue;
            piece.transform.SetPositionAndRotation(positions[i], baseline.targets[i].rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(piece.transform);
            var moving = new SerializedObject(piece.GetComponent<BonusRotatingPieceConnector>());
            moving.FindProperty("connectionDistance").floatValue = .6f;
            moving.ApplyModifiedPropertiesWithoutUndo();
            var ring = (SpriteRenderer)challenge.FindProperty("targetRings").GetArrayElementAtIndex(i).objectReferenceValue;
            Vector3 center = baseline.targets[i].center + positions[i] - baseline.targets[i].position;
            ring.transform.position = new Vector3(center.x, ring.transform.position.y, center.z);
        }
        so.FindProperty("availableBonusLoanPieces").intValue = 0;
        var card = new SerializedObject(so.FindProperty("bonusLoanPieceCard").objectReferenceValue);
        card.FindProperty("activeInPalette").boolValue = false;
        card.ApplyModifiedPropertiesWithoutUndo();
        ((PieceSelectionCard)card.targetObject).gameObject.SetActive(false);
        var spawn = (Transform)so.FindProperty("ballSpawnPoint").objectReferenceValue;
        var anchor = (Transform)so.FindProperty("startAnchor").objectReferenceValue;
        spawn.position = new Vector3(anchor.position.x, 2.09f, anchor.position.z);
        var ball = (Rigidbody)so.FindProperty("ball").objectReferenceValue;
        ball.transform.position = spawn.position;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        EditorSceneManager.SaveScene(level.gameObject.scene);
        Run();
    }

    [MenuItem("Ball Puzzle/Bonus01Test/Probar solución elegida (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        SnapshotPrefs();
        SessionState.SetBool(ActiveKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (!initialized)
            {
                controller = UnityEngine.Object.FindFirstObjectByType<BallPuzzleLevelController>();
                if (controller == null) return;
                foreach (var intro in UnityEngine.Object.FindObjectsByType<HideAfterSeconds>(FindObjectsSortMode.None))
                    intro.gameObject.SetActive(false);
                Time.timeScale = 1;
                Application.targetFrameRate = 120;
                bonus = (BonusPieceChallengeController)Field(controller, "bonusChallenge");
                targets = (CircuitPiece[])Field(bonus, "targetVisuals");
                model = JsonUtility.FromJson<BonusSolutionAudit.Solutions>("{\"models\":" + File.ReadAllText("BonusSolutions/strict-candidates.json") + "}").models[1];
                built.Clear(); entries.Clear(); report.Clear(); next = passed = completions = 0; started = false;
                controller.LevelCompleted += () => completions++;
                initialized = true; physicsTrial = 0;
                deadline = EditorApplication.timeSinceStartup + 18;
                Log("REAL EDITOR PLAY MODE. Model 2. No forced captures or ball guidance. " + DateTime.Now.ToString("s"));
                Require(!(bool)Call(controller, "CanStartBallTest"), "Empty puzzle allowed launch");
                return;
            }
            if (!started && next < model.pieces.Length)
            {
                Require(!(bool)Call(controller, "CanStartBallTest"), "Incomplete circuit allowed launch at piece " + next);
                var spec = model.pieces[next]; int type = Math.Abs(spec.type);
                CircuitPiece piece;
                if (type == 90 || type == 180 || type == 8)
                {
                    piece = targets[type == 90 ? 0 : type == 180 ? 1 : 2];
                    if (!piece.GetComponent<BonusRotatingPieceConnector>().IsConnected)
                    {
                        Require(EditorApplication.timeSinceStartup < deadline, "Natural capture timeout, piece " + (next + 1));
                        return;
                    }
                    Vector3 previousExit = built[built.Count - 1].GetConnectorPosition(1 - entries[entries.Count - 1]);
                    int entry = Vector3.Distance(piece.GetConnectorPosition(0), previousExit) < Vector3.Distance(piece.GetConnectorPosition(1), previousExit) ? 0 : 1;
                    entries.Add(entry);
                    Log("Natural capture " + piece.name + ": root=" + piece.transform.position.ToString("F5") + ", yaw=" + piece.transform.eulerAngles.y);
                }
                else
                {
                    string prefabName = type == 0 ? "HalfStraightPiece" : "Curve45RightPiece";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CircuitEditor/" + prefabName + ".prefab");
                    var card = (PieceSelectionCard)Field(controller, type == 0 ? "halfStraightPieceCard" : "curve45PieceCard");
                    Require(card.ActiveInPalette && !card.LockedInPalette, "Required player card unavailable");
                    Call(controller, "BeginPlacement", prefab.GetComponent<CircuitPiece>(), card);
                    var pending = Field(controller, "pendingPlacement");
                    piece = (CircuitPiece)pending.GetType().GetProperty("Piece").GetValue(pending);
                    pending.GetType().GetProperty("RotationIndex").SetValue(pending, Mathf.RoundToInt(spec.yaw / 45f));
                    var camera = (Camera)Field(controller, "buildCamera");
                    Vector3 screen = camera.WorldToScreenPoint(new Vector3(spec.root[0] + .08f, (float)Field(controller, "buildSurfaceHeight"), spec.root[1] - .07f));
                    Call(controller, "UpdatePendingPiece", new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y)));
                    Call(controller, "FinishPendingPieceDrag");
                    int before = controller.PlacedPieceCount;
                    Call(controller, "PlacePendingPiece");
                    Require(controller.PlacedPieceCount == before + 1, "Placement rejected at " + (next + 1) + ": " + Field(controller, "status"));
                    entries.Add(spec.entry);
                    Log("Player palette + rounded pointer + 45-degree rotation + release + PLACE " + (next + 1) + ": " + piece.name);
                }
                built.Add(piece); next++;
                deadline = EditorApplication.timeSinceStartup + 18;
                return;
            }
            if (!started)
            {
                Require(bonus.AreAllTargetsConnected, "Missing moving piece");
                Require((bool)Call(controller, "HasClosedBonusCircuit", false), "Circuit not closed");
                Vector2 center = (Vector2)Field(controller, "buildAreaCenter"); float half = (float)Field(controller, "buildHalfSize");
                float maxGap = 0;
                for (int i = 0; i < built.Count; i++)
                {
                    var b = built[i].GetRenderBounds();
                    Require(b.min.x >= center.x - half && b.max.x <= center.x + half && b.min.z >= center.y - half && b.max.z <= center.y + half, "Outside board " + (i + 1));
                    int j = (i + 1) % built.Count;
                    Vector3 delta = built[i].GetConnectorPosition(1 - entries[i]) - built[j].GetConnectorPosition(entries[j]);
                    maxGap = Mathf.Max(maxGap, new Vector2(delta.x, delta.z).magnitude);
                }
                Require(maxGap < .01f, "Connector gap " + maxGap);
                Log("22 pieces connected, all bounds inside board. Maximum connector gap=" + maxGap.ToString("F9"));
                Require((bool)Call(controller, "CanStartBallTest"), "Complete circuit blocked launch");
                // Negative tests deliberately alter test-only state, then restore it before the real run.
                var moving = targets[0].GetComponent<BonusRotatingPieceConnector>();
                moving.GetType().GetField("isConnected", Private).SetValue(moving, false);
                Require(!(bool)Call(controller, "CanStartBallTest"), "Launch accepted with missing mobile");
                moving.GetType().GetField("isConnected", Private).SetValue(moving, true);
                var savedPosition = built[0].transform.position;
                built[0].transform.position += Vector3.right * 100;
                Require(!(bool)Call(controller, "CanStartBallTest"), "Launch accepted outside board");
                built[0].transform.position = savedPosition; Physics.SyncTransforms();
                Call(controller, "StartBallTest");
                ((Rigidbody)Field(controller, "ball")).position = new Vector3(0, -5, 0);
                Call(controller, "UpdateBallTest");
                Require(Field(controller, "state").ToString() == "Failure" && completions == 0, "Fall counted as success");
                Call(controller, "ReturnToBuild");
                Log("PASS negative checks: incomplete/mobile missing/outside board block launch; falling causes failure and zero completions.");
                ApplyTrialPhysics();
                Call(controller, "StartBallTest"); started = true; runStarted = Time.time;
                deadline = EditorApplication.timeSinceStartup + 65;
                Log("Rigidbody launched: " + ((Rigidbody)Field(controller, "ball")).linearVelocity.ToString("F4"));
                Capture("BonusSolutions/playtest-assembled.png");
                return;
            }
            var rigidbody = (Rigidbody)Field(controller, "ball");
            if (passed < built.Count && Vector2.Distance(new Vector2(rigidbody.position.x, rigidbody.position.z),
                new Vector2(built[passed].GetConnectorPosition(1 - entries[passed]).x, built[passed].GetConnectorPosition(1 - entries[passed]).z)) < 1.25f)
            { passed++; Log("Ball passed route exit " + passed + "/22 at " + (Time.time - runStarted).ToString("F2") + "s"); }
            if (EditorApplication.timeSinceStartup >= nextSample)
            {
                nextSample = EditorApplication.timeSinceStartup + .1;
                var p = rigidbody.position;
                trace.AppendLine(FormattableString.Invariant($"{Time.time-runStarted:F3},{p.x:F4},{p.y:F4},{p.z:F4},{rigidbody.linearVelocity.magnitude:F4},{bonus.SecuredCount},{passed}"));
            }
            string currentState = Field(controller, "state").ToString();
            if (currentState == "Success")
            {
                Require(passed == built.Count && completions == 1 && bonus.SecuredCount == 3, "False success / incomplete full-lap proof");
                Finish(true, "PASS: physical lap, 22/22 ordered exits, 3/3 targets, exactly one completion.");
            }
            else if (currentState == "Failure" || EditorApplication.timeSinceStartup > deadline)
            {
                string failure = "Physical run failed: " + Field(controller, "status") + "; exits=" + passed + "; position=" + rigidbody.position.ToString("F4") + "; completions=" + completions;
                if (SessionState.GetBool(ActiveKey + ".Tune", false) && physicsTrial + 1 < trialSpeeds.Length)
                {
                    Log(failure);
                    File.WriteAllText("BonusSolutions/playtest-trial-" + physicsTrial + ".csv", trace.ToString());
                    physicsTrial++; passed = 0; trace.Clear();
                    Call(controller, "ReturnToBuild"); ApplyTrialPhysics(); Call(controller, "StartBallTest");
                    Require(bonus.SecuredCount == 0, "Retry retained secured targets");
                    runStarted = Time.time; deadline = EditorApplication.timeSinceStartup + 65;
                }
                else Finish(false, failure);
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Capture(string path)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = (Camera)Field(controller, "buildCamera");
        var rt = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        UnityEngine.Object.Destroy(texture); rt.Release(); UnityEngine.Object.Destroy(rt);
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(ActiveKey, false);
        Log(message);
        File.WriteAllText("BonusSolutions/playtest-ball.csv", trace.ToString());
        RestorePrefs();
        if (success && SessionState.GetBool(ActiveKey + ".Tune", false))
        {
            File.WriteAllText("BonusSolutions/verified-physics.json", JsonUtility.ToJson(new VerifiedPhysics { speed = trialSpeeds[physicsTrial], friction = trialFrictions[physicsTrial], staticFriction = trialStaticFrictions[physicsTrial], bounce = trialBounces[physicsTrial] }, true));
            SessionState.SetBool(ActiveKey + ".Tune", false);
            SessionState.SetBool(ActiveKey + ".SavePhysics", true);
            EditorApplication.ExitPlaymode();
            return;
        }
        SessionState.SetBool(ActiveKey + ".Tune", false);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
    }
    static void ApplyTrialPhysics()
    {
        if (!SessionState.GetBool(ActiveKey + ".Tune", false)) return;
        var body = (Rigidbody)Field(controller, "ball");
        var material = new PhysicsMaterial("Temporary bonus friction test") {
            staticFriction = trialStaticFrictions[physicsTrial], dynamicFriction = trialFrictions[physicsTrial],
            frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = trialBounces[physicsTrial], bounceCombine = PhysicsMaterialCombine.Maximum
        };
        body.GetComponent<SphereCollider>().sharedMaterial = material;
        controller.GetType().GetField("launchSpeed", Private).SetValue(controller, trialSpeeds[physicsTrial]);
        Log("Physical trial " + physicsTrial + ": mass=" + body.mass + ", speed=" + trialSpeeds[physicsTrial] + ", friction=" + trialFrictions[physicsTrial] + ", bounce=" + trialBounces[physicsTrial]);
    }
    static void SnapshotPrefs()
    {
        var keys = new[] { "BallPuzzleLastPlayedLevel", "BallPuzzleTotalCompletionTime", "BallPuzzleCompletionTime.Bonus01Test", "BallPuzzleUnlockedPiece.Straight", "BallPuzzleUnlockedPiece.Curve90", "BallPuzzleUnlockedPiece.Curve180" };
        var values = keys.Select((key, i) => new Pref { key = key, exists = PlayerPrefs.HasKey(key), kind = i == 0 ? "s" : i < 3 ? "f" : "i",
            value = i == 0 ? PlayerPrefs.GetString(key) : i < 3 ? PlayerPrefs.GetFloat(key).ToString(System.Globalization.CultureInfo.InvariantCulture) : PlayerPrefs.GetInt(key).ToString() }).ToArray();
        SessionState.SetString(ActiveKey + ".Prefs", JsonUtility.ToJson(new Prefs { items = values }));
    }
    static void RestorePrefs()
    {
        var prefs = JsonUtility.FromJson<Prefs>(SessionState.GetString(ActiveKey + ".Prefs", ""));
        if (prefs == null) return;
        foreach (var p in prefs.items)
            if (!p.exists) PlayerPrefs.DeleteKey(p.key);
            else if (p.kind == "s") PlayerPrefs.SetString(p.key, p.value);
            else if (p.kind == "i") PlayerPrefs.SetInt(p.key, int.Parse(p.value));
            else PlayerPrefs.SetFloat(p.key, float.Parse(p.value, System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
