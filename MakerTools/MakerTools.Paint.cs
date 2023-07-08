using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using MaterialEditorAPI;
using KKAPI.Maker;
using EatInputCollider;
using ADV.Commands.Base;
using KKAPI.Utilities;

namespace MakerTools
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(MakerTools.GUID, MakerTools.Version)]
    class MakerTools_Paint : BaseUnityPlugin
    {
        public const string PluginName = "MakerTools.Paint";
        public const string GUID = "org.njaecha.plugins.makertoolspaint";
        public const string Version = "0.0.1";

        private new static ManualLogSource Logger = MakerTools.Logger;

        // Config
        private static ConfigEntry<KeyboardShortcut> shortcut;
        private static ConfigEntry<int> historyMaxLength;

        // UI
        private bool isUIActive = false;
        private Rect windowRect = new Rect(100, 100, 200, 260);
        List<Texture2D> colorTextures = new List<Texture2D>();
        List<Color> colors = new List<Color>() { Color.white, Color.black, Color.red, Color.green, Color.blue, Color.yellow };
        private int colorIndex = 0;

        // Paint
        private static bool paintModeEnabled;
        private static GameObject paintObject;
        private static MakerTools_Cursor3D paintCursor;
        public static Color paintColor = Color.white;
        private static Texture2D paintBrush;
        private static RenderTexture paintTexture; //rt
        private static RenderTexture paintTexture2; //rt2
        private static Material paintMaterial;
        private static float paintScale = 0.01f;
        private static float paintDensity = 0.001f;
        private static bool isDragging = false;
        private static Vector2 prevPaintPos;
        private List<Texture2D> paintBrushesDefault = new List<Texture2D>();
        private static float paintOpacity = 1f;

        // Stroke mechanic
        private static Stroke currentStroke;
        private static RenderTexture baseTex;
        private static RenderTexture strokeBaseTex;
        private static RenderTexture outputTex;
        private static List<RenderTexture> history = new List<RenderTexture>();
        private static int historyIndex = -1;

        private static Material compositeMaterial;

        void Awake()
        {
            shortcut = Config.Bind("", "Shortcut", new KeyboardShortcut(KeyCode.P, KeyCode.LeftAlt), "Keyboard shortcut to toggle the Paint UI");
            historyMaxLength = Config.Bind("", "History Length", 10, "Maximum strokes that are saved in the history");

            foreach (Color c in colors)
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.SetPixels(Enumerable.Repeat(c, 4).ToArray());
                tex.Apply();
                colorTextures.Add(tex);
            }

            Texture brush = null; // 33

            AssetBundle ab = AssetBundle.LoadFromMemory(System.IO.File.ReadAllBytes($@"{Paths.PluginPath}\MakerTools\Paint\paint.unity3d")); //36
            try
            {
                brush = ab.LoadAsset<Texture>("assets/brush.png");
                for (int i = 1; i < 6; i++)
                {
                    Texture2D b = new Texture2D(256, 256);
                    b.wrapMode = TextureWrapMode.Clamp;
                    ImageConversion.LoadImage(b, System.IO.File.ReadAllBytes($@"{Paths.PluginPath}\MakerTools\Paint\brush{i}.png"));
                    paintBrushesDefault.Add(b);
                }
                paintMaterial = ab.LoadAsset<Material>("assets/paint_reduced.mat");
            }
            catch (Exception) {
                Logger.LogError("didn't load");
                return;
            }
            ab.Unload(false);

            paintBrush = paintBrushesDefault[0]; // 45

            paintMaterial.SetTexture("_Brush", paintBrush); // 56

            Shader compShader;
            AssetBundle xy = AssetBundle.LoadFromMemory(System.IO.File.ReadAllBytes($@"{Paths.PluginPath}\MakerTools\Paint\composite.unity3d"));
            try
            {
                compShader = xy.LoadAsset<Shader>("composite");
            }
            catch (Exception)
            {
                Logger.LogError("didn't load");
                return;
            }
            xy.Unload(false);
            if (compShader != null)
            {
                compositeMaterial = new Material(compShader);
            }
            else Logger.LogInfo("Composite Shader could not be loaded!");

        }

        void OnGUI()
        {
            if (!KKAPI.Maker.MakerAPI.InsideAndLoaded || !isUIActive) return;
            windowRect = GUI.Window(25648, windowRect, WindowFunction, $"{PluginName} v{Version}");
            KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect);
        }

        private void WindowFunction(int WindowID)
        {
            if (GUI.Button(new Rect(10,30, 180, 30), paintModeEnabled ? "Exit Paint mode" : "Enter Paint Mode"))
            {
                if (!paintModeEnabled) startPaintMode();
                else stopPaintMode();
            }
            for(int i = 0; i < colorTextures.Count; i++)
            {
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.background = colorTextures[i];
                style.hover.background = colorTextures[i];
                if (GUI.Button(new Rect(10+i*30, 70, 30,30), "", style))
                {
                    paintColor = colors[i];
                    colorIndex = i;
                    paintMaterial?.SetColor("_Color", colors[i]);
                }
            }
            for(int i = 0; i < paintBrushesDefault.Count; i++)
            {
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.background = paintBrushesDefault[i];
                style.hover.background = paintBrushesDefault[i];
                if (GUI.Button(new Rect(10 + i * 35, 110, 35, 35), "", style))
                {
                    paintBrush = paintBrushesDefault[i];
                    paintMaterial.SetTexture("_Brush", paintBrush);
                }
            }
            float logValue = Mathf.Log10(paintScale);
            logValue = GUI.HorizontalSlider(new Rect(10, 150, 120, 30), logValue, Mathf.Log10(0.001f), Mathf.Log10(1f));
            paintScale = Mathf.Pow(10f, logValue);
            GUI.Label(new Rect(130, 150, 50, 30), paintScale.ToString().Length > 7 ? paintScale.ToString().Substring(0, 6) : paintScale.ToString());
            paintOpacity = GUI.HorizontalSlider(new Rect(10, 180, 120, 30), paintOpacity, 0f, 1f);
            GUI.Label(new Rect(130, 180, 50, 30), paintOpacity.ToString().Length > 7 ? paintOpacity.ToString().Substring(0, 6) : paintOpacity.ToString());

            // history control
            if (!(history.Count > 0 && historyIndex > -1)) GUI.enabled = false;
            if (GUI.Button(new Rect(10, 210, 90, 30), "Undo"))
            {
                historyBack();
            }
            GUI.enabled = true;
            if (!(history.Count > 1 && historyIndex < history.Count - 1)) GUI.enabled = false;
            if (GUI.Button(new Rect(100, 210, 90, 30), "Redo"))
            {
                historyForward();
            }
            GUI.enabled = true;

            GUI.DragWindow();
        }

        void Update()
        {
            if (shortcut.Value.IsDown())
            {
                isUIActive = !isUIActive;
            }

            if (!paintModeEnabled) return;

            if (EatInputCollider.EatInputCollider.isHit && EatInputCollider.EatInputCollider.isEating)
            {
                if (EatInputCollider.EatInputCollider.hit.collider.gameObject.Equals(paintObject))
                {
                    paintCursor?.positionCursor(EatInputCollider.EatInputCollider.hit);

                    if (EatInputCollider.EatInputCollider.GetMouseButtonDown(0))
                    {
                        MakerTools.camCtrl.enabled = false;
                        Vector2 uv = calcUVCoordiante(EatInputCollider.EatInputCollider.hit);
                        //Logger.LogInfo($"Calculated: UV {uv.x}|{uv.y}");
                        startStroke(paintOpacity);
                    }
                    if (EatInputCollider.EatInputCollider.GetMouseButton(0))
                    {
                        Vector2 uv = calcUVCoordiante(EatInputCollider.EatInputCollider.hit);
                        //Logger.LogInfo($"Calculated: UV {uv.x}|{uv.y}");
                        stroke(uv, paintScale);
                        isDragging = true;
                    }
                    if (EatInputCollider.EatInputCollider.GetMouseButtonUp(0))
                    {
                        finishStroke();
                        MakerTools.camCtrl.enabled = true;
                        isDragging = false;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the UV coordinate on of the hit in UV set 1.
        /// The hit collider should be a MeshCollider.
        /// </summary>
        /// <param name="hit">Raycasthit for which the coordinates should be calculated</param>
        /// <returns>Value between (0,0) and (1,1) or (2,2) if no mesh could be retrieved</returns>
        private static Vector2 calcUVCoordiante(RaycastHit hit)
        {
            if (!(hit.collider is MeshCollider)) return Vector2.one * 2;
            Mesh mesh = ((MeshCollider)hit.collider)?.sharedMesh;
            if (mesh == null) return Vector2.one * 2;
            Vector2 uv1 = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 0]];
            Vector2 uv2 = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 1]];
            Vector2 uv3 = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 2]];
            Vector3 bary = hit.barycentricCoordinate; // might also be able to cause access violations.
            Vector2 uvResult = bary.x * uv1 + bary.y * uv2 + bary.z * uv3;
            return uvResult;
        }

        private static void paint(Vector2 pos, Vector2 prevPos, RenderTexture output)
        {
            // if distance between new pain position and last, recursively fill line
            //Logger.LogInfo((pos - prevPos).sqrMagnitude);
            float sqrD = (pos - prevPos).sqrMagnitude;
            if (sqrD > paintDensity*paintDensity && sqrD < 0.01f && isDragging)
            {
                Vector2 mid = Vector2.Lerp(prevPos, pos, 0.5f);
                paint(mid, prevPos, output);
                paint(pos, mid, output);
                //Logger.LogInfo($"Interpolating : {mid.x}|{mid.y}");
            }
            else
            {
                paint(pos, output);
            }
        }

        private static void paint(Vector2 pos, RenderTexture output)
        {
            prevPaintPos = pos;
            paintMaterial.SetFloat("_Scale", paintScale); // 55
            paintMaterial.SetVector("_Offset", pos); // 139
            Graphics.Blit(output, paintTexture2, paintMaterial, 0); // 140
            Graphics.Blit(paintTexture2, output); // 141
        }

        private static void startPaintMode()
        {
            List<Renderer> renderers = MakerTools.GetCurrentMaterialEditorRenderers();
            if (renderers.IsNullOrEmpty())
            {
                Logger.LogError("No rendereres found");
                Logger.LogMessage("Please select an Object in Material Editor and filter it so that only one Renderer is displayed");
                return;
            }
            if (renderers.Count > 1)
            {
                Logger.LogMessage("Please select an Object in Material Editor and filter it so that only one Renderer is displayed");
                return;
            }
            Renderer rend = renderers[0];
            if (!(rend is SkinnedMeshRenderer))
            {
                Logger.LogWarning("Not a SkinnedMeshRenderer");
                return;
            }

            paintObject = MakerTools.createStaticObjectCopy((SkinnedMeshRenderer)rend);
            EatInputCollider.EatInputCollider.RegisterCollider(paintObject.GetComponent<MeshCollider>());
            MakerTools.characterActive = false;
            paintCursor = new MakerTools_Cursor3D(Color.green);
            paintModeEnabled = true;
            // feed object with rendertexture
            Texture mainTexture = paintObject.GetComponent<MeshRenderer>()?.material?.mainTexture;
            if (mainTexture == null)
            {
                stopPaintMode();
                return;
            }

            // baseTexture for this paint process
            baseTex = new RenderTexture(mainTexture.width, mainTexture.height, 0);
            MakerTools.ClearRenderTexture(baseTex);
            Graphics.Blit(mainTexture, baseTex);

            // reset history
            history = new List<RenderTexture>();
            historyIndex = -1;

            // outputTexture for this paint process
            outputTex = new RenderTexture(mainTexture.width, mainTexture.height, 0);
            applyBaseAndHistoryToOutput();
            paintObject.GetComponent<MeshRenderer>().material.mainTexture = outputTex; // 62

        }

        private static void stopPaintMode()
        {
            // TODO: apply paintjob
            EatInputCollider.EatInputCollider.UnregisterCollider(paintObject.GetComponent<MeshCollider>());
            MakerTools.camCtrl.listCollider?.Remove(paintObject.GetComponent<MeshCollider>());
            DestroyImmediate(paintObject);
            MakerTools.characterActive = true;
            paintCursor.Destroy();
            paintCursor = null;
            paintModeEnabled = false;
        }

        private static void startStroke(float opacity)
        {
            currentStroke = new Stroke() { strokeTex = new RenderTexture(baseTex.width, baseTex.height, 0), opacity = opacity };
            MakerTools.ClearRenderTexture(currentStroke.strokeTex);

            paintTexture = new RenderTexture(baseTex.width, baseTex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default); // 52
            MakerTools.ClearRenderTexture(paintTexture);
            paintTexture2 = new RenderTexture(baseTex.width, baseTex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default); // 53
            MakerTools.ClearRenderTexture(paintTexture2);
            paintMaterial.mainTexture = paintTexture; // 58

            if (historyIndex != history.Count - 1)
            {
                while (history.Count > historyIndex + 1)
                {
                    history.RemoveAt(historyIndex + 1);
                }
            }
            strokeBaseTex = new RenderTexture(baseTex.width, baseTex.height, 0);
            BlitOntoTexture(baseTex, strokeBaseTex);
            foreach(RenderTexture historyTex in history)
            {
                BlitOntoTexture(historyTex, strokeBaseTex);
            }
            paintMaterial.SetFloat("_BrushAlpha", opacity);
        }

        private static void stroke(Vector2 pos, float scale)
        {
            paint(pos, prevPaintPos, currentStroke.strokeTex);
            MakerTools.ClearRenderTexture(outputTex); // clear output
            BlitOntoTexture(strokeBaseTex, outputTex); // add content
            BlitOntoTexture(currentStroke.strokeTex, outputTex); // add current stroke
        }

        private static void finishStroke()
        {
            history.Add(currentStroke.strokeTex);
            historyIndex++;
            if (historyIndex > historyMaxLength.Value) // pop oldest history entry if capacity reached
            {
                BlitOntoTexture(history[0], baseTex);
                history.RemoveAt(0);
            }
            applyBaseAndHistoryToOutput();
        }

        private static void applyBaseAndHistoryToOutput(int maxHistoryIndex = -2)
        {
            MakerTools.ClearRenderTexture(outputTex); // clear output
            Graphics.Blit(baseTex, outputTex); // add base
            if (maxHistoryIndex == -2)
            {
                foreach (RenderTexture historyTex in history) // add strokes from history
                {
                    BlitOntoTexture(historyTex, outputTex);
                }
            }
            else
            {
                for(int i = 0; i <= maxHistoryIndex; i++)
                {
                    BlitOntoTexture(history[i], outputTex);
                }
            }
        }

        private static void BlitOntoTexture(RenderTexture source, RenderTexture destination)
        {
            RenderTexture rtTemp = RenderTexture.GetTemporary(destination.width, destination.height, 0, destination.format);
            MakerTools.ClearRenderTexture(rtTemp);
            compositeMaterial.SetTexture("_Overlay", source);
            Graphics.Blit(destination, rtTemp);
            Graphics.Blit(rtTemp, destination, compositeMaterial);
            RenderTexture.ReleaseTemporary(rtTemp);
        }

        private void historyBack()
        {
            if (historyIndex == -1) return;
            historyIndex--;
            applyBaseAndHistoryToOutput(historyIndex);
        }

        private static void historyForward()
        {
            if (historyIndex == history.Count - 1) return;
            historyIndex++;
            applyBaseAndHistoryToOutput(historyIndex);
        }
    }

    internal class Stroke
    {
        public RenderTexture strokeTex { get; set; }
        public float opacity { get; set; }
    }
}
