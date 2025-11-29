using Steamworks;

namespace MiscMods;

public static class RecordVersion
{
  private const string basePath = "//wsl.localhost/Ubuntu/home/pocke/ghq/github.com/pocke/elin-chara-viewer";

  public static void Run()
  {
    var versionString = version();
    var branch = isNightly() ? "nightly" : "EA";
    System.IO.File.WriteAllText($"{basePath}/versions/{branch}", versionString);
  }

  private static string version()
  {
    return EClass.core.version.GetText();
  }

  private static bool isNightly()
  {
    var pchName = "public";
    SteamApps.GetCurrentBetaName(out pchName, 128);
    return pchName == "nightly";
  }
}
