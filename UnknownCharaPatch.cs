using HarmonyLib;
using UnityEngine;

namespace MiscMods;

[HarmonyPatch]
public static class UnknownCharaPatch
{

  [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.GetHoverText))]
  public static void Chara_GetHoverText_Postfix(Chara __instance, ref string __result)
  {
    var codex = EClass.player.codex;
    if (!codex.DroppedCard(__instance.id))
    {
      __result = __result + " ♠";
    }
  }
}
