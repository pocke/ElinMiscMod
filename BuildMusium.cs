using System.Linq;

namespace MiscMods;

public static class BuildMusium
{
  // 50 for figure2, 20 for figure
  public static int Width = 20;

  public static void Build()
  {
    var figures = EClass.pc.things.List(IsFigure, onlyAccessible: true);
    foreach (var thing in figures)
    {
      SourceChara.Row row;
      if (!EClass.sources.charas.map.TryGetValue(thing.c_idRefCard, out row))
        continue;

      var card = row.model;
      if (!card.isChara)
        continue;
      var chara = card.Chara;
      if (chara == null || chara.IsMultisize)
        continue;

      var pos = PointFor(chara.source._id);
      var current = pos.Things.FirstOrDefault();
      if (pos.Things.Count > 1)
        continue;
      if (current != null && (!IsFigure(current) || thing.GetValue() <= current.GetValue()))
        continue;

      if (pos.Things.Count == 1)
        EClass.pc.Pick(current);

      var t = thing.Split(1);

      Zone.ignoreSpawnAnime = true;
      EClass._zone.AddCard(t, pos);
      t.Install();
    }
  }

  private static Point PointFor(int index)
  {
    return new Point(index / Width + 1, index % Width + 1);
  }

  private static bool IsFigure(Thing t)
  {
    return t.id == "figure" || t.id == "figure2";
  }
}
