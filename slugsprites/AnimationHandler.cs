using gelbi_silly_lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static slugsprites.LogWrapper;

namespace slugsprites
{
  public class AnimationHandler
  {
    public delegate Animation AnimationNewD(Dictionary<string, object> animation);
    public delegate AnimationColor.BaseColorHandler ColorHandlerNewD(Dictionary<string, object> animation);
    public static event Action OnInitiate;

    public static Dictionary<string, Animation> animations = new();
    public static Dictionary<Animation.Type, AnimationNewD> animationCtors = new();
    public static Dictionary<AnimationColor.Subtype, ColorHandlerNewD> colorHandlerCtors = new();

    public static void Initiate()
    {
      animationCtors[Animation.Type.Color] = animation => new AnimationColor(animation);
      animationCtors[Animation.Type.Pos] = animation => new AnimationPos(animation);
      animationCtors[Animation.Type.Element] = animation => new AnimationElement(animation);

      colorHandlerCtors[AnimationColor.Subtype.RandomPulse] = animation => new AnimationColor.RandomPulse(animation);
      colorHandlerCtors[AnimationColor.Subtype.Wave] = animation => new AnimationColor.Wave(animation);

      OnInitiate?.Invoke();
    }
    
    public static void LoadAnimations()
    {
      try
      {
        Log.LogInfo("Loading animations");
        animations.Clear();
        foreach (string path in AssetManager.ListDirectory("slugsprites/animations"))
        {
          Log.LogInfo($"Reading at: {path}");
          foreach (KeyValuePair<string, object> animationData in Json.Parser.Parse(File.ReadAllText(path)) as Dictionary<string, object>)
          {
            Log.LogInfo($"Loading animation \"{animationData.Key}\"");
            try
            {
              if (animations.ContainsKey(animationData.Key))
                throw new Exception($"animation with name \"{animationData.Key}\" already exists");
              Dictionary<string, object> animationDictionary = animationData.Value as Dictionary<string, object>;
              if (!animationDictionary.ContainsKey("type"))
                throw new Exception($"animation misses \"type\" field");
              if (!animationDictionary.TryGetExtEnum("type", out Animation.Type animationType))
                throw new Exception($"field \"type\" in animation is neither of supported animation types");

              Animation animation = null;
              if (!animationCtors.TryGetValue(animationType, out AnimationNewD ctor))
                throw new Exception($"no delegate for animation of that type was stored - hook AnimationHandler.OnInitiate and store it with your method");
              animation = ctor(animationData.Value as Dictionary<string, object>);
              animation.name = animationData.Key;
              animation.type = animationType;
              animations[animationData.Key] = animation;
            }
            catch (Exception ei)
            {
              Log.LogError($"Failed to load animation \"{animationData.Key}\": {ei}");
            }
          }
        }
        Log.LogInfo($"Finished loading animations");
      }
      catch (Exception e)
      {
        Log.LogError(e);
      }
    }
  }

  public abstract class Animation
  {
    public class Type : ExtEnum<Type>
    {
      public Type(string value, bool register = false)
      : base(value, register) { }

      public static Type
        Color = new("Color", true),
        Pos = new("Pos", true),
        Element = new("Element", true);
    }

    public int tick = 0, ticksPerUpdate = 1;
    public string name;
    public Type type;

    public Animation() { }

    public Animation(Dictionary<string, object> animation)
    {
      animation.TryUpdateNumber("ticksPerUpdate", ref ticksPerUpdate);
      if (ticksPerUpdate < 1)
        throw new Exception($"\"ticksPerUpdate\" can't be lower than 1");
    }

    public void CopyBase(Animation target)
    {
      target.ticksPerUpdate = ticksPerUpdate;
      target.name = name;
      target.type = type;
    }

    public abstract Animation Clone();

    public abstract void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData);

    public abstract void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData);
  }

  public class AnimationColor : Animation
  {
    public class ColorModifier
    {
      public class Type : ExtEnum<Type>
      {
        public Type(string value, bool register = false)
        : base(value, register) { }

        public static Type
          HSV1 = new("HSV1", true),
          HSVB = new("HSVB", true);
      }

      public Color color;
      public float[] modifiers = new float[3];
      public Type type;

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

      public static void ProcessModifiers(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, ColorModifier[] modifiers)
      {
        ColorModifier colorModifier1 = modifiers[0];
        Color baseColor = sprites.colors[spriteData.colorIndex];
        if (colorModifier1.type == Type.HSV1 || colorModifier1.type == Type.HSVB)
          colorModifier1.ApplyHSV(baseColor);
        for (int i = 1; i < modifiers.Length; ++i)
        {
          ColorModifier colorModifier = modifiers[i];
          if (colorModifier.type == Type.HSV1)
            colorModifier.ApplyHSV(modifiers[i - 1].color);
          else if (colorModifier.type == Type.HSV1)
            colorModifier.ApplyHSV(baseColor);
        }
      }
    }

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
      public int currentIndex = 0;

      public BaseColorHandler() { }

      public BaseColorHandler(Dictionary<string, object> animation)
      {

      }

      public BaseColorHandler(BaseColorHandler other)
      {

      }

      public abstract BaseColorHandler Clone();

      public abstract void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner);

      public abstract void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData);
    }

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

      public override void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner)
      {

      }

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

      public override void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData, Animation owner)
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

    public ColorModifier[] colorModifiers;
    public Color[] cachedColors;
    public Subtype subtype;
    public BaseColorHandler handler;

    public AnimationColor() { }

    public AnimationColor(Dictionary<string, object> animation)
      : base(animation)
    {
      if (!animation.TryGetExtEnum("subtype", out subtype))
        throw new Exception($"field \"subtype\" doesn't exist or neither of supported color animation types");
      if (!AnimationHandler.colorHandlerCtors.TryGetValue(subtype, out AnimationHandler.ColorHandlerNewD ctor))
        throw new Exception($"no delegate for color handler of that type was stored - hook AnimationHandler.OnInitiate and store it with your method");
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

    public override void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
    {
      ColorModifier.ProcessModifiers(self, sLeaser, sprites, spriteData, colorModifiers);

      cachedColors = new Color[colorModifiers.Length];
      for (int i = 0; i < colorModifiers.Length; ++i)
        cachedColors[i] = colorModifiers[i].color;

      handler.Initiate(self, sLeaser, sprites, spriteData, this);
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

    public override void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
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

    public override void Initiate(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites, SlugSpriteData spriteData)
    {

    }

    public override void Update(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData spriteData)
    {

    }
  }
}
