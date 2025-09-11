using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace MiscMods;

[HarmonyPatch]
public static class MultipleBuildingPatch
{
  [HarmonyPostfix, HarmonyPatch(typeof(AM_Build), nameof(AM_Build.selectType), MethodType.Getter)]
  public static void AM_Build_selectType_Postfix(ref BaseTileSelector.SelectType __result)
  {
    if (Input.GetKey(KeyCode.LeftControl))
      __result = BaseTileSelector.SelectType.Multiple;
  }
}
