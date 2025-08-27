using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Text;

namespace gelbi_silly_lib.MonoModUtilsPreloader;

/// <summary>
/// Different methods to work with MonoMod definitions
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Executes provided action with type and each type nested in it
  /// </summary>
  public static void ForEach(this TypeDefinition self, Action<TypeDefinition> action)
  {
    foreach (TypeDefinition type in self.NestedTypes)
      type.ForEach(action);
    action(self);
  }

  /// <summary>
  /// Executes provided action with each type in module, including nested types
  /// </summary>
  public static void ForEachType(this ModuleDefinition self, Action<TypeDefinition> action)
  {
    foreach (TypeDefinition type in self.Types)
      type.ForEach(action);
  }

  /// <summary>
  /// Returns list with all types and nested types, defined in module
  /// </summary>
  public static List<TypeDefinition> GetAllTypes(this ModuleDefinition self)
  {
    List<TypeDefinition> types = new();
    self.ForEachType(type => types.Add(type));
    return types;
  }

  /// <summary>
  /// Returns string with logged il body of the method
  /// </summary>
  public static string LogBody(this MethodDefinition self)
  {
    StringBuilder sb = new();
    foreach (Instruction i in self.Body.Instructions)
      sb.AppendLine($"{i.Offset:X4}: {i.OpCode} {i.Operand}");
    return sb.ToString();
  }
}