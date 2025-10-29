using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MiscMods;

[HarmonyPatch]
public static class FoodOriginPatch
{
  [HarmonyPostfix, HarmonyPatch(typeof(Trait), nameof(Trait.OnSetCardGrid))]
  public static void Trait_OnSetCardGrid_Postfix(Trait __instance, ButtonGrid b)
  {
    if (__instance is not TraitFoodMeat)
    {
      return;
    }

    var t = __instance;
    if (!t.owner.c_idRefCard.IsEmpty())
    {
      // Copied from TraitFigure.cs
      SourceChara.Row row = EClass.sources.charas.map.TryGetValue(t.owner.c_idRefCard) ?? EClass.sources.charas.map["putty"];
      Transform transform = b.Attach<Transform>($"{ModInfo.Guid}/figure", rightAttach: false);
      int idSkin = ((EClass.core.config.game.antiSpider && row.skinAntiSpider != 0) ? row.skinAntiSpider : 0);
      var image = transform.GetChild(0).GetComponent<Image>();
      row.SetImage(image, null, 0, setNativeSize: false, 0, idSkin);

      image.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
    }
  }
}

[HarmonyPatch]
public static class PoolManagerPatch
{
  static MethodBase TargetMethod()
  {
    return typeof(PoolManager).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
      .First(m => m.Name == "_Spawn" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == typeof(string) && m.GetParameters()[2].ParameterType == typeof(Transform));
  }

  [HarmonyPrefix]
  public static void Prefix(ref string path)
  {
    MiscMods.Log("PoolManager Spawn: " + path);
    if (path.Contains(ModInfo.Guid))
    {
      MiscMods.Log("Redirecting figure path");
      path = path.Replace($"{ModInfo.Guid}/", "");
    }
  }
}
