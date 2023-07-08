using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using BepInEx;

namespace EatInputCollider
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class EatInputCollider : BaseUnityPlugin
    {
        public const string PluginName = "EatInputCollider";
        public const string GUID = "org.njaecha.plugins.eatinputcollider";
        public const string Version = "0.1.1";

        internal static List<Collider> colliders = new List<Collider>();
        public static RaycastHit hit { get; private set; }
        public static bool isHit { get; private set; }

        public static bool isEating { get => eatInput; }

		private static Dictionary<int, Dictionary<MouseState, bool>> _bools = new Dictionary<int, Dictionary<MouseState, bool>>();
        internal static bool eatInput;

		internal enum MouseState
		{
			Up,
			Down,
			Pressed
		}

        void Awake()
        {

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(EatInputCollider_InputPatch));
        }

        void Update()
        {
            if (Camera.main == null || colliders.Count < 1) return;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity) 
                && colliders.Contains(hit.collider))
            {
                if (!isMouseUsed()) eatInput = true;
                isHit = true;
                EatInputCollider.hit = hit;
            }
            else
            {
                eatInput = false;
                isHit = false;
            }
        }

        private bool isMouseUsed()
        {
            return Input.GetMouseButton(0)
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonUp(0)
                || Input.GetMouseButton(1)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonUp(1)
                || Input.GetMouseButton(2)
                || Input.GetMouseButtonDown(2)
                || Input.GetMouseButtonUp(2);
        }

        void LateUpdate()
		{
			foreach(int button in _bools.Keys)
			{
                _bools[button][MouseState.Pressed] = false;
                _bools[button][MouseState.Down] = false;
                _bools[button][MouseState.Up] = false;
            }
		}

        public static void RegisterCollider(Collider collider)
        {
            colliders.Add(collider);
        }

        public static void UnregisterCollider(Collider collider)
        {
            colliders.Remove(collider);
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (_bools.ContainsKey(button)) return _bools[button][MouseState.Down];
            return false;
        }
        public static bool GetMouseButtonUp(int button)
        {
            if (_bools.ContainsKey(button)) return _bools[button][MouseState.Up];
            return false;
        }
        public static bool GetMouseButton(int button)
        {
            if (_bools.ContainsKey(button)) return _bools[button][MouseState.Pressed];
            return false;
        }

        internal static void SetMouseButton(int button, MouseState state)
		{
			if (!_bools.ContainsKey(button))
			{
				_bools[button] = new Dictionary<MouseState, bool>();
				_bools[button][MouseState.Pressed] = false;
                _bools[button][MouseState.Down] = false;
                _bools[button][MouseState.Up] = false;
            }
			_bools[button][state] = true;
		}

	}
}

