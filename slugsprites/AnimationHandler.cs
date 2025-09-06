using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.ConverterExt;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static slugsprites.AnimationColor;
using static slugsprites.LogWrapper;

namespace slugsprites;

public class AnimationHandler
{
  public delegate Animation AnimationNewD(Dictionary<string, object> animation);
  public delegate AnimationColor.BaseColorHandler ColorHandlerNewD(Dictionary<string, object> animation);
  /// <summary>
  /// Event, used to add custom ExtEnums
  /// </summary>
  public static event Action OnInitialize;

  /// <summary>
  /// Managed animations
  /// </summary>
  public static Dictionary<string, Animation> animations = new();
  /// <summary>
  /// Constructors of managed animations
  /// </summary>
  public static Dictionary<AnimationType, AnimationNewD> animationCtors = new();
  /// <summary>
  /// Constructors of managed color handlers
  /// </summary>
  public static Dictionary<AnimationColor.Subtype, ColorHandlerNewD> colorHandlerCtors = new();

  public static void Initialize()
  {
    animationCtors[AnimationType.Color] = animation => new AnimationColor(animation);
    animationCtors[AnimationType.Pos] = animation => new AnimationPos(animation);
    animationCtors[AnimationType.Element] = animation => new AnimationElement(animation);

    colorHandlerCtors[AnimationColor.Subtype.RandomPulse] = animation => new AnimationColor.RandomPulse(animation);
    colorHandlerCtors[AnimationColor.Subtype.Wave] = animation => new AnimationColor.Wave(animation);

    OnInitialize?.Invoke();
  }
  
  public static void LoadAnimations()
  {
    try
    {
      Log.LogInfo("[*] Loading animations");
      animations.Clear();

      List<string> paths = HandlerUtils.ListDirectoryE("slugsprites/animations", out FileUtils.Result opResult);
      if (opResult != FileUtils.Result.Success)
        return;

      int failureCounter = 0;
      foreach (string path in paths)
      {
        Log.LogInfo($" Reading at: {path}");
        foreach (KeyValuePair<string, object> animationData in Json.Parser.Parse(File.ReadAllText(path)) as Dictionary<string, object>)
        {
          Log.LogInfo($"<> Loading animation \"{animationData.Key}\"");
          try
          {
            if (animations.ContainsKey(animationData.Key))
              throw new Exception($"animation with name \"{animationData.Key}\" already exists");
            Dictionary<string, object> animationDictionary = animationData.Value as Dictionary<string, object>;
            if (!animationDictionary.ContainsKey("type"))
              throw new Exception($"animation misses \"type\" field");
            if (!animationDictionary.TryGetExtEnum("type", out AnimationType animationType))
              throw new Exception($"field \"type\" in animation is neither of supported animation types - if you've defined custom animation type, you have to register its AnimationType ExtEnum");

            if (!animationCtors.TryGetValue(animationType, out AnimationNewD ctor))
              throw new Exception($"no delegate for animation of that type was stored - subcribe your method, storing ctor, to AnimationHandler.OnInitialize");
            Animation animation = ctor(animationData.Value as Dictionary<string, object>);
            animation.name = animationData.Key;
            animation.type = animationType;
            animations[animationData.Key] = animation;
          }
          catch (Exception ei)
          {
            Log.LogError($"Failed to load animation \"{animationData.Key}\": {ei}");
            ++failureCounter;
          }
        }
      }
      Log.LogInfo($"[+] Finished loading animations: {animations.Count}/{animations.Count + failureCounter} animations were successfully loaded");
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}

public class AnimationType : ExtEnum<AnimationType>
{
  public AnimationType(string value, bool register = false)
  : base(value, register) { }

  public static AnimationType
    Color = new("Color", true),
    Pos = new("Pos", true),
    Element = new("Element", true);
}

/// <summary>
/// Base for other animation handlers
/// </summary>
public abstract class Animation
{
  public int tick = 0, ticksPerUpdate = 1;
  public string name;
  public AnimationType type;

  public Animation() { }

  /// <summary>
  /// Constructor, called when new animation template is being created via animation file
  /// </summary>
  public Animation(Dictionary<string, object> animation)
  {
    animation.TryUpdateNumber("ticksPerUpdate", ref ticksPerUpdate);
    if (ticksPerUpdate < 1)
      throw new Exception($"\"ticksPerUpdate\" can't be lower than 1");
  }

  /// <summary>
  /// Method, used to make defined properties unique for assigned animation
  /// </summary>
  public abstract Animation Clone();

  /// <summary>
  /// Method, called on OnInitiateSprites call during animation initiation(use it to define specific properties, that can't be defined in constructor)
  /// </summary>
  public abstract void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData);

  /// <summary>
  /// Method, used to update animation(called on each DrawSprites call)
  /// </summary>
  public abstract void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData);

  /// <summary>
  /// Copies base fields to target animation
  /// </summary>
  public void CopyBase(Animation target)
  {
    target.ticksPerUpdate = ticksPerUpdate;
    target.name = name;
    target.type = type;
  }
}

public class AnimationColor : Animation
{
  #region ColorModifier
  public class ColorModifierType : ExtEnum<ColorModifierType>
  {
    public ColorModifierType(string value, bool register = false)
    : base(value, register) { }

    public static ColorModifierType
      HSV1 = new("HSV1", true),
      HSVB = new("HSVB", true),
      HSL1 = new("HSL1", true),
      HSLB = new("HSLB", true);
  }

  public class ColorModifier
  {
    /// <summary>
    /// Caches color before it is added to cached colors
    /// </summary>
    public Color color;
    public float[] modifiers = new float[3];
    public ColorModifierType type;

    public ColorModifier(List<object> modifierData)
    {
      if (modifierData.Count != 4)
        throw new Exception($"incorrect size of color modifier");
      if (!modifierData.TryGetExtEnum(0, out type))
        throw new Exception($"color modifier with type \"{modifierData[0]}\" doesn't exist");
      for (int i = 1; i < 4; ++i)
        modifierData.UpdateNumber(i, ref modifiers[i - 1]);
    }

    public ColorModifier(ColorModifier other)
    {
      color = other.color;
      modifiers = (float[])other.modifiers.Clone();
      type = other.type;
    }

    public void ApplyHSV(Color baseColor)
    {
      Color.RGBToHSV(baseColor, out float h, out float s, out float v);
      if (baseColor.r == baseColor.g && baseColor.g == baseColor.b && s == 0f)
        s = v;
      color = Color.HSVToRGB((h + modifiers[0]) % 1f, Mathf.Clamp(s * modifiers[1], 0f, 1f), Mathf.Clamp(v * modifiers[2], 0f, 1f));
    }

    public void ApplyHSL(Color baseColor)
    {
      Vector3 HSLColor = Custom.RGB2HSL(baseColor);
      color = Custom.HSL2RGB((HSLColor[0] + modifiers[0]) % 1f, Mathf.Clamp(HSLColor[1] * modifiers[1], 0f, 1f), Mathf.Clamp(HSLColor[2] * modifiers[2], 0f, 1f));
    }

    public static void ProcessModifiers(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, ColorModifier[] modifiers)
    {
      ColorModifier colorModifier1 = modifiers[0];
      Color baseColor = sprites.colors[spriteData.colorIndex];
      if (colorModifier1.type == ColorModifierType.HSV1 || colorModifier1.type == ColorModifierType.HSVB)
        colorModifier1.ApplyHSV(baseColor);
      else if (colorModifier1.type == ColorModifierType.HSL1 || colorModifier1.type == ColorModifierType.HSLB)
        colorModifier1.ApplyHSL(baseColor);
      for (int i = 1; i < modifiers.Length; ++i)
      {
        ColorModifier colorModifier = modifiers[i];
        if (colorModifier.type == ColorModifierType.HSV1)
          colorModifier.ApplyHSV(modifiers[i - 1].color);
        else if (colorModifier.type == ColorModifierType.HSVB)
          colorModifier.ApplyHSV(baseColor);
        else if (colorModifier.type == ColorModifierType.HSL1)
          colorModifier.ApplyHSL(modifiers[i - 1].color);
        else if (colorModifier.type == ColorModifierType.HSLB)
          colorModifier.ApplyHSL(baseColor);
      }
    }
  }
  #endregion
  #region BaseColorHandler
  public class Subtype : ExtEnum<Subtype>
  {
    public Subtype(string value, bool register = false)
    : base(value, register) { }

    public static Subtype
      RandomPulse = new("RandomPulse", true),
      Wave = new("Wave", true);
  }

  public abstract class BaseColorHandler
  {
    /// <summary>
    /// Current animation stage
    /// </summary>
    public int currentIndex = 0;

    public BaseColorHandler() { }

    /// <summary>
    /// Constructor called, when new color handler is created via animation from animation file
    /// </summary>
    public BaseColorHandler(Dictionary<string, object> animation) { }

    /// <summary>
    /// Method, used to make defined properties unique for assigned color handler
    /// </summary>
    public abstract BaseColorHandler Clone();

    /// <summary>
    /// Method, called on OnInitiateSprites call during animation initiation(use it to define specific properties, that can't be defined in constructor)
    /// </summary>
    public abstract void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner);

    /// <summary>
    /// Method, used to update color handler(called on each DrawSprites call)
    /// </summary>
    public abstract void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData);
  }
  #endregion
  #region Animation subtype - RandomPulse
  public class RandomPulse : BaseColorHandler
  {
    public int delay = 5, chanceBase = 2, chanceRange = 100,
      currentDelay = 0;
    public float delayMultiplier = 1f;

    public RandomPulse() { }

    public RandomPulse(Dictionary<string, object> animation)
    : base(animation)
    {
      animation.TryUpdateNumber("delay", ref delay);
      animation.TryUpdateNumber("delayMultiplier", ref delayMultiplier);
      animation.TryUpdateNumber("chanceBase", ref chanceBase);
      animation.TryUpdateNumber("chanceRange", ref chanceRange);
    }

    public override BaseColorHandler Clone()
    {
      RandomPulse newHandler = new()
      {
        delay = delay,
        delayMultiplier = delayMultiplier,
        chanceBase = chanceBase,
        chanceRange = chanceRange
      };
      return newHandler;
    }

    public override void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner) { }

    public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
    {
      AnimationColor animation = spriteData.animationColor;
      if (currentIndex == 0)
      {
        if (UnityEngine.Random.Range(0, chanceRange) < chanceBase)
          currentIndex = 1;
      }
      else if (currentDelay < delay * Math.Pow(delayMultiplier, currentIndex))
        ++currentDelay;
      else
      {
        currentIndex = (currentIndex + 1) % animation.cachedColors.Length;
        currentDelay = 0;
      }

      sLeaser.sprites[spriteData.realIndex].color = animation.cachedColors[currentIndex];
    }
  }
  #endregion
  #region Animation subtype - Wave
  public class Wave : BaseColorHandler
  {
    public bool randomOffset = false, equalOffset = false;
    public int currentEqualOffsetIndex = 0;
    public float transitionSpeed = 0.01f, transitionOffset = 0f, equalOffsetMultiplier = 1f,
      currentTransition = 0f;

    public Wave() { }

    public Wave(Dictionary<string, object> animation)
    : base(animation)
    {
      animation.TryUpdateNumber("transitionSpeed", ref transitionSpeed);
      animation.TryUpdateNumber("transitionOffset", ref transitionOffset);
      animation.TryUpdateValueWithType("equalOffset", ref equalOffset);
      animation.TryUpdateValueWithType("randomOffset", ref randomOffset);

      currentTransition = transitionOffset;
    }

    public override BaseColorHandler Clone()
    {
      Wave newHandler = new()
      {
        transitionSpeed = transitionSpeed,
        transitionOffset = transitionOffset,
        currentTransition = currentTransition,
        equalOffset = equalOffset,
        randomOffset = randomOffset,
      };
      if (randomOffset)
        newHandler.currentTransition = UnityEngine.Random.Range(0f, 1f);
      return newHandler;
    }

    public override void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner)
    {
      if (!equalOffset)
        return;

      Wave originalWave = (AnimationHandler.animations[owner.name] as AnimationColor).handler as Wave;

      if (currentTransition == 0f && originalWave.equalOffsetMultiplier != 1f)
      {
        currentTransition = ++originalWave.currentEqualOffsetIndex * originalWave.equalOffsetMultiplier;
        return;
      }

      int animationAmount = 0;
      foreach (List<SlugSpriteData> spriteList in sprites.additionalSprites)
        if (spriteList != null)
          foreach (SlugSpriteData sprite in spriteList)
            if (sprite.animationColor?.name == owner.name)
              ++animationAmount;
      originalWave.equalOffsetMultiplier = 1f / animationAmount;
    }

    public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
    {
      Color[] cachedColors = spriteData.animationColor.cachedColors;
      currentTransition += transitionSpeed;
      if (currentTransition > 1f)
      {
        currentIndex = (currentIndex + 1) % cachedColors.Length;
        currentTransition = 0f;
      }

      Color color1 = cachedColors[currentIndex], color2 = cachedColors[(currentIndex + 1) % cachedColors.Length];
      sLeaser.sprites[spriteData.realIndex].color = color1 + (color2 - color1) * currentTransition;
    }
  }
  #endregion

  public ColorModifier[] colorModifiers;
  public Color[] cachedColors;
  public Subtype subtype;
  public BaseColorHandler handler;

  public AnimationColor() { }

  public AnimationColor(Dictionary<string, object> animation)
    : base(animation)
  {
    if (!animation.TryGetExtEnum("subtype", out subtype))
      throw new Exception($"field \"subtype\" doesn't exist or neither of supported color animation subtypes - if you've defined custom color animation subtype, you have to register its AnimationColor.Subtype ExtEnum");
    if (!AnimationHandler.colorHandlerCtors.TryGetValue(subtype, out AnimationHandler.ColorHandlerNewD ctor))
      throw new Exception($"no delegate for color handler of that type was stored - subcribe your method, storing ctor, to AnimationHandler.OnInitialize");
    handler = ctor(animation);

    if (!animation.TryGetValueWithType("colorModifiers", out List<object> modifiers))
      throw new Exception($"field \"colorModifiers\" doesn't exist or isn't array");
    colorModifiers = new ColorModifier[modifiers.Count];
    for (int i = 0; i < modifiers.Count; ++i)
      try
      {
        colorModifiers[i] = new(modifiers[i] as List<object>);
      }
      catch (Exception e)
      {
        throw new Exception($"failed to parse color modifer {i}: {e}");
      }
  }

  public override Animation Clone()
  {
    AnimationColor newAnimation = new();
    CopyBase(newAnimation);
    newAnimation.colorModifiers = new ColorModifier[colorModifiers.Length];
    for (int i = 0; i < colorModifiers.Length; ++i)
      newAnimation.colorModifiers[i] = new(colorModifiers[i]);
    newAnimation.subtype = subtype;
    newAnimation.handler = handler.Clone();
    return newAnimation;
  }

  public override void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
  {
    ColorModifier.ProcessModifiers(self, sLeaser, sprites, spriteData, colorModifiers);

    cachedColors = new Color[colorModifiers.Length];
    for (int i = 0; i < colorModifiers.Length; ++i)
      cachedColors[i] = colorModifiers[i].color;

    handler.Initialize(self, sLeaser, sprites, spriteData, this);
  }

  public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
  {
    handler.Update(self, sLeaser, rCam, timeStacker, camPos, sprites, spriteData);
  }
}

public class AnimationPos : Animation
{
  public AnimationPos() { }

  public AnimationPos(Dictionary<string, object> animation)
    : base(animation)
  {

  }

  public override Animation Clone()
  {
    AnimationPos newAnimation = new();
    CopyBase(newAnimation);

    return newAnimation;
  }

  public override void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
  {

  }

  public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
  {

  }
}

public class AnimationElement : Animation
{
  public AnimationElement() { }

  public AnimationElement(Dictionary<string, object> animation)
    : base(animation)
  {

  }

  public override Animation Clone()
  {
    AnimationElement newAnimation = new();
    CopyBase(newAnimation);

    return newAnimation;
  }

  public override void Initialize(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
  {

  }

  public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
  {

  }
}
