using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class Classic45CurveAddOnce
{
    private const string MarkerPath = "Classic45CurveAdd.request";

    [InitializeOnLoadMethod]
    private static void AddWhenRequested()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        string marker = Path.GetFullPath(MarkerPath);
        if (!File.Exists(marker))
        {
            return;
        }

        File.Delete(marker);
        EditorApplication.delayCall += () =>
        {
            Classic45CurveTrackBuilder.CreateClassic45DegreeCurve();
            RenderPreview();
        };
    }

    private static void RenderPreview()
    {
        GameObject track = GameObject.Find("45 Degree Curve Classic Reference");
        if (track == null)
        {
            Debug.LogError("The 45-degree curve was not found for preview.");
            return;
        }

        HashSet<Renderer> trackRenderers = new HashSet<Renderer>(track.GetComponentsInChildren<Renderer>(true));
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        Dictionary<Renderer, bool> rendererStates = new Dictionary<Renderer, bool>();
        foreach (Renderer item in allRenderers)
        {
            rendererStates[item] = item.enabled;
            item.enabled = trackRenderers.Contains(item);
        }

        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        Dictionary<Light, bool> lightStates = new Dictionary<Light, bool>();
        foreach (Light item in allLights)
        {
            lightStates[item] = item.enabled;
            item.enabled = false;
        }

        AmbientMode oldAmbientMode = RenderSettings.ambientMode;
        Color oldAmbientLight = RenderSettings.ambientLight;
        float oldAmbientIntensity = RenderSettings.ambientIntensity;
        float oldReflectionIntensity = RenderSettings.reflectionIntensity;
        Material oldSkybox = RenderSettings.skybox;
        bool oldFog = RenderSettings.fog;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.28f;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;

        GameObject lightObject = new GameObject("Curve Preview Light");
        Light previewLight = lightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.color = Color.white;
        previewLight.intensity = 1.05f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        GameObject cameraObject = new GameObject("Curve Preview Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.82f, 0.84f, 0.86f);
        camera.fieldOfView = 33f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;

        Vector3 target = track.transform.position + new Vector3(-0.7f, 0.55f, 1.8f);
        cameraObject.transform.position = target + new Vector3(5.8f, 5.6f, -7.4f);
        cameraObject.transform.LookAt(target);

        RenderTexture renderTexture = new RenderTexture(1000, 800, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        camera.Render();
        camera.Render();

        RenderTexture oldActive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0);
        image.Apply();
        string outputPath = Path.GetFullPath("Logs/Classic45CurvePreview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, image.EncodeToPNG());

        RenderTexture.active = oldActive;
        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(lightObject);

        foreach (KeyValuePair<Renderer, bool> state in rendererStates)
        {
            state.Key.enabled = state.Value;
        }
        foreach (KeyValuePair<Light, bool> state in lightStates)
        {
            state.Key.enabled = state.Value;
        }
        RenderSettings.ambientMode = oldAmbientMode;
        RenderSettings.ambientLight = oldAmbientLight;
        RenderSettings.ambientIntensity = oldAmbientIntensity;
        RenderSettings.reflectionIntensity = oldReflectionIntensity;
        RenderSettings.skybox = oldSkybox;
        RenderSettings.fog = oldFog;
        Debug.Log("Rendered classic 45-degree curve preview.");
    }
}
