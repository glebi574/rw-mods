using gelbi_silly_lib.MonoModUtils;
using gelbi_silly_lib.ReflectionUtils;
using Mono.Cecil;
using Mono.Collections.Generic;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace gelbi_silly_lib.OptimizedImplementation;

public static class SillyOptimizations
{
  static SillyOptimizations()
  {
    typeof(ImplMonoMod).RunClassConstructor();
    DetourUtils.newNativeDetour(typeof(MonoMod.Utils.Extensions).GetMethod("Is", [typeof(MemberReference), typeof(MemberInfo)]), ImplMonoMod.Extensions_Is);
    DetourUtils.newNativeDetour(typeof(ReflectionHelper), "_ResolveReflection", ImplMonoMod.ReflectionHelper__ResolveReflection);
  }
}

public static class ImplMonoMod
{
  public static Dictionary<string, WeakReference> AssemblyCache = (Dictionary<string, WeakReference>)typeof(ReflectionHelper).GetField("AssemblyCache", BFlags.anyDeclaredStatic).GetValue(null),
    ResolveReflectionCache = (Dictionary<string, WeakReference>)typeof(ReflectionHelper).GetField("ResolveReflectionCache", BFlags.anyDeclaredStatic).GetValue(null);
  public static Dictionary<string, WeakReference[]> AssembliesCache = (Dictionary<string, WeakReference[]>)typeof(ReflectionHelper).GetField("AssembliesCache", BFlags.anyDeclaredStatic).GetValue(null);
  public static Func<string, MemberInfo, MemberInfo> _Cache = (Func<string, MemberInfo, MemberInfo>)typeof(ReflectionHelper).GetMethod("_Cache", BFlags.anyDeclaredStatic).CreateDelegate(typeof(Func<string, MemberInfo, MemberInfo>));

  public static bool Extensions_Is(MemberReference mref, MemberInfo minfo)
  {
    if (mref == null)
      return false;
    TypeReference typeReference = mref.DeclaringType;
    if (typeReference?.Name == "<Module>")
      typeReference = null;
    if (mref is GenericParameter genericParameter)
    {
      if (minfo is Type type)
        return type.IsGenericParameter ? genericParameter.Position == type.GenericParameterPosition : (genericParameter.Owner as IGenericInstance)?.GenericArguments[genericParameter.Position].Is(type) ?? false;
      return false;
    }
    else
    {
      if (minfo.DeclaringType != null)
      {
        if (typeReference == null)
          return false;
        Type type2 = minfo.DeclaringType;
        if (minfo is Type && type2.IsGenericType && !type2.IsGenericTypeDefinition)
          type2 = type2.GetGenericTypeDefinition();
        if (!typeReference.Is(type2))
          return false;
      }
      else if (typeReference != null)
        return false;
      if (mref is not TypeSpecification && mref.Name != minfo.Name)
        return false;
      if (mref is TypeReference typeReference2)
      {
        if (minfo is not Type type3 || type3.IsGenericParameter)
          return false;
        if (mref is GenericInstanceType genericInstanceType)
        {
          if (!type3.IsGenericType)
            return false;
          Collection<TypeReference> genericArguments = genericInstanceType.GenericArguments;
          Type[] genericArguments2 = type3.GetGenericArguments();
          if (genericArguments.Count != genericArguments2.Length)
            return false;
          for (int i = 0; i < genericArguments2.Length; ++i)
            if (!genericArguments[i].Is(genericArguments2[i]))
              return false;
          return genericInstanceType.ElementType.Is(type3.GetGenericTypeDefinition());
        }
        else
        {
          if (typeReference2.HasGenericParameters)
          {
            if (!type3.IsGenericType)
              return false;
            Collection<GenericParameter> genericParameters = typeReference2.GenericParameters;
            Type[] genericArguments3 = type3.GetGenericArguments();
            if (genericParameters.Count != genericArguments3.Length)
              return false;
            for (int i = 0; i < genericArguments3.Length; ++i)
              if (!genericParameters[i].Is(genericArguments3[i]))
                return false;
          }
          else if (type3.IsGenericType)
            return false;
          if (mref is ArrayType arrayType)
            return type3.IsArray && arrayType.Dimensions.Count == type3.GetArrayRank() && arrayType.ElementType.Is(type3.GetElementType());
          if (mref is ByReferenceType byReferenceType)
            return type3.IsByRef && byReferenceType.ElementType.Is(type3.GetElementType());
          if (mref is PointerType pointerType)
            return type3.IsPointer && pointerType.ElementType.Is(type3.GetElementType());
          if (mref is TypeSpecification typeSpecification)
            return typeSpecification.ElementType.Is(type3.HasElementType ? type3.GetElementType() : type3);
          if (typeReference != null)
            return mref.Name == type3.Name;
          return mref.FullName == type3.FullName.Replace('+', '/');
        }
      }
      else
      {
        if (minfo is Type)
          return false;
        if (mref is not MethodReference methodRef)
          return minfo is not MethodInfo && mref is FieldReference == minfo is FieldInfo && mref is PropertyReference == minfo is PropertyInfo && mref is EventReference == minfo is EventInfo;
        if (minfo is not MethodBase methodBase)
          return false;
        Collection<ParameterDefinition> parameters = methodRef.Parameters;
        ParameterInfo[] parameters2 = methodBase.GetParameters();
        if (parameters.Count != parameters2.Length)
          return false;
        if (mref is GenericInstanceMethod genericInstanceMethod)
        {
          if (!methodBase.IsGenericMethod)
            return false;
          Collection<TypeReference> genericArguments5 = genericInstanceMethod.GenericArguments;
          Type[] genericArguments6 = methodBase.GetGenericArguments();
          if (genericArguments5.Count != genericArguments6.Length)
            return false;
          for (int i = 0; i < genericArguments6.Length; ++i)
            if (!genericArguments5[i].Is(genericArguments6[i]))
              return false;
          return genericInstanceMethod.ElementMethod.Is(((methodBase as MethodInfo)?.GetGenericMethodDefinition()) ?? methodBase);
        }
        if (methodRef.HasGenericParameters)
        {
          if (!methodBase.IsGenericMethod)
            return false;
          Collection<GenericParameter> genericParameters2 = methodRef.GenericParameters;
          Type[] genericArguments4 = methodBase.GetGenericArguments();
          if (genericParameters2.Count != genericArguments4.Length)
            return false;
          for (int i = 0; i < genericArguments4.Length; ++i)
            if (!genericParameters2[i].Is(genericArguments4[i]))
              return false;
        }
        else if (methodBase.IsGenericMethod)
          return false;
        IMetadataTokenProvider relinker(IMetadataTokenProvider paramMemberRef, IGenericParameterProvider ctx)
        {
          if (paramMemberRef is not TypeReference typeReference3)
            return paramMemberRef;
          if (typeReference3 is not GenericParameter genericParameter)
          {
            if (typeReference3 == methodRef.DeclaringType.GetElementType())
              return methodRef.DeclaringType;
            return typeReference3;
          }
          if (genericParameter.Owner is MethodReference && methodRef is GenericInstanceMethod genericInstanceMethod)
            return genericInstanceMethod.GenericArguments[genericParameter.Position];
          if (genericParameter.Owner is TypeReference typeReference && methodRef.DeclaringType is GenericInstanceType genericInstanceType && typeReference.FullName == genericInstanceType.ElementType.FullName)
            return genericInstanceType.GenericArguments[genericParameter.Position];
          return typeReference3;
        }
        if (methodRef.ReturnType.Relink(relinker, null).Is((methodBase as MethodInfo)?.ReturnType ?? typeof(void))
         || methodRef.ReturnType.Is((methodBase as MethodInfo)?.ReturnType ?? typeof(void)))
        {
          for (int i = 0; i < parameters2.Length; ++i)
            if (!parameters[i].ParameterType.Relink(relinker, null).Is(parameters2[i].ParameterType) && !parameters[i].ParameterType.Is(parameters2[i].ParameterType))
              return false;
          return true;
        }
        return false;
      }
    }
  }

  public static MemberInfo ReflectionHelper__ResolveReflection(MemberReference mref, Module[] modules)
  {
    if (mref == null)
      return null;
    if (mref is DynamicMethodReference dynamicMethodReference)
      return dynamicMethodReference.DynamicMethod;
    TypeReference typeReference;
    if ((typeReference = mref.DeclaringType) == null)
      typeReference = mref as TypeReference;
    IMetadataScope metadataScope = typeReference?.Scope;
    string asmName;
    string moduleName;
    if (metadataScope is not AssemblyNameReference assemblyNameReference)
    {
      if (metadataScope is not ModuleDefinition moduleDefinition)
      {
        if (metadataScope is not ModuleReference)
        {
          asmName = null;
          moduleName = null;
        }
        else
        {
          asmName = typeReference.Module.Assembly.Name.GetRuntimeHashedFullName();
          moduleName = typeReference.Module.Name;
        }
      }
      else
      {
        asmName = moduleDefinition.Assembly.Name.GetRuntimeHashedFullName();
        moduleName = moduleDefinition.Name;
      }
    }
    else
    {
      asmName = assemblyNameReference.GetRuntimeHashedFullName();
      moduleName = null;
    }
    string text = string.Concat([(mref as MethodReference)?.GetID(null, null, true, false) ?? mref.FullName, " | ", asmName ?? "NOASSEMBLY", ", ", moduleName ?? "NOMODULE"]);
    Dictionary<string, WeakReference> dictionary = ResolveReflectionCache;
    lock (dictionary)
      if (ResolveReflectionCache.TryGetValue(text, out WeakReference weakReference) && weakReference?.SafeGetTarget() is MemberInfo memberInfo)
        return memberInfo;
    if (mref is GenericParameter)
      throw new NotSupportedException("ResolveReflection on GenericParameter currently not supported");
    Type type;
    if (mref is MethodReference methodReference2 && mref.DeclaringType is ArrayType)
    {
      type = ReflectionHelper__ResolveReflection(mref.DeclaringType, modules) as Type;
      string methodID = methodReference2.GetID(null, null, false, false);

      MethodBase methodBase = null;
      foreach (MethodInfo method in type.GetMethods((BindingFlags)(-1)))
        if (method.GetID(null, null, false, false, false) == methodID)
        {
          methodBase = method;
          break;
        }
      if (methodBase == null)
        foreach (ConstructorInfo ctor in type.GetConstructors((BindingFlags)(-1)))
          if (ctor.GetID(null, null, false, false, false) == methodID)
          {
            methodBase = ctor;
            break;
          }
      if (methodBase != null)
        return _Cache(text, methodBase);
    }
    if (typeReference == null)
      throw new ArgumentException("MemberReference hasn't got a DeclaringType / isn't a TypeReference in itself");
    if (asmName == null && moduleName == null)
      throw new NotSupportedException("Unsupported scope type " + typeReference.Scope.GetType().FullName);

    bool flag = true, flag2 = false, flag3 = false;
    TypeSpecification typeSpecification;
    MemberInfo memberInfo2 = null;
    for (; ; )
    {
      if (flag3)
        modules = null;
      flag3 = true;
      if (modules == null)
      {
        Assembly[] array = null;
        if (flag && flag2)
        {
          flag2 = false;
          flag = false;
        }
        if (flag)
        {
          dictionary = AssemblyCache;
          lock (dictionary)
            if (AssemblyCache.TryGetValue(asmName, out WeakReference weakReference2) && weakReference2.SafeGetTarget() is Assembly assembly)
              array = [assembly];
        }
        if (array == null && !flag2)
        {
          Dictionary<string, WeakReference[]> dictionary2 = AssembliesCache;
          lock (dictionary2)
          {
            if (AssembliesCache.TryGetValue(asmName, out WeakReference[] array2))
            {
              List<Assembly> assemblies = [];
              foreach (WeakReference asmRef in array2)
                if (asmRef.SafeGetTarget() is Assembly asm)
                  assemblies.Add(asm);
              array = [.. assemblies];
            }
          }
        }
        if (array == null)
        {
          int num = asmName.IndexOf(ReflectionHelper.AssemblyHashNameTag, StringComparison.Ordinal);
          if (num != -1 && int.TryParse(asmName.Substring(num + 2), out int hash))
          {
            List<Assembly> assemblies = [];
            foreach (Assembly other in AppDomain.CurrentDomain.GetAssemblies())
              if (other.GetHashCode() == hash)
                assemblies.Add(other);
            if (assemblies.Count != 0)
              array = [.. assemblies];
            asmName = asmName.Substring(0, num);
          }
          if (array == null)
          {
            List<Assembly> assemblies = [];
            foreach (Assembly other in AppDomain.CurrentDomain.GetAssemblies())
              if (other.GetName().FullName == asmName)
                assemblies.Add(other);
            if (assemblies.Count == 0)
            {
              foreach (Assembly other in AppDomain.CurrentDomain.GetAssemblies())
                if (other.GetName().Name == asmName)
                  assemblies.Add(other);
              if (assemblies.Count != 0)
                array = [.. assemblies];
            }
            else
              array = [.. assemblies];
            if (array.Length == 0 && Assembly.Load(new AssemblyName(asmName)) is Assembly assembly2)
              array = [assembly2];
          }
          if (array.Length != 0)
          {
            Dictionary<string, WeakReference[]> dictionary2 = AssembliesCache;
            lock (dictionary2)
            {
              WeakReference[] references = new WeakReference[array.Length];
              for (int i = 0; i < array.Length; ++i)
                references[i] = new(array[i]);
              AssembliesCache[asmName] = references;
            }
          }
        }
        List<Module> moduleList = [];
        if (string.IsNullOrEmpty(moduleName))
          foreach (Assembly asm in array)
            moduleList.AddRange(asm.GetModules());
        else
          foreach (Assembly asm in array)
            if (asm.GetModule(moduleName) is Module module)
              moduleList.Add(module);
        if (moduleList.Count == 0)
          break;
        modules = [.. moduleList];
      }
      if (mref is TypeReference typeReference3)
      {
        if (typeReference3.Name == "<Module>")
          throw new ArgumentException("Type <Module> cannot be resolved to a runtime reflection type");
        typeSpecification = mref as TypeSpecification;
        if (typeSpecification != null)
          goto Block_46;
        type = null;
        foreach (Module module in modules)
          if (module.GetType(mref.FullName.Replace('/', '+'), false, false) is Type t)
          {
            type = t;
            break;
          }
        if (type == null)
        {
          foreach (Module module in modules)
            foreach (Type t in module.GetTypes())
              if (mref.Is(t))
              {
                type = t;
                goto _j2e;
              }
        }
      _j2e:
        if (type != null || flag2)
          goto IL_071D;
      }
      else
      {
        if (mref is GenericInstanceMethod genericInstanceMethod)
        {
          if (ReflectionHelper__ResolveReflection(genericInstanceMethod.ElementMethod, modules) is MethodInfo methodInfo)
          {
            List<Type> types = [];
            foreach (TypeReference arg in genericInstanceMethod.GenericArguments)
              types.Add(ReflectionHelper__ResolveReflection(arg, null) as Type);
            memberInfo2 = methodInfo.MakeGenericMethod([.. types]);
          }
          else
            memberInfo2 = null;
        }
        else if (mref.DeclaringType.Name == "<Module>")
        {
          if (mref is MethodReference)
          {
            memberInfo2 = null;
            foreach (Module module in modules)
              foreach (MethodInfo method in module.GetMethods((BindingFlags)(-1)))
                if (mref.Is(method))
                {
                  memberInfo2 = method;
                  goto _j1e;
                }
          }
          else if (mref is FieldReference)
          {
            memberInfo2 = null;
            foreach (Module module in modules)
              foreach (FieldInfo fieldInfo in module.GetFields((BindingFlags)(-1)))
                if (mref.Is(fieldInfo))
                {
                  memberInfo2 = fieldInfo;
                  goto _j1e;
                }
          }
          else
            throw new NotSupportedException("Unsupported <Module> member type " + mref.GetType().FullName);
        }
        else
        {
          Type type2 = ReflectionHelper__ResolveReflection(mref.DeclaringType, modules) as Type;
          memberInfo2 = null;
          if (mref is MethodReference)
          {
            foreach (MethodInfo method in type2.GetMethods((BindingFlags)(-1)))
              if (mref.Is(method))
              {
                memberInfo2 = method;
                break;
              }
            if (memberInfo2 == null)
              foreach (ConstructorInfo ctor in type2.GetConstructors((BindingFlags)(-1)))
                if (mref.Is(ctor))
                {
                  memberInfo2 = ctor;
                  break;
                }
          }
          else if (mref is FieldReference)
          {
            foreach (FieldInfo field in type2.GetFields((BindingFlags)(-1)))
              if (mref.Is(field))
              {
                memberInfo2 = field;
                break;
              }
          }
          else
          {
            foreach (MemberInfo member in type2.GetMembers((BindingFlags)(-1)))
              if (mref.Is(member))
              {
                memberInfo2 = member;
                break;
              }
          }
        }
      _j1e:
        if (memberInfo2 != null || flag2)
          return _Cache(text, memberInfo2);
      }
      flag2 = true;
    }
    throw new Exception("Cannot resolve assembly / module " + asmName + " / " + moduleName);
  Block_46:
    type = ReflectionHelper__ResolveReflection(typeSpecification.ElementType, null) as Type;
    if (type == null)
      return null;
    if (typeSpecification.IsByReference)
      return _Cache(text, type.MakeByRefType());
    if (typeSpecification.IsPointer)
      return _Cache(text, type.MakePointerType());
    if (typeSpecification.IsArray)
      return _Cache(text, (typeSpecification as ArrayType).IsVector ? type.MakeArrayType() : type.MakeArrayType((typeSpecification as ArrayType).Dimensions.Count));
    if (typeSpecification.IsGenericInstance)
    {
      List<Type> types = [];
      foreach (TypeReference arg in ((GenericInstanceType)typeSpecification).GenericArguments)
        types.Add(ReflectionHelper__ResolveReflection(arg, null) as Type);
      return _Cache(text, type.MakeGenericType([.. types]));
    }
  IL_071D:
    return _Cache(text, type);
  }
}