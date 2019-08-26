using UnityEditor;
using UnityEngine;
using System.IO;

public class ScreenTextureBaker : EditorWindow
{
    private Texture2D bakingTexture;

    private RenderTexture screenRenderTexture;
    private Texture2D screenTexture;

    private Camera camera;

    private int bakingTextureSize = 1024;
    private int screenTextureSize = 1024;
    private int spp = 1;

    private Color32 blankPixelColor = new Color32(255, 255, 255, 0);

    [MenuItem("Window/Rendering/Screen Texture Baker")]
    public static void ShowWindow()
    {
        GetWindow<ScreenTextureBaker>(false, "Screen Texture Baker", true);
    }

    void OnGUI()
    {
        bakingTextureSize = (int)EditorGUILayout.IntField("Baking Texture Size", bakingTextureSize);
        screenTextureSize = EditorGUILayout.IntField("Screen Texture Size", screenTextureSize);
        spp = EditorGUILayout.IntSlider("Samples Per Pixel", spp, 1, 10);

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

        for (int i = 0; i < spp; i++)
        {
            for (int x = 0; x < screenTexture.width; x++)
            {
                float progressBarAmount = x / (float)screenTexture.width;

                EditorUtility.DisplayProgressBar("Screen Texture Baker", "Progress: " + (i + 1) + " / " + spp + " SPP", progressBarAmount);

                for (int y = 0; y < screenTexture.height; y++)
                {
                    Ray ray = camera.ScreenPointToRay(new Vector3(x + Random.value, y + Random.value, 0));
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, 90f))
                    {
                        Vector2 pixelUV = hit.textureCoord;
                        pixelUV.x *= bakingTextureSize;
                        pixelUV.y *= bakingTextureSize;

                        currentPassPixels[bakingTexture.width * (int)pixelUV.y + (int)pixelUV.x] = screenTexturePixels[screenTexture.width * y + x];
                    }
                }
            }

            for (int n = 0; n < bakingTexturePixels.Length; n++)
            {
                bakingTexturePixels[n] = (bakingTexturePixels[n].a <= 0) ? currentPassPixels[n] : Color.Lerp(bakingTexturePixels[n], currentPassPixels[n], 1.0f / (i + 1.0f));
            }
        }

        bakingTexture.SetPixels(bakingTexturePixels);
        bakingTexture.Apply();

        byte[] bytes = bakingTexture.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/ScreenTextureBaker_OutputImage.png", bytes);

        camera.targetTexture = null;

        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();
    }
}
