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

        // UI
        private bool isUIActive = false;
        private Rect windowRect = new Rect(100, 100, 200, 260);
        List<Texture2D> colorTextures = new List<Texture2D>();
        List<Color> colors = new List<Color>() { Color.white, Color.black, Color.red, Color.green, Color.blue, Color.yellow };

        // Paint
        private static bool paintModeEnabled;
        private static GameObject paintObject;
        private static GameObject realObject;
        private static MakerTools_Cursor3D paintCursor;
        public static Color paintColor = Color.white;

        void Awake()
        {
            shortcut = Config.Bind("", "", new KeyboardShortcut(KeyCode.P, KeyCode.LeftAlt), "Keyboard shortcut to toggle the Paint UI");
            foreach(Color c in colors)
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.SetPixels(Enumerable.Repeat(c, 4).ToArray());
                tex.Apply();
                colorTextures.Add(tex);
            }
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
                if (GUI.Button(new Rect(10+i*30, 70, 30,30), "", style))
                {
                    paintColor = colors[i];
                }
            }

            GUI.DragWindow();
        }

        void Update()
        {
            if (shortcut.Value.IsDown())
            {
                isUIActive = !isUIActive;
            }

            if (!paintModeEnabled) return;

            if (EatInputCollider.EatInputCollider.isHit)
            {
                if (EatInputCollider.EatInputCollider.hit.collider.gameObject.Equals(paintObject))
                {
                    paintCursor?.positionCursor(EatInputCollider.EatInputCollider.hit);

                    if (EatInputCollider.EatInputCollider.GetMouseButtonDown(0))
                    {
                        MakerTools.camCtrl.enabled = false;
                    }
                    if (EatInputCollider.EatInputCollider.GetMouseButton(0))
                    {
                        Vector2 uv = calcUVCoordiante(EatInputCollider.EatInputCollider.hit);
                        Logger.LogInfo($"Calculated: UV {uv.x}|{uv.y}");
                    }
                    if (EatInputCollider.EatInputCollider.GetMouseButtonUp(0))
                    {
                        MakerTools.camCtrl.enabled = true;
                    }
                }
            }
        }

        private void coolio()
        {
            if (Singleton<GameCursor>.IsInstance())
            {
                Singleton<GameCursor>.Instance.SetCursorLock(false);
            }
        }

        /// <summary>
        /// Calculates the UV coordinate on of the hit in UV set 1.
        /// The hit collider should be a MeshCollider.
        /// </summary>
        /// <param name="hit">Raycasthit for which the coordinates should be calculated</param>
        /// <returns>Value between (0,0) and (1,1) or (2,2) if no mesh could be retrieved</returns>
        private Vector2 calcUVCoordiante(RaycastHit hit)
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

        private void paintTexture(int x, int y, int radius, Color color, Texture2D texture)
        {
            foreach(Vector2 coord in CalculateCirclePixels(x, y, radius))
            {
                texture.SetPixel((int)coord.x, (int)coord.y, color);
            }
            texture.Apply();
        }

        // thank you ChatGPT
        public static List<Vector2> CalculateCirclePixels(int pX, int pY, int pRad)
        {
            int diameter = pRad * 2;
            int radiusSquared = pRad * pRad;

            // Create a list to store the coordinate pairs
            var coordinates = new List<Vector2>();

            for (int x = -pRad; x <= pRad; x++)
            {
                for (int y = -pRad; y <= pRad; y++)
                {
                    // Calculate the squared distance from the center
                    int distanceSquared = x * x + y * y;

                    // Check if the point is within the circle
                    if (distanceSquared <= radiusSquared)
                    {
                        // Calculate the actual coordinates
                        int coordX = pX + x;
                        int coordY = pY + y;

                        // Create a new Vector2 with the calculated coordinates
                        Vector2 coordinate = new Vector2(coordX, coordY);

                        // Add the coordinate pair to the list
                        coordinates.Add(coordinate);
                    }
                }
            }

            // Convert the list to an array and return it
            return coordinates;
        }

        private void startPaintMode()
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
        }

        private void stopPaintMode()
        {
            // TODO: apply paintjob
            EatInputCollider.EatInputCollider.UnregisterCollider(paintObject.GetComponent<MeshCollider>());
            DestroyImmediate(paintObject);
            MakerTools.characterActive = true;
            paintCursor.Destroy();
            paintCursor = null;
            paintModeEnabled = false;
        }
    }
}
