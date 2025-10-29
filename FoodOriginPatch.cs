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
    if (!(__instance is TraitFoodMeat || __instance is TraitFoodEgg || __instance is TraitDrinkMilkMother))
    {
      return;
    }

    var t = __instance;
    if (!t.owner.c_idRefCard.IsEmpty())
    {
      // ----------------------
      // Copied from TraitFigure.cs
      SourceChara.Row row = EClass.sources.charas.map.TryGetValue(t.owner.c_idRefCard) ?? EClass.sources.charas.map["putty"];
      Transform transform = b.Attach<Transform>($"{ModInfo.Guid}/figure", rightAttach: false);
      int idSkin = ((EClass.core.config.game.antiSpider && row.skinAntiSpider != 0) ? row.skinAntiSpider : 0);
      var image = transform.GetChild(0).GetComponent<Image>();
      row.SetImage(image, null, 0, setNativeSize: false, 0, idSkin);
      // ----------------------

      image.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
    }
  }

  [HarmonyPrefix, HarmonyPatch(typeof(PoolManager), "_Spawn", new[] { typeof(string), typeof(string), typeof(Transform) })]
  public static void Prefix(ref string path)
  {
    if (path.Contains(ModInfo.Guid))
    {
      path = path.Replace($"{ModInfo.Guid}/", "");
    }
  }
}
