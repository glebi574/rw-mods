using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.ConverterExt;
using gelbi_silly_lib.OtherExt;
using SlugBase;
using SlugBase.DataTypes;
using SlugBase.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using static slugsprites.AnimationColor;
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
    foreach (SlugSpriteData spriteData in self)
    {
      FSprite newSprite = sLeaser.sprites[spriteData.realIndex] = spriteData.mesh == null ? new FSprite(spriteData.defaultSprite)
        : new TriangleMesh(spriteData.defaultSprite, (TriangleMesh.Triangle[])spriteData.mesh.triangles.Clone(), false),
        baseSprite = sLeaser.sprites[baseIndex];
      spriteData.previousBaseElement = baseSprite._element;
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
  /// Replaces sprite's element with one, that has given name
  /// </summary>
  public static void SetSpriteFromName(this FSprite self, string spriteName)
  {
    self.element = Futile.atlasManager.GetElementWithName(spriteName);
  }

  /// <summary>
  /// Wrapper layer for handler calls to allow applying offsets/scales/rotation to mesh
  /// </summary>
  public static void ApplyMeshes(this SlugSpriteData self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser,
    RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
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
  public static void ManageDefinedSpriteHandlers(this List<SlugSpriteData> self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser,
    RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
  {
    if (self == null)
      return;
    foreach (SlugSpriteData spriteData in self)
      if (spriteData.mesh != null)
        spriteData.ApplyMeshes(playerGraphics, sLeaser, rCam, timeStacker, camPos, sprites);
  }

  /// <summary>
  /// Processes sprite handlers if any are defiend and returns unprocessed sprites to be processed
  /// </summary>
  public static List<SlugSpriteData> ManageSpriteHandlers(this List<SlugSpriteData> self, PlayerGraphics playerGraphics, RoomCamera.SpriteLeaser sLeaser,
    RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites)
  {
    List<SlugSpriteData> unhandled = new();
    if (self == null)
      return unhandled;
    foreach (SlugSpriteData spriteData in self)
      if (spriteData.mesh != null)
        spriteData.ApplyMeshes(playerGraphics, sLeaser, rCam, timeStacker, camPos, sprites);
      else
        unhandled.Add(spriteData);
    return unhandled;
  }

  /// <summary>
  /// Syncs elements according to cache, selecting left/right version based on scaleX of original sprite
  /// </summary>
  public static void UpdateElements(this SlugcatSprites self, RoomCamera.SpriteLeaser sLeaser, int groupIndex)
  {
    SlugSpriteData baseSprite = self.baseSprites[groupIndex];
    List<SlugSpriteData> additionalSprites = self.additionalSprites[groupIndex];
    FAtlasElement element = sLeaser.sprites[groupIndex]._element;
    bool hasBaseSprite = baseSprite != null, hasAdditionalSprites = additionalSprites != null,
      // only 1st arm uses scaleY = -1
      asymmetricRight = groupIndex != _sprite.iarm1 && sLeaser.sprites[groupIndex]._scaleX >= 0f;
    int asymmetricIndex = asymmetricRight ? 2 : 1;
    if (hasBaseSprite && baseSprite.previousBaseElement != element || hasAdditionalSprites && additionalSprites[0].previousBaseElement != element)
    {
      string key = null;
      if (hasAdditionalSprites)
      {
        key = element.name.Substring(additionalSprites[0].cachedElements.keyOffset);
        foreach (SlugSpriteData spriteData in additionalSprites)
        {
          int index = 0;
          if (spriteData.isAsymmetric)
          {
            index = asymmetricIndex;
            spriteData.asymmetricRight = asymmetricRight;
            spriteData.lastCacheKey = key;
          }
          sLeaser.sprites[spriteData.realIndex].element = spriteData.cachedElements.elements[index][key];
        }
        if (!hasBaseSprite)
          foreach (SlugSpriteData spriteData in additionalSprites)
            spriteData.previousBaseElement = element;
      }
      if (hasBaseSprite)
      {
        key ??= element.name.Substring(baseSprite.cachedElements.keyOffset);
        int index = 0;
        if (baseSprite.isAsymmetric)
        {
          index = asymmetricIndex;
          baseSprite.asymmetricRight = asymmetricRight;
          baseSprite.lastCacheKey = key;
        }
        baseSprite.previousBaseElement = sLeaser.sprites[groupIndex].element = baseSprite.cachedElements.elements[index][key];
        if (hasAdditionalSprites)
          foreach (SlugSpriteData spriteData in additionalSprites)
            spriteData.previousBaseElement = baseSprite.previousBaseElement;
      }
      return;
    }
    if (hasAdditionalSprites)
      foreach (SlugSpriteData spriteData in additionalSprites)
        if (spriteData.isAsymmetric && spriteData.asymmetricRight != asymmetricRight)
        {
          sLeaser.sprites[spriteData.realIndex].element = spriteData.cachedElements.elements[asymmetricIndex][spriteData.lastCacheKey];
          spriteData.asymmetricRight = asymmetricRight;
        }
    if (hasBaseSprite && baseSprite.isAsymmetric && baseSprite.asymmetricRight != asymmetricRight)
    {
      baseSprite.previousBaseElement = sLeaser.sprites[groupIndex].element = baseSprite.cachedElements.elements[asymmetricIndex][baseSprite.lastCacheKey];
      baseSprite.asymmetricRight = asymmetricRight;
      if (hasAdditionalSprites)
        foreach (SlugSpriteData spriteData in additionalSprites)
          spriteData.previousBaseElement = baseSprite.previousBaseElement;
    }
  }
}

public static class _sprite
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
  /// Whether group can be asymmetric
  /// </summary>
  public static readonly bool[] asymmetricGroups = new bool[] { false, false, false, true, false, true, true, true, true, true, false, false };
  /// <summary>
  /// Sprites, elements of which are managed dynamically
  /// </summary>
  public static readonly bool[] managedSprites = new bool[] { false, false, false, true, true, true, true, true, true, true, false, false };
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

public class SpecificCachedElements
{
  public Dictionary<string, FAtlasElement>[] elements;
  public int keyOffset = 0;
  public string defaultCacheKey;
  public bool hasAsymmetricSprites = false;

  public SpecificCachedElements(Dictionary<string, FAtlasElement>[] elements, int keyOffset, string defaultCacheKey, bool cacheAsymmetric = true)
  {
    this.elements = elements;
    this.defaultCacheKey = defaultCacheKey;
    this.keyOffset = keyOffset;
    if (cacheAsymmetric)
      CacheAsymmetric();
  }

  /// <summary>
  /// Clones base cache as asymmetric if asymmetric sprites exist
  /// </summary>
  public void CacheAsymmetric()
  {
    if (!elements[0].IsAsymmetric())
      return;
    hasAsymmetricSprites = true;
    Dictionary<string, FAtlasElement> left = elements[1] = new(), right = elements[2] = new();
    foreach (KeyValuePair<string, FAtlasElement> kvp in elements[0])
    {
      left[kvp.Key] = Futile.atlasManager.GetElementWithName("Left" + kvp.Value.name);
      right[kvp.Key] = Futile.atlasManager.GetElementWithName("Right" + kvp.Value.name);
    }
  }
}

public static class CachedElements
{
  public static readonly Dictionary<string, SpecificCachedElements> head = new(), legs = new(), arms = new(), hands = new(), face = new();
  /// <summary>
  /// Index for default sprite names at which key for sprite starts
  /// </summary>
  public const int offsetHead = 5, offsetLegs = 5, offsetArms = 9, offsetHands = 18, offsetFace = 4;
  // HeadA | LegsA | PlayerArm | OnTopOfTerrainHand | Face

  public static void Clear()
  {
    head.Clear();
    legs.Clear();
    arms.Clear();
    hands.Clear();
    face.Clear();
  }

  /// <summary>
  /// Caches element name+suffix at specified suffix
  /// </summary>
  public static void Cache(this Dictionary<string, FAtlasElement> self, string name, string suffix)
  {
    self[suffix] = Futile.atlasManager.GetElementWithName(name + suffix);
  }

  /// <summary>
  /// Caches elements name{0..N} at suffix{0..N}
  /// </summary>
  public static void CacheFor(this Dictionary<string, FAtlasElement> self, string name, string suffix, int lastN)
  {
    for (int i = 0; i <= lastN; ++i)
    {
      string suffixN = suffix + i;
      self[suffixN] = Futile.atlasManager.GetElementWithName(name + suffixN);
    }
  }

  /// <summary>
  /// Returns <c>true</c> if sprite has asymmetric version
  /// </summary>
  public static bool IsAsymmetric(this Dictionary<string, FAtlasElement> self)
  {
    Dictionary<string, FAtlasElement>.Enumerator enumerator = self.GetEnumerator();
    enumerator.MoveNext();
    return Futile.atlasManager._allElementsByName.ContainsKey("Left" + enumerator.Current.Value.name);
  }

  public static SpecificCachedElements GetOrCreateWith(SlugSpriteData sprite)
  {
    string baseName = sprite.sprite;
    Dictionary<string, FAtlasElement>[] elements;
    SpecificCachedElements result;
    switch (sprite.groupIndex)
    {
      case _sprite.ihead:
        if (head.TryGetValue(baseName, out result))
          return result;
        elements = new Dictionary<string, FAtlasElement>[3];
        elements[0] = new();
        elements[0].CacheFor(baseName + "A", "", 17);
        head[baseName] = result = new(elements, offsetHead, "0");
        return result;
      case _sprite.ilegs:
        if (legs.TryGetValue(baseName, out result))
          return result;
        elements = new Dictionary<string, FAtlasElement>[1];
        Dictionary<string, FAtlasElement> legCache = elements[0] = new();
        string legsA = baseName + 'A';
        legCache.CacheFor(legsA, "", 6);
        legCache.CacheFor(legsA, "Climbing", 6);
        legCache.CacheFor(legsA, "Crawling", 5);
        legCache.CacheFor(legsA, "OnPole", 6);
        legCache.Cache(legsA, "Air0");
        legCache.Cache(legsA, "Pole");
        legCache.Cache(legsA, "VerticalPole");
        legCache.Cache(legsA, "Wall");
        legs[baseName] = result = new(elements, offsetLegs, "", false);
        return result;
      case _sprite.iarm1:
      case _sprite.iarm2:
        if (arms.TryGetValue(baseName, out result))
          return result;
        elements = new Dictionary<string, FAtlasElement>[3];
        elements[0] = new();
        elements[0].CacheFor(baseName, "", 12);
        arms[baseName] = result = new(elements, offsetArms, "0");
        return result;
      case _sprite.iterrainHand1:
      case _sprite.iterrainHand2:
        if (hands.TryGetValue(baseName, out result))
          return result;
        elements = new Dictionary<string, FAtlasElement>[3];
        elements[0] = new();
        elements[0].Cache(baseName, "");
        elements[0].Cache(baseName, "2");
        hands[baseName] = result = new(elements, offsetHands, "");
        return result;
      case _sprite.iface:
        if (face.TryGetValue(baseName, out result))
          return result;
        elements = new Dictionary<string, FAtlasElement>[3];
        Dictionary<string, FAtlasElement> faceCache = elements[0] = new();
        faceCache.CacheFor(baseName, "A", 8);
        faceCache.CacheFor(baseName, "B", 8);
        faceCache.Cache(baseName, "Dead");
        faceCache.Cache(baseName, "Stunned");
        face[baseName] = result = new(elements, offsetFace, "A0");
        return result;
    }
    throw new Exception();
  }
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
  /// List of managed players
  /// </summary>
  public static List<WeakReference<Player>> managedPlayerList = new();
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

  /// <summary>
  /// Initializes colors and animations(automatically called when body color changes)
  /// </summary>
  public static void InitializeColors(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, SlugcatSprites sprites)
  {
    sprites.colors = new Color[Math.Max(sprites.colorAmount, 2)];
    if (sprites.colors.Length == 2)
    {
      sprites.colors[0] = sLeaser.sprites[_sprite.ibody].color;
      sprites.colors[1] = sLeaser.sprites[_sprite.iface].color;
    }

    if (!self.RenderAsPup)
    {
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
    }

    foreach (List<SlugSpriteData> spriteList in sprites.additionalSprites)
      if (spriteList != null)
        foreach (SlugSpriteData spriteData in spriteList)
          foreach (Animation animation in spriteData.animations)
            animation.Initialize(self, sLeaser, sprites, spriteData);
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

    InitializeColors(self, sLeaser, sprites);

    return sprites;
  }

  public static SlugcatSprites DrawSprites(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
  {
    if (Plugin.pluginInterface.debugMode.Value)
    {
      ManagedPlayerData managedPlayer = managedPlayers.GetValue(self.owner as Player, p =>
      {
        managedPlayerList.Add(new(p));
        return new();
      });
      managedPlayer.rCam = rCam;
      managedPlayer.sLeaser = sLeaser;
    }

    if (!self.TryGetSupportedSlugcat(out SlugcatSprites sprites))
      return null;

    if (sprites.colors[0] != sLeaser.sprites[_sprite.ibody]._color)
      InitializeColors(self, sLeaser, sprites);

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

    sprites.CustomTail.ManageDefinedSpriteHandlers(self, sLeaser, rCam, timeStacker, camPos, sprites);

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

    sprites.UpdateElements(sLeaser, _sprite.ihead);
    sprites.UpdateElements(sLeaser, _sprite.ilegs);
    sprites.UpdateElements(sLeaser, _sprite.iarm1);
    sprites.UpdateElements(sLeaser, _sprite.iarm2);
    sprites.UpdateElements(sLeaser, _sprite.iterrainHand1);
    sprites.UpdateElements(sLeaser, _sprite.iterrainHand2);
    sprites.UpdateElements(sLeaser, _sprite.iface);

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
      spriteList?.UpdateNodes(sLeaser, spriteList[0].groupIndex);
    sprites.CustomTail?.UpdateNodes(sLeaser, _sprite.itail);
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
      CachedElements.Clear();
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

              // Other properties
              if (groupIndex == _sprite.iother)
              {
                if (spriteData.order == 0)
                  throw new Exception($"can't have sprite of this group with order 0");
                if (!spriteData.mesh?.compatibleAsOther ?? false)
                  throw new Exception($"can't apply mesh \"{spriteData.mesh.name}\" to sprite of the group \"other\" due to its properties");
              }
              if (_sprite.managedSprites[groupIndex])
              {
                spriteData.cachedElements = CachedElements.GetOrCreateWith(spriteData);
                spriteData.lastCacheKey = spriteData.cachedElements.defaultCacheKey;
              }
              if (spriteData.isAsymmetric)
              {
                if (!_sprite.asymmetricGroups[groupIndex])
                  throw new Exception($"sprite of group \"{fieldName}\" can't be asymmetric");
                if (!spriteData.cachedElements.hasAsymmetricSprites)
                  throw new Exception($"sprite \"{spriteData.sprite}\" is specified as asymmetric, but asymmetric versions could not be found");
              }

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
            throw new Exception($"Error loading sprite configuration of group \"{field}\": {e}");
          }
        }
        additionalSprites[groupIndex]?.Sort((a, b) => a.order.CompareTo(b.order));
      }
    if (additionalSprites.Length != 0)
      ++colorAmount;

    SetUpdatableSpriteSets();
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
  /// <para>If you're using bse sprite, it'll always be 0 - use group index instead</para>
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
  public bool areLocalVerticesDirty = false, isMatrixDirty = false, customOffsets = false, customScale = false, customRotation = false,
    isAsymmetric = false, asymmetricRight = true;
  /// <summary>
  /// Sprite name
  /// </summary>
  public string sprite = "Futile_White";
  /// <summary>
  /// Initial sprite name(sprite name with suffix)
  /// </summary>
  public string defaultSprite;
  /// <summary>
  /// Last used cached elements key(used to update asymmetric sprite states)
  /// </summary>
  public string lastCacheKey;
  /// <summary>
  /// Cached elements for that sprite(used to sync animated sprite states)
  /// </summary>
  public SpecificCachedElements cachedElements;
  /// <summary>
  /// Previous base sprite(used to sync animated sprite states)
  /// </summary>
  public FAtlasElement previousBaseElement = null;
  public List<Animation> animations = new();
  public AnimationColor animationColor;
  public AnimationPos animationPos;
  public AnimationElement animationElement;
  public CustomMesh mesh;

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
    spriteData.TryUpdateValueWithType("isAsymmetric", ref isAsymmetric);
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
    isAsymmetric = other.isAsymmetric;
    sprite = other.sprite;
    defaultSprite = other.defaultSprite;
    cachedElements = other.cachedElements;
    lastCacheKey = other.lastCacheKey;

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
    previousBaseElement = sLeaser.sprites[groupIndex].element = Futile.atlasManager._allElementsByName[defaultSprite];
  }
}
