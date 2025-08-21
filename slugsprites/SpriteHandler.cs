using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.Other;
using SlugBase;
using SlugBase.Assets;
using SlugBase.DataTypes;
using SlugBase.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using static slugsprites.LogWrapper;

namespace slugsprites;

public static class Extensions
{
  /// <summary>
  /// Clones SlugSpriteData[]
  /// </summary>
  public static void CloneFrom(this SlugSpriteData[] self, SlugSpriteData[] other)
  {
    for (int i = 0; i < self.Length; ++i)
      if (other[i] != null)
        self[i] = new(other[i]);
  }

  /// <summary>
  /// Clones List&lt;SlugSpriteData>[]
  /// </summary>
  public static void CloneFrom(this List<SlugSpriteData>[] self, List<SlugSpriteData>[] other)
  {
    for (int i = 0; i < self.Length; ++i)
      if (other[i] != null)
      {
        self[i] = new();
        foreach (SlugSpriteData spriteData in other[i])
          self[i].Add(new(spriteData));
      }
  }

  /// <summary>
  /// Returns custom sprites for slugcat, if they exist
  /// </summary>
  public static bool TryGetSupportedSlugcat(this PlayerGraphics self, out SlugcatSprites sprites)
  {
    Player player = self.owner as Player;
    if (SpriteHandler.customSprites.TryGetValue(player.slugcatStats.name, out SlugcatSprites baseSprites))
    {
      sprites = SpriteHandler.playerSprites.GetValue(player, k => new(baseSprites));
      return true;
    }
    sprites = null;
    return false;
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
      FSprite newSprite = sLeaser.sprites[spriteData.realIndex] = spriteData.mesh == null ? new FSprite(spriteData.defaultSprite)
        : new TriangleMesh(spriteData.defaultSprite, (TriangleMesh.Triangle[])spriteData.mesh.triangles.Clone(), false),
        baseSprite = sLeaser.sprites[baseIndex];
      if (spriteData.mesh?.mapUV ?? false)
        SpriteHandler.MapUV(newSprite as TriangleMesh);
      newSprite._anchorX = baseSprite._anchorX;
      newSprite._anchorY = baseSprite._anchorY;
      newSprite._areLocalVerticesDirty = spriteData.areLocalVerticesDirty || baseSprite._areLocalVerticesDirty;
      newSprite._scaleX = baseSprite._scaleX;
      newSprite._scaleY = baseSprite._scaleY;
      newSprite._rotation = baseSprite._rotation;
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
      sLeaser.sprites[self[behindFirstIndex].realIndex].MoveBehindOtherNode(sLeaser.sprites[baseIndex]);
      for (int i = behindFirstIndex - 1; i >= 0; --i)
        sLeaser.sprites[self[i].realIndex].MoveBehindOtherNode(sLeaser.sprites[self[i + 1].realIndex]);
    }
    if (++behindFirstIndex >= self.Count)
      return;
    sLeaser.sprites[self[behindFirstIndex].realIndex].MoveInFrontOfOtherNode(sLeaser.sprites[baseIndex]);
    for (int i = behindFirstIndex + 1; i < self.Count; ++i)
      sLeaser.sprites[self[i].realIndex].MoveInFrontOfOtherNode(sLeaser.sprites[self[i - 1].realIndex]);
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
      sLeaser.sprites[spriteData.realIndex].SetSpriteFromName(spriteData.sprite + suffix);
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
  
  /// <summary>
   /// Moves all custom sprites at given index behind their base sprite
   /// </summary>
  public static void ApplyPartOrder(this List<SlugSpriteData> self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites)
  {
    int baseIndex = sprites.basePartOrder.IndexOf(self[0].groupIndex) + 1;
    if (baseIndex == 0 || baseIndex == sprites.basePartOrder.Count)
      return;
    FSprite baseSprite = sLeaser.sprites[sprites.basePartOrder[baseIndex]];
    foreach (SlugSpriteData spriteData in self)
      sLeaser.sprites[spriteData.realIndex].MoveBehindOtherNode(baseSprite);
  }

  /// <summary>
  /// Replaces sprite's element with one, that has given name
  /// </summary>
  public static void SetSpriteFromName(this FSprite self, string spriteName)
  {
    self.element = Futile.atlasManager.GetElementWithName(spriteName);
  }

  /// <summary>
  /// Wrapper layer for handler calls to allow applying offsets/scales/rotation to mesh
  /// </summary>
  public static void SpriteHandlerWrapper(this SlugSpriteData self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
  {
    self.mesh.handler(playerGraphics, sLeaser, rCam, timeStacker, camPos, sprites, self);
    if (!self.customRotation && !self.customOffsets)
      return;
    Vector2[] vertexes = (sLeaser.sprites[self.realIndex] as TriangleMesh).vertices;
    Vector2 basePos;
    if (self.mesh.firstVertexAsOrigin)
      basePos = vertexes[0];
    else if (self.mesh.fv2AsOrigin)
      basePos = (vertexes[0] + vertexes[1]) / 2f;
    else
      basePos = sLeaser.sprites[_sprite.ibody].GetPosition();
    float
      offsetX = basePos.x, offsetY = basePos.y,
      scaleX = self.scaleX, scaleY = self.scaleY,
      anchorX = self.anchorX, anchorY = self.anchorY,
      rx = self.rx, ry = self.ry;

    if (self.mesh.rotateOrigin)
    {
      float rotation = sLeaser.sprites[_sprite.ibody]._rotation * Mathf.Deg2Rad;
      offsetX += self.mesh.originX * (float)Math.Cos(rotation);
      offsetY += self.mesh.originY * (float)Math.Sin(rotation);
    }
    else
    {
      offsetX += self.mesh.originX;
      offsetY += self.mesh.originY;
    }

    if (self.customRotation && self.customOffsets)
    {
      float dx = anchorX + offsetX, dy = anchorY + offsetY;
      if (self.customScale)
        for (int i = 0; i < vertexes.Length; ++i)
        {
          ref Vector2 vertex = ref vertexes[i];
          float x = vertex.x - offsetX, y = vertex.y - offsetY;
          vertex.x = (x * rx - y * ry) * scaleX + dx;
          vertex.y = (x * ry + y * rx) * scaleY + dy;
        }
      else
        for (int i = 0; i < vertexes.Length; ++i)
        {
          ref Vector2 vertex = ref vertexes[i];
          float x = vertex.x - offsetX, y = vertex.y - offsetY;
          vertex.x = x * rx - y * ry + dx;
          vertex.y = x * ry + y * rx + dy;
        }
    }
    else if (self.customRotation)
      for (int i = 0; i < vertexes.Length; ++i)
      {
        ref Vector2 vertex = ref vertexes[i];
        float x = vertex.x - offsetX, y = vertex.y - offsetY;
        vertex.x = x * rx - y * ry + offsetX;
        vertex.y = x * ry + y * rx + offsetY;
      }
    else
    {
      if (self.customScale)
      {
        float dx = anchorX + offsetX, dy = anchorY + offsetY;
        for (int i = 0; i < vertexes.Length; ++i)
        {
          ref Vector2 vertex = ref vertexes[i];
          vertex.x = (vertex.x - offsetX) * scaleX + dx;
          vertex.y = (vertex.y - offsetY) * scaleY + dy;
        }
      }
      else
        for (int i = 0; i < vertexes.Length; ++i)
        {
          ref Vector2 vertex = ref vertexes[i];
          vertex.x += anchorX;
          vertex.y += anchorY;
        }
    }
  }

  /// <summary>
  /// Processes sprite handlers if any are defiend
  /// </summary>
  public static void ManageSpriteHandlersS(this List<SlugSpriteData> self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
  {
    if (self == null)
      return;
    foreach (SlugSpriteData spriteData in self)
      if (spriteData.mesh != null)
        spriteData.SpriteHandlerWrapper(playerGraphics, sLeaser, rCam, timeStacker, camPos, sprites);
  }

  /// <summary>
  /// Processes sprite handlers if any are defiend and returns left non-processed sprites to be processed
  /// </summary>
  public static List<SlugSpriteData> ManageSpriteHandlers(this List<SlugSpriteData> self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
  {
    List<SlugSpriteData> unhandled = new();
    if (self == null)
      return unhandled;
    foreach (SlugSpriteData spriteData in self)
      if (spriteData.mesh != null)
        spriteData.SpriteHandlerWrapper(playerGraphics, sLeaser, rCam, timeStacker, camPos, sprites);
      else
        unhandled.Add(spriteData);
    return unhandled;
  }
}

public class _sprite
{
  /// <summary>
  /// Universal handled name or suffix
  /// </summary>
  public const string body = "body", hips = "hips", tail = "tail", head = "head", legs = "legs", arm = "arm",
    arm1 = "arm1", arm2 = "arm2", terrainHand = "terrainHand", terrainHand1 = "terrainHand1",
    terrainHand2 = "terrainHand2", face = "face", pixel = "pixel", other = "other",
    Abody = "A", Ahips = "A", Atail = "", Ahead = "A0", Alegs = "A0", Aarm = "0", AterrainHand = "",
    Aface = "A0", Apixel = "", Aother = "";
  /// <summary>
  /// Base sprite index
  /// <para><c>iother</c> - not real index. It's only used by slugsprites as optimization</para>
  /// </summary>
  public const int ibody = 0, ihips = 1, itail = 2, ihead = 3, ilegs = 4, iarm1 = 5, iarm2 = 6,
    iterrainHand1 = 7, iterrainHand2 = 8, iface = 9, ipixel = 11, iother = 10;
  /// <summary>
  /// All handled group names
  /// </summary>
  public static readonly string[] groups = { body, hips, tail, head, legs, arm1, arm2, terrainHand1,
    terrainHand2, face, other, pixel };
  /// <summary>
  /// Suffixes for each sprite
  /// </summary>
  public static readonly string[] suffixes = { Abody, Ahips, Atail, Ahead, Alegs, Aarm, Aarm, AterrainHand,
  AterrainHand, Aface, Aother, Apixel };
  /// <summary>
  /// Suffix length for each sprite
  /// </summary>
  public static readonly int[] suffixLength = { 1, 1, 0, 2, 2, 1, 1, 0, 0, 2, 0, 0 };
  /// <summary>
  /// Indexes for each body part name
  /// <para><c>other</c> - not real index. It's only used as one internally by slugsprites as optimization</para>
  /// </summary>
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
  /// <summary>
  /// Stores player data for sprite hot reloading
  /// </summary>
  public static ConditionalWeakTable<Player, ManagedPlayerData> managedPlayers = new();
  /// <summary>
  /// Stores custom sprite data per slugcat name
  /// </summary>
  public static Dictionary<SlugcatStats.Name, SlugcatSprites> customSprites = new();
  /// <summary>
  /// Stores unique sprites for each managed player
  /// </summary>
  public static ConditionalWeakTable<Player, SlugcatSprites> playerSprites = new();
  /// <summary>
  /// Manages sprites loaded by slugsprites to unload, before hot reloading
  /// </summary>
  public static List<string> loadedAtlases = new();

  /// <summary>
  /// Maybe makes detailed textures, assigned to meshes appear properly. Slightly modified version of similar method from <c>DMS</c>.
  /// </summary>
  public static void MapUV(TriangleMesh mesh)
  {
    for (int vertex = mesh.vertices.Length - 1; vertex >= 0; vertex--)
    {
      float uvix = 1f, uviy = 0f;
      if (vertex != mesh.vertices.Length - 1)
      {
        uvix = (float)vertex / mesh.vertices.Length;
        if (vertex % 2 != 0)
          uviy = 1f;
      }

      mesh.UVvertices[vertex] = new(
        Mathf.Lerp(mesh.element.uvBottomLeft.x, mesh.element.uvTopRight.x, uvix),
        Mathf.Lerp(mesh.element.uvBottomLeft.y, mesh.element.uvTopRight.y, uviy));
    }
  }

  /// <summary>
  /// Returns proper tail segment radius. Slightly modified version of similar method from <c>DMS</c>.
  /// </summary>
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
      MapUV(sLeaser.sprites[_sprite.itail] as TriangleMesh);

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
    sprites.CustomTail?.CreateSprites(sLeaser, _sprite.itail);
    sprites.CustomOther?.CreateSprites(sLeaser, _sprite.ibody);

    sprites.colors = new Color[Math.Max(sprites.colorAmount, 2)];
    if (sprites.colors.Length == 2)
    {
      sprites.colors[0] = sLeaser.sprites[_sprite.ibody].color;
      sprites.colors[1] = sLeaser.sprites[_sprite.iface].color;
    }

    // Use default colour sets, when they can't be customized
    if (sprites.colorSets != null && (self.owner.room.game.session is ArenaGameSession || self.owner.room.game.session is StoryGameSession
      && self.useJollyColor && RWCustom.Custom.rainWorld.options.jollyColorMode == Options.JollyColorMode.AUTO))
      for (int i = 0; i < sprites.colorAmount; ++i)
        sprites.colors[i] = sprites.colorSets[(self.owner as Player).playerState.playerNumber % 4][i];
    // Use SlugBase colours if it's present
    else if (Plugin.isSlugBaseActive && SlugBaseCharacter.Registry.Keys.Contains(sprites.owner))
      for (int i = 0; i < sprites.colorAmount; ++i)
        sprites.colors[i] = PlayerColor.GetCustomColor(self, i);
    // Use existing custom colours, if SlugBase isn't present
    else if (PlayerGraphics.CustomColorsEnabled())
      for (int i = 0; i < sprites.colorAmount; ++i)
        sprites.colors[i] = PlayerGraphics.customColors[i];

    foreach (List<SlugSpriteData> spriteList in sprites.additionalSprites)
      if (spriteList != null)
        foreach (SlugSpriteData spriteData in spriteList)
          foreach (Animation animation in spriteData.animations)
            animation.Initialize(self, sLeaser, sprites, spriteData);

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

    if (sprites.BaseLegs != null)
    {
      FSprite legsSprite = sLeaser.sprites[_sprite.ilegs];
      string currentName = legsSprite._element.name;
      if (currentName.StartsWith("LegsA"))
      {
        switch (currentName.Substring(5, currentName.Length - 6))
        {
          case "Air":
            legsSprite.SetSpriteFromName(sprites._cachedLegsM.AAir0);
            break;
          case "Pol":
            legsSprite.SetSpriteFromName(sprites._cachedLegsM.APole);
            break;
          case "VerticalPol":
            legsSprite.SetSpriteFromName(sprites._cachedLegsM.AVerticalPole);
            break;
          case "Wal":
            legsSprite.SetSpriteFromName(sprites._cachedLegsM.AWall);
            break;
          case "OnPol":
            legsSprite.SetSpriteFromName(sprites._cachedLegsM.AOnPole[currentName[currentName.Length - 1] - '0']);
            break;
        }
      }
    }

    foreach (List<SlugSpriteData> spriteList in sprites.additionalUpdatable)
      foreach (SlugSpriteData spriteData in spriteList.ManageSpriteHandlers(self, sLeaser, rCam, timeStacker, camPos, sprites))
      {
        FSprite sprite = sLeaser.sprites[spriteData.realIndex],
          baseSprite = sLeaser.sprites[spriteData.groupIndex];
        sprite._x = baseSprite._x;
        sprite._y = baseSprite._y;
        sprite._scaleX = baseSprite._scaleX * spriteData.scaleX;
        sprite._scaleY = baseSprite._scaleY * spriteData.scaleY;
        sprite._anchorX = baseSprite._anchorX + spriteData.anchorX;
        sprite._anchorY = baseSprite._anchorY + spriteData.anchorY;
        sprite._rotation = baseSprite._rotation + spriteData.rotation;
        sprite._isMatrixDirty = spriteData.isMatrixDirty || baseSprite._isMatrixDirty;
        sprite._areLocalVerticesDirty = spriteData.areLocalVerticesDirty || baseSprite._areLocalVerticesDirty;
        if (spriteData.groupIndex != _sprite.ipixel)
          continue;
        sprite.alpha = baseSprite.alpha;
        sprite.isVisible = baseSprite.isVisible;
      }

    sprites.CustomTail.ManageSpriteHandlersS(self, sLeaser, rCam, timeStacker, camPos, sprites);

    foreach (SlugSpriteData spriteData in sprites.CustomOther.ManageSpriteHandlers(self, sLeaser, rCam, timeStacker, camPos, sprites))
    {
      FSprite sprite = sLeaser.sprites[spriteData.realIndex],
        baseSprite = sLeaser.sprites[_sprite.ibody];
      sprite._x = baseSprite._x;
      sprite._y = baseSprite._y;
      sprite._rotation = baseSprite._rotation + spriteData.rotation;
      sprite._isMatrixDirty = baseSprite._isMatrixDirty;
      sprite._areLocalVerticesDirty = baseSprite._areLocalVerticesDirty;
    }

    sprites.CustomFace?.UpdateElements(sLeaser);
    sprites.CustomHead?.UpdateElements(sLeaser);
    sprites.CustomArm1?.UpdateElements(sLeaser);
    sprites.CustomArm2?.UpdateElements(sLeaser);
    sprites.CustomLegs?.UpdateElements(sLeaser);

    foreach (SlugSpriteData spriteData in sprites.baseSprites)
      if (spriteData != null)
        foreach (Animation animation in spriteData.animations)
          animation.Update(self, sLeaser, rCam, timeStacker, camPos, sprites, spriteData);
    foreach (List<SlugSpriteData> spriteList in sprites.additionalSprites)
      if (spriteList != null)
        foreach (SlugSpriteData spriteData in spriteList)
          foreach (Animation animation in spriteData.animations)
            animation.Update(self, sLeaser, rCam, timeStacker, camPos, sprites, spriteData);

    return sprites;
  }

  public static SlugcatSprites AddToContainer(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
  {
    if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
      return null;

    newContatiner ??= rCam.ReturnFContainer("Midground");
    for (int i = sprites.firstSpriteIndex; i < sLeaser.sprites.Length; ++i)
      newContatiner.AddChild(sLeaser.sprites[i]);

    int? otherIndexReplacer = null;
    if (sprites.basePartOrder.Any())
    {
      int otherIndex = sprites.partOrder.IndexOf(_sprite.iother);
      if (otherIndex != -1)
        otherIndexReplacer = otherIndex == 0 ? sprites.partOrder[1] : sprites.partOrder[otherIndex - 1];
      if (sprites.hasFullPartOrder)
        sLeaser.sprites[sprites.basePartOrder[0]].MoveToBack();
      for (int i = 1; i < sprites.basePartOrder.Count; ++i)
        sLeaser.sprites[sprites.basePartOrder[i]].MoveInFrontOfOtherNode(sLeaser.sprites[sprites.basePartOrder[i - 1]]);
    }

    foreach (List<SlugSpriteData> spriteList in sprites.additionalUpdatable)
    {
      spriteList?.UpdateNodes(sLeaser, spriteList[0].groupIndex);
      // spriteList?.ApplyPartOrder(sLeaser, sprites);
    }
    sprites.CustomTail?.UpdateNodes(sLeaser, _sprite.itail);
    // sprites.CustomTail?.ApplyPartOrder(sLeaser, sprites);
    sprites.CustomOther?.UpdateNodes(sLeaser, otherIndexReplacer ?? _sprite.ibody);

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
            sLeaser.sprites[spriteData.realIndex].color = sprites.colors[spriteData.colorIndex];

    return sprites;
  }

  public static void UnloadAtlases()
  {
    foreach (string atlasPath in loadedAtlases)
      Futile.atlasManager.UnloadAtlas(atlasPath);
    loadedAtlases.Clear();
  }

  public static void LoadAtlases(string atlasesFolder)
  {
    List<string> paths = FileUtils.ListDirectory(atlasesFolder, out FileUtils.Result opResult, ".txt");
    if (opResult == FileUtils.Result.NoDirectory)
      throw new Exception($"specified \"atlases\" folder doesn't exist");
    if (opResult == FileUtils.Result.NoFiles)
      throw new Exception($"specified \"atlases\" folder doesn't contain any files");
    if (opResult == FileUtils.Result.NoFilesWithExtension)
      throw new Exception($"specified \"atlases\" folder doesn't contain any atlases(.txt files) - slugsprites requires atlases");
    foreach (string path in paths)
    {
      string atlasPath = path.Substring(0, path.Length - 4);
      loadedAtlases.Add(atlasPath);
      Futile.atlasManager.LoadAtlas(atlasPath);
    }
  }

  /// <summary>
  /// Reloads slugsprites assets and atlases
  /// </summary>
  public static void LoadCustomSprites()
  {
    try
    {
      Log.LogInfo($"[[#]] Started loading process");
      AnimationHandler.LoadAnimations();
      MeshHandler.LoadMeshes();

      Log.LogInfo("[*] Loading custom sprites");
      UnloadAtlases();
      customSprites.Clear();
      playerSprites = new();

      List<string> paths = FileUtils.ListDirectory("slugsprites", out FileUtils.Result opResult, ".json");
      if (opResult == FileUtils.Result.NoFiles)
      {
        Log.LogError($"Failed to load - \"slugsprites\" folder doesn't contain any files");
        return;
      }
      if (opResult == FileUtils.Result.NoFilesWithExtension)
      {
        Log.LogError($"Failed to load - \"slugsprites\" folder doesn't contain any .json files");
        return;
      }
      int failureCounter = 0;
      foreach (string path in paths)
      {
        Log.LogInfo($" Reading at: {path}");
        foreach (KeyValuePair<string, object> slugcatSprites in Json.Parser.Parse(File.ReadAllText(path)) as Dictionary<string, object>)
        {
          if (slugcatSprites.Key.TryGetExtEnum(out SlugcatStats.Name slugcatName))
          {
            if (Plugin.isSlugBaseActive && SlugBaseCharacter.TryGet(slugcatName, out SlugBaseCharacter character)
              && !PlayerFeatures.CustomColors.TryGet(character, out _))
            {
              Log.LogError($"SlugBase config of slugcat \"{slugcatName}\" is missing required \"custom_colors\" feature - its custom sprites won't be loaded.");
              continue;
            }
            Log.LogInfo($"<> Loading sprites for {slugcatSprites.Key}");
            try
            {
              if (customSprites.ContainsKey(slugcatName))
                Log.LogWarning($"Overriding configuration for {slugcatName} - multiple configurations for this slugcat are present");
              customSprites[slugcatName] = new(slugcatSprites.Value as Dictionary<string, object>) { owner = slugcatName };
            }
            catch (Exception ei)
            {
              Log.LogError($"Failed to load custom sprites: {ei}");
              ++failureCounter;
            }
          }
          else
          {
            Log.LogError($"Failed to find slugcat with name {slugcatSprites.Key}");
            ++failureCounter;
          }
        }
      }
      Log.LogInfo($"[+] Finished loading sprites: {customSprites.Count}/{customSprites.Count + failureCounter} sprite sets were successfully loaded");
      Log.LogInfo($"[[#]] Finished loading process");
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}

/// <summary>
/// Cached names for some legs sprites, that have to be replaced manually
/// </summary>
public struct CachedLegsM
{
  public string AAir0, APole, AVerticalPole, AWall;
  public string[] AOnPole;

  public CachedLegsM(string baseName)
  {
    AAir0 = baseName + "AAir0";
    APole = baseName + "APole";
    AVerticalPole = baseName + "AVerticalPole";
    AWall = baseName + "AWall";
    AOnPole = new[] { baseName + "AOnPole0", baseName + "AOnPole1", baseName + "AOnPole2",
      baseName + "AOnPole3", baseName + "AOnPole4", baseName + "AOnPole5", baseName + "AOnPole6" };
  }
}

public class SlugcatSprites
{
  /// <summary>
  /// Supported sprite group name
  /// </summary>
  public static readonly string[] supportedSprites = new string[] { _sprite.body, _sprite.hips, _sprite.tail, _sprite.head, _sprite.legs,
    _sprite.arm, _sprite.terrainHand, _sprite.face, _sprite.pixel, _sprite.other };
  public SlugcatStats.Name owner;
  /// <summary>
  /// Cached base colors
  /// </summary>
  public Color[] colors;
  /// <summary>
  /// Color sets to replace colors in specific situations
  /// </summary>
  public Color[][] colorSets;
  /// <summary>
  /// Use respective property instead in most cases.
  /// <para><c>baseSprites</c> - all managed base sprites</para>
  /// <para><c>baseUpdatable</c> - all easily updatable managed base sprites</para>
  /// </summary>
  public SlugSpriteData[] baseSprites = new SlugSpriteData[_sprite.groups.Length],
    baseUpdatable = new SlugSpriteData[_sprite.groups.Length - 2];
  /// <summary>
  /// Use respective property instead in most cases.
  /// <para><c>baseSprites</c> - all managed custom sprites</para>
  /// <para><c>baseUpdatable</c> - all easily updatable managed custom sprites</para>
  /// </summary>
  public List<SlugSpriteData>[] additionalSprites = new List<SlugSpriteData>[_sprite.groups.Length],
    additionalUpdatable = new List<SlugSpriteData>[_sprite.groups.Length - 2];
  /// <summary>
  /// Order of body parts, used to customize order in which sprites are drawn
  /// </summary>
  public List<int> partOrder = new(), basePartOrder = new();
  public CachedLegsM _cachedLegsM = new();
  public bool hasFullPartOrder = false;
  /// <summary>
  /// Index of first custom sprite
  /// </summary>
  public int firstSpriteIndex = 0;
  /// <summary>
  /// Amount of all used colors
  /// </summary>
  public int colorAmount = 0;
  public int tailLength = 4, tailWideness = 1;
  public float tailRoundness = 0f;

  #region SlugSpriteData simple access properties
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
      Log.LogWarning("Slugcat configuration is missing \"atlases\" field");
    sprites.TryUpdateNumber("tailLength", ref tailLength);
    if (tailLength < 4)
      throw new Exception("\"tailLength\" can't be lower than 4");
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
    if (sprites.TryGetValueWithType("partOrders", out List<object> groupOrder))
    {
      foreach (object groupName in groupOrder)
        if (_sprite.indexes.TryGetValue((string)groupName, out int index))
        {
          if (partOrder.Contains(index))
            Log.LogWarning($"Found duplicate part, in \"partOrders\": \"{groupName}\" - skipping it");
          else
            partOrder.Add(index);
        }
        else
          Log.LogWarning($"Found invalid part name, in \"partOrders\": \"{groupName}\" - skipping it");
      if (partOrder.Count != 12)
        Log.LogWarning($"\"partOrders\" isn't full and won't be applied entirely. Full list requires 12 body parts");
      else
        hasFullPartOrder = true;
      basePartOrder = partOrder.Where(i => i != _sprite.iother).ToList();
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
              if (groupIndex == _sprite.iother && (!spriteData.mesh?.compatibleAsOther ?? false))
                throw new Exception($"can't apply mesh \"{spriteData.mesh.name}\" to sprite of the group \"other\" due to its properties");

              // Debug message
              string suffix = _sprite.suffixes[groupIndex],
                spriteName = spriteData.defaultSprite = spriteData.sprite + suffix;
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

              colorAmount = Math.Max(colorAmount, spriteData.colorIndex);
              if (spriteData.order == 0)
                baseSprites[groupIndex] = spriteData;
              else
              {
                if (spriteData.mesh == null && groupIndex == _sprite.itail)
                  spriteData.mesh = MeshHandler.meshes["tailDefault"];

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

    SetUpdatableSpriteSets();
    if (BaseLegs != null)
      _cachedLegsM = new(BaseLegs.sprite);
  }

  public SlugcatSprites(SlugcatSprites other)
  {
    owner = other.owner;
    tailLength = other.tailLength;
    tailWideness = other.tailWideness;
    tailRoundness = other.tailRoundness;
    colorAmount = other.colorAmount;

    if (other.colorSets != null)
    {
      colorSets = new Color[other.colorSets.Length][];
      for (int i = 0; i < other.colorSets.Length; ++i)
        colorSets[i] = (Color[])other.colorSets[i].Clone();
    }

    partOrder = new(other.partOrder);
    basePartOrder = new(other.basePartOrder);

    baseSprites.CloneFrom(other.baseSprites);
    additionalSprites.CloneFrom(other.additionalSprites);
    SetUpdatableSpriteSets();
    _cachedLegsM = other._cachedLegsM;
  }

  public void SetUpdatableSpriteSets()
  {
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
  /// <summary>
  /// Sprite order
  /// <para>If 0, sprite is based</para>
  /// <para>If less than 0, sprite is drawn behind base sprite</para>
  /// <para>If bigger than 0, sprite is drawn in front of base sprite</para>
  /// </summary>
  public int order = 0;
  /// <summary>
  /// Index of sprite in sLeaser
  /// </summary>
  public int realIndex = 0;
  /// <summary>
  /// Index of color, used by sprite
  /// </summary>
  public int colorIndex = 0;
  /// <summary>
  /// Internal group index(base sprite index)
  /// </summary>
  public int groupIndex = 0;
  public float anchorX = 0f, anchorY = 0f, scaleX = 1f, scaleY = 1f, rotation = 0f, rx = 0f, ry = 0f;
  public bool areLocalVerticesDirty = false, isMatrixDirty = false, customOffsets = false, customScale = false, customRotation = false;
  /// <summary>
  /// Sprite name
  /// </summary>
  public string sprite = "Futile_White";
  /// <summary>
  /// Initial sprite name(sprite name with suffix)
  /// </summary>
  public string defaultSprite;
  /// <summary>
  /// Base sprite name(used to sync animated sprite states)
  /// </summary>
  public string baseSprite;
  /// <summary>
  /// Previous base sprite name(used to sync animated sprite states)
  /// </summary>
  public string previousBaseSprite = "";
  public List<Animation> animations = new();
  public AnimationColor animationColor;
  public AnimationPos animationPos;
  public AnimationElement animationElement;
  public CustomMesh mesh;
  //public AGCachedStrings3Dim _cachedFaceSpriteNames;
  //public AGCachedStrings2Dim _cachedHeads;
  //public AGCachedStrings _cachedPlayerArms, _cachedLegsA, _cachedLegsACrawling, _cachedLegsAClimbing, _cachedLegsAOnPole;

  public SlugSpriteData(Dictionary<string, object> spriteData)
  {
    spriteData.TryUpdateNumber("order", ref order);
    if (!spriteData.TryGetValueWithType("sprite", out sprite))
      throw new Exception($"sprite configuration with order {order} is missing \"sprite\" field");
    spriteData.TryUpdateNumber("colorIndex", ref colorIndex);
    if (colorIndex < 0)
      throw new Exception($"sprite configuration with order {order} has incorrect value of \"colorIndex\" - must be 0 or greater");

    // animations
    if (spriteData.TryGetValueWithType("animations", out List<object> animationList))
      foreach (object animationName in animationList)
        if (AnimationHandler.animations.TryGetValue((string)animationName, out Animation animation))
        {
          Animation newAnimation = animation.Clone();
          animations.Add(newAnimation);
          if (animation is AnimationColor)
          {
            if (animationColor != null)
              Log.LogWarning($"Color animation for \"{sprite}[{order}]\" already was defined, overriding \"{animationColor.name}\" with \"{animation.name}\"");
            animationColor = newAnimation as AnimationColor;
          }
          else if (animation is AnimationPos)
          {
            if (animationPos != null)
              Log.LogWarning($"Pos animation for \"{sprite}[{order}]\" already was defined, overriding \"{animationPos.name}\" with \"{animation.name}\"");
            animationPos = newAnimation as AnimationPos;
          }
          else if (animation is AnimationElement)
          {
            if (animationElement != null)
              Log.LogWarning($"Element animation for \"{sprite}[{order}]\" already was defined, overriding \"{animationElement.name}\" with \"{animation.name}\"");
            animationElement = newAnimation as AnimationElement;
          }
        }
        else
          throw new Exception($"animation \"{animationName}\", used in \"{sprite}[{order}]\" doesn't exist");
    if (order == 0 && animations.Count != 0)
      throw new Exception($"can't assign animations to sprite with order 0");

    // mesh
    if (spriteData.TryGetValueWithType("mesh", out string meshType))
    {
      if (order == 0)
        throw new Exception($"can't assign mesh to sprite with order 0");
      if (!MeshHandler.meshes.TryGetValue(meshType, out mesh))
        throw new Exception($"mesh with type \"{meshType}\" doesn't exist");
    }

    spriteData.TryUpdateNumber("anchorX", ref anchorX);
    spriteData.TryUpdateNumber("anchorY", ref anchorY);
    spriteData.TryUpdateNumber("scaleX", ref scaleX);
    spriteData.TryUpdateNumber("scaleY", ref scaleY);
    spriteData.TryUpdateNumber("rotation", ref rotation);
    areLocalVerticesDirty = anchorX != 0f || anchorY != 0f;
    isMatrixDirty = scaleX != 1f || scaleY != 1f || rotation != 0f;
    customOffsets = anchorX != 0f || anchorY != 0f || scaleX != 1f || scaleY != 1f;
    customScale = scaleX != 1f || scaleY != 1f;
    if (customRotation = rotation != 0f)
    {
      rx = (float)Math.Cos(rotation);
      ry = (float)Math.Sin(rotation);
    }
  }

  public SlugSpriteData(SlugSpriteData other)
  {
    order = other.order;
    realIndex = other.realIndex;
    colorIndex = other.colorIndex;
    groupIndex = other.groupIndex;
    anchorX = other.anchorX;
    anchorY = other.anchorY;
    scaleX = other.scaleX;
    scaleY = other.scaleY;
    rotation = other.rotation;
    rx = other.rx;
    ry = other.ry;
    areLocalVerticesDirty = other.areLocalVerticesDirty;
    isMatrixDirty = other.isMatrixDirty;
    customOffsets = other.customOffsets;
    customScale = other.customScale;
    customRotation = other.customRotation;
    sprite = other.sprite;
    defaultSprite = other.defaultSprite;

    foreach (Animation animation in other.animations)
      animations.Add(animation.Clone());
    animationColor = animations.LastOrDefault(a => a is AnimationColor) as AnimationColor;
    animationPos = animations.LastOrDefault(a => a is AnimationPos) as AnimationPos;
    animationElement = animations.LastOrDefault(a => a is AnimationElement) as AnimationElement;
    mesh = other.mesh;
  }

  /// <summary>
  /// Changes element of the sprite to custom one
  /// </summary>
  public void ResetToCustomElement(RoomCamera.SpriteLeaser sLeaser)
  {
    sLeaser.sprites[groupIndex].element = Futile.atlasManager._allElementsByName[defaultSprite];
  }
}
