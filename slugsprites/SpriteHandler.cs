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
using System.Runtime.CompilerServices;
using UnityEngine.Events;

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
    /// Creates new custom sprites based on original sprite.
    /// Custom properties are prioritized, but most of them would be changed by the game later
    /// </summary>
    public static void CreateSprites(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, int baseIndex)
    {
      string baseSpriteName = sLeaser.sprites[baseIndex]._element.name,
        suffix = _sprite.suffixes[self[0].groupIndex];
      if (suffix != "")
        baseSpriteName = baseSpriteName.Substring(0, baseSpriteName.Length - suffix.Length);
      foreach (SlugSpriteData spriteData in self)
      {
        spriteData.baseSprite = baseSpriteName;
        FSprite newSprite = sLeaser.sprites[spriteData.realIndex] = new FSprite(spriteData.defaultSprite),
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
    public static void UpdateStates(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser)
    {
      foreach (SlugSpriteData spriteData in self)
      {
        FSprite sprite = sLeaser.sprites[spriteData.realIndex],
          baseSprite = sLeaser.sprites[spriteData.groupIndex];
        sprite._x = baseSprite._x;
        sprite._y = baseSprite._y;
        sprite._scaleX = baseSprite._scaleX;
        sprite._scaleY = baseSprite._scaleY;
        sprite._anchorX = baseSprite._anchorX;
        sprite._anchorY = baseSprite._anchorY;
        sprite._isMatrixDirty = baseSprite._isMatrixDirty;
        if (spriteData.groupIndex != _sprite.ipixel)
          continue;
        sprite.alpha = baseSprite.alpha;
        sprite.isVisible = baseSprite.isVisible;
      }
    }

    /// <summary>
    /// Updates sprites, based on original ones, to match sprite element
    /// </summary>
    public static void UpdateElements(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser)
    {
      SlugSpriteData firstSprite = self[0];
      string newName = sLeaser.sprites[firstSprite.groupIndex].element.name;
      if (!firstSprite.previousBaseSprite.CheckSpriteChange(newName))
        return;
      firstSprite.previousBaseSprite = newName;
      string suffix = newName.Substring(newName[newName.Length - 1] == 'd' ? 4 : firstSprite.baseSprite.Length);
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

    /// <summary>
    /// Returns additional sprite with same base name
    /// </summary>
    public static SlugSpriteData GetSprite(this List<SlugSpriteData> self, string sprite)
    {
      return self.FirstOrDefault(s => s.sprite == sprite);
    }

    /// <summary>
    /// Returns additional sprite with same base or default name
    /// </summary>
    public static SlugSpriteData GetSpriteFull(this List<SlugSpriteData> self, string sprite)
    {
      return self.FirstOrDefault(s => s.sprite == sprite || s.sprite == s.defaultSprite);
    }
  }

  public class _sprite
  {
    public const string body = "body", hips = "hips", tail = "tail", head = "head", legs = "legs", arm = "arm",
      arm1 = "arm1", arm2 = "arm2", terrainHand = "terrainHand", terrainHand1 = "terrainHand1",
      terrainHand2 = "terrainHand2", face = "face", pixel = "pixel", other = "other",
      Abody = "A", Ahips = "A", Atail = "", Ahead = "A0", Alegs = "A0", Aarm = "0", AterrainHand = "",
      Aface = "A0", Apixel = "", Aother = "";
    public const int ibody = 0, ihips = 1, itail = 2, ihead = 3, ilegs = 4, iarm1 = 5, iarm2 = 6,
      iterrainHand1 = 7, iterrainHand2 = 8, iface = 9, ipixel = 11, iother = 10; // 10th index isn't used by SlugSprites, but `other` isn't drawn automatically either
    public static readonly string[] groups = { body, hips, tail, head, legs, arm1, arm2, terrainHand1,
      terrainHand2, face, other, pixel };
    public static readonly string[] suffixes = { Abody, Ahips, Atail, Ahead, Alegs, Aarm, Aarm, AterrainHand,
    AterrainHand, Aface, Aother, Apixel };
    public static readonly int[] suffixLength = { 1, 1, 0, 2, 2, 1, 1, 0, 0, 2, 0, 0 };
    public static readonly Dictionary<string, int> indexes = new() {
      { body, ibody },
      { hips, ihips },
      { tail, itail },
      { head, ihead },
      { legs, ilegs },
      { arm1, iarm1 },
      { arm2, iarm2 },
      { terrainHand1, iterrainHand1 },
      { terrainHand2, iterrainHand2 },
      { face, iface },
      { other, iother },
      { pixel, ipixel }
    };
  }

  public class ManagedPlayerData
  {
    public RoomCamera rCam;
    public RoomCamera.SpriteLeaser sLeaser;
  }

  public class SpriteHandler
  {
    public static ConditionalWeakTable<Player, ManagedPlayerData> managedPlayers = new();

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

    public static SlugcatSprites InitCachedSpriteNames(PlayerGraphics self)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return null;

      if (sprites.BaseFace != null)
        self._cachedFaceSpriteNames = new AGCachedStrings3Dim(new string[] { sprites.BaseFace.sprite, "PFace" }, new string[] { "A", "B", "C", "D", "E" }, 9);
      if (sprites.BaseHead != null)
        self._cachedHeads = new AGCachedStrings2Dim(new string[] { $"{sprites.BaseHead.sprite}A", $"{sprites.BaseHead.sprite}B", $"{sprites.BaseHead.sprite}C" }, 18);
      if (sprites.BaseArm1 != null)
        self._cachedPlayerArms = new AGCachedStrings(sprites.BaseArm1.sprite, 13);
      if (sprites.BaseLegs != null)
      {
        string legsSpriteA = $"{sprites.BaseLegs.sprite}A";
        self._cachedLegsA = new AGCachedStrings(legsSpriteA, 31);
        self._cachedLegsACrawling = new AGCachedStrings($"{legsSpriteA}Crawling", 31);
        self._cachedLegsAClimbing = new AGCachedStrings($"{legsSpriteA}Climbing", 31);
        self._cachedLegsAOnPole = new AGCachedStrings($"{legsSpriteA}OnPole", 31);
      }

      return sprites;

      // If I figure out, how to retrieve indexes, can use the instead(just big IL hook, which I'm lazy to do)
      // getting needed name part from base sprite works fine so far

      // Would need to rewrite that part too lol

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

    public static SlugcatSprites InitiateSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return null;

      int baseLength, newLength = baseLength = sLeaser.sprites.Length;
      foreach (List<SlugSpriteData> partSprites in sprites.additionalSprites)
        if (partSprites != null)
          foreach (SlugSpriteData spriteData in partSprites)
            spriteData.realIndex = newLength++;
      sprites.firstSpriteIndex = baseLength;
      if (baseLength != newLength)
        Array.Resize(ref sLeaser.sprites, newLength);

      foreach (SlugSpriteData spriteData in sprites.baseUpdatable)
        spriteData?.ResetToCustomElement(sLeaser);

      if (sprites.BaseTail != null)
      {
        sprites.BaseTail.ResetToCustomElement(sLeaser);
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

      foreach (List<SlugSpriteData> spriteList in sprites.additionalUpdatable)
        spriteList?.CreateSprites(sLeaser, spriteList[0].groupIndex);
      sprites.CustomOther?.CreateSprites(sLeaser, _sprite.ibody);

      if (sprites.CustomTail != null)
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
        foreach (SlugSpriteData spriteData in sprites.CustomTail)
        {
          FSprite tailSprite = sLeaser.sprites[spriteData.realIndex] = new TriangleMesh("Futile_White", tailArray, false);
          tailSprite.element = Futile.atlasManager.GetElementWithName(spriteData.sprite);
          tailSprite._isMeshDirty = true;
          MapTailUV(tailSprite as TriangleMesh);
        }
      }

      if (sprites.additionalSprites.Length != 0)
        sprites.colors = new Color[sprites.colorAmount];

      // Use default colour sets, when they can't be customized
      if (self.owner.room.game.session is ArenaGameSession || self.owner.room.game.session is StoryGameSession
        && self.useJollyColor && RWCustom.Custom.rainWorld.options.jollyColorMode == Options.JollyColorMode.AUTO)
        for (int i = 0; i < sprites.colorAmount; ++i)
          sprites.colors[i] = sprites.colorSets[(self.owner as Player).playerState.playerNumber % 4][i];
      // Use SlugBase colours if it's present
      else if (Plugin.isSlugBaseActive && SlugBase.SlugBaseCharacter.Registry.Keys.Contains(sprites.owner))
        for (int i = 0; i < sprites.colorAmount; ++i)
          sprites.colors[i] = PlayerColor.GetCustomColor(self, i);
      // Use existing custom colours, if SlugBase isn't present
      else if (PlayerGraphics.CustomColorsEnabled())
        for (int i = 0; i < sprites.colorAmount; ++i)
          sprites.colors[i] = PlayerGraphics.customColors[i];
      
      return sprites;
    }

    public static SlugcatSprites DrawSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
      if (Plugin.pluginInterface.debugMode.Value)
      {
        ManagedPlayerData managedPlayer = managedPlayers.GetOrCreateValue(self.owner as Player);
        managedPlayer.rCam = rCam;
        managedPlayer.sLeaser = sLeaser;
      }

      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return null;

      foreach (List<SlugSpriteData> spriteList in sprites.additionalUpdatable)
        spriteList?.UpdateStates(sLeaser);

      if (sprites.CustomOther != null)
        foreach (SlugSpriteData spriteData in sprites.CustomOther)
        {
          FSprite sprite = sLeaser.sprites[spriteData.realIndex],
            baseSprite = sLeaser.sprites[_sprite.ibody];
          sprite._x = baseSprite._x;
          sprite._y = baseSprite._y;
        }

      if (sprites.CustomTail != null)
      {
        Vector2[] verticies = (sLeaser.sprites[_sprite.itail] as TriangleMesh).vertices;
        foreach (SlugSpriteData spriteData in sprites.CustomTail)
        {
          TriangleMesh tail = sLeaser.sprites[spriteData.realIndex] as TriangleMesh;
          for (int i = 0; i < verticies.Length; ++i)
            tail.MoveVertice(i, verticies[i]);
        }
      }

      sprites.CustomFace?.UpdateElements(sLeaser);
      sprites.CustomHead?.UpdateElements(sLeaser);
      sprites.CustomArm1?.UpdateElements(sLeaser);
      sprites.CustomArm2?.UpdateElements(sLeaser);
      sprites.CustomLegs?.UpdateElements(sLeaser);

      return sprites;
    }

    public static SlugcatSprites AddToContainer(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return null;

      newContatiner ??= rCam.ReturnFContainer("Midground");
      for (int i = sprites.firstSpriteIndex; i < sLeaser.sprites.Length; ++i)
        newContatiner.AddChild(sLeaser.sprites[i]);

      foreach (List<SlugSpriteData> spriteList in sprites.additionalUpdatable)
        spriteList?.UpdateNodes(sLeaser, spriteList[0].groupIndex);
      sprites.CustomTail?.UpdateNodes(sLeaser, _sprite.itail);
      sprites.CustomOther?.UpdateNodes(sLeaser, _sprite.ibody);

      return sprites;
    }

    public static SlugcatSprites ApplyPalette(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
    {
      if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
        return null;

      foreach (List<SlugSpriteData> partSprites in sprites.additionalSprites)
        if (partSprites != null)
          foreach (SlugSpriteData spriteData in partSprites)
            if (spriteData.colorIndex != -1)
            {
              Color? color = sprites.colors[spriteData.colorIndex];
              sLeaser.sprites[spriteData.realIndex].color = color ?? spriteData.defaultColor;
            }

      return sprites;
    }

    public static List<string> loadedAtlases = new();

    public static void UnloadAtlases()
    {
      foreach (string atlasPath in loadedAtlases)
        Futile.atlasManager.UnloadAtlas(atlasPath);
      loadedAtlases.Clear();
    }

    public static void LoadAtlases(string atlasesFolder)
    {
      foreach (string path in AssetManager.ListDirectory(atlasesFolder))
        if (Path.GetExtension(path) == ".txt")
        {
          string atlasPath = path.Substring(0, path.Length - 4);
          loadedAtlases.Add(atlasPath);
          Futile.atlasManager.LoadAtlas(atlasPath);
        }
    }

    public static void LoadCustomSprites()
    {
      Log.LogInfo("Loading custom sprites");
      UnloadAtlases();
      customSprites.Clear();
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
              try
              {
                customSprites[slugcatName] = new(slugcatSprites.Value as Dictionary<string, object>) { owner = slugcatName };
              }
              catch (Exception ei)
              {
                Log.LogError($"Failed to load custom sprites: {ei}");
              }
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

    public static Dictionary<SlugcatStats.Name, SlugcatSprites> customSprites = new();
  }

  public class SlugcatSprites
  {
    public static readonly string[] supportedSprites = new string[] { _sprite.body, _sprite.hips, _sprite.tail, _sprite.head, _sprite.legs,
      _sprite.arm, _sprite.terrainHand, _sprite.face, _sprite.pixel, _sprite.other };
    public SlugcatStats.Name owner;
    public Color[] colors;
    public Color[][] colorSets;
    public SlugSpriteData[] baseSprites = new SlugSpriteData[_sprite.groups.Length],
      baseUpdatable = new SlugSpriteData[_sprite.groups.Length - 2];
    public List<SlugSpriteData>[] additionalSprites = new List<SlugSpriteData>[_sprite.groups.Length],
      additionalUpdatable = new List<SlugSpriteData>[_sprite.groups.Length - 2];
    public int firstSpriteIndex = 0, tailLength = 3, tailWideness = 1, colorAmount = 0;
    public float tailRoundness = 1f;

    #region
    public SlugSpriteData BaseBody => baseSprites[_sprite.ibody];
    public SlugSpriteData BaseHips => baseSprites[_sprite.ihips];
    public SlugSpriteData BaseTail => baseSprites[_sprite.itail];
    public SlugSpriteData BaseHead => baseSprites[_sprite.ihead];
    public SlugSpriteData BaseLegs => baseSprites[_sprite.ilegs];
    public SlugSpriteData BaseArm1 => baseSprites[_sprite.iarm1];
    public SlugSpriteData BaseArm2 => baseSprites[_sprite.iarm2];
    public SlugSpriteData BaseTerrainHand1 => baseSprites[_sprite.iterrainHand1];
    public SlugSpriteData BaseTerrainHand2 => baseSprites[_sprite.iterrainHand2];
    public SlugSpriteData BaseFace => baseSprites[_sprite.iface];
    public SlugSpriteData BasePixel => baseSprites[_sprite.ipixel];

    public List<SlugSpriteData> CustomBody => additionalSprites[_sprite.ibody];
    public List<SlugSpriteData> CustomHips => additionalSprites[_sprite.ihips];
    public List<SlugSpriteData> CustomTail => additionalSprites[_sprite.itail];
    public List<SlugSpriteData> CustomHead => additionalSprites[_sprite.ihead];
    public List<SlugSpriteData> CustomLegs => additionalSprites[_sprite.ilegs];
    public List<SlugSpriteData> CustomArm1 => additionalSprites[_sprite.iarm1];
    public List<SlugSpriteData> CustomArm2 => additionalSprites[_sprite.iarm2];
    public List<SlugSpriteData> CustomTerrainHand1 => additionalSprites[_sprite.iterrainHand1];
    public List<SlugSpriteData> CustomTerrainHand2 => additionalSprites[_sprite.iterrainHand2];
    public List<SlugSpriteData> CustomFace => additionalSprites[_sprite.iface];
    public List<SlugSpriteData> CustomPixel => additionalSprites[_sprite.ipixel];
    public List<SlugSpriteData> CustomOther => additionalSprites[_sprite.iother];
    #endregion

    public SlugcatSprites(Dictionary<string, object> sprites)
    {
      // Loading supported fields
      if (sprites.TryGetValueWithType("atlases", out string folderPath))
        SpriteHandler.LoadAtlases($"slugsprites/{folderPath}");
      else
        throw new Exception("Slugcat configuration is missing \"atlases\" field");
      sprites.TryUpdateNumber("tailLength", ref tailLength);
      sprites.TryUpdateNumber("tailWideness", ref tailWideness);
      sprites.TryUpdateNumber("tailRoundness", ref tailRoundness);
      if (sprites.TryGetValueWithType("colorSets", out List<object> colorListSet))
      {
        if (colorListSet.Count != 4)
          throw new Exception($"Expected 4 color sets in \"colorSets\"");
        colorSets = new Color[colorListSet.Count][];
        colorSets[0] = (colorListSet[0] as List<object>).ToRGBColorArray();
        for (int i = 1; i < 4; ++i)
        {
          colorSets[i] = (colorListSet[i] as List<object>).ToRGBColorArray();
          if (colorSets[i].Length != colorSets[0].Length)
            throw new Exception($"Inconsistent amount of colors in \"colorSets\"");
        }
      }
      // Loading supported sprite groups
      foreach (string field in supportedSprites)
        if (sprites.TryGetValueWithType(field, out List<object> spriteSet))
        {
          int groupIndex = 0;
          foreach (object sprite in spriteSet)
          {
            try
            {
              bool isDoubled = field == _sprite.arm || field == _sprite.terrainHand;
              for (int i = 1; i < (isDoubled ? 3 : 2); ++i)
              {
                string fieldName = field;
                if (isDoubled)
                  fieldName += i;
                SlugSpriteData spriteData = new(sprite as Dictionary<string, object>);
                spriteData.groupIndex = groupIndex = _sprite.indexes[fieldName];
                // Debug message
                string suffix = _sprite.suffixes[groupIndex],
                  spriteName = spriteData.sprite + suffix;
                if (!Futile.atlasManager._allElementsByName.TryGetValue(spriteName, out _))
                {
                  string additionalInfo = "";
                  if (spriteData.sprite.EndsWith(suffix))
                  {
                    string fixedName = spriteData.sprite.Substring(0, spriteData.sprite.Length - suffix.Length);
                    if (Futile.atlasManager._allElementsByName.TryGetValue(spriteData.sprite, out _))
                      additionalInfo = $". Use \"{fixedName}\" in your sprite configuration instead - it's automatically appended to \"{spriteData.sprite}\"";
                  }
                  throw new Exception($"\"{spriteName}\" doesn't exist{additionalInfo}");
                }
                if (groupIndex == _sprite.iother && spriteData.order == 0)
                  throw new Exception($"can't have sprite of this group with order 0");

                spriteData.defaultSprite = spriteName;
                colorAmount = Math.Max(colorAmount, spriteData.colorIndex);
                if (spriteData.order == 0)
                  baseSprites[groupIndex] = spriteData;
                else
                {
                  List<SlugSpriteData> group = additionalSprites[groupIndex];
                  if (group == null)
                    additionalSprites[groupIndex] = new() { spriteData };
                  else
                    group.Add(spriteData);
                }
              }
            }
            catch (Exception e)
            {
              throw new Exception($"Error loading sprite configuration at field \"{field}\": {e}");
            }
          }
          additionalSprites[groupIndex]?.Sort((a, b) => a.order.CompareTo(b.order));
        }
      if (additionalSprites.Length != 0)
        ++colorAmount;

      for (int i = 0; i < baseUpdatable.Length; ++i)
      {
        baseUpdatable[i] = baseSprites[i];
        additionalUpdatable[i] = additionalSprites[i];
      }
      baseUpdatable[_sprite.itail] = baseSprites[_sprite.ipixel];
      additionalUpdatable[_sprite.itail] = additionalSprites[_sprite.ipixel];
    }
  }

  public class SlugSpriteData
  {
    public int order = 0, realIndex = 0, colorIndex = -1, groupIndex = 0;
    public float? anchorX, anchorY, scaleX, scaleY, rotation;
    public bool areLocalVerticesDirty = false, isMatrixDirty = false;
    public string sprite = "Futile_White", defaultSprite, baseSprite, previousBaseSprite = "";
    public Color defaultColor = Color.white;
    //public AGCachedStrings3Dim _cachedFaceSpriteNames;
    //public AGCachedStrings2Dim _cachedHeads;
    //public AGCachedStrings _cachedPlayerArms, _cachedLegsA, _cachedLegsACrawling, _cachedLegsAClimbing, _cachedLegsAOnPole;

    public SlugSpriteData(Dictionary<string, object> spriteData)
    {
      spriteData.TryUpdateNumber("order", ref order);
      if (!spriteData.TryGetValueWithType("sprite", out sprite))
        throw new Exception($"Sprite configuration with order {order} is missing \"sprite\" field");
      spriteData.TryUpdateNumber("colorIndex", ref colorIndex);
      if (colorIndex < -1)
        throw new Exception($"Sprite configuration with order {order} has incorrect value of \"colorIndex\" - must be greater than -2");
      spriteData.TryUpdateColorFromHex("defaultColor", ref defaultColor);
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
    public void ResetToCustomElement(RoomCamera.SpriteLeaser sLeaser)
    {
      sLeaser.sprites[groupIndex].element = Futile.atlasManager._allElementsByName[defaultSprite];
    }
  }
}
