using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public class MappedMethodData(byte[] hash, byte[] opCodes)
{
  public byte[] hash = hash, opCodes = opCodes;
}

public class AssemblyMap
{
  /// <summary>
  /// 'asm-maps' path
  /// </summary>
  public static string targetPath = null;
  /// <summary>
  /// All loaded assembly maps
  /// </summary>
  public static Dictionary<string, AssemblyMap> loadedMaps = [];
  /// <summary>
  /// Versions for which assembly maps can be retrieved
  /// </summary>
  public static HashSet<string> managedVersions = null;

  static AssemblyMap()
  {
    try
    {
      targetPath = Path.Combine(Directory.GetParent(typeof(Patcher).Assembly.Location).Parent.FullName, "asm-maps");
      if (!Directory.Exists(targetPath))
      {
        LogError("Couldn't find \"gelbi-silly-lib\\asm-maps\"");
        return;
      }
      managedVersions = [];
      foreach (string path in Directory.GetDirectories(targetPath))
        managedVersions.Add(Path.GetFileName(path));
      TryGenerateMaps();
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  /// <summary>
  /// Creates assembly maps for current version if they don't already exist
  /// </summary>
  static void TryGenerateMaps()
  {
    string versionedPath = Path.Combine(targetPath, GSLPUtils.gameVersion);
    Directory.CreateDirectory(versionedPath);
    if (Directory.GetFiles(versionedPath).Length != 0)
      return;
    LogInfo($"Generating assembly maps for {GSLPUtils.gameVersion}");
    WriterThread defMapWriter = new(Path.Combine(versionedPath, "def-map.txt"), false);
    List<byte> hashMap = [], opCodeMap = [];
    MapAssembly("RainWorld_Data\\Managed\\Assembly-CSharp-firstpass.dll", defMapWriter, hashMap, opCodeMap);
    MapAssembly("RainWorld_Data\\Managed\\Assembly-CSharp.dll", defMapWriter, hashMap, opCodeMap);
    defMapWriter.Close();
    File.WriteAllBytes(Path.Combine(versionedPath, "hash-map.txt"), [.. hashMap]);
    File.WriteAllBytes(Path.Combine(versionedPath, "opcode-map.txt"), [.. opCodeMap]);
    LogInfo($"Generated assembly maps for {GSLPUtils.gameVersion}");
  }

  static void MapAssembly(string asmPath, WriterThread defMapWriter, List<byte> hashMap, List<byte> opCodeMap)
  {
    List<byte> codes = [];
    foreach (ModuleDefinition module in AssemblyDefinition.ReadAssembly(asmPath).Modules)
    {
      module.ForEachType(type =>
      {
        foreach (MethodDefinition method in type.Methods)
          if (method.Body != null)
          {
            defMapWriter.WriteLine(method.GetSimpleName());
            codes.Clear();
            foreach (Instruction i in method.Body.Instructions)
            {
              codes.Add((byte)i.OpCode.Code);
              opCodeMap.Add((byte)i.OpCode.Code);
            }
            opCodeMap.Add(255);
            hashMap.AddRange(SHA256.Create().ComputeHash([.. codes]));
          }
      });
    }
  }

  /// <summary>
  /// Returns assembly map for given version
  /// </summary>
  public static AssemblyMap GetMap(string version)
  {
    if (loadedMaps.TryGetValue(version, out AssemblyMap map))
      return map;
    if (managedVersions.Contains(version))
      return new(version);
    return null;
  }

  /// <summary>
  /// Generates diff for 2 versions
  /// </summary>
  public static void Diff(string v1, string v2, int checksumThreshold, float sizeThreshold)
  {
    try
    {
      AssemblyMap map1 = GetMap(v1), map2 = GetMap(v2);
      WriterThread writer = new(Path.Combine(targetPath, $"diff-{v1}-{v2}.txt"), false);
      foreach (KeyValuePair<string, MappedMethodData> methodData1 in map1.methods)
        if (!map2.methods.ContainsKey(methodData1.Key))
          writer.WriteLine("- " + methodData1.Key);
      foreach (KeyValuePair<string, MappedMethodData> methodData2 in map2.methods)
        if (!map1.methods.TryGetValue(methodData2.Key, out MappedMethodData methodData1))
          writer.WriteLine("+ " + methodData2.Key);
      foreach (KeyValuePair<string, MappedMethodData> methodData2 in map2.methods)
        if (map1.methods.TryGetValue(methodData2.Key, out MappedMethodData methodData1) && !methodData1.hash.SequenceEqual(methodData2.Value.hash))
          if (methodData1.opCodes.Length == methodData2.Value.opCodes.Length)
          {
            int checksum = 0;
            for (int i = 0; i < methodData1.opCodes.Length; ++i)
              checksum += methodData1.opCodes[i] - methodData2.Value.opCodes[i];
            if (Math.Abs(checksum) > checksumThreshold)
              writer.WriteLine("* " + methodData2.Key);
          }
          else if (Math.Abs((float)methodData2.Value.opCodes.Length / methodData1.opCodes.Length - 1f) > sizeThreshold)
            writer.WriteLine("* " + methodData2.Key);
      writer.Close();
    }
    catch (Exception e)
    {
      LogError(e);
    }
  }

  public enum ComparisonResult
  {
    /// <summary>
    /// Method is defined in both maps with same hash
    /// </summary>
    Unchanged,
    /// <summary>
    /// Method isn't defined in either map
    /// </summary>
    Undefined,
    /// <summary>
    /// Method is defined in current map, but not in base one
    /// </summary>
    Added,
    /// <summary>
    /// Method is defined in base map, but not in current one
    /// </summary>
    Removed,
    /// <summary>
    /// Method is defined in both maps, but hashes are different
    /// </summary>
    Changed
  }

  /// <summary>
  /// Compares definition in provided maps
  /// </summary>
  public static ComparisonResult CompareMethod(string def, AssemblyMap baseMap, AssemblyMap currentMap)
  {
    if (!baseMap.methods.TryGetValue(def, out MappedMethodData baseData))
    {
      if (currentMap.methods.TryGetValue(def, out MappedMethodData _))
        return ComparisonResult.Added;
      return ComparisonResult.Undefined;
    }
    else if (!currentMap.methods.TryGetValue(def, out MappedMethodData currentData))
      return ComparisonResult.Removed;
    else if (!baseData.hash.SequenceEqual(currentData.hash))
      return ComparisonResult.Changed;
    return ComparisonResult.Unchanged;
  }

  public static void LogHookComparisonResult(MethodBase method, AssemblyMap baseMap, AssemblyMap currentMap, string onSuccessMessage = "")
  {
    string def = method.GetSimpleName();
    switch (CompareMethod(def, baseMap, currentMap))
    {
      case ComparisonResult.Removed:
        LogInfo("[removed] " + def + onSuccessMessage);
        return;
      case ComparisonResult.Changed:
        LogInfo("[changed] " + def + onSuccessMessage);
        return;
      case ComparisonResult.Undefined:
      case ComparisonResult.Added:
        LogInfo($"Failed to find base definition for {def} <is not supposed to happen>");
        return;
    }
  }

  /// <summary>
  /// Logs all methods of hooks defined by <paramref name="asm"/> that changed between provided versions
  /// </summary>
  public static void FindChangesForHooks(Assembly asm, string baseVersion, string currentVersion,
    bool checkHarmonyPatches = false, bool checkNativeDetours = true, bool checkILHooks = true, bool checkDetours = false)
  {
    LogInfo($" * Searching for changes of base methods of hooks for {asm.GetName().Name} [{baseVersion} -> {currentVersion}]:");
    if (!managedVersions.Contains(baseVersion))
    {
      LogInfo($" * No assembly maps are defined for \"{baseVersion}\"");
      return;
    }
    AssemblyMap baseMap = GetMap(baseVersion), currentMap = GetMap(currentVersion);
    if (RuntimeDetourManager.hookLists.TryGetValue(asm, out List<IDetour> hooks))
      foreach (IDetour idetour in hooks)
      {
        switch (idetour)
        {
          case Hook:
          case Detour:
            if (!checkDetours)
              continue;
            break;
          case ILHook:
            if (!checkILHooks)
              continue;
            break;
          case NativeDetour:
            if (!checkNativeDetours)
              continue;
            break;
        }
        if (idetour.GetMethod() is not MethodBase method)
        {
          LogInfo("Failed to retrieve base method");
          continue;
        }
        LogHookComparisonResult(method, baseMap, currentMap, idetour.GetTarget() is MethodBase target ? $" \\ hooked via {target.GetFullSimpleName()}" : "");
      }
    else
      LogInfo("> Assembly doesn't define any hooks");
    if (checkHarmonyPatches)
    {
      Dictionary<MethodBase, List<MethodInfo>> patches = RuntimeDetourManager.GetHarmonyPatches(asm);
      if (patches.Count == 0)
        LogInfo("> Assembly doesn't define any harmony patches");
      else
        foreach (KeyValuePair<MethodBase, List<MethodInfo>> patchData in patches)
          LogHookComparisonResult(patchData.Key, baseMap, currentMap, $"hooked via:\n  {string.Join("\n  ", patchData.Value.Select(m => m.GetSimpleName()))}");
    }
    LogInfo($" * Finished searching");
  }

  /// <summary>
  /// Data assigned to method definitions
  /// </summary>
  public Dictionary<string, MappedMethodData> methods = [];

  AssemblyMap(string version)
  {
    string versionedPath = Path.Combine(targetPath, version);
    string[] defs = File.ReadAllLines(Path.Combine(versionedPath, "def-map.txt"));
    byte[] hashMap = File.ReadAllBytes(Path.Combine(versionedPath, "hash-map.txt")),
      opCodeMap = File.ReadAllBytes(Path.Combine(versionedPath, "opcode-map.txt"));
    int opCodeOffset = 0;
    for (int i = 0; i < defs.Length - 1; ++i)
    {
      int initialOffset = ++opCodeOffset;
      while (opCodeMap[opCodeOffset] != 255)
        ++opCodeOffset;
      byte[] hash = new byte[32], opCodes = new byte[opCodeOffset - initialOffset];
      Buffer.BlockCopy(hashMap, i * 32, hash, 0, 32);
      Buffer.BlockCopy(opCodeMap, initialOffset, opCodes, 0, opCodes.Length);
      methods[defs[i]] = new(hash, opCodes);
    }
    loadedMaps[version] = this;
  }
}

/// <summary>
/// Class that allows to define version specific conditions
/// </summary>
public static class VersionSpecific
{
  public enum MismatchAction
  {
    /// <summary>
    /// Warning will be logged on hash mismatch
    /// </summary>
    Warn,
    /// <summary>
    /// Expression won't be invoked on hash mismatch
    /// </summary>
    Skip
  }

  static string targetVersion;
  static MismatchAction mismatchAction = MismatchAction.Warn;
  static AssemblyMap baseMap, currentMap;

  /// <summary>
  /// Sets variables, that will be used for version specific method until changed
  /// </summary>
  public static void Update(string targetVersion, MismatchAction mismatchAction)
  {
    if (currentMap == null && (currentMap = AssemblyMap.GetMap(GSLPUtils.gameVersion)) == null
      || (baseMap = AssemblyMap.GetMap(VersionSpecific.targetVersion = targetVersion)) == null)
      return;
    VersionSpecific.mismatchAction = mismatchAction;
  }

  /// <summary>
  /// Returns <c>true</c> if expression should not be invoked according to set restrictions.
  /// Returns <c>false</c> if expression should be invoked or version could not be verified.
  /// </summary>
  public static bool CompareMethod(MethodBase method)
  {
    if (baseMap == null || baseMap == currentMap)
      return false;
    string def = method.GetSimpleName();
    AssemblyMap.ComparisonResult result = AssemblyMap.CompareMethod(def, baseMap, currentMap);
    if (mismatchAction == MismatchAction.Warn)
    {
      if (result > AssemblyMap.ComparisonResult.Added)
        LogWarning($"Method \"{def}\" was {result} in current {GSLPUtils.gameVersion} compared to {targetVersion}");
      return false;
    }
    if (result > AssemblyMap.ComparisonResult.Added)
    {
      LogError($"Method \"{def}\" was {result} in current {GSLPUtils.gameVersion} compared to {targetVersion} - skipping hook");
      return true;
    }
    return false;
  }

  public static IDetour New(MethodBase from, Func<MethodBase, IDetour> ctor) => CompareMethod(from) ? null : ctor(from);
  public static IDetour New(Delegate from, Func<MethodBase, IDetour> ctor) => CompareMethod(from.Method) ? null : ctor(from.Method);

  public static NativeDetour newNativeDetour(Delegate from, IntPtr to) => CompareMethod(from.Method) ? null : new(from.Method, to);
  public static NativeDetour newNativeDetour(Delegate from, IntPtr to, NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, config);
  public static NativeDetour newNativeDetour(Delegate from, IntPtr to, ref NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, ref config);
  public static NativeDetour newNativeDetour(MethodBase from, IntPtr to) => CompareMethod(from) ? null : new(from, to);
  public static NativeDetour newNativeDetour(MethodBase from, IntPtr to, NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to, config);
  public static NativeDetour newNativeDetour(MethodBase from, IntPtr to, ref NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to, ref config);
  public static NativeDetour newNativeDetour(Delegate from, Delegate to) => CompareMethod(from.Method) ? null : new(from.Method, to.Method);
  public static NativeDetour newNativeDetour(Delegate from, Delegate to, NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, config);
  public static NativeDetour newNativeDetour(Delegate from, Delegate to, ref NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, ref config);
  public static NativeDetour newNativeDetour(Delegate from, MethodBase to) => CompareMethod(from.Method) ? null : new(from.Method, to);
  public static NativeDetour newNativeDetour(Delegate from, MethodBase to, NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, config);
  public static NativeDetour newNativeDetour(Delegate from, MethodBase to, ref NativeDetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, ref config);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to) => CompareMethod(from) ? null : new(from, to.Method);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to, NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to.Method, config);
  public static NativeDetour newNativeDetour(MethodBase from, Delegate to, ref NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to.Method, ref config);
  public static NativeDetour newNativeDetour(MethodBase from, MethodBase to) => CompareMethod(from) ? null : new(from, to);
  public static NativeDetour newNativeDetour(MethodBase from, MethodBase to, NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to, config);
  public static NativeDetour newNativeDetour(MethodBase from, MethodBase to, ref NativeDetourConfig config) => CompareMethod(from) ? null : new(from, to, ref config);

  public static Detour newDetour(Delegate from, IntPtr to) => CompareMethod(from.Method) ? null : new(from.Method, to);
  public static Detour newDetour(Delegate from, IntPtr to, DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, config);
  public static Detour newDetour(Delegate from, IntPtr to, ref DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, ref config);
  public static Detour newDetour(MethodBase from, IntPtr to) => CompareMethod(from) ? null : new(from, to);
  public static Detour newDetour(MethodBase from, IntPtr to, DetourConfig config) => CompareMethod(from) ? null : new(from, to, config);
  public static Detour newDetour(MethodBase from, IntPtr to, ref DetourConfig config) => CompareMethod(from) ? null : new(from, to, ref config);
  public static Detour newDetour(Delegate from, Delegate to) => CompareMethod(from.Method) ? null : new(from.Method, to.Method);
  public static Detour newDetour(Delegate from, Delegate to, DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, config);
  public static Detour newDetour(Delegate from, Delegate to, ref DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, ref config);
  public static Detour newDetour(Delegate from, MethodBase to) => CompareMethod(from.Method) ? null : new(from.Method, to);
  public static Detour newDetour(Delegate from, MethodBase to, DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, config);
  public static Detour newDetour(Delegate from, MethodBase to, ref DetourConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, ref config);
  public static Detour newDetour(MethodBase from, Delegate to) => CompareMethod(from) ? null : new(from, to.Method);
  public static Detour newDetour(MethodBase from, Delegate to, DetourConfig config) => CompareMethod(from) ? null : new(from, to.Method, config);
  public static Detour newDetour(MethodBase from, Delegate to, ref DetourConfig config) => CompareMethod(from) ? null : new(from, to.Method, ref config);
  public static Detour newDetour(MethodBase from, MethodBase to) => CompareMethod(from) ? null : new(from, to);
  public static Detour newDetour(MethodBase from, MethodBase to, DetourConfig config) => CompareMethod(from) ? null : new(from, to, config);
  public static Detour newDetour(MethodBase from, MethodBase to, ref DetourConfig config) => CompareMethod(from) ? null : new(from, to, ref config);

  public static Hook newHook(Delegate from, IntPtr to) => CompareMethod(from.Method) ? null : new(from.Method, to);
  public static Hook newHook(Delegate from, IntPtr to, HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, config);
  public static Hook newHook(Delegate from, IntPtr to, ref HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, ref config);
  public static Hook newHook(MethodBase from, IntPtr to) => CompareMethod(from) ? null : new(from, to);
  public static Hook newHook(MethodBase from, IntPtr to, HookConfig config) => CompareMethod(from) ? null : new(from, to, config);
  public static Hook newHook(MethodBase from, IntPtr to, ref HookConfig config) => CompareMethod(from) ? null : new(from, to, ref config);
  public static Hook newHook(Delegate from, Delegate to) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, to.Target);
  public static Hook newHook(Delegate from, Delegate to, HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, to.Target, config);
  public static Hook newHook(Delegate from, Delegate to, ref HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to.Method, to.Target, ref config);
  public static Hook newHook(Delegate from, MethodInfo to) => CompareMethod(from.Method) ? null : new(from.Method, to, null);
  public static Hook newHook(Delegate from, MethodInfo to, HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, null, config);
  public static Hook newHook(Delegate from, MethodInfo to, ref HookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, to, null, ref config);
  public static Hook newHook(MethodBase from, Delegate to) => CompareMethod(from) ? null : new(from, to.Method, to.Target);
  public static Hook newHook(MethodBase from, Delegate to, HookConfig config) => CompareMethod(from) ? null : new(from, to.Method, to.Target, config);
  public static Hook newHook(MethodBase from, Delegate to, ref HookConfig config) => CompareMethod(from) ? null : new(from, to.Method, to.Target, ref config);
  public static Hook newHook(MethodBase from, MethodInfo to) => CompareMethod(from) ? null : new(from, to, null);
  public static Hook newHook(MethodBase from, MethodInfo to, HookConfig config) => CompareMethod(from) ? null : new(from, to, null, config);
  public static Hook newHook(MethodBase from, MethodInfo to, ref HookConfig config) => CompareMethod(from) ? null : new(from, to, null, ref config);

  public static ILHook newILHook(Delegate from, ILContext.Manipulator manipulator) => CompareMethod(from.Method) ? null : new(from.Method, manipulator);
  public static ILHook newILHook(Delegate from, ILContext.Manipulator manipulator, ILHookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, manipulator, config);
  public static ILHook newILHook(Delegate from, ILContext.Manipulator manipulator, ref ILHookConfig config) => CompareMethod(from.Method) ? null : new(from.Method, manipulator, ref config);
  public static ILHook newILHook(MethodBase from, ILContext.Manipulator manipulator) => CompareMethod(from) ? null : new(from, manipulator);
  public static ILHook newILHook(MethodBase from, ILContext.Manipulator manipulator, ILHookConfig config) => CompareMethod(from) ? null : new(from, manipulator, config);
  public static ILHook newILHook(MethodBase from, ILContext.Manipulator manipulator, ref ILHookConfig config) => CompareMethod(from) ? null : new(from, manipulator, ref config);
}