using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace EatInputCollider
{
    class EatInputCollider_InputPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton))]
        static void GetMouseButtonPostfix(ref bool __result, ref int button)
        {
            if (EatInputCollider.eatInput)
            {
                if (__result == true) EatInputCollider.SetMouseButton(button, EatInputCollider.MouseState.Pressed);
                __result = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown))]
        static void GetMouseButtonDownPostfix(ref bool __result, ref int button)
        {
            if (EatInputCollider.eatInput)
            {
                if (__result == true) EatInputCollider.SetMouseButton(button, EatInputCollider.MouseState.Down);
                __result = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp))]
        static void GetMouseButtonUpPostfix(ref bool __result, ref int button)
        {
            if (EatInputCollider.eatInput)
            {
                if (__result == true) EatInputCollider.SetMouseButton(button, EatInputCollider.MouseState.Up);
                __result = false;
            }
        }

    }
}
