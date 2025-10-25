using gelbi_silly_lib.ReflectionUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.ReflectionValueUtils
{
  /// <summary>
  /// Extensions for Reflection, optimizing getting/setting/invoking
  /// <para>Separated into their own namespace for being annoying</para>
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Returns value of field in provided instance
    /// </summary>
    public static object GetFieldValue(this object self, string fieldName)
    {
      return self.GetType().GetField(fieldName, BFlags.any).GetValue(self);
    }

    /// <summary>
    /// Sets value of field in provided instance
    /// </summary>
    public static void SetFieldValue(this object self, string fieldName, object value)
    {
      self.GetType().GetField(fieldName, BFlags.any).SetValue(self, value);
    }

    /// <summary>
    /// Invokes method in provided instance
    /// </summary>
    public static object InvokeStaticMethod(this object self, string fieldName, params object[] args)
    {
      return self.GetType().GetMethod(fieldName, BFlags.any).Invoke(self, args);
    }

    /// <summary>
    /// Returns value of static field in provided type
    /// </summary>
    public static object GetStaticFieldValue(this Type self, string fieldName)
    {
      return self.GetField(fieldName, BFlags.any).GetValue(null);
    }

    /// <summary>
    /// Sets value of static field in provided type
    /// </summary>
    public static void SetStaticFieldValue(this Type self, string fieldName, object value)
    {
      self.GetField(fieldName, BFlags.any).SetValue(null, value);
    }

    /// <summary>
    /// Invokes static method in provided type
    /// </summary>
    public static object InvokeStaticMethod(this Type self, string fieldName, params object[] args)
    {
      return self.GetMethod(fieldName, BFlags.any).Invoke(null, args);
    }
  }
}

namespace gelbi_silly_lib.ReflectionUtils
{
  /// <summary>
  /// Some common binding flags
  /// </summary>
  public static class BFlags
  {
    /// <summary>
    /// <c>static / not static / private / public</c>
    /// </summary>
    public const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>
    /// <c>static / private / public</c>
    /// </summary>
    public const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>
    /// <c>not static / private / public</c>
    /// </summary>
    public const BindingFlags anyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
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
      foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        LogInfo(asm);
    }
  }

  /// <summary>
  /// Extensions for Reflection
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
    /// Returns types, defined by assembly. May throw less, than default version
    /// </summary>
    public static FieldInfo[] GetFieldsSafe(this Type self, BindingFlags flags)
    {
      try
      {
        return self.GetFields(flags);
      }
      catch { }
      return [];
    }

    /// <summary>
    /// Calls <c>cctor</c> of provided type
    /// </summary>
    public static void RunClassConstructor(this Type type)
    {
      System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }

    /// <summary>
    /// Checks if type inherits other generic type
    /// </summary>
    public static bool InheritsGenericType(this Type self, Type other)
    {
      return self.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == other);
    }

    /// <summary>
    /// Returns type definition similar to how it'd be initially written in c# (doesn't differentiate ref/out)
    /// </summary>
    public static string GetSimpleName(this Type type)
    {
      if (ReflectionUtils.baseTypeNameAtlas.TryGetValue(type, out string name))
        return name;
      if (type.IsArray)
        return $"{type.GetElementType().GetSimpleName()}[]";
      if (type.IsByRef)
        return $"{type.GetElementType().GetSimpleName()}&";
      if (!type.IsGenericType)
        return type.FullName;
      if (type.FullName == null)
        return type.ToString();
      if (type.FullName.StartsWith("System.Nullable"))
        return $"{type.GenericTypeArguments[0].GetSimpleName()}?";

      string baseName = "";
      if ((type.Namespace?.StartsWith("System.") ?? false) || type.Namespace == "System")
        baseName = type.FullName.Substring(type.Namespace.Length + 1);
      else
        baseName = type.FullName;
      int genericCutIndex = baseName.IndexOf('`');
      if (genericCutIndex != -1)
        baseName = baseName.Substring(0, genericCutIndex);

      return $"{baseName}<{string.Join(", ", type.GenericTypeArguments.Select(GetSimpleName))}>";
    }

    /// <summary>
    /// Returns field definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this FieldInfo field)
    {
      return $"{field.FieldType.GetSimpleName()} {field.DeclaringType?.FullName}+{field.Name}";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this MethodInfo method)
    {
      return $"{method.ReturnType.GetSimpleName()} {method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(x => x.ParameterType.GetSimpleName()))})";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
    /// </summary>
    public static string GetSimpleName(this ConstructorInfo method)
    {
      return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(x => x.ParameterType.GetSimpleName()))})";
    }

    /// <summary>
    /// Returns method definition similar to how it'd be written in c#
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
    /// Returns full method name
    /// </summary>
    public static string GetFullName(this MethodBase method)
    {
      return $"{method.DeclaringType.FullName}.{method.Name}";
    }
  }
}