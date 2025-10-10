using System.ComponentModel;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiscMods;

[HarmonyPatch]
public static class ZoneNotifyPatch
{
  [HarmonyPostfix, HarmonyPatch(typeof(Msg), nameof(Msg.Say), new[] { typeof(string) })]
  public static void Zone_TryGenerateBigDaddy_Prefix(string idLang)
  {
    if (idLang == "sign_bigdaddy")
    {
      WidgetPopText.Say("ビッグダディ出現");
      WidgetFeed.Instance?.Nerun("野生のビッグダディだ!!");
    }
  }

  [HarmonyPostfix, HarmonyPatch(typeof(Zone), nameof(Zone.Activate))]
  public static void Zone_Activate_Postfix(Zone __instance)
  {
    if (!(__instance is Zone_Field) && !(__instance is Zone_Dungeon))
    {
      return;
    }
    if (__instance.IsPCFaction)
    {
      return;
    }

    var hasTreasure = __instance.map.things.Any(t => t.id == "chest3");
    if (hasTreasure)
    {
      WidgetFeed.Instance?.Nerun("お宝発見!");
    }

    var shrineCount = __instance.map.things.Count(t => t.id == "statue_power");
    if (shrineCount > 0)
    {
      WidgetPopText.Say($"祠*{shrineCount}");
    }

    var statueGods = __instance.map.things.Where(t => t.source._origin == "statue_god");
    foreach (var t in statueGods)
    {
      WidgetPopText.Say(t.Name);
      WidgetFeed.Instance?.Nerun($"{t.Name}が近くにいるよ!!");
    }
  }
}
