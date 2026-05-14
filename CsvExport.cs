using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

namespace MiscMods;

[HarmonyPatch(typeof(SourceManager), nameof(SourceManager.Init))]
internal static class CsvExportPatch
{
  private static bool exported;

  private static void Prefix(SourceManager __instance)
  {
    if (exported || __instance.initialized)
    {
      return;
    }
    exported = true;
    try
    {
      CsvExport.Run(__instance);
    }
    catch (Exception e)
    {
      MiscMods.Log("CSV export failed: " + e);
    }
  }
}

internal static class CsvExport
{
  private const string BasePath = @"\\wsl.localhost\Ubuntu\home\pocke\ghq\github.com\pocke\elin-chara-viewer\db";

  public static void Run(SourceManager sources)
  {
    var version = EClass.core.version.GetText();
    var dir = Path.Combine(BasePath, version);
    Directory.CreateDirectory(dir);

    var elementAliasById = BuildElementAliasMap(sources);

    MiscMods.Log("CSV export starts at " + dir);
    var count = 0;
    foreach (var (fieldName, sourceData) in EnumerateSources(sources))
    {
      if (TryExport(dir, fieldName, sourceData, elementAliasById))
      {
        count++;
      }
    }
    MiscMods.Log($"CSV export finished: {count} files");
  }

  // ParseElements / GetElement は alias→id 方向の変換しかゲーム本体に無く、
  // ScriptableObject の Row.* には変換後の int / int[] しか保存されていない。
  // CSV をエクセル原形に近づけるため id → alias の逆引きマップを用意する。
  // sources.elements.rows は ScriptableObject にシリアライズ済みなので Init() 前でも参照できる。
  private static Dictionary<int, string> BuildElementAliasMap(SourceManager sources)
  {
    var map = new Dictionary<int, string>();
    foreach (var row in sources.elements.rows)
    {
      if (!string.IsNullOrEmpty(row.alias))
      {
        map[row.id] = row.alias;
      }
    }
    return map;
  }

  private static IEnumerable<(string, SourceData)> EnumerateSources(SourceManager sources)
  {
    foreach (var field in typeof(SourceManager).GetFields(BindingFlags.Instance | BindingFlags.Public))
    {
      if (!typeof(SourceData).IsAssignableFrom(field.FieldType))
      {
        continue;
      }
      var data = field.GetValue(sources) as SourceData;
      if (data == null)
      {
        continue;
      }
      yield return (field.Name, data);
    }
  }

  private static bool TryExport(string dir, string fieldName, SourceData sourceData, Dictionary<int, string> elementAliasById)
  {
    var rowsField = sourceData.GetType().GetField("rows", BindingFlags.Instance | BindingFlags.Public);
    if (rowsField == null)
    {
      return false;
    }
    var rows = rowsField.GetValue(sourceData) as IList;
    if (rows == null || rows.Count == 0)
    {
      return false;
    }

    var rowType = rowsField.FieldType.GetGenericArguments().FirstOrDefault();
    if (rowType == null)
    {
      return false;
    }

    var createRow = AccessTools.Method(sourceData.GetType(), "CreateRow");
    var columns = CollectColumnOrder(createRow, rowType);
    if (columns.Count == 0)
    {
      return false;
    }
    var elementArrayFields = DetectCoreCallFields(createRow, nameof(Core.ParseElements));
    var elementIdFields = DetectCoreCallFields(createRow, nameof(Core.GetElement));

    var path = Path.Combine(dir, fieldName + ".csv");
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    writer.WriteLine(string.Join(",", columns.Select(c => Escape(c.Field?.Name ?? "***"))));
    foreach (var row in rows)
    {
      writer.WriteLine(string.Join(",", columns.Select(c => Escape(FormatCell(c.Field, row, elementArrayFields, elementIdFields, elementAliasById)))));
    }
    return true;
  }

  private static string FormatCell(FieldInfo field, object row, HashSet<string> elementArrayFields, HashSet<string> elementIdFields, Dictionary<int, string> elementAliasById)
  {
    if (field == null)
    {
      return "";
    }
    var value = field.GetValue(row);
    if (value is int[] intArray && elementArrayFields.Contains(field.Name))
    {
      return FormatElementArray(intArray, elementAliasById);
    }
    if (value is int id && elementIdFields.Contains(field.Name))
    {
      return elementAliasById.TryGetValue(id, out var alias) ? alias : id.ToString();
    }
    return FormatValue(value);
  }

  // int[] {id, power, id, power, ...} を "alias/power,alias/power,..." 形式に戻す。
  // ParseElements の inverse 対応(power == 1 のときは "/1" を省略する流儀も合わせる)。
  private static string FormatElementArray(int[] arr, Dictionary<int, string> elementAliasById)
  {
    if (arr == null || arr.Length == 0)
    {
      return "";
    }
    var sb = new StringBuilder();
    for (var i = 0; i < arr.Length; i += 2)
    {
      if (i > 0)
      {
        sb.Append(',');
      }
      var id = arr[i];
      var power = i + 1 < arr.Length ? arr[i + 1] : 1;
      sb.Append(elementAliasById.TryGetValue(id, out var alias) ? alias : id.ToString());
      if (power != 1)
      {
        sb.Append('/');
        sb.Append(power);
      }
    }
    return sb.ToString();
  }

  private static List<(int Column, FieldInfo Field)> CollectColumnOrder(MethodBase createRow, Type rowType)
  {
    // CreateRow() の IL を走査:
    //  - Ldc_I4* を ldc バッファに貯める
    //  - SourceData.Get* の call で「最初に push された値」を引数の第1引数 = 列番号として pendingColumn にする
    //    (例: GetStr(int id, bool useDefault=false) は内部で Ldc_I4_0 が混入するため
    //     単純な「直前の Ldc_I4」だと useDefault に上書きされてしまう)
    //  - Stfld でその列に field を割り当てる
    if (createRow == null)
    {
      return new List<(int Column, FieldInfo Field)>();
    }

    var byColumn = new SortedDictionary<int, FieldInfo>();
    var ldc = new List<int>();
    int? pendingColumn = null;
    foreach (var ins in PatchProcessor.GetOriginalInstructions(createRow))
    {
      var constant = TryGetIntConstant(ins);
      if (constant.HasValue)
      {
        ldc.Add(constant.Value);
        continue;
      }
      if (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
      {
        if (ins.operand is MethodInfo m
            && m.DeclaringType == typeof(SourceData)
            && m.Name.StartsWith("Get")
            && ldc.Count > 0)
        {
          pendingColumn = ldc[0];
        }
        // Get* 以外の call でも ldc は引数として消費されたとみなしてクリア
        // (例: field = Helper.X() + GetStr(2) のようなケースで Helper.X の前に積んだ
        // 定数が残って次の Get* の引数解析を狂わせるのを防ぐ)
        ldc.Clear();
        continue;
      }
      if (ins.opcode != OpCodes.Stfld || ins.operand is not FieldInfo fi)
      {
        continue;
      }
      ldc.Clear();
      if (!pendingColumn.HasValue || fi.IsNotSerialized || !IsExportableType(fi.FieldType))
      {
        pendingColumn = null;
        continue;
      }
      if (fi.DeclaringType == null || !fi.DeclaringType.IsAssignableFrom(rowType))
      {
        pendingColumn = null;
        continue;
      }
      // 同じ列に複数回書き込まれる場合は最後の代入を採用(SourceChara の components 13/46 など)
      byColumn[pendingColumn.Value] = fi;
      pendingColumn = null;
    }

    if (byColumn.Count == 0)
    {
      return new List<(int Column, FieldInfo Field)>();
    }

    var result = new List<(int Column, FieldInfo Field)>();
    var max = byColumn.Keys.Last();
    for (var i = 0; i <= max; i++)
    {
      result.Add((i, byColumn.TryGetValue(i, out var fi) ? fi : null));
    }
    return result;
  }

  // CreateRow() 内で Core.<methodName>(...) の戻り値が直接 Stfld されている field を検出する。
  // (Core.ParseElements / Core.GetElement の両方に同一ロジックで対応するためメソッド名で引数化)
  private static HashSet<string> DetectCoreCallFields(MethodBase createRow, string methodName)
  {
    var result = new HashSet<string>();
    if (createRow == null)
    {
      return result;
    }
    var target = AccessTools.Method(typeof(Core), methodName);
    if (target == null)
    {
      return result;
    }
    var instructions = PatchProcessor.GetOriginalInstructions(createRow).ToList();
    for (var i = 0; i < instructions.Count; i++)
    {
      var ins = instructions[i];
      if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt)
      {
        continue;
      }
      if ((ins.operand as MethodInfo) != target)
      {
        continue;
      }
      // 呼び出しから先頭に向かう最初の Stfld を field として記録。
      // 間に算術演算等が挟まると意図しない field 名を拾ってしまうため、
      // 「次の命令が Stfld」のときだけマーク(= 直接代入のみ対応)。
      if (i + 1 < instructions.Count
          && instructions[i + 1].opcode == OpCodes.Stfld
          && instructions[i + 1].operand is FieldInfo fi)
      {
        result.Add(fi.Name);
      }
    }
    return result;
  }

  private static int? TryGetIntConstant(CodeInstruction ins)
  {
    var op = ins.opcode;
    if (op == OpCodes.Ldc_I4_M1) return -1;
    if (op == OpCodes.Ldc_I4_0) return 0;
    if (op == OpCodes.Ldc_I4_1) return 1;
    if (op == OpCodes.Ldc_I4_2) return 2;
    if (op == OpCodes.Ldc_I4_3) return 3;
    if (op == OpCodes.Ldc_I4_4) return 4;
    if (op == OpCodes.Ldc_I4_5) return 5;
    if (op == OpCodes.Ldc_I4_6) return 6;
    if (op == OpCodes.Ldc_I4_7) return 7;
    if (op == OpCodes.Ldc_I4_8) return 8;
    if (op == OpCodes.Ldc_I4_S) return Convert.ToInt32(ins.operand);
    if (op == OpCodes.Ldc_I4) return Convert.ToInt32(ins.operand);
    return null;
  }

  private static bool IsExportableType(Type t)
  {
    if (t.IsEnum)
    {
      return true;
    }
    if (t == typeof(string) || t == typeof(int) || t == typeof(long) || t == typeof(bool)
        || t == typeof(float) || t == typeof(double) || t == typeof(byte))
    {
      return true;
    }
    if (t.IsArray)
    {
      var et = t.GetElementType();
      return et.IsEnum
             || et == typeof(string) || et == typeof(int) || et == typeof(float) || et == typeof(double)
             || et == typeof(bool) || et == typeof(long) || et == typeof(byte);
    }
    return false;
  }

  private static string FormatValue(object value)
  {
    if (value == null)
    {
      return "";
    }
    if (value is string s)
    {
      return s;
    }
    if (value is Array array)
    {
      var sb = new StringBuilder();
      for (var i = 0; i < array.Length; i++)
      {
        if (i > 0)
        {
          sb.Append(',');
        }
        sb.Append(array.GetValue(i));
      }
      return sb.ToString();
    }
    if (value is bool b)
    {
      return b ? "1" : "0";
    }
    return value.ToString();
  }

  private static string Escape(string s)
  {
    if (string.IsNullOrEmpty(s))
    {
      return "";
    }
    if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
    {
      return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
    return s;
  }
}
