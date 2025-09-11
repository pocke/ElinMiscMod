using BepInEx;
using HarmonyLib;

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
  }

  public static void Log(object message)
  {
    Instance.Logger.LogInfo(message);
  }
}
