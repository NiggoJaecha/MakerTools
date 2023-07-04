using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using MakerTools;

namespace MakerTools
{
    class MakerTools_InputPatch
    {
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton))]
        //static bool GetMouseButtonPrefix(ref bool __result, ref int button)
        //{
        //    if (MakerTools.EatMouseInput)
        //    {
        //        __result = false;
        //        return true;
        //    }
        //    return false;
        //}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown))]
        static void GetMouseButtonDownPrefix(ref bool __result, ref int button)
        {
            if (MakerTools.EatMouseInput)
            {
                if (__result == true) MakerTools.SetMouseButtonDown(button);
                __result = false;
            }
        }

        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp))]
        //static bool GetMouseButtonUpPrefix(ref bool __result, ref int button)
        //{
        //    if (MakerTools.EatMouseInput)
        //    {
        //        __result = false;
        //        return false;
        //    }
        //    return true;
        //}

    }
}
