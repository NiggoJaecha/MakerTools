using BepInEx;
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
using HarmonyLib;

namespace MakerTools
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class MakerTools : BaseUnityPlugin
    {
        public const string PluginName = "MakerTools";
        public const string GUID = "org.njaecha.plugins.makertools";
        public const string Version = "0.0.1";

        public static MakerTools Instance;

        internal new static ManualLogSource Logger;

        public static CameraControl_Ver2 camCtrl;
        private static Vector3 oldCamPosition;
        private static Quaternion oldCamRotation;

        public event EventHandler<CameraMovedEventArgs> CameraMovedEvent;

        private static GameObject characterObject;
        public static bool characterActive { get => characterObject.activeInHierarchy; set => characterObject.SetActive(value); }

        public static bool EatMouseInput = false;
        internal static bool isMouseButtonDown0 = false;
        internal static bool isMouseButtonDown1 = false;

        void Awake()
        {
            MakerTools.Logger = base.Logger;
            MakerTools.Instance = this;

            MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(MakerTools_InputPatch));
        }

        void Update()
        {
            if (!KKAPI.Maker.MakerAPI.InsideAndLoaded) return;

            // invoke CameraMovedEvent if camera moved
            if (camCtrl.transform.position != oldCamPosition || camCtrl.transform.rotation != oldCamRotation)
            {
                CameraMovedEventArgs args = new CameraMovedEventArgs
                {
                    newPosition = camCtrl.transform.position,
                    newRotation = camCtrl.transform.rotation,
                    oldPosition = oldCamPosition,
                    oldRotation = oldCamRotation
                };
                if (!CameraMovedEvent.IsNullOrEmpty())
                {
                    CameraMovedEvent.Invoke(this, args);
                }
                oldCamPosition = camCtrl.transform.position;
                oldCamRotation = camCtrl.transform.rotation;
            }
        }

        void LateUpdate()
        {
            isMouseButtonDown0 = false;
            isMouseButtonDown1 = false;
        }

        public static bool GetMouseButtonDown(int button)
        {
            switch (button){
                case 0:
                    return isMouseButtonDown0;
                case 1:
                    return isMouseButtonDown1;
                default:
                    return false;
            }
        }

        internal static void SetMouseButtonDown(int button)
        {
            Logger.LogInfo($"Set MouseButtonDown: {button}");
            switch (button)
            {
                case 0:
                    isMouseButtonDown0 = true;
                    break;
                case 1:
                    isMouseButtonDown1 = true;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Retrieves the GameObject the MaterialEditor UI is currently pointing to.
        /// </summary>
        /// <returns></returns>
        public static GameObject GetCurrentMaterialEditorObject()
        {
            GameObject obj = Singleton<MaterialEditorUI>.Instance.CurrentGameObject;
            return obj;
        }

        public static List<Renderer> GetCurrentMaterialEditorRenderers()
        {
            string filter = Singleton<MaterialEditorUI>.Instance.CurrentFilter;

            List<string> filterList = new List<string>();
            if (!filter.IsNullOrEmpty())
                filterList = filter.Split(',').ToList();
            filterList.RemoveAll(x => x.IsNullOrWhiteSpace());

            GameObject obj = GetCurrentMaterialEditorObject();
            if (obj == null)
            {
                Logger.LogError("No Object selected in Material Editor");
                return null;
            }
            IEnumerable<Renderer> rendListFull = MaterialAPI.GetRendererList(obj);

            List<Renderer> rendList = new List<Renderer>();
            if (filterList.Count == 0)
                rendList = rendListFull.ToList();
            else
                foreach (var rend in rendListFull)
                {
                    for (var j = 0; j < filterList.Count; j++)
                    {
                        var filterWord = filterList[j];
                        if ((rend == null ? "" : rend.name.Replace("(Instance)", "").Replace(" Instance", "").Trim()).ToLower().Contains(filterWord.Trim().ToLower()) && !rendList.Contains(rend))
                            rendList.Add(rend);
                    }
                }
            return rendList;
        }

        private static void RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
        {
            GameObject cameraObject = GameObject.Find("CustomScene/CamBase/Camera");
            if (cameraObject == null) return;
            camCtrl = cameraObject.GetComponent<CameraControl_Ver2>();
            oldCamPosition = camCtrl.transform.position;
            oldCamRotation = camCtrl.transform.rotation;

            characterObject = GameObject.Find("chaF_001");
        }

        /// <summary>
        /// Returns a unskinned copy of the passed SkinnedMeshRenderer in the same worldposition
        /// </summary>
        /// <param name="meshRenderer">SkinnedMeshRenderer on the object you want to copy</param>
        /// <param name="onVisibleLayer">If true will set copy to layer 10</param>
        /// <returns></returns>
        public static GameObject createStaticObjectCopy(SkinnedMeshRenderer meshRenderer, bool onVisibleLayer = true)
        {
            GameObject copy = new GameObject();
            MeshRenderer meshRend = copy.AddComponent<MeshRenderer>();
            MeshFilter meshFilt = copy.AddComponent<MeshFilter>();
            MeshCollider meshColl = copy.AddComponent<MeshCollider>();
            if (onVisibleLayer) copy.layer = 10;

            Mesh bakedMesh = new Mesh();

            meshRenderer.BakeMesh(bakedMesh);
            // modify mesh with the scaleing fix

            Vector3 scale = meshRenderer.transform.lossyScale;
            Matrix4x4 inverseScale = Matrix4x4.Scale(scale).inverse;

            Vector3[] newVerts = new Vector3[bakedMesh.vertexCount];
            for (int i = 0; i < bakedMesh.vertexCount; i++)
            {
                newVerts[i] = inverseScale.MultiplyPoint(bakedMesh.vertices[i]);
            }
            bakedMesh.vertices = newVerts;

            meshFilt.mesh = bakedMesh;
            meshColl.sharedMesh = bakedMesh;

            copy.transform.position = meshRenderer.transform.position;
            copy.transform.localScale = meshRenderer.transform.lossyScale;
            copy.transform.rotation = meshRenderer.transform.rotation;
            copy.transform.localRotation = meshRenderer.transform.localRotation;

            meshRend.material = Instantiate(meshRenderer.material);

            return copy;
        }
    }

    public class CameraMovedEventArgs : EventArgs
    {
        public Vector3 newPosition { get; set; }
        public Quaternion newRotation { get; set; }
        public Vector3 oldPosition { get; set; }
        public Quaternion oldRotation { get; set; }
    }

}
