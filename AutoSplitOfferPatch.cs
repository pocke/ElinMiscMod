using HarmonyLib;
using UnityEngine;

namespace MiscMods;

[HarmonyPatch]
public static class AutoSplitOfferPatch
{
  public static bool IsExecuting = false;

  [HarmonyPrefix, HarmonyPatch(typeof(TraitAltar), nameof(TraitAltar._OnOffer))]
  public static bool TraitAltar__OnOffer_Prefix(Chara c, Thing t, TraitAltar __instance)
  {
    var altar = __instance;

    if (IsExecuting)
      return true;

    var valuePerUnit = altar.Deity.GetOfferingValue(t, 1);
    var maxValue = 1500;
    var batchSize = maxValue / valuePerUnit;
    var remainder = t.Num % batchSize;
    MiscMods.Log($"Offering {t} to {altar.Deity} in batches of {batchSize} (value per unit: {valuePerUnit}, remainder: {remainder})");
    var elmPiety = c.elements.GetOrCreateElement(85);
    var elmFeith = c.elements.GetOrCreateElement(306);
    MiscMods.Log($"Piety.UseExpMod: {elmPiety.UseExpMod}, Feith.UseExpMod: {elmFeith.UseExpMod}");
    MiscMods.Log($"Piety.UsePotential: {elmPiety.UsePotential}, Feith.UsePotential: {elmFeith.UsePotential}");
    MiscMods.Log($"Piety.Potential: {elmPiety.Potential}, Feith.Potential: {elmFeith.Potential}");
    MiscMods.Log($"Piety.ValueWithoutLink: {elmPiety.ValueWithoutLink}, Feith.ValueWithoutLink: {elmFeith.ValueWithoutLink}");
    var a = 1500 * (float)Mathf.Clamp(elmFeith.UsePotential ? elmFeith.Potential : 100, 10, 1000) / (float)(100 + Mathf.Max(0, elmFeith.ValueWithoutLink) * 25);
    MiscMods.Log($"Adjusted max value per offering: {a}");


    try
    {
      IsExecuting = true;

      for (int i = 0; i < t.Num / batchSize; i++)
      {
        var batch = t.Duplicate(batchSize);
        altar._OnOffer(c, batch);
      }
      if (remainder > 0)
      {
        var batch = t.Duplicate(remainder);
        altar._OnOffer(c, batch);
      }
    }
    finally
    {
      IsExecuting = false;
    }

    return false;
  }
}
