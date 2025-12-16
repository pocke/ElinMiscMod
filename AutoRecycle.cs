using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiscMods;

public static class AutoRecycle
{
  public static void RecycleAll(List<Thing> things, Thing trash = null)
  {
    if (trash == null)
    {
      trash = FindTrash();
    }

    if (trash == null)
    {
      Msg.Say("No trash found in inventory");
      SE.Beep();
      return;
    }
    var r = new InvOwnerRecycle(trash)
    {
      recycle = trash.trait as TraitRecycle,
    };

    foreach (var t in things.ToList())
    {
      r._OnProcess(t);
    }
  }

  private static Thing FindTrash()
  {
    var backpack = LayerInventory.listInv.FirstOrDefault(layer => layer.mainInv);
    return backpack.Inv.Container.things.Find<TraitRecycle>();
  }
}

[HarmonyPatch]
public static class AutoRecyclePatch
{
  [HarmonyPostfix, HarmonyPatch(typeof(TraitContainer), nameof(TraitContainer.TrySetAct))]
  public static void TraitContainer_TrySetAct_Postfix(ActPlan p, TraitContainer __instance)
  {
    if (p.input != ActInput.AllAction)
      return;

    var self = __instance;
    if (self.owner.c_lockLv > 0)
      return;

    p.TrySetAct("Recycle_all", delegate
    {
      AutoRecycle.RecycleAll(self.owner.things);
      return false;
    }, self.owner, CursorSystem.Container, 1);
  }

  [HarmonyPostfix, HarmonyPatch(typeof(Trait), nameof(Trait.TrySetAct))]
  public static void Trait_TrySetAct_Postfix(ActPlan p, Trait __instance)
  {
    RecycleAllOnFloor(p, __instance);
  }

  public static void RecycleAllOnFloor(ActPlan p, Trait t)
  {
    if (p.input != ActInput.AllAction)
      return;

    if (t is not TraitRecycle)
      return;

    p.TrySetAct("Recycle_all_on_floor", delegate
    {
      var things = new List<Thing>();
      Point.map.ForeachPoint(point =>
      {
        List<Card> list = point.ListCards();
        list.Reverse();
        foreach (Card item in list)
        {
          if (!item.isThing || !item.trait.CanPutAway || item.IsInstalled || item.IsPCParty)
          {
            continue;
          }
          things.Add(item.Thing);
        }
      });

      AutoRecycle.RecycleAll(things, t.owner.Thing);
      return false;
    }, t.owner, CursorSystem.Container, 1);
  }
}
