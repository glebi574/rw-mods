using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Collections.Generic;
using MonoMod.Utils;
using MonoMod.Utils.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace gelbi_silly_lib.OptimizedImplementation;

public static class SillyOptimizations
{
  static SillyOptimizations()
  {
    typeof(ImplMonoMod).RunClassConstructor();
    foreach ((MethodBase from, Delegate to) in ((MethodBase from, Delegate to)[])[
      (typeof(MonoMod.Utils.Extensions).GetMethod("Is", [typeof(MemberReference), typeof(MemberInfo)]), ImplMonoMod.Extensions_Is),
      (typeof(ReflectionHelper).GetMethod("_ResolveReflection", BFlags.anyDeclaredStatic), ImplMonoMod.ReflectionHelper__ResolveReflection),
      (typeof(ILGeneratorShimExt).GetMethod("DynEmit", BFlags.anyDeclaredStatic, null, [typeof(ILGenerator), typeof(OpCode), typeof(object)], null), ImplMonoMod.ILGeneratorShimExt_DynEmit),
      (typeof(MonoMod.Utils.Extensions).GetMethod("GetPatchFullName", BFlags.anyDeclaredStatic, null, [typeof(MemberReference)], null), ImplMonoMod.Extensions_GetPatchFullName),
    ])
      RuntimeDetourManager.AddHook(DetourUtils.newNativeDetour(from, to), from, to.Method);
  }
}

public static class ImplMonoMod
{
  public static ConcurrentDictionary<string, MemberInfo> resolveCache = [];
  public static ConcurrentDictionary<AssemblyNameReference, string> assemblyNameCache = [];
  public static ConcurrentDictionary<Type, MethodInfo[]> methodCache = [];
  public static ConcurrentDictionary<Type, ConstructorInfo[]> constructorCache = [];
  public static ConcurrentDictionary<Type, FieldInfo[]> fieldCache = [];
  public static ConcurrentDictionary<Type, MemberInfo[]> memberCache = [];

  public static Dictionary<Type, Action<ILGenerator, OpCode, object>> emitters = new() {
    { typeof(byte), EmitByte },
    { typeof(sbyte), EmitSByte },
    { typeof(short), EmitShort },
    { typeof(int), EmitInt },
    { typeof(long), EmitLong },
    { typeof(float), EmitFloat },
    { typeof(double), EmitDouble },
    { typeof(string), EmitString },
    { typeof(Label), EmitLabel },
    { typeof(Label[]), EmitSwitch },
    { typeof(LocalBuilder), EmitLocalBuilder },
    { typeof(SignatureHelper), EmitSignatureHelper },
    { typeof(Type), EmitType },
    { typeof(MethodInfo), EmitMethod },
    { typeof(ConstructorInfo), EmitConstructor },
    { typeof(FieldInfo), EmitField },
    { typeof(Assembly).Assembly.GetType("System.Reflection.MonoMethod"), EmitMethod },
    { typeof(DynamicMethod), EmitMethod },
    { typeof(Assembly).Assembly.GetType("System.Reflection.MonoCMethod"), EmitConstructor },
    { typeof(Assembly).Assembly.GetType("System.Reflection.MonoField"), EmitField },
    { typeof(Assembly).Assembly.GetType("System.RuntimeType"), EmitType },
  };
  public static Dictionary<Type, Action<StringBuilder, TypeSpecification>> mrefParsers = new() {
    { typeof(ByReferenceType), (sb, type) => sb.Append('&') },
    { typeof(PointerType), (sb, type) => sb.Append('*') },
    { typeof(ArrayType), (sb, type) => {
        if (((ArrayType)type).IsVector)
          sb.Append("[]");
        else
        {
          Collection<ArrayDimension> dimensions = ((ArrayType)type).Dimensions;
          sb.Append('[').Append(dimensions[0].ToString());
          for (int j = 1; j < dimensions.Count; ++j)
            sb.Append(',').Append(dimensions[j].ToString());
          sb.Append(']');
        }
      }},
    { typeof(RequiredModifierType), (sb, type) => sb.Append(string.Concat("modreq(", ((RequiredModifierType)type).ModifierType, ")")) },
    { typeof(OptionalModifierType), (sb, type) => sb.Append(string.Concat("modopt(", ((OptionalModifierType)type).ModifierType, ")")) },
    { typeof(GenericInstanceType), (sb, type) => {
        Collection<TypeReference> genericArgs = ((GenericInstanceType)type).GenericArguments;
        sb.Append('<').Append(genericArgs[0].GetPatchFullName());
        for (int j = 1; j < genericArgs.Count; ++j)
          sb.Append(',').Append(genericArgs[j].GetPatchFullName());
        sb.Append(">");
      }},
    { typeof(FunctionPointerType), (sb, type) => {
        FunctionPointerType functionPointerType = (FunctionPointerType)type;
        sb.Append(' ').Append(functionPointerType.ReturnType.GetPatchFullName()).Append(" *(");
        if (functionPointerType.HasParameters)
        {
          Collection<ParameterDefinition> functionPtrParams = functionPointerType.Parameters;
          if (functionPtrParams[0].ParameterType is SentinelType)
            sb.Append("...,");
          sb.Append(functionPtrParams[0].ParameterType.FullName);
          for (int j = 1; j < functionPtrParams.Count; ++j)
            sb.Append(functionPtrParams[j].ParameterType is SentinelType ? ",...," : ",").Append(functionPtrParams[j].ParameterType.FullName);
        }
        sb.Append(')');
      }},
    { typeof(PinnedType), (sb, type) => {} },
    { typeof(SentinelType), (sb, type) => {} },
    { typeof(TypeSpecification), (sb, type) => { throw new NotSupportedException($"MonoMod can't handle TypeSpecification: {type.FullName} ({type.GetType()})");} },
  };
  public static Dictionary<Type, Func<TypeReference, TypeReference>> simpleRelinkers = new() {
    { typeof(SentinelType), (typeReference) => new SentinelType(typeReference)},
    { typeof(ByReferenceType), (typeReference) => new ByReferenceType(typeReference)},
    { typeof(PointerType), (typeReference) => new PointerType(typeReference)},
    { typeof(PinnedType), (typeReference) => new PinnedType(typeReference)}
  };
  public static Dictionary<Type, Func<TypeReference, TypeReference, MethodReference, TypeReference, TypeReference>> relinkers = new()
  {
    { typeof(ArrayType), (type, typeReference, _, _) => {
        ArrayType arrayType = (ArrayType)type, newArrayType = new(typeReference, arrayType.Rank);
        for (int i = 0; i < newArrayType.Rank; ++i)
          newArrayType.Dimensions[i] = arrayType.Dimensions[i];
        return newArrayType;
      }},
    { typeof(RequiredModifierType), (type, typeReference, methodRef, refDeclaringType) =>
      new RequiredModifierType(((RequiredModifierType)type).ModifierType.Relink(methodRef, refDeclaringType), typeReference)},
    { typeof(OptionalModifierType), (type, typeReference, methodRef, refDeclaringType) =>
      new OptionalModifierType(((OptionalModifierType)type).ModifierType.Relink(methodRef, refDeclaringType), typeReference)},
    { typeof(GenericInstanceType), (type, typeReference, methodRef, refDeclaringType) => {
        GenericInstanceType genericInstanceType = new(typeReference);
        foreach (TypeReference arg in ((GenericInstanceType)type).GenericArguments)
          genericInstanceType.GenericArguments.Add(arg?.Relink(methodRef, refDeclaringType));
        return genericInstanceType;
      }},
    { typeof(FunctionPointerType), (type, typeReference, methodRef, refDeclaringType) => {
        FunctionPointerType functionPointerType = (FunctionPointerType)type;
        functionPointerType.ReturnType = functionPointerType.ReturnType.Relink(methodRef, refDeclaringType);
        foreach (ParameterDefinition param in functionPointerType.Parameters)
          param.ParameterType = param.ParameterType.Relink(methodRef, refDeclaringType);
        return functionPointerType;
      }}
  };

  public static TypeReference Relink(this TypeReference type, MethodReference methodRef, TypeReference refDeclaringType)
  {
    if (type == null)
      return null;
    if (type is not TypeSpecification typeSpecification)
    {
      if (type is not GenericParameter genericParameter)
        return type == refDeclaringType.GetElementType() ? methodRef.DeclaringType : type;
      return genericParameter.Owner switch
      {
        MethodReference when
          methodRef is GenericInstanceMethod genericInstanceMethod
          => genericInstanceMethod.GenericArguments[genericParameter.Position],
        TypeReference ownerRef when
          methodRef.DeclaringType is GenericInstanceType genericInstanceType && ownerRef.FullName == genericInstanceType.ElementType.FullName
          => genericInstanceType.GenericArguments[genericParameter.Position],
        _ => type
      };
    }
    if (simpleRelinkers.TryGetValue(type.GetType(), out Func<TypeReference, TypeReference> simpleRelinker))
      return simpleRelinker(typeSpecification.ElementType.Relink(methodRef, refDeclaringType));
    if (relinkers.TryGetValue(type.GetType(), out Func<TypeReference, TypeReference, MethodReference, TypeReference, TypeReference> relinker))
      return relinker(type, typeSpecification.ElementType.Relink(methodRef, refDeclaringType), methodRef, refDeclaringType);
    throw new NotSupportedException($"MonoMod can't handle TypeSpecification: {type.FullName} ({type.GetType()})");
  }

  static bool _Is(MemberReference mref, MemberInfo minfo)
  {
    TypeReference refDeclaringType = mref.DeclaringType;
    Type type = minfo as Type, declaringType = minfo?.DeclaringType;
    if (refDeclaringType?.Name == "<Module>")
      refDeclaringType = null;
    if (mref is GenericParameter genericParameter)
      return type != null && (type.IsGenericParameter
           ? genericParameter.Position == type.GenericParameterPosition
           : (genericParameter.Owner as IGenericInstance)?.GenericArguments[genericParameter.Position].Is(type) ?? false);
    if (declaringType != null)
    {
      if (refDeclaringType == null)
        return false;
      if (type != null && declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
        declaringType = declaringType.GetGenericTypeDefinition();
      if (!refDeclaringType.Is(declaringType))
        return false;
    }
    else if (refDeclaringType != null)
      return false;
    if (mref is not TypeSpecification && mref.Name != minfo.Name)
      return false;
    if (mref is TypeReference typeReference)
    {
      if (type == null || type.IsGenericParameter)
        return false;
      if (mref is GenericInstanceType genericInstanceType)
      {
        if (!type.IsGenericType)
          return false;
        Collection<TypeReference> mrefArgs = genericInstanceType.GenericArguments;
        Type[] minfoArgs = type.GetGenericArguments();
        if (mrefArgs.Count != minfoArgs.Length)
          return false;
        for (int i = 0; i < minfoArgs.Length; ++i)
          if (!mrefArgs[i].Is(minfoArgs[i]))
            return false;
        return genericInstanceType.ElementType.Is(type.GetGenericTypeDefinition());
      }
      if (typeReference.HasGenericParameters)
      {
        if (!type.IsGenericType)
          return false;
        Collection<GenericParameter> mrefArgs = typeReference.GenericParameters;
        Type[] minfoArgs = type.GetGenericArguments();
        if (mrefArgs.Count != minfoArgs.Length)
          return false;
        for (int i = 0; i < minfoArgs.Length; ++i)
          if (!mrefArgs[i].Is(minfoArgs[i]))
            return false;
      }
      else if (type.IsGenericType)
        return false;
      return mref switch
      {
        ArrayType => type.IsArray && ((ArrayType)mref).Dimensions.Count == type.GetArrayRank() && ((ArrayType)mref).ElementType.Is(type.GetElementType()),
        ByReferenceType => type.IsByRef && ((ByReferenceType)mref).ElementType.Is(type.GetElementType()),
        PointerType => type.IsPointer && ((PointerType)mref).ElementType.Is(type.GetElementType()),
        TypeSpecification => ((TypeSpecification)mref).ElementType.Is(type.HasElementType ? type.GetElementType() : type),
        _ => refDeclaringType != null ? mref.Name == type.Name : mref.FullName == type.FullName.Replace('+', '/'),
      };
    }
    if (type != null)
      return false;
    if (mref is not MethodReference methodRef)
      return minfo is not MethodInfo
        && mref is FieldReference == minfo is FieldInfo
        && mref is PropertyReference == minfo is PropertyInfo
        && mref is EventReference == minfo is EventInfo;
    if (minfo is not MethodBase methodBase)
      return false;
    Collection<ParameterDefinition> mrefParams = methodRef.Parameters;
    ParameterInfo[] minfoParams = methodBase.GetParameters();
    if (mrefParams.Count != minfoParams.Length)
      return false;
    if (mref is GenericInstanceMethod genericInstanceMethod)
    {
      if (!methodBase.IsGenericMethod)
        return false;
      Collection<TypeReference> mrefArgs = genericInstanceMethod.GenericArguments;
      Type[] minfoArgs = methodBase.GetGenericArguments();
      if (mrefArgs.Count != minfoArgs.Length)
        return false;
      for (int i = 0; i < minfoArgs.Length; ++i)
        if (!mrefArgs[i].Is(minfoArgs[i]))
          return false;
      return genericInstanceMethod.ElementMethod.Is(((methodBase as MethodInfo)?.GetGenericMethodDefinition()) ?? methodBase);
    }
    if (methodRef.HasGenericParameters)
    {
      if (!methodBase.IsGenericMethod)
        return false;
      Collection<GenericParameter> mrefArgs = methodRef.GenericParameters;
      Type[] minfoArgs = methodBase.GetGenericArguments();
      if (mrefArgs.Count != minfoArgs.Length)
        return false;
      for (int i = 0; i < minfoArgs.Length; ++i)
        if (!mrefArgs[i].Is(minfoArgs[i]))
          return false;
    }
    else if (methodBase.IsGenericMethod)
      return false;
    if (methodRef.ReturnType.Relink(methodRef, refDeclaringType).Is((methodBase as MethodInfo)?.ReturnType ?? typeof(void))
     || methodRef.ReturnType.Is((methodBase as MethodInfo)?.ReturnType ?? typeof(void)))
    {
      for (int i = 0; i < minfoParams.Length; ++i)
        if (!mrefParams[i].ParameterType.Relink(methodRef, refDeclaringType).Is(minfoParams[i].ParameterType) && !mrefParams[i].ParameterType.Is(minfoParams[i].ParameterType))
          return false;
      return true;
    }
    return false;
  }

  public static bool Extensions_Is(MemberReference mref, MemberInfo minfo)
  {
    if (mref == null)
      return false;
    return _Is(mref, minfo);
  }

  public static MemberInfo Cache(string cacheKey, MemberInfo value) => resolveCache[cacheKey] = value;

  public static string CacheAssemblyName(this AssemblyNameReference self)
    => assemblyNameCache.TryGetValue(self, out string name) ? name : (assemblyNameCache[self] = self.GetRuntimeHashedFullName());

  public static string GetMethodName(this MethodReference self)
  {
    StringBuilder sb = new StringBuilder(64).Append(self.ReturnType.GetPatchFullName()).Append(' ');
    if (self.DeclaringType != null)
      sb.Append(self.DeclaringType.GetPatchFullName()).Append("::");
    sb.Append(self.Name);
    if (self is GenericInstanceMethod genericInstanceMethod && genericInstanceMethod.HasGenericArguments)
    {
      Collection<TypeReference> genericArgs = genericInstanceMethod.GenericArguments;
      sb.Append('<').Append(genericArgs[0].GetPatchFullName());
      for (int i = 1; i < genericArgs.Count; ++i)
        sb.Append(',').Append(genericArgs[i].GetPatchFullName());
      sb.Append('>');
    }
    else if (self.HasGenericParameters)
    {
      Collection<GenericParameter> genericParams = self.GenericParameters;
      sb.Append('<').Append(genericParams[0].Name);
      for (int i = 1; i < genericParams.Count; ++i)
        sb.Append(',').Append(genericParams[i].Name);
      sb.Append('>');
    }
    sb.Append('(');
    if (self.HasParameters)
    {
      Collection<ParameterDefinition> methodParams = self.Parameters;
      if (methodParams[0].ParameterType.IsSentinel)
        sb.Append("...,");
      sb.Append(methodParams[0].ParameterType.GetPatchFullName());
      for (int i = 1; i < methodParams.Count; ++i)
        sb.Append(methodParams[i].ParameterType.IsSentinel ? ",...," : ",").Append(methodParams[i].ParameterType.GetPatchFullName());
    }
    return sb.Append(')').ToString();
  }

  public static string GetPatchFullName(this FieldReference self)
  {
    string fieldName = self.Name;
    return string.Concat(self.FieldType.GetPatchFullName(), " ", self.DeclaringType.GetPatchFullName(), "::",
      fieldName.StartsWith("patch_", StringComparison.Ordinal) ? fieldName.Substring(6) : fieldName);
  }

  public static string GetPatchFullName(this TypeReference self)
  {
    string name = self.Name;
    if (name.StartsWith("patch_", StringComparison.Ordinal))
      name = name.Substring(6);
    if (name.StartsWith("global::", StringComparison.Ordinal))
      name = name.Substring(8);
    else
    {
      foreach (char c in name)
        if (c == '.' || c == '/')
          goto _other;
      if (!string.IsNullOrEmpty(self.Namespace))
        name = string.Concat(self.Namespace, ".", name);
      else if (self.DeclaringType != null)
        name = string.Concat(self.DeclaringType.GetPatchFullName(), "/", name);
    }
  _other:
    if (self is not TypeSpecification type)
      return name;
    List<TypeSpecification> list = [];
    do
      list.Add(type);
    while ((type = type.ElementType as TypeSpecification) != null);
    StringBuilder sb = new StringBuilder(name.Length + list.Count * 4).Append(name);
    for (int i = list.Count - 1; i >= 0; --i)
      mrefParsers[list[i].GetType()](sb, list[i]);
    return sb.ToString();
  }

  public static string Extensions_GetPatchFullName(MemberReference mr)
  {
    if (mr is not Mono.Cecil.ICustomAttributeProvider cap)
      return mr.FullName;
    return cap switch
    {
      TypeReference typeReference => typeReference.GetPatchFullName(),
      FieldReference fieldReference => fieldReference.GetPatchFullName(),
      MethodReference => throw new InvalidOperationException("GetPatchFullName not supported on MethodReferences - use GetID instead"),
      _ => throw new InvalidOperationException($"GetPatchFullName not supported on type {cap.GetType()}")
    };
  }

  public static MethodInfo[] CacheMethods(Type type)
    => methodCache.TryGetValue(type, out MethodInfo[] methods) ? methods : (methodCache[type] = type.GetMethods((BindingFlags)(-1)));
  public static ConstructorInfo[] CacheConstructors(Type type)
    => constructorCache.TryGetValue(type, out ConstructorInfo[] constructors) ? constructors : (constructorCache[type] = type.GetConstructors((BindingFlags)(-1)));
  public static FieldInfo[] CacheFields(Type type)
    => fieldCache.TryGetValue(type, out FieldInfo[] fields) ? fields : (fieldCache[type] = type.GetFields((BindingFlags)(-1)));
  public static MemberInfo[] CacheMembers(Type type)
    => memberCache.TryGetValue(type, out MemberInfo[] members) ? members : (memberCache[type] = type.GetMembers((BindingFlags)(-1)));

  public static MemberInfo ReflectionHelper__ResolveReflection(MemberReference mref, Module[] modules)
  {
    if (mref == null)
      return null;
    if (mref is DynamicMethodReference dynamicMethodReference)
      return dynamicMethodReference.DynamicMethod;
    TypeReference typeReference = mref.DeclaringType ?? mref as TypeReference;
    string asmName, moduleName, defName;
    switch (typeReference?.Scope)
    {
      case AssemblyNameReference assemblyNameReference:
        asmName = assemblyNameReference.CacheAssemblyName();
        moduleName = null;
        defName = string.Concat(" | ", asmName, ", NOMODULE");
        break;
      case ModuleDefinition moduleDefinition:
        asmName = moduleDefinition.Assembly.Name.CacheAssemblyName();
        moduleName = moduleDefinition.Name;
        defName = string.Concat(" | ", asmName, ", ", moduleName);
        break;
      case ModuleReference:
        asmName = typeReference.Module.Assembly.Name.CacheAssemblyName();
        moduleName = typeReference.Module.Name;
        defName = string.Concat(" | ", asmName, ", ", moduleName);
        break;
      default:
        asmName = moduleName = null;
        defName = " | NOASSEMBLY, NOMODULE";
        break;
    }
    MethodReference methodRef = mref as MethodReference;
    string mrefName = string.Concat(methodRef != null ? methodRef.GetMethodName() : mref.FullName, defName);
    if (resolveCache.TryGetValue(mrefName, out MemberInfo cachedMember))
      return cachedMember;
    if (mref is GenericParameter)
      throw new NotSupportedException("ResolveReflection on GenericParameter currently not supported");
    if (methodRef != null && mref.DeclaringType is ArrayType)
    {
      Type type = (Type)ReflectionHelper__ResolveReflection(mref.DeclaringType, modules);
      string methodID = methodRef.GetID(null, null, false, false);
      foreach (MethodInfo method in CacheMethods(type))
        if (method.GetID(null, null, false, false, false) == methodID)
          return Cache(mrefName, method);
      foreach (ConstructorInfo ctor in CacheConstructors(type))
        if (ctor.GetID(null, null, false, false, false) == methodID)
          return Cache(mrefName, ctor);
    }
    if (typeReference == null)
      throw new ArgumentException("MemberReference hasn't got a DeclaringType / isn't a TypeReference in itself");
    if (asmName == null && moduleName == null)
      throw new NotSupportedException("Unsupported scope type " + typeReference.Scope.GetType().FullName);

    bool flag2 = false, nullModules = modules == null;
    for (;;)
    {
      if (modules == null)
      {
        Assembly[] array = null;
        int num = asmName.IndexOf("@#", StringComparison.Ordinal);
        if (num != -1 && int.TryParse(asmName.Substring(num + 2), out int hash))
        {
          asmName = asmName.Substring(0, num);
          if (AssemblyUtils.assemblyHashes.TryGetValue(hash, out Assembly asm))
          {
            array = [asm];
            goto _foundAsm;
          }
        }
        if (AssemblyUtils.assemblyFullNames.TryGetValue(asmName, out List<Assembly> assemblies) || AssemblyUtils.assemblyNames.TryGetValue(asmName, out assemblies))
          array = [.. assemblies];
        else if (Assembly.Load(new AssemblyName(asmName)) is Assembly assembly2)
          array = [assembly2];
        _foundAsm:
        List<Module> moduleList = [];
        if (string.IsNullOrEmpty(moduleName))
          foreach (Assembly asm in array)
            moduleList.AddRange(asm.GetModules());
        else
          foreach (Assembly asm in array)
            if (asm.GetModule(moduleName) is Module module)
              moduleList.Add(module);
        if (moduleList.Count == 0)
          throw new Exception("Cannot resolve assembly / module " + asmName + " / " + moduleName);
        modules = [.. moduleList];
      }
      if (mref is TypeReference typeRef)
      {
        if (typeRef.Name == "<Module>")
          throw new ArgumentException("Type <Module> cannot be resolved to a runtime reflection type");
        if (mref is TypeSpecification typeSpecification)
        {
          if (ReflectionHelper__ResolveReflection(typeSpecification.ElementType, null) is not Type type)
            return null;
          if (typeSpecification is ByReferenceType)
            return Cache(mrefName, type.MakeByRefType());
          if (typeSpecification is PointerType)
            return Cache(mrefName, type.MakePointerType());
          if (typeSpecification is ArrayType arrayType)
            return Cache(mrefName, arrayType.IsVector ? type.MakeArrayType() : type.MakeArrayType(arrayType.Dimensions.Count));
          if (typeSpecification is not GenericInstanceType genericInstanceType)
            return Cache(mrefName, type);
          List<Type> types = [];
          foreach (TypeReference arg in genericInstanceType.GenericArguments)
            types.Add(ReflectionHelper__ResolveReflection(arg, null) as Type);
          return Cache(mrefName, type.MakeGenericType([.. types]));
        }
        foreach (Module module in modules)
          if (module.GetType(mref.FullName.Replace('/', '+'), false, false) is Type t)
            return Cache(mrefName, t);
        foreach (Module module in modules)
          foreach (Type t in module.GetTypes())
            if (mref.Is(t))
              return Cache(mrefName, t);
      }
      else if (mref is GenericInstanceMethod genericInstanceMethod)
      {
        if (ReflectionHelper__ResolveReflection(genericInstanceMethod.ElementMethod, modules) is MethodInfo methodInfo)
        {
          Type[] types = new Type[genericInstanceMethod.GenericArguments.Count];
          for (int i = 0; i < types.Length; ++i)
            types[i] = ReflectionHelper__ResolveReflection(genericInstanceMethod.GenericArguments[i], null) as Type;
          return Cache(mrefName, methodInfo.MakeGenericMethod([.. types]));
        }
      }
      else if (mref.DeclaringType.Name == "<Module>")
      {
        if (methodRef != null)
        {
          foreach (Module module in modules)
            foreach (MethodInfo method in module.GetMethods((BindingFlags)(-1)))
              if (mref.Is(method))
                return Cache(mrefName, method);
        }
        else if (mref is FieldReference)
        {
          foreach (Module module in modules)
            foreach (FieldInfo fieldInfo in module.GetFields((BindingFlags)(-1)))
              if (mref.Is(fieldInfo))
                return Cache(mrefName, fieldInfo);
        }
        else
          throw new NotSupportedException("Unsupported <Module> member type " + mref.GetType().FullName);
      }
      else
      {
        string mrefBaseName = mref.Name;
        Type declaringType = (Type)ReflectionHelper__ResolveReflection(mref.DeclaringType, modules);
        if (methodRef != null)
        {
          if (methodRef.Name == ".ctor")
            foreach (ConstructorInfo ctor in CacheConstructors(declaringType))
              if (ctor.GetParameters().Length == methodRef.Parameters.Count && methodRef.Is(ctor))
                return Cache(mrefName, ctor);
          foreach (MethodInfo method in CacheMethods(declaringType))
            if (method.GetParameters().Length == methodRef.Parameters.Count && mrefBaseName == method.Name && methodRef.Is(method))
              return Cache(mrefName, method);
          // original fallback
          foreach (MethodInfo method in CacheMethods(declaringType))
            if (methodRef.Is(method))
              return Cache(mrefName, method);
          foreach (ConstructorInfo ctor in CacheConstructors(declaringType))
            if (methodRef.Is(ctor))
              return Cache(mrefName, ctor);
        }
        else if (mref is FieldReference)
        {
          if (declaringType.GetField(mrefBaseName, BFlags.anyDeclared) is FieldInfo fieldInfo && mref.Is(fieldInfo))
            return Cache(mrefName, fieldInfo);
          // original fallback ; half of all .Is overhead
          foreach (FieldInfo field in CacheFields(declaringType))
            if (mref.Is(field))
              return Cache(mrefName, field);
        }
        else
        {
          foreach (MemberInfo member in CacheMembers(declaringType))
            if (mref.Is(member))
              return Cache(mrefName, member);
        }
      }
      if (flag2 || nullModules)
        return null;
      flag2 = true;
      modules = null;
    }
  }

  public static void EmitByte(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (byte)arg);
  public static void EmitSByte(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (sbyte)arg);
  public static void EmitShort(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (short)arg);
  public static void EmitInt(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (int)arg);
  public static void EmitLong(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (long)arg);
  public static void EmitFloat(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (float)arg);
  public static void EmitDouble(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (double)arg);
  public static void EmitString(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (string)arg);
  public static void EmitLabel(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (Label)arg);
  public static void EmitSwitch(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (Label[])arg);
  public static void EmitLocalBuilder(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (LocalBuilder)arg);
  public static void EmitSignatureHelper(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (SignatureHelper)arg);
  public static void EmitType(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (Type)arg);
  public static void EmitMethod(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (MethodInfo)arg);
  public static void EmitConstructor(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (ConstructorInfo)arg);
  public static void EmitField(ILGenerator self, OpCode opcode, object arg) => self.Emit(opcode, (FieldInfo)arg);

  public static object ILGeneratorShimExt_DynEmit(ILGenerator il, OpCode opcode, object operand)
  {
    Type operandType = operand.GetType();
    if (emitters.TryGetValue(operandType, out Action<ILGenerator, OpCode, object> emitter))
    {
      emitter(il, opcode, operand);
      return null;
    }
    else
      foreach (KeyValuePair<Type, Action<ILGenerator, OpCode, object>> kvp in emitters)
        if (kvp.Key.IsAssignableFrom(operandType))
        {
          kvp.Value(il, opcode, operand);
          return null;
        }
    throw new InvalidOperationException("<gsl> Unexpected unemittable operand type " + operandType.FullName);
  }
}