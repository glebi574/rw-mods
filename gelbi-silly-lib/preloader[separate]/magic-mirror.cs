using gelbi_silly_lib.Other;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.ReflectionUtils;

/// <summary>
/// Some common binding flags
/// </summary>
public static class BFlags
{
  /// <summary>
  /// <c>static / private / public</c>
  /// </summary>
  public const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  /// <summary>
  /// <c>not static / private / public</c>
  /// </summary>
  public const BindingFlags anyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  /// <summary>
  /// <c>static / not static / private / public</c>
  /// </summary>
  public const BindingFlags any = anyStatic | BindingFlags.Instance;
  /// <summary>
  /// <c>static / private / public / declared only</c>
  /// </summary>
  public const BindingFlags anyDeclaredStatic = anyStatic | BindingFlags.DeclaredOnly;
  /// <summary>
  /// <c>not static / private / public / declared only</c>
  /// </summary>
  public const BindingFlags anyDeclaredInstance = anyInstance | BindingFlags.DeclaredOnly;
  /// <summary>
  /// <c>static / not static / private / public / declared only</c>
  /// </summary>
  public const BindingFlags anyDeclared = any | BindingFlags.DeclaredOnly;
}

public static class ReflectionUtils
{
  public static readonly Dictionary<Type, string> baseTypeNameAtlas = new()
  {
    { typeof(void), "void" },
    { typeof(string), "string" },
    { typeof(object), "object" },
    { typeof(bool), "bool" },
    { typeof(char), "char" },
    { typeof(byte), "byte" },
    { typeof(sbyte), "sbyte" },
    { typeof(short), "short" },
    { typeof(ushort), "ushort" },
    { typeof(int), "int" },
    { typeof(uint), "uint" },
    { typeof(long), "long" },
    { typeof(ulong), "ulong" },
    { typeof(float), "float" },
    { typeof(double), "double" },
    { typeof(decimal), "decimal" }
  };

  /// <summary>
  /// Logs all loaded assemblies in current domain
  /// </summary>
  public static void LogAssemblies()
  {
    LogInfo($" * Logging all loaded assemblies:");
    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
      LogInfo("  " + asm);
    LogInfo($" * Finished logging");
  }
}

/// <summary>
/// Extensions for Reflection and related classes
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Returns types, defined by assembly. May throw less, than default version
  /// </summary>
  public static IEnumerable<Type> GetTypesSafe(this Assembly self)
  {
    foreach (Module module in self.GetModules())
      foreach (Type type in module.GetTypes())
        yield return type;
  }

  /// <summary>
  /// Invokes class constructor of provided type
  /// </summary>
  public static void RunClassConstructor(this Type type) => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

  /// <summary>
  /// Checks if type inherits other generic type
  /// </summary>
  public static bool InheritsGenericType(this Type self, Type other) => self.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == other);

  /// <summary>
  /// Returns effective return type of method/constructor
  /// </summary>
  public static Type GetReturnType(this MethodBase methodBase) => methodBase switch
  {
    MethodInfo method => method.ReturnType,
    ConstructorInfo => typeof(void),
    _ => null,
  };

  /// <summary>
  /// Returns string, containing formatted list of types, presented in array
  /// </summary>
  public static string GetFormattedTypes<T>(this T[] self) where T : Type
  {
    if (self.Length == 0)
      return "";
    StringBuilder sb = new(self[0].GetSimpleName());
    for (int i = 1; i < self.Length; ++i)
    {
      sb.Append(", ");
      sb.Append(self[i].GetSimpleName());
    }
    return sb.ToString();
  }

  /// <summary>
  /// Returns string, containing formatted list of parameter types, presented in array
  /// </summary>
  public static string GetFormattedParameterTypes(this ParameterInfo[] self)
  {
    if (self.Length == 0)
      return "";
    StringBuilder sb = new(self[0].ParameterType.GetSimpleName());
    for (int i = 1; i < self.Length; ++i)
    {
      sb.Append(", ");
      sb.Append(self[i].ParameterType.GetSimpleName());
    }
    return sb.ToString();
  }

  /// <summary>
  /// Returns type definition similar to how it'd be initially written in c# (doesn't differentiate ref/out)
  /// </summary>
  public static string GetSimpleName(this Type self)
  {
    if (ReflectionUtils.baseTypeNameAtlas.TryGetValue(self, out string name))
      return name;
    if (self.IsArray)
      return $"{self.GetElementType().GetSimpleName()}[{new string(',', self.GetArrayRank() - 1)}]";
    if (self.IsByRef)
      return $"{self.GetElementType().GetSimpleName()}&";
    if (self.IsPointer)
      return $"{self.GetElementType().GetSimpleName()}*";
    if (self.IsGenericParameter)
      return self.Name;
    if (!self.IsGenericType)
    {
      if (self.DeclaringType == null)
        return self.Name;
      return $"{self.DeclaringType.GetSimpleName()}.{self.Name}";
    }
    if (self.Namespace == "System")
      if (self.Name.StartsWith("Nullable") && self != typeof(Nullable<>))
        return $"{self.GenericTypeArguments[0].GetSimpleName()}?";
      else if (self.Name.StartsWith("ValueTuple") && self != typeof(ValueTuple<>))
        return $"({self.GenericTypeArguments.GetFormattedTypes()})";

    if (self.IsGenericTypeDefinition)
      return $"{self.Name.SubstringUntil('`')}<{self.GetGenericArguments().GetFormattedTypes()}>";
    return $"{self.Name.SubstringUntil('`')}<{self.GetGenericArguments().GetFormattedTypes()}>";
  }

  /// <summary>
  /// Returns type definition with namespace similar to how it'd be initially written in c# (doesn't differentiate ref/out)
  /// </summary>
  public static string GetSimpleNameWithNamespace(this Type self)
  {
    if (self.Namespace == null || ReflectionUtils.baseTypeNameAtlas.ContainsKey(self))
      return self.GetSimpleName();
    return self.Namespace + '.' + self.GetSimpleName();
  }

  /// <summary>
  /// Returns field definition similar to how it'd be written in c#
  /// </summary>
  public static string GetSimpleName(this FieldInfo field)
  {
    return $"{field.FieldType.GetSimpleName()} {field.DeclaringType.GetSimpleName()}.{field.Name}";
  }

  /// <summary>
  /// Returns method definition similar to how it'd be written in c#
  /// </summary>
  public static string GetSimpleName(this MethodInfo method)
  {
    return $"{method.ReturnType.GetSimpleName()} {method.GetFullSimpleName()}({method.GetParameters().GetFormattedParameterTypes()})";
  }

  /// <summary>
  /// Returns constructor definition similar to how it'd be written in c#
  /// </summary>
  public static string GetSimpleName(this ConstructorInfo method)
  {
    return $"void {method.GetFullSimpleName()}({method.GetParameters().GetFormattedParameterTypes()})";
  }

  /// <summary>
  /// Returns method definition similar to how it'd be written in c# (if possible)
  /// </summary>
  public static string GetSimpleName(this MethodBase method)
  {
    if (method is MethodInfo methodInfo)
      return methodInfo.GetSimpleName();
    if (method is ConstructorInfo constructorInfo)
      return constructorInfo.GetSimpleName();
    return method.ToString();
  }

  /// <summary>
  /// Returns full method name with declaring type definition similar to how it'd be initially written in c#
  /// </summary>
  public static string GetFullSimpleName(this MethodBase method)
  {
    StringBuilder sb = new(64);
    if (method.DeclaringType?.Namespace != null)
      sb.Append(method.DeclaringType.Namespace).Append('.');
    if (method.DeclaringType != null)
      sb.Append(method.DeclaringType.GetSimpleName()).Append('.');
    sb.Append(method.Name);
    return sb.ToString();
  }
}