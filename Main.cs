using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MiscMods;

internal static class ModInfo
{
  internal const string Guid = "me.pocke.misc";
  internal const string Name = "Misc mods";
  internal const string Version = "1.0.0";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal class MiscMods : BaseUnityPlugin
{
  internal static MiscMods Instance { get; private set; }

  public void Awake()
  {
    Instance = this;

    var harmony = new Harmony(ModInfo.Guid);
    harmony.PatchAll();

    RecordVersion.Run();
  }

  private void Update()
  {
    if (!EClass.core.IsGameStarted)
    {
      return;
    }

    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.M))
    {
      BuildMusium.Build();
    }

    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.L))
    {
      var i = 0;
      while (true)
      {
        Rand.SetSeed(EClass.game.seed + EClass.player.stats.days + i);
        if (EClass.rnd(5) == 0)
        {
          Log($"Day + {i}: Lucky day!");
          break;
        }
        i++;
      }
    }
  }

  public static void Log(object message)
  {
    Instance.Logger.LogInfo(message);
  }
}
