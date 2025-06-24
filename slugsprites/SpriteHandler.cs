using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using gelbi_silly_lib;
using SlugBase.DataTypes;
using BepInEx;
using static slugsprites.LogWrapper;

namespace slugsprites
{
  public static class Extensions
  {
    /// <summary>
    /// Returns custom sprites for slugcat, if they exist
    /// </summary>
    public static bool TryGetSupportedSlugcat(this PlayerGraphics self, out SlugcatSprites sprites)
    {
      return SpriteHandler.customSprites.TryGetValue((self.owner as Player).slugcatStats.name, out sprites);
    }

    /// <summary>
    /// Changes element of sprite from field to custom one if custom replacement was defined
    /// </summary>
    public static void TryUpdateSprite(this Dictionary<string, SlugSpriteData> self, string fieldName, RoomCamera.SpriteLeaser sLeaser, int spriteIndex, string nameAddition = "")
    {
      if (self.TryGetValue(fieldName, out SlugSpriteData spriteData))
        spriteData.TryUpdateSprite(sLeaser, spriteIndex, nameAddition);
    }

    /// <summary>
    /// Creates new custom sprites based on original sprite.
    /// Custom properties are prioritized, but most of them would be changed by the game later
    /// </summary>
    public static void TryCreateSprites(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, int baseIndex, string nameAddition = "")
    {
      string baseSpriteName = sLeaser.sprites[baseIndex]._element.name;
      if (nameAddition != "")
        baseSpriteName = baseSpriteName.Substring(0, baseSpriteName.Length - nameAddition.Length);
      foreach (SlugSpriteData spriteData in self)
      {
        spriteData.baseSprite = baseSpriteName;
        FSprite newSprite = sLeaser.sprites[spriteData.realIndex] = new FSprite(spriteData.sprite + nameAddition),
          baseSprite = sLeaser.sprites[baseIndex];
        newSprite._anchorX = spriteData.anchorX ?? baseSprite._anchorX;
        newSprite._anchorY = spriteData.anchorY ?? baseSprite._anchorY;
        newSprite._areLocalVerticesDirty = spriteData.areLocalVerticesDirty || baseSprite._areLocalVerticesDirty;
        newSprite._scaleX = spriteData.scaleX ?? baseSprite._scaleX;
        newSprite._scaleY = spriteData.scaleY ?? baseSprite._scaleY;
        newSprite._rotation = spriteData.rotation ?? baseSprite._rotation;
        newSprite._isMatrixDirty = spriteData.isMatrixDirty || baseSprite._isMatrixDirty;
      }
    }

    /// <summary>
    /// Defines order of custom sprites depending on config, relatively to original sprite
    /// </summary>
    public static void UpdateNodes(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, int baseIndex)
    {
      int behindFirstIndex = self.FindLastIndex(e => e.order < 0);
      if (behindFirstIndex != -1)
      {
        sLeaser.sprites[behindFirstIndex].MoveBehindOtherNode(sLeaser.sprites[baseIndex]);
        for (int i = behindFirstIndex - 1; i >= 0; --i)
          sLeaser.sprites[i].MoveBehindOtherNode(sLeaser.sprites[i + 1]);
      }
      if (++behindFirstIndex >= self.Count)
        return;
      sLeaser.sprites[behindFirstIndex].MoveInFrontOfOtherNode(sLeaser.sprites[baseIndex]);
      for (int i = behindFirstIndex + 1; i < self.Count; ++i)
        sLeaser.sprites[i].MoveInFrontOfOtherNode(sLeaser.sprites[i - 1]);
    }

    /// <summary>
    /// Updates state(position, rotation, scale, etc.) of sprites to match original ones
    /// </summary>
    public static void UpdateStates(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, int baseIndex)
    {
      foreach (SlugSpriteData spriteData in self)
      {
        FSprite sprite = sLeaser.sprites[spriteData.realIndex],
          baseSprite = sLeaser.sprites[baseIndex];
        sprite._x = baseSprite._x;
        sprite._y = baseSprite._y;
        sprite._scaleX = baseSprite._scaleX;
        sprite._scaleY = baseSprite._scaleY;
        sprite._rotation = baseSprite._rotation;
        sprite._isMatrixDirty = baseSprite._isMatrixDirty;
        if (baseIndex != _sprite.ipixel)
          continue;
        sprite.alpha = baseSprite.alpha;
        sprite.isVisible = baseSprite.isVisible;
      }
    }

    /// <summary>
    /// Updates sprites, based on original ones, to match sprite element
    /// </summary>
    public static void UpdateElements(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, int baseIndex)
    {
      string newName = sLeaser.sprites[baseIndex].element.name;
      SlugSpriteData firstSprite = self[0];
      if (!firstSprite.previousBaseSprite.CheckSpriteChange(newName))
        return;
      firstSprite.previousBaseSprite = newName;
      string suffix = newName.Substring(firstSprite.baseSprite.Length);
      foreach (SlugSpriteData spriteData in self)
        sLeaser.sprites[spriteData.realIndex].element = Futile.atlasManager.GetElementWithName(spriteData.sprite + suffix);
    }

    /// <summary>
    /// Checks whether string is different from other one by last 3 letters
    /// </summary>
    public static bool CheckSpriteChange(this string self, string other)
    {
      if (self.Length != other.Length)
        return true;
      int a = self.Length - 1;
      // If this throws, you know why
      return self[a] != other[a--]
          || self[a] != other[a--]
          || self[a] != other[a];
    }
  }

  public class _sprite
  {
    public const string body = "body", hips = "hips", tail = "tail", head = "head", legs = "legs", arm = "arm",
      arm1 = "arm1", arm2 = "arm2", terrainHand = "terrainHand", terrainHand1 = "terrainHand1",
      terrainHand2 = "terrainHand2", face = "face", pixel = "pixel", other = "other";
    public const int ibody = 0, ihips = 1, itail = 2, ihead = 3, ilegs = 4, iarm1 = 5, iarm2 = 6,
      iterrainHand1 = 7, iterrainHand2 = 8, iface = 9, ipixel = 11;
  }

  public class SpriteHandler
  {
    // Some magic from DMS
    public static void MapTailUV(TriangleMesh tail)
    {
      for (int vertex = tail.vertices.Length - 1; vertex >= 0; vertex--)
      {
        float uvix = 1f, uviy = 0f;
        if (vertex != tail.vertices.Length - 1)
        {
          uvix = (float)vertex / tail.vertices.Length;
          if (vertex % 2 != 0)
            uviy = 1f;
        }

        tail.UVvertices[vertex] = new(
          Mathf.Lerp(tail.element.uvBottomLeft.x, tail.element.uvTopRight.x, uvix),
          Mathf.Lerp(tail.element.uvBottomLeft.y, tail.element.uvTopRight.y, uviy));
      }
    }
    
    // Some magic from DMS
    public static float GetSegmentRadius(int segment, int length, float wideness, float roundness)
    {
      return Mathf.Lerp(6f, 1f, Mathf.Pow((segment + 1) / (float)length, wideness))
        * (1f + (Mathf.Sin((float)(segment * Math.PI / length)) * roundness));
    }

    public static void InitCachedSpriteNames(PlayerGraphics self)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return;

      if (sprites.baseSprites.TryGetValue(_sprite.face, out SlugSpriteData faceSprite))
        self._cachedFaceSpriteNames = new AGCachedStrings3Dim(new string[] { faceSprite.sprite, "PFace" }, new string[] { "A", "B", "C", "D", "E" }, 9);
      if (sprites.baseSprites.TryGetValue(_sprite.head, out SlugSpriteData headSprite))
        self._cachedHeads = new AGCachedStrings2Dim(new string[] { $"{headSprite.sprite}A", $"{headSprite.sprite}B", $"{headSprite.sprite}C" }, 18);
      if (sprites.baseSprites.TryGetValue(_sprite.arm1, out SlugSpriteData armSprite))
        self._cachedPlayerArms = new AGCachedStrings(armSprite.sprite, 13);
      if (sprites.baseSprites.TryGetValue(_sprite.legs, out SlugSpriteData legsSprite))
      {
        self._cachedLegsA = new AGCachedStrings($"{legsSprite.sprite}A", 31);
        self._cachedLegsACrawling = new AGCachedStrings($"{legsSprite.sprite}ACrawling", 31);
        self._cachedLegsAClimbing = new AGCachedStrings($"{legsSprite.sprite}AClimbing", 31);
        self._cachedLegsAOnPole = new AGCachedStrings($"{legsSprite.sprite}AOnPole", 31);
      }

      // If I figure out, how to retrieve indexes, can use the instead(just big IL hook, which I'm lazy to do)
      // getting needed name part from base sprite works fine so far

      //if (sprites.additionalSprites.TryGetValue(_sprite.face, out List<SlugSpriteData> faceSpriteList))
      //  foreach (SlugSpriteData spriteData in faceSpriteList)
      //    spriteData._cachedFaceSpriteNames = new AGCachedStrings3Dim(new string[] { spriteData.sprite, "PFace" }, new string[] { "A", "B", "C", "D", "E" }, 9);
      //if (sprites.additionalSprites.TryGetValue(_sprite.head, out List<SlugSpriteData> headSpriteList))
      //  foreach (SlugSpriteData spriteData in headSpriteList)
      //    spriteData._cachedHeads = new AGCachedStrings2Dim(new string[] { $"{spriteData.sprite}A", $"{spriteData.sprite}B", $"{spriteData.sprite}C" }, 18);
      //if (sprites.additionalSprites.TryGetValue(_sprite.arm1, out List<SlugSpriteData> armSpriteList))
      //  foreach (SlugSpriteData spriteData in armSpriteList)
      //    spriteData._cachedPlayerArms = new AGCachedStrings(armSprite.sprite, 13);
      //if (sprites.additionalSprites.TryGetValue(_sprite.legs, out List<SlugSpriteData> legsSpriteList))
      //  foreach (SlugSpriteData spriteData in legsSpriteList)
      //  {
      //    spriteData._cachedLegsA = new AGCachedStrings($"{spriteData.sprite}A", 31);
      //    spriteData._cachedLegsACrawling = new AGCachedStrings($"{spriteData.sprite}ACrawling", 31);
      //    spriteData._cachedLegsAClimbing = new AGCachedStrings($"{spriteData.sprite}AClimbing", 31);
      //    spriteData._cachedLegsAOnPole = new AGCachedStrings($"{spriteData.sprite}AOnPole", 31);
      //  }
    }

    public static void InitiateSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return;

      int baseLength, newLength = baseLength = sLeaser.sprites.Length;
      foreach (KeyValuePair<string, List<SlugSpriteData>> partSprites in sprites.additionalSprites)
        foreach (SlugSpriteData spriteData in partSprites.Value)
          spriteData.realIndex = newLength++;
      sprites.firstSpriteIndex = baseLength;
      if (baseLength != newLength)
        Array.Resize(ref sLeaser.sprites, newLength);

      sprites.baseSprites.TryUpdateSprite(_sprite.body, sLeaser, _sprite.ibody, "A");
      sprites.baseSprites.TryUpdateSprite(_sprite.hips, sLeaser, _sprite.ihips, "A");
      sprites.baseSprites.TryUpdateSprite(_sprite.head, sLeaser, _sprite.ihead, "A0");
      sprites.baseSprites.TryUpdateSprite(_sprite.legs, sLeaser, _sprite.ilegs, "A0");
      sprites.baseSprites.TryUpdateSprite(_sprite.arm1, sLeaser, _sprite.iarm1, "0");
      sprites.baseSprites.TryUpdateSprite(_sprite.arm2, sLeaser, _sprite.iarm2, "0");
      sprites.baseSprites.TryUpdateSprite(_sprite.terrainHand1, sLeaser, _sprite.iterrainHand1);
      sprites.baseSprites.TryUpdateSprite(_sprite.terrainHand2, sLeaser, _sprite.iterrainHand2);
      sprites.baseSprites.TryUpdateSprite(_sprite.face, sLeaser, _sprite.iface, "A0");
      sprites.baseSprites.TryUpdateSprite(_sprite.pixel, sLeaser, _sprite.ipixel);

      if (sprites.baseSprites.TryGetValue(_sprite.tail, out SlugSpriteData tailSpriteData))
      {
        tailSpriteData.TryUpdateSprite(sLeaser, _sprite.itail);
        MapTailUV(sLeaser.sprites[_sprite.itail] as TriangleMesh);

        self.tail = new TailSegment[sprites.tailLength];
        for (int i = 0; i < sprites.tailLength; i++)
        {
          self.tail[i] = new TailSegment(self,
            GetSegmentRadius(i, sprites.tailLength, sprites.tailWideness, sprites.tailRoundness),
            ((i == 0) ? 4 : 7) * (self.RenderAsPup ? 0.5f : 1f),
            (i > 0) ? self.tail[i - 1] : null,
            0.85f, 1f, (i == 0) ? 1f : 0.5f, true);
        }
      }

      sprites.customBody?.TryCreateSprites(sLeaser, _sprite.ibody, "A");
      sprites.customHips?.TryCreateSprites(sLeaser, _sprite.ihips, "A");
      sprites.customHead?.TryCreateSprites(sLeaser, _sprite.ihead, "A0");
      sprites.customLegs?.TryCreateSprites(sLeaser, _sprite.ilegs, "A0");
      sprites.customArm1?.TryCreateSprites(sLeaser, _sprite.iarm1, "0");
      sprites.customArm2?.TryCreateSprites(sLeaser, _sprite.iarm2, "0");
      sprites.customTerrainHand1?.TryCreateSprites(sLeaser, _sprite.iterrainHand1);
      sprites.customTerrainHand2?.TryCreateSprites(sLeaser, _sprite.iterrainHand2);
      sprites.customFace?.TryCreateSprites(sLeaser, _sprite.iface, "A0");
      sprites.customPixel?.TryCreateSprites(sLeaser, _sprite.ipixel);

      sprites.customOther?.TryCreateSprites(sLeaser, _sprite.ibody);

      if (sprites.customTail != null)
      {
        TriangleMesh.Triangle[] tailArray = new TriangleMesh.Triangle[]
        {
          new(0, 1, 2),
          new(1, 2, 3),
          new(4, 5, 6),
          new(5, 6, 7),
          new(8, 9, 10),
          new(9, 10, 11),
          new(12, 13, 14),
          new(2, 3, 4),
          new(3, 4, 5),
          new(6, 7, 8),
          new(7, 8, 9),
          new(10, 11, 12),
          new(11, 12, 13)
        };
        foreach (SlugSpriteData spriteData in sprites.customTail)
        {
          FSprite tailSprite = sLeaser.sprites[spriteData.realIndex] = new TriangleMesh("Futile_White", tailArray, false);
          tailSprite.element = Futile.atlasManager.GetElementWithName(spriteData.sprite);
          tailSprite._isMeshDirty = true;
          MapTailUV(tailSprite as TriangleMesh);
        }
      }

      if (sprites.additionalSprites.Count != 0)
        sprites.colors = new Color[sprites.colorAmount + 1];
    }

    public static void DrawSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return;

      sprites.customBody?.UpdateStates(sLeaser, _sprite.ibody);
      sprites.customHips?.UpdateStates(sLeaser, _sprite.ihips);
      sprites.customHead?.UpdateStates(sLeaser, _sprite.ihead);
      sprites.customLegs?.UpdateStates(sLeaser, _sprite.ilegs);
      sprites.customArm1?.UpdateStates(sLeaser, _sprite.iarm1);
      sprites.customArm2?.UpdateStates(sLeaser, _sprite.iarm2);
      sprites.customTerrainHand1?.UpdateStates(sLeaser, _sprite.iterrainHand1);
      sprites.customTerrainHand2?.UpdateStates(sLeaser, _sprite.iterrainHand2);
      sprites.customFace?.UpdateStates(sLeaser, _sprite.iface);
      sprites.customPixel?.UpdateStates(sLeaser, _sprite.ipixel);

      if (sprites.customOther != null)
        foreach (SlugSpriteData spriteData in sprites.customOther)
        {
          FSprite sprite = sLeaser.sprites[spriteData.realIndex],
            baseSprite = sLeaser.sprites[_sprite.ibody];
          sprite._x = baseSprite._x;
          sprite._y = baseSprite._y;
        }

      if (sprites.customTail != null)
      {
        Vector2[] verticies = (sLeaser.sprites[_sprite.itail] as TriangleMesh).vertices;
        foreach (SlugSpriteData spriteData in sprites.customTail)
        {
          TriangleMesh tail = sLeaser.sprites[spriteData.realIndex] as TriangleMesh;
          for (int i = 0; i < verticies.Length; ++i)
            tail.MoveVertice(i, verticies[i]);
        }
      }

      sprites.customFace?.UpdateElements(sLeaser, _sprite.iface);
      sprites.customHead?.UpdateElements(sLeaser, _sprite.ihead);
      sprites.customArm1?.UpdateElements(sLeaser, _sprite.iarm1);
      sprites.customArm2?.UpdateElements(sLeaser, _sprite.iarm2);
      sprites.customLegs?.UpdateElements(sLeaser, _sprite.ilegs);
    }

    public static void AddToContainer(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return;

      newContatiner ??= rCam.ReturnFContainer("Midground");
      for (int i = sprites.firstSpriteIndex; i < sLeaser.sprites.Length; ++i)
        newContatiner.AddChild(sLeaser.sprites[i]);

      sprites.customBody?.UpdateNodes(sLeaser, _sprite.ibody);
      sprites.customHips?.UpdateNodes(sLeaser, _sprite.ihips);
      sprites.customTail?.UpdateNodes(sLeaser, _sprite.itail);
      sprites.customHead?.UpdateNodes(sLeaser, _sprite.ihead);
      sprites.customLegs?.UpdateNodes(sLeaser, _sprite.ilegs);
      sprites.customArm1?.UpdateNodes(sLeaser, _sprite.iarm1);
      sprites.customArm2?.UpdateNodes(sLeaser, _sprite.iarm2);
      sprites.customTerrainHand1?.UpdateNodes(sLeaser, _sprite.iterrainHand1);
      sprites.customTerrainHand2?.UpdateNodes(sLeaser, _sprite.iterrainHand2);
      sprites.customFace?.UpdateNodes(sLeaser, _sprite.iface);
      sprites.customPixel?.UpdateNodes(sLeaser, _sprite.ipixel);

      sprites.customOther?.UpdateNodes(sLeaser, _sprite.ibody);
    }

    public static void ApplyPalette(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return;

      for (int i = 0; i < sprites.colorAmount; ++i)
        sprites.colors[i] = PlayerColor.GetCustomColor(self, i);

      foreach (KeyValuePair<string, List<SlugSpriteData>> partSprites in sprites.additionalSprites)
        foreach (SlugSpriteData spriteData in partSprites.Value)
          if (spriteData.colorIndex != -1)
            sLeaser.sprites[spriteData.realIndex].color = sprites.colors[spriteData.colorIndex];
    }

    public static void LoadAtlases(string atlasesFolder)
    {
      foreach (string path in AssetManager.ListDirectory(atlasesFolder))
        if (Path.GetExtension(path) == ".txt")
          Futile.atlasManager.LoadAtlas(path.Substring(0, path.Length - 4));
    }

    public static void LoadCustomSprites()
    {
      customSprites = new();
      try
      {
        foreach (string path in AssetManager.ListDirectory("slugsprites"))
        {
          Log.LogInfo($"Reading at: {path}");
          foreach (KeyValuePair<string, object> slugcatSprites in Json.Parser.Parse(File.ReadAllText(path)) as Dictionary<string, object>)
          {
            if (slugcatSprites.Key.TryGetExtEnum(out SlugcatStats.Name slugcatName))
            {
              Log.LogInfo($"Loading sprites for {slugcatSprites.Key}");
              customSprites[slugcatName] = new(slugcatSprites.Value as Dictionary<string, object>);
            }
            else
              Log.LogError($"Failed to find slugcat with name {slugcatSprites.Key}");
          }
        }
        Log.LogInfo($"Finished reading");
      }
      catch (Exception e)
      {
        Log.LogError(e);
      }
    }

    public static Dictionary<SlugcatStats.Name, SlugcatSprites> customSprites;
  }

  public class SlugcatSprites
  {
    public static string[] supportedSprites = new string[] { _sprite.body, _sprite.hips, _sprite.tail, _sprite.head, _sprite.legs,
      _sprite.arm, _sprite.terrainHand, _sprite.face, _sprite.pixel, _sprite.other };
    public List<SlugSpriteData> customBody, customHips, customTail, customHead, customLegs, customArm1, customArm2,
      customTerrainHand1, customTerrainHand2, customFace, customPixel, customOther;
    public Color[] colors;
    public Dictionary<string, SlugSpriteData> baseSprites = new();
    public Dictionary<string, List<SlugSpriteData>> additionalSprites = new();
    public int firstSpriteIndex = 0, tailLength = 3, tailWideness = 1, colorAmount = 0;
    public float tailRoundness = 1f;

    public SlugcatSprites(Dictionary<string, object> sprites)
    {
      if (sprites.TryGetValueWithType("atlases", out string folderPath))
        SpriteHandler.LoadAtlases($"slugsprites/{folderPath}");
      else
        Log.LogError($"Atlases weren't loaded, specified field wasn't found");
      sprites.TryUpdateNumber("tailLength", ref tailLength);
      sprites.TryUpdateNumber("tailWideness", ref tailWideness);
      sprites.TryUpdateNumber("tailRoundness", ref tailRoundness);
      foreach (string field in supportedSprites)
        if (sprites.TryGetValueWithType(field, out List<object> spriteSet))
        {
          foreach (object sprite in spriteSet)
          {
            bool isDoubled = field == _sprite.arm || field == _sprite.terrainHand;
            for (int i = 1; i < (isDoubled ? 3 : 2); ++i)
            {
              string fieldName = field;
              if (isDoubled)
                fieldName += i;
              SlugSpriteData spriteData = new(sprite as Dictionary<string, object>);
              colorAmount = Math.Max(colorAmount, spriteData.colorIndex);
              if (spriteData.order == 0)
                baseSprites[fieldName] = spriteData;
              else
              {
                if (additionalSprites.TryGetValue(fieldName, out List<SlugSpriteData> spriteList))
                  spriteList.Add(spriteData);
                else
                  additionalSprites[fieldName] = new() { spriteData };
              }
            }
          }
          if (additionalSprites.TryGetValue(field, out List<SlugSpriteData> _spriteList))
            _spriteList.Sort((a, b) => a.order.CompareTo(b.order));
        }
      if (additionalSprites.Count != 0)
        ++colorAmount;
      foreach (KeyValuePair<string, List<SlugSpriteData>> spriteSet in additionalSprites)
        switch (spriteSet.Key)
        {
          case _sprite.body:
            customBody = spriteSet.Value;
            break;
          case _sprite.hips:
            customHips = spriteSet.Value;
            break;
          case _sprite.tail:
            customTail = spriteSet.Value;
            break;
          case _sprite.head:
            customHead = spriteSet.Value;
            break;
          case _sprite.legs:
            customLegs = spriteSet.Value;
            break;
          case _sprite.arm1:
            customArm1 = spriteSet.Value;
            break;
          case _sprite.arm2:
            customArm2 = spriteSet.Value;
            break;
          case _sprite.terrainHand1:
            customTerrainHand1 = spriteSet.Value;
            break;
          case _sprite.terrainHand2:
            customTerrainHand2 = spriteSet.Value;
            break;
          case _sprite.face:
            customFace = spriteSet.Value;
            break;
          case _sprite.pixel:
            customPixel = spriteSet.Value;
            break;
          case _sprite.other:
            customOther = spriteSet.Value;
            break;
        }
    }
  }

  public class SlugSpriteData
  {
    public int order = 0, realIndex = 0, colorIndex = -1;
    public float? anchorX, anchorY, scaleX, scaleY, rotation;
    public bool areLocalVerticesDirty = false, isMatrixDirty = false;
    public string sprite = "Futile_White", baseSprite, previousBaseSprite = "";
    //public AGCachedStrings3Dim _cachedFaceSpriteNames;
    //public AGCachedStrings2Dim _cachedHeads;
    //public AGCachedStrings _cachedPlayerArms, _cachedLegsA, _cachedLegsACrawling, _cachedLegsAClimbing, _cachedLegsAOnPole;

    public SlugSpriteData(Dictionary<string, object> spriteData)
    {
      if (!spriteData.TryGetValueWithType("sprite", out sprite))
      {
        Log.LogError($"Couldn't find sprite name");
        return;
      }
      spriteData.TryUpdateNumber("order", ref order);
      spriteData.TryUpdateNumber("colorIndex", ref colorIndex);
      if (spriteData.TryGetNumber("anchorX", out float _anchorX))
        anchorX = _anchorX;
      if (spriteData.TryGetNumber("anchorY", out float _anchorY))
        anchorY = _anchorY;
      if (spriteData.TryGetNumber("scaleX", out float _scaleX))
        scaleX = _scaleX;
      if (spriteData.TryGetNumber("scaleY", out float _scaleY))
        scaleY = _scaleY;
      if (spriteData.TryGetNumber("rotation", out float _rotation))
        rotation = _rotation;
      if (anchorX != null || anchorY != null)
        areLocalVerticesDirty = true;
      if (scaleX != null || scaleY != null || rotation != null)
        isMatrixDirty = true;
    }

    /// <summary>
    /// Changes element of the sprite to custom one
    /// </summary>
    public void TryUpdateSprite(RoomCamera.SpriteLeaser sLeaser, int spriteIndex, string nameAddition = "")
    {
      if (Futile.atlasManager._allElementsByName.TryGetValue(sprite + nameAddition, out FAtlasElement value))
        sLeaser.sprites[spriteIndex].element = value;
      else
        Log.LogError($"Failed to find sprite with name: {sprite}{nameAddition}");
    }
  }
}
