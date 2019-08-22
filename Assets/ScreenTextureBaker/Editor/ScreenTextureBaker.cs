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
    /// Clears the target Texture2D to white transparent.
    /// </summary>
    /// <param name="targetTexture"></param>
    private void ClearTexture(Texture2D targetTexture)
    {
        Color32 resetColor = new Color32(255, 255, 255, 0);
        Color32[] resetColorArray = targetTexture.GetPixels32();

        for (int i = 0; i < resetColorArray.Length; i++)
        {
            resetColorArray[i] = resetColor;
        }

        targetTexture.SetPixels32(resetColorArray);
        targetTexture.Apply();
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

        bakingTexture = new Texture2D(bakingTextureSize, bakingTextureSize, TextureFormat.ARGB32, false);

        ClearTexture(bakingTexture);

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

                        Color currentSample = screenTexture.GetPixel(x, y);

                        if (currentSample.a > 0)
                        {
                            Color accumulatedResult = bakingTexture.GetPixel((int)pixelUV.x, (int)pixelUV.y);

                            currentSample = Color.Lerp(accumulatedResult, currentSample, 1.0f / (i + 1.0f));
                        }

                        bakingTexture.SetPixel((int)pixelUV.x, (int)pixelUV.y, currentSample);
                    }
                }
            }
        }
        bakingTexture.Apply();

        byte[] bytes = bakingTexture.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/ScreenTextureBaker_OutputImage.png", bytes);

        camera.targetTexture = null;

        EditorUtility.ClearProgressBar();
    }
}
