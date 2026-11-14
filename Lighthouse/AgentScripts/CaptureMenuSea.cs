using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEngine;
using UnityEngine.UIElements;

// One-off capture for HTML approval. Compiled in memory through Unity CLI;
// never imported into Assets or saved into the scene.
public static class CaptureMenuSea
{
    public static Task<string> Main(int frameCount = 480)
    {
        var session = UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();
        if (!Application.isPlaying || !session || session.Running)
        {
            throw new InvalidOperationException("Capture requires the existing free-preview Play Mode session.");
        }
        var completion = new TaskCompletionSource<string>();
        session.StartCoroutine(Capture(session, frameCount, completion));
        return completion.Task;
    }

    private static IEnumerator Capture(BlockoutSession session, int frameCount, TaskCompletionSource<string> completion)
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/UI/MainMenu/SeaCapture"));
        Directory.CreateDirectory(folder);
        var player = session.Player;
        var camera = player.View;
        var environment = CinematicEnvironment.Active;
        var girl = UnityEngine.Object.FindAnyObjectByType<WeepingGirlEncounter>();
        var documents = UnityEngine.Object.FindObjectsByType<UIDocument>();
        var documentStates = Array.ConvertAll(documents, document => document.enabled);
        var cameraPosition = camera.transform.localPosition;
        var cameraRotation = camera.transform.localRotation;
        var cameraFov = camera.fieldOfView;
        var playerEnabled = player.enabled;
        var heldVisible = player.HeldCamera && player.HeldCamera.gameObject.activeSelf;
        var girlEnabled = girl && girl.enabled;
        var automatic = session.Observations && session.Observations.Automatic;
        var hour = environment.Hour;
        var weather = environment.Weather;
        var advance = environment.AdvanceTime;
        var autoWeather = environment.AutoWeather;
        var captureDelta = Time.captureDeltaTime;
        var full = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var small = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var originalTarget = RenderTexture.active;
        var captured = 0;
        try
        {
            player.enabled = false;
            if (player.HeldCamera) player.HeldCamera.gameObject.SetActive(false);
            if (girl) girl.enabled = false;
            if (session.Observations) session.Observations.Automatic = false;
            foreach (var document in documents) document.enabled = false;
            environment.AdvanceTime = false;
            environment.AutoWeather = false;
            environment.SetWeather(CoastalWeather.Clear, true);
            environment.SetHour(17.4f);
            camera.transform.position = new Vector3(5.0f, 8.0f, -11.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(22.0f, 1.8f, -180.0f) - camera.transform.position);
            camera.fieldOfView = 65;
            for (var warmup = 0; warmup < 90; warmup++) yield return new WaitForEndOfFrame();
            Time.captureDeltaTime = 1f / 24f;
            full.Create(); small.Create();
            for (var frame = 0; frame < frameCount; frame++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(full, small, new Vector2(1, -1), new Vector2(0, 1));
                else Graphics.Blit(full, small);
                RenderTexture.active = small;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply(false);
                RenderTexture.active = originalTarget;
                File.WriteAllBytes(Path.Combine(folder, "frame-" + frame.ToString("D4") + ".jpg"), image.EncodeToJPG(90));
                captured++;
                if (frame % 24 == 0) File.WriteAllText(Path.Combine(folder, "progress.json"), "{\"frames\":" + captured + ",\"target\":" + frameCount + "}");
            }
        }
        finally
        {
            Time.captureDeltaTime = captureDelta;
            RenderTexture.active = originalTarget;
            full.Release(); small.Release();
            UnityEngine.Object.Destroy(full); UnityEngine.Object.Destroy(small); UnityEngine.Object.Destroy(image);
            camera.transform.localPosition = cameraPosition;
            camera.transform.localRotation = cameraRotation;
            camera.fieldOfView = cameraFov;
            environment.SetWeather(weather, true); environment.SetHour(hour);
            environment.AdvanceTime = advance; environment.AutoWeather = autoWeather;
            if (session.Observations) session.Observations.Automatic = automatic;
            if (girl) girl.enabled = girlEnabled;
            for (var i = 0; i < documents.Length; i++) if (documents[i]) documents[i].enabled = documentStates[i];
            if (player.HeldCamera) player.HeldCamera.gameObject.SetActive(heldVisible);
            player.enabled = playerEnabled;
            var result = "{\"frames\":" + captured + ",\"fps\":24,\"width\":1280,\"height\":720,\"restored\":true,\"sceneSaved\":false}";
            File.WriteAllText(Path.Combine(folder, "capture.json"), result);
            if (captured == frameCount) completion.TrySetResult(result);
            else completion.TrySetException(new InvalidOperationException("Capture interrupted after " + captured + " frames; scene state restored."));
        }
    }
}
