using UnityEditor;
using UnityEngine;
using Unity.Collections;
using System.IO;
using Unity.Jobs;

public class ScreenTextureBaker : EditorWindow
{
    private Texture2D bakingTexture;

    private RenderTexture screenRenderTexture;
    private Texture2D screenTexture;

    private Camera camera;

    private int bakingTextureSize = 1024;
    private int screenTextureSize = 1024;
    private int samples = 1;
    private const string bakingTextureDefaultName = "ScreenTextureBaker_OutputImage";
    private string bakingTextureName = bakingTextureDefaultName;

    private Color32 blankPixelColor = new Color32(255, 255, 255, 0);

    [MenuItem("Window/Rendering/Screen Texture Baker")]
    public static void ShowWindow()
    {
        GetWindow<ScreenTextureBaker>(false, "Screen Texture Baker", true);
    }

    void OnGUI()
    {
        bakingTextureName = (string)EditorGUILayout.TextField("Baking Texture Name", bakingTextureName);
        bakingTextureSize = (int)EditorGUILayout.IntField("Baking Texture Size", bakingTextureSize);
        screenTextureSize = EditorGUILayout.IntField("Screen Texture Size", screenTextureSize);
        samples = EditorGUILayout.IntSlider("Samples Per Pixel", samples, 1, 10);

        if (GUILayout.Button("Bake Screen to Texture"))
        {
            BakeTexture();
        }
    }

    /// <summary>
    /// Bakes main camera texture onto the UVs of the model in view.
    /// </summary>
    void BakeTexture()
    {
        screenRenderTexture = new RenderTexture(screenTextureSize, screenTextureSize, 2);
        RenderTexture.active = screenRenderTexture;

        camera = Camera.main;
        camera.targetTexture = screenRenderTexture;
        camera.Render();

        screenTexture = new Texture2D(screenTextureSize, screenTextureSize, TextureFormat.RGB24, false);
        screenTexture.ReadPixels(new Rect(0, 0, screenTextureSize, screenTextureSize), 0, 0);
        Color[] screenTexturePixels = screenTexture.GetPixels();

        bakingTexture = new Texture2D(bakingTextureSize, bakingTextureSize, TextureFormat.ARGB32, false);

        Color[] bakingTexturePixels = bakingTexture.GetPixels();

        for (int i = 0; i < bakingTexturePixels.Length; i++)
        { 
            bakingTexturePixels[i] = blankPixelColor;
        }

        Color[] currentPassPixels = bakingTexturePixels;

        for (int i = 0; i < samples; i++)
        {
            var results = new NativeArray<RaycastHit>(screenTexture.height * screenTexture.width, Allocator.TempJob);
            var commands = new NativeArray<RaycastCommand>(screenTexture.height * screenTexture.width, Allocator.TempJob);

            if (EditorUtility.DisplayCancelableProgressBar("Screen Texture Baker", "Progress: " + (i + 1) + " / " + samples + " Sample(s)", (i + 1) / (float)samples))
            {
                EditorUtility.ClearProgressBar();
                results.Dispose();
                commands.Dispose();

                Debug.Log("Screen Texture Baker: Bake canceled by the user.");

                return;
            }

            for (int y = 0; y < screenTexture.height; y++)
            {
                for (int x = 0; x < screenTexture.width; x++)
                {
                    Ray ray = camera.ScreenPointToRay(new Vector3(x + Random.value, y + Random.value, 0));
                    commands[screenTexture.width * y + x] = new RaycastCommand(ray.origin, ray.direction, 90);
                }
            }

            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));

            handle.Complete();

            for (int j = 0; j < results.Length; j++)
            {
                RaycastHit batchedHit = results[j];

                if (batchedHit.collider != null)
                {
                    Vector2 pixelUV = batchedHit.textureCoord;
                    pixelUV.x *= bakingTextureSize;
                    pixelUV.y *= bakingTextureSize;

                    currentPassPixels[bakingTexture.width * (int)pixelUV.y + (int)pixelUV.x] = screenTexturePixels[j];
                }
            }

            for (int n = 0; n < bakingTexturePixels.Length; n++)
            {
                bakingTexturePixels[n] = (bakingTexturePixels[n].a <= 0) ? currentPassPixels[n] : Color.Lerp(bakingTexturePixels[n], currentPassPixels[n], 1.0f / (i + 1.0f));
            }

            results.Dispose();
            commands.Dispose();
        }

        bakingTexture.SetPixels(bakingTexturePixels);
        bakingTexture.Apply();

        byte[] bytes = bakingTexture.EncodeToPNG();

        if (bakingTextureName == "")
        {
            bakingTextureName = bakingTextureDefaultName;
        }

        File.WriteAllBytes(Application.dataPath + "/" + bakingTextureName + ".png", bytes);

        camera.targetTexture = null;

        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();

        Debug.Log("Screen Texture Baker: Bake Complete.");
    }
}
