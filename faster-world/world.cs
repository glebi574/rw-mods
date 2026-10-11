using Expedition;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Watcher;

namespace faster_world;

public static class M_World
{
  public static void WorldLoader_CappingBrokenExits(WorldLoader self)
  {
    int
      counter = self.cntr,
      connectionOffset = 1,
      faultyRoom = self.faultyExits[counter].room,
      roomIndex = faultyRoom - self.world.firstRoomIndex;
    while (++counter < self.faultyExits.Count && self.faultyExits[counter].room == faultyRoom)
      ++connectionOffset;
    self.cntr = --counter;
    AbstractRoom room = self.abstractRooms[roomIndex];
    int[] newConnections = new int[room.connections.Length + connectionOffset];
    for (int i = 0; i < room.connections.Length; ++i)
      newConnections[i] = room.connections[i];
    for (int i = room.connections.Length; i < room.connections.Length + connectionOffset; ++i)
      newConnections[i] = -1;
    self.abstractRooms[roomIndex] = new(self.roomAdder[roomIndex][0], newConnections, faultyRoom,
      self.swarmRoomsList.IndexOf(faultyRoom), self.sheltersList.IndexOf(faultyRoom), self.gatesList.IndexOf(faultyRoom));
    WorldLoader.LoadAbstractRoom(self.world, self.roomAdder[roomIndex][0], self.abstractRooms[roomIndex], self.setupValues);
  }

  // well that was a disappointment
  public static string RoomPreprocessor_ConnMapToString(int connMapGeneration, AbstractRoomNode[] connMap)
  {
    StringBuilder sb = new StringBuilder().Append(connMapGeneration).Append('|').Append(connMap.Length).Append('|').Append(connMap[0].connectivity.GetLength(0)).Append('|');
    for (int i = 0; i < connMap.Length; ++i)
    {
      if (connMap[i].type.Index == -1)
        continue;
      ref AbstractRoomNode node = ref connMap[i];
      sb.Append((int)node.type).Append(',').Append(node.shortCutLength).Append(',').Append(node.submerged ? "1" : "0").Append(',').Append(node.viewedByCamera).Append(',').Append(node.entranceWidth).Append(',');
      int[,,] connectivity = node.connectivity;
      for (int n = 0; n < connectivity.GetLength(0); ++n)
        for (int k = 0; k < connMap.Length; ++k)
          sb.Append(connectivity[n, k, 0]).Append(' ').Append(connectivity[n, k, 1]).Append(',');
      sb.Append('|');
    }
    return sb.ToString();
  }

  public static AbstractRoomNode[] RoomPreprocessor_StringToConnMap(string str)
  {
    string[] defs = str.Split('|');
    AbstractRoomNode[] nodes = new AbstractRoomNode[int.Parse(defs[1], CultureInfo.InvariantCulture)];
    int size = int.Parse(defs[2], CultureInfo.InvariantCulture);
    for (int i = 0; i < nodes.Length; ++i)
    {
      string[] roomValues = defs[i + 3].Split(',');
      int[,,] connectivity = (nodes[i] =
        new(new(ExtEnum<AbstractRoomNode.Type>.values.GetEntry(int.Parse(roomValues[0], CultureInfo.InvariantCulture))),
        int.Parse(roomValues[1], CultureInfo.InvariantCulture),
        nodes.Length,
        int.Parse(roomValues[2], CultureInfo.InvariantCulture) == 1,
        int.Parse(roomValues[3], CultureInfo.InvariantCulture),
        int.Parse(roomValues[4], CultureInfo.InvariantCulture))).connectivity;
      for (int j = 0, offset = 5; j < size; ++j, offset += nodes.Length)
        for (int k = 0; k < nodes.Length; ++k)
        {
          string[] values = roomValues[offset + k].Split(' ');
          connectivity[j, k, 0] = int.Parse(values[0], CultureInfo.InvariantCulture);
          connectivity[j, k, 1] = int.Parse(values[1], CultureInfo.InvariantCulture);
        }
    }
    return nodes;
  }

  public static string RoomPreprocessor_CompressAIMapsToString(AImap aimap)
  {
    StringBuilder sb = new(RoomPreprocessor.IntArrayToString(aimap.GetCompressedVisibilityMap()));
    for (int i = 0; i < StaticWorld.preBakedPathingCreatures.Length; ++i)
      sb.Append("<<DIV - A>>").Append(RoomPreprocessor.IntArrayToString(aimap.creatureSpecificAImaps[i].ReturnCompressedIntGrid()))
        .Append("<<DIV - B>>").Append(RoomPreprocessor.FloatArrayToString(aimap.creatureSpecificAImaps[i].ReturnCompressedFloatGrid()));
    return sb.ToString();
  }

  public static string RoomPreprocessor_IntArrayToString(int[] ia)
  {
    byte[] result = new byte[ia.Length * 4];
    Buffer.BlockCopy(ia, 0, result, 0, result.Length);
    return Convert.ToBase64String(result);
  }

  public static string RoomPreprocessor_FloatArrayToString(float[] fa)
  {
    byte[] result = new byte[fa.Length * 4];
    Buffer.BlockCopy(fa, 0, result, 0, result.Length);
    return Convert.ToBase64String(result);
  }

  public static void CreatureSpecificAImap_ctor(CreatureSpecificAImap self, AImap aiMap, CreatureTemplate crit)
  {
    self.numberOfNodes = AIdataPreprocessor.NodesRelevantToCreature(aiMap.room, crit);
    int minWidth = aiMap.width, maxHeight = 0, maxWidth = 0, minHeight = aiMap.height;
    bool hasAccessibleTiles = false;
    bool[,] accessibilityMap = new bool[aiMap.width, aiMap.height];
    for (int i = 0; i < aiMap.width; ++i)
      for (int n = 0; n < aiMap.height; ++n)
        if (aiMap.TileAccessibleToCreature(new IntVector2(i, n), crit))
        {
          accessibilityMap[i, n] = true;
          hasAccessibleTiles = true;
          if (i < minWidth)
            minWidth = i;
          if (n > maxHeight)
            maxHeight = n;
          if (i > maxWidth)
            maxWidth = i;
          if (n < minHeight)
            minHeight = n;
        }
    self.coveredArea = new(minWidth, minHeight, maxWidth, maxHeight);
    int targetWidth = maxWidth + 1 - minWidth, targetHeight = maxHeight + 1 - minHeight;
    if (hasAccessibleTiles)
    {
      self.intGrid = new int[targetWidth, targetHeight, self.numberOfNodes];
      self.floatGrid = new float[aiMap.width, aiMap.height];
    }
    else
    {
      self.intGrid = new int[1, 1, self.numberOfNodes];
      self.floatGrid = new float[1, 1];
    }
    List<IntVector2> accessableTiles = [];
    for (int i = 0; i < targetWidth; ++i)
      for (int n = 0; n < targetHeight; ++n)
        for (int k = 0; k < self.numberOfNodes; ++k)
          self.intGrid[i, n, k] = -1;
    for (int i = 0; i < aiMap.width; ++i)
      for (int n = 0; n < aiMap.height; ++n)
        if (accessibilityMap[i, n])
        {
          for (int k = 0; k < self.numberOfNodes; ++k)
            self.intGrid[i - self.coveredArea.left, n - self.coveredArea.bottom, k] = 0;
          accessableTiles.Add(new(i, n));
        }
    self.accessableTiles = [.. accessableTiles];
  }

  public static bool Room_RayTraceTilesForTerrain(Room self, int x0, int y0, int x1, int y1)
  {
    bool inBounds;
    int dx, dy, dxs, dys;
    if (x1 > x0)
    {
      dx = x1 - x0;
      dxs = 1;
      if (y1 > y0)
      {
        dy = y1 - y0;
        dys = 1;
        inBounds = x0 > -1 && y0 > -1 && x1 < self.Width && y1 < self.Height;
      }
      else
      {
        dy = y0 - y1;
        dys = -1;
        inBounds = x0 > -1 && y1 > -1 && x1 < self.Width && y0 < self.Height;
      }
    }
    else
    {
      dx = x0 - x1;
      dxs = -1;
      if (y1 > y0)
      {
        dy = y1 - y0;
        dys = 1;
        inBounds = x1 > -1 && y0 > -1 && x0 < self.Width && y1 < self.Height;
      }
      else
      {
        dy = y0 - y1;
        dys = -1;
        inBounds = x1 > -1 && y1 > -1 && x0 < self.Width && y0 < self.Height;
      }
    }
    x1 = dx + dy;
    y1 = dx - dy;
    dx <<= 1;
    dy <<= 1;
    dy = -dy;
    if (inBounds)
    {
      bool hasTerrain = self.terrain != null;
      Room.Tile[,] tiles = self.Tiles;
      while (--x1 > -2)
      {
        if (tiles[x0, y0].Terrain == Room.Tile.TerrainType.Solid || hasTerrain && self.terrain.ObstructsTile(x0, y0))
          return false;
        if (y1 > 0)
        {
          x0 += dxs;
          y1 += dy;
        }
        else
        {
          y0 += dys;
          y1 += dx;
        }
      }
    }
    else
      while (--x1 > -2)
      {
        if (self.HasAnySolid(x0, y0))
          return false;
        if (y1 > 0)
        {
          x0 += dxs;
          y1 += dy;
        }
        else
        {
          y0 += dys;
          y1 += dx;
        }
      }
    return true;
  }

  public static PathCost AImap_ConnectionCostForCreature(AImap self, MovementConnection connection, CreatureTemplate crit)
  {
    ref PathCost preference = ref crit.pathingPreferencesConnections[(int)connection.type];
    PathCost tileCost = self.TileCostForCreature(connection.destinationCoord.Tile, crit);
    PathCost.Legality legality;
    if (self.IsConnectionAllowedForCreature(connection, crit))
      legality = PathCost.Legality.Allowed;
    else
      legality = PathCost.Legality.IllegalConnection;
    if (legality < preference.legality)
      legality = preference.legality;
    if (legality < tileCost.legality)
      legality = tileCost.legality;
    return new(preference.resistance * connection.distance + tileCost.resistance, legality);
  }

  public static void Room_LoadFromDataString(Room self, string[] lines)
  {
    RoomPreprocessor.VersionFix(ref lines);
    string[]
      lineSize = lines[1].Split('|'),
      strSize = lineSize[0].Split('*'),
      strLightAngle = lines[2].Split('|')[0].Split('*'),
      strCamPos = lines[3].Split('|');
    if (lineSize[1] == "-1")
    {
      self.water = false;
      self.defaultWaterLevel = -1;
      self.floatWaterLevel = float.MinValue;
    }
    else
    {
      self.water = true;
      self.defaultWaterLevel = int.Parse(lineSize[1], CultureInfo.InvariantCulture);
      self.floatWaterLevel = self.MiddleOfTile(0, self.defaultWaterLevel).y;
      self.waterInFrontOfTerrain = int.Parse(lineSize[2], CultureInfo.InvariantCulture) == 1;
    }
    int width = self.Width = int.Parse(strSize[0], CultureInfo.InvariantCulture),
      height = self.Height = int.Parse(strSize[1], CultureInfo.InvariantCulture);
    self.lightAngle = new Vector2(float.Parse(strLightAngle[0], CultureInfo.InvariantCulture), float.Parse(strLightAngle[1], CultureInfo.InvariantCulture));
    self.cameraPositions = new Vector2[strCamPos.Length];
    for (int i = 0; i < strCamPos.Length; ++i)
    {
      string[] cameraPos = strCamPos[i].Split(',');
      self.cameraPositions[i] = new Vector2(float.Parse(cameraPos[0], CultureInfo.InvariantCulture), height * 20 - 800 - float.Parse(cameraPos[1]));
    }
    self.DefaultTile = null;
    if (self.world != null && self.game != null && self.abstractRoom.firstTimeRealized && (!self.game.IsArenaSession || self.game.GetArenaGameSession.GameTypeSetup.levelItems))
    {
      string[] strPlacedObjects = lines[5].Split('|');
      for (int i = 0; i < strPlacedObjects.Length - 1; ++i)
      {
        string[] objectPos = strPlacedObjects[i].Split(',');
        int type = int.Parse(objectPos[0], CultureInfo.InvariantCulture),
          ex = int.Parse(objectPos[1], CultureInfo.InvariantCulture) - 1,
          ey = height - int.Parse(objectPos[2], CultureInfo.InvariantCulture);
        if (!(type == 1 ? UnityEngine.Random.value < 0.6f : UnityEngine.Random.value < 0.75f))
          continue;
        if (type == 0)
          self.abstractRoom.AddEntity(new AbstractPhysicalObject(self.world, AbstractPhysicalObject.AbstractObjectType.Rock, null, new(self.abstractRoom.index, ex, ey, -1), self.game.GetNewID(-self.abstractRoom.index)));
        else if (type == 1)
          self.abstractRoom.AddEntity(new AbstractSpear(self.world, null, new(self.abstractRoom.index, ex, ey, -1), self.game.GetNewID(-self.abstractRoom.index), false));
      }
    }
    self.Tiles = new Room.Tile[width, height];
    if (self.defaultWaterLevel == -1)
      for (int i = 0; i < width; ++i)
        for (int j = 0; j < height; ++j)
          self.Tiles[i, j] = new Room.Tile(i, j, Room.Tile.TerrainType.Air, false, false, false, 0, 0);
    else
      for (int i = 0; i < width; ++i)
      {
        for (int j = 0; j < self.defaultWaterLevel; ++j)
          self.Tiles[i, j] = new Room.Tile(i, j, Room.Tile.TerrainType.Air, false, false, false, 0, 1);
        self.Tiles[i, self.defaultWaterLevel] = new Room.Tile(i, self.defaultWaterLevel, Room.Tile.TerrainType.Air, false, false, false, 0, 2);
        for (int j = self.defaultWaterLevel + 1; j < height; ++j)
          self.Tiles[i, j] = new Room.Tile(i, j, Room.Tile.TerrainType.Air, false, false, false, 0, 0);
      }
    List<IntVector2> hiveTiles = [], list2 = [], waterfalls = [], grass = [];
    HashSet<IntVector2> waterfallsSet = [];
    List<int> hiveTileIndexes = [];
    int x = 0, y = height - 1;
    string[] strTiles = lines[11].Split('|');
    int hiveCounter = -1;
    for (int i = 0; i < strTiles.Length - 1; ++i)
    {
      string[] array6 = strTiles[i].Split(',');
      self.Tiles[x, y].Terrain = (Room.Tile.TerrainType)int.Parse(array6[0], NumberStyles.Any, CultureInfo.InvariantCulture);
      for (int j = 1; j < array6.Length; ++j)
        switch (array6[j])
        {
          case "1":
            self.Tiles[x, y].verticalBeam = true;
            break;
          case "2":
            self.Tiles[x, y].horizontalBeam = true;
            break;
          case "3":
            if (self.Tiles[x, y].shortCut < 1)
              self.Tiles[x, y].shortCut = 1;
            break;
          case "4":
            self.Tiles[x, y].shortCut = 2;
            break;
          case "5":
            self.Tiles[x, y].shortCut = 3;
            break;
          case "6":
            self.Tiles[x, y].wallbehind = true;
            break;
          case "7":
            Room.AddHiveTile(ref hiveTiles, ref hiveTileIndexes, ref hiveCounter, new(x, y));
            self.Tiles[x, y].hive = true;
            break;
          case "8":
            waterfalls.Add(new(x, y));
            waterfallsSet.Add(new(x, y));
            break;
          case "9":
            self.Tiles[x, y].shortCut = 4;
            break;
          case "10":
            list2.Add(new(x, y));
            break;
          case "11":
            grass.Add(new(x, y));
            break;
          case "12":
            self.Tiles[x, y].shortCut = 5;
            break;
        }
      if (--y >= 0)
        continue;
      ++x;
      y = self.Height - 1;
    }
    self.ModifyTileWaterLevels();
    if (list2.Count > 0)
      self.garbageHoles = [.. list2];
    if (hiveCounter > -1)
    {
      self.hives = new IntVector2[hiveCounter + 1][];
      for (int i = 0; i <= hiveCounter; ++i)
      {
        int num5 = 0, num7 = 0;
        for (int j = 0; j < hiveTileIndexes.Count; ++j)
          if (hiveTileIndexes[j] == i)
            ++num5;
        self.hives[i] = new IntVector2[num5];
        for (int j = 0; j < hiveTileIndexes.Count; ++j)
          if (hiveTileIndexes[j] == i)
            self.hives[i][num7++] = hiveTiles[j];
      }
    }
    else
      self.hives = [];
    List<WaterFall> waterfallList = [];
    while (waterfalls.Count > 0)
    {
      int wx = 0, wy = 0;
      foreach (IntVector2 tile in waterfalls)
      {
        wx = tile.x;
        wy = tile.y;
        if (!waterfalls.Contains(new(wx - 1, wy)) && !waterfalls.Contains(new(wx, wy + 1)))
          break;
      }
      bool flag2 = waterfalls.Contains(new(wx, wy - 1));
      int waterfallWidth = 0, bx = wx;
      while (bx < width && waterfallsSet.Contains(new(bx, wy)) && waterfallsSet.Contains(new(bx, wy - 1)) == flag2)
      {
        ++waterfallWidth;
        ++bx;
      }
      waterfallList.Add(new WaterFall(self, new(wx, wy), flag2 ? 1f : 0.5f, waterfallWidth));
    }
    self.waterFalls = [.. waterfallList];
    foreach (WaterFall waterfall in waterfallList)
      self.AddObject(waterfall);
    if (grass.Count > 0)
      self.AddObject(new WormGrass(self, grass));
    self.Loaded();
  }

  public static readonly Dictionary<string, int> scavengerGroupLimit = new()
  {
    { "SU", 3 },
    { "UW", 3 },
    { "SI", 3 },
    { "SH", 5 },
    { "CC", 5 },
    { "LC", 5 },
  };
  public static void ScavengerAbstractAI_TryAssembleSquad(ScavengerAbstractAI self)
  {
    if (self.parent.nightCreature && self.world.rainCycle.dayNightCounter < 600
     || self.world.rainCycle.TimeUntilRain < 800 && !self.parent.nightCreature && !self.parent.ignoreCycle
     || self.parent.creatureTemplate.type == WatcherEnums.CreatureTemplateType.ScavengerTemplar)
      return;
    int followerScavs = 0;
    foreach (AbstractCreature creature in self.parent.Room.creatures)
    {
      if (creature == self.parent
       || creature.creatureTemplate.TopAncestor().type != CreatureTemplate.Type.Scavenger
       || !(creature.abstractAI as ScavengerAbstractAI).ReadyToJoinSquad()
       || !self.parent.CanTravelTogether(creature))
        continue;
      if (ScavengerAbstractAI.ScavengerSquad.LeaderScore(creature) > ScavengerAbstractAI.ScavengerSquad.LeaderScore(self.parent))
        return;
      ++followerScavs;
    }
    if (followerScavs < 2)
    {
      if (!self.world.singleRoomWorld)
        return;
      int aliveScavs = 0;
      foreach (AbstractRoom room in self.world.abstractRooms)
        foreach (AbstractCreature creature in room.creatures)
          if (creature.state.alive && creature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger && self.parent.CanTravelTogether(creature))
            ++aliveScavs;
      return;
    }
    WorldCoordinate worldCoordinate = self.RandomDestinationRoom();
    if (!self.CanRoamThroughRoom(worldCoordinate.room)
     || !self.worldAI.floodFiller.IsRoomAccessible(worldCoordinate.room))
      return;
    foreach (AbstractCreature creature in self.world.abstractRooms[worldCoordinate.room - self.world.firstRoomIndex].creatures)
      if (creature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger && (creature.abstractAI as ScavengerAbstractAI).squad != null)
        return;
    self.squad?.RemoveMember(self.parent);
    self.squad = new(self.parent);
    int groupSize = Math.Min(followerScavs, UnityEngine.Random.Range(2, ModManager.MMF && self.world.region != null && scavengerGroupLimit.TryGetValue(self.world.region.name, out int regionLimit) ? regionLimit : 7));
    foreach (AbstractCreature creature in self.parent.Room.creatures)
      if (creature != self.parent
       && creature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger
       && (creature.abstractAI as ScavengerAbstractAI).ReadyToJoinSquad()
       && self.parent.CanTravelTogether(creature))
      {
        self.squad.AddMember(creature);
        if (--groupSize == 0)
          break;
      }
    if (!self.squad.StayIn)
    {
      self.squad = null;
      goto _dest;
    }
    ScavengersWorldAI.Outpost outpost = self.worldAI.OutPostInNeedOfGuard();
    if (outpost == null || self.RoomGhostScary(outpost.room) != 0f)
      goto _dest;
    if (ModManager.Watcher)
    {
      bool hasTemplar = false, hasDisciple = false;
      foreach (AbstractCreature abstractCreature in self.squad.members)
      {
        if (abstractCreature.creatureTemplate.type == WatcherEnums.CreatureTemplateType.ScavengerTemplar)
          hasTemplar = true;
        if (abstractCreature.creatureTemplate.type == WatcherEnums.CreatureTemplateType.ScavengerDisciple)
          hasDisciple = true;
      }
      if (!hasTemplar || !hasDisciple)
        foreach (AbstractCreature creature in self.parent.Room.creatures)
          if (creature != self.parent
           && creature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger
           && (creature.abstractAI as ScavengerAbstractAI).ReadyToJoinSquad()
           && self.parent.CanTravelTogether(creature))
          {
            if (!hasTemplar && creature.creatureTemplate.type == WatcherEnums.CreatureTemplateType.ScavengerTemplar)
            {
              self.squad.AddMember(creature);
              hasTemplar = true;
            }
            if (!hasDisciple && creature.creatureTemplate.type == WatcherEnums.CreatureTemplateType.ScavengerDisciple)
            {
              self.squad.AddMember(creature);
              hasDisciple = true;
            }
            if (hasTemplar && hasDisciple)
              break;
          }
    }
    outpost.guardSquad = self.squad;
    self.squad.guardOutpost = outpost;
    self.squad.missionType = ScavengerAbstractAI.ScavengerSquad.MissionID.GuardOutpost;
    for (int i = 0; i < self.world.GetAbstractRoom(outpost.room).nodes.Length; ++i)
      if (self.world.GetAbstractRoom(outpost.room).nodes[i].type == AbstractRoomNode.Type.RegionTransportation)
      {
        worldCoordinate = new WorldCoordinate(outpost.room, -1, -1, i);
        break;
      }
    _dest:
    self.SetDestination(worldCoordinate);
    self.longTermMigration = worldCoordinate;
    self.dontMigrate = UnityEngine.Random.Range(400, 4800);
    if (self.squad == null || self.squad.leader != self.parent)
      return;
    self.squad.CommonMovement(worldCoordinate.room, self.parent, false);
    for (int i = 0; i < self.squad.members.Count; ++i)
      if (self.squad.members[i] != self.parent)
      {
        (self.squad.members[i].abstractAI as ScavengerAbstractAI).freeze = i * 10 + UnityEngine.Random.Range(0, 10);
        (self.squad.members[i].abstractAI as ScavengerAbstractAI).dontMigrate = UnityEngine.Random.Range(400, 4800);
      }
  }

  public static int World_RegionTransportationDistanceBetweenNodes(World self, WorldCoordinate A, WorldCoordinate B)
  {
    if (A.room == B.room)
      return 1;
    AbstractRoom a = self.abstractRooms[A.room - self.firstRoomIndex], b = self.abstractRooms[B.room - self.firstRoomIndex];
    if (a.offScreenDen || b.offScreenDen)
      return 1;
    float dx = a.mapPos.x - b.mapPos.x, dy = a.mapPos.y - b.mapPos.y, v = dx * dx + dy * dy;
    if (v <= 10000f)
      return 1;
    return (int)(Mathf.Sqrt(v) * 0.15f) - 14;
  }

  public static void ScavengerAbstractAI_GoToRoom(ScavengerAbstractAI self, int dstRoom)
  {
    AbstractRoom room = self.world.abstractRooms[dstRoom - self.world.firstRoomIndex];
    if (ModManager.MSC && (room.shelter || room.gate) || self.MigrationDestination.room == dstRoom || self.parent.pos.room == dstRoom || self.RoomGhostScary(dstRoom) > 0f)
      return;
    List<WorldCoordinate> regionShortcuts = [], nodes = [];
    for (int i = 0; i < room.nodes.Length; ++i)
    {
      AbstractRoomNode.Type type = room.nodes[i].type;
      if (type.Index == -1 || !self.parent.creatureTemplate.mappedNodeTypes[type.Index])
        continue;
      if (type == AbstractRoomNode.Type.RegionTransportation)
        regionShortcuts.Add(new(dstRoom, -1, -1, i));
      else
        for (int j = 0; j < room.nodes.Length; ++j)
          if (room.nodes[j].type == AbstractRoomNode.Type.RegionTransportation && room.ConnectionAndBackPossible(j, i, self.parent.creatureTemplate))
          {
            regionShortcuts.Add(new(dstRoom, -1, -1, i));
            break;
          }
      if (self.worldAI.floodFiller.IsNodeAccessible(dstRoom, i))
        nodes.Add(new(dstRoom, -1, -1, i));
    }
    if (regionShortcuts.Count > 0)
    {
      self.SetDestination(regionShortcuts[UnityEngine.Random.Range(0, regionShortcuts.Count)]);
      self.longTermMigration = self.destination;
    }
    else if (nodes.Count > 0)
    {
      self.SetDestination(nodes[UnityEngine.Random.Range(0, nodes.Count)]);
      self.longTermMigration = self.destination;
    }
    if (dstRoom == self.world.offScreenDen.index && self.MigrationDestination.room == self.world.offScreenDen.index && self.longTermMigration.room == self.world.offScreenDen.index)
      self.freeze = UnityEngine.Random.Range(300, 500);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool AddNode(ref WorldCoordinate pos, ref WorldCoordinate start, AbstractSpacePathFinder.Node parent, float cost, ref List<AbstractSpacePathFinder.Node> checkNext, ref AbstractSpacePathFinder.Node foundStart, ref bool visited, IOwnAnAbstractSpacePathFinder owner)
  {
    if (owner != null)
      cost += owner.CostAddOfNode(pos);
    if (pos.abstractNode == start.abstractNode && pos.room == start.room)
    {
      foundStart = new(pos, parent, parent.costToGoal + (int)cost);
      return true;
    }
    visited = true;
    checkNext.Add(new(pos, parent, parent.costToGoal + (int)cost));
    return false;
  }

  public static readonly Dictionary<AbstractRoomNode.Type, int> nodeTypes = new()
  {
    { AbstractRoomNode.Type.Exit, 0 },
    { AbstractRoomNode.Type.SideExit, 1 },
    { AbstractRoomNode.Type.SkyExit, 2 },
    { AbstractRoomNode.Type.SeaExit, 3 },
    { AbstractRoomNode.Type.RegionTransportation, 4 },
  };
  public static List<WorldCoordinate> AbstractSpacePathFinder_Path(World world, WorldCoordinate start, WorldCoordinate goal, CreatureTemplate creatureType, IOwnAnAbstractSpacePathFinder owner)
  {
    if (start == goal)
      return [];
    int startNumber = start.room, goalNumber = goal.room;
    if (!world.IsRoomInRegion(startNumber) || !world.IsRoomInRegion(goalNumber))
      return null;
    AbstractRoom startRoom = world.GetAbstractRoom(startNumber), goalRoom = world.GetAbstractRoom(goalNumber);
    if (startRoom == null || goalRoom == null || startRoom.nodes.Length == 0 || goalRoom.nodes.Length == 0)
      return null;
    int startNode = start.abstractNode, goalNode = goal.abstractNode;
    if (!start.NodeDefined || startNode >= startRoom.nodes.Length)
      start.abstractNode = UnityEngine.Random.Range(0, startRoom.nodes.Length);
    if (!goal.NodeDefined || goalNode >= goalRoom.nodes.Length)
      goal.abstractNode = UnityEngine.Random.Range(0, goalRoom.nodes.Length);
    int startNodeIndex = startRoom.nodes[startNode].type.Index;
    if ((startNodeIndex == -1 || !creatureType.mappedNodeTypes[startNodeIndex]) && !startRoom.offScreenDen)
    {
      CLogWarning("Looking for path from inside node that's inaccessible to creature: ", creatureType.name);
      CLogWarning("start: ", start);
      return null;
    }
    int goalNodeIndex = goalRoom.nodes[goalNode].type.Index;
    List<WorldCoordinate> result = [];
    if (goalNodeIndex == -1 || !creatureType.mappedNodeTypes[goalNodeIndex])
    {
      CLogWarning(creatureType.name, " is looking for path to inaccessible node. Attempting to assign random other node in room.");
      for (int n = 0; n < goalRoom.nodes.Length; ++n)
        if (goalRoom.nodes[n].type.Index != -1 && creatureType.mappedNodeTypes[goalRoom.nodes[n].type.Index])
          result.Add(new(goalNumber, -1, -1, n));
      if (result.Count < 1)
      {
        CLogWarning("However... failed, as there are no possible nodes!");
        return null;
      }
      goal = result[UnityEngine.Random.Range(0, result.Count)];
      if (start == goal)
        return [];
    }
    if (startNumber == goalNumber && startRoom.ConnectivityCost(startNode, goalNode, creatureType) >= 0)
      return [goal];
    for (int i = 0; i < startRoom.connections.Length; ++i)
      if (startRoom.connections[i] == goalNumber)
      {
        int exitIndex = goalRoom.ExitIndex(startNumber);
        if (exitIndex == -1
         || startNode != i && startRoom.ConnectivityCost(startNode, i, creatureType) < 0f
         || exitIndex != goalNode && goalRoom.ConnectivityCost(exitIndex, goalNode, creatureType) < 0f)
          break;
        if (goalNode != exitIndex)
          result.Add(new(goalNumber, -1, -1, goalNode));
        result.Add(new(goalNumber, -1, -1, exitIndex));
        if (startNode != i)
          result.Add(new(startNumber, -1, -1, i));
        return result;
      }
    start.Tile = goal.Tile = new(-1, -1);
    List<AbstractSpacePathFinder.Node> checkNext = [new(goal, null, 0)];
    bool[,] alreadyChecked = new bool[world.NumberOfRooms, world.mostNodesInARoom];
    AbstractSpacePathFinder.Node foundStart = null;
    int attempts = 0, firstRoomIndex = -world.firstRoomIndex;
    PathCost[] resistance = creatureType.pathingPreferencesConnections;
    bool[] allowedNodes = creatureType.mappedNodeTypes;
    float
      offscreenRes = resistance[(int)MovementConnection.MovementType.OffScreenMovement].resistance,
      shortcutRes = resistance[(int)MovementConnection.MovementType.ShortCut].resistance,
      sideRes = resistance[(int)MovementConnection.MovementType.SideHighway].resistance,
      skyRes = resistance[(int)MovementConnection.MovementType.SkyHighway].resistance,
      seaRes = resistance[(int)MovementConnection.MovementType.SeaHighway].resistance,
      roomRes = resistance[(int)MovementConnection.MovementType.BetweenRooms].resistance;
    bool
      shortcutDisallowed = !creatureType.mappedNodeTypes[0],
      sideDisallowed = !allowedNodes[(int)AbstractRoomNode.Type.SideExit] || !resistance[(int)MovementConnection.MovementType.SideHighway].Allowed,
      skyDisallowed = !allowedNodes[(int)AbstractRoomNode.Type.SkyExit] || !resistance[(int)MovementConnection.MovementType.SkyHighway].Allowed,
      seaDisallowed = !allowedNodes[(int)AbstractRoomNode.Type.SeaExit] || !resistance[(int)MovementConnection.MovementType.SeaHighway].Allowed,
      regionDisallowed = !allowedNodes[(int)AbstractRoomNode.Type.RegionTransportation] || !resistance[(int)MovementConnection.MovementType.RegionTransportation].Allowed,
      // Enable extra optimization if start is offscreen den (and check just in case that no nodes were added, idk why)
      offscreenDenOptimization = startRoom.offScreenDen && startRoom.nodes.Length == 5,
      sideFound = sideDisallowed, skyFound = skyDisallowed, seaFound = seaDisallowed, regionFound = regionDisallowed;
    while (checkNext.Count != 0)
    {
      AbstractSpacePathFinder.Node current;
      int lcIndex = 0;
      for (int i = 1; i < checkNext.Count; ++i)
        if (checkNext[i].costToGoal < checkNext[lcIndex].costToGoal)
          lcIndex = i; // go to node with lowest cost
      current = checkNext[lcIndex];
      checkNext.RemoveAt(lcIndex);
      AbstractRoom room = world.abstractRooms[current.pos.room + firstRoomIndex];
      for (int i = 0; i < room.nodes.Length; ++i)
        if (i != current.pos.abstractNode)
        {
          WorldCoordinate worldCoordinate = new(room.index, -1, -1, i);
          ref bool visited = ref alreadyChecked[room.index + firstRoomIndex, i];
          if (visited)
            continue;
          float cost = room.ConnectivityCost(i, current.pos.abstractNode, creatureType) * offscreenRes;
          if (cost >= 0f && AddNode(ref worldCoordinate, ref start, current, cost, ref checkNext, ref foundStart, ref visited, owner))
            goto _found_path;
        }
      if (nodeTypes.TryGetValue(room.nodes[current.pos.abstractNode].type, out int type))
        switch (type)
        {
          case 0:
            if (shortcutDisallowed)
              break;
            int connection = room.connections[current.pos.abstractNode];
            if (connection > -1 && current.pos.abstractNode < room.connections.Length)
            {
              WorldCoordinate worldCoordinate = new(connection, -1, -1, world.abstractRooms[connection + firstRoomIndex].ExitIndex(room.index));
              ref bool visited = ref alreadyChecked[worldCoordinate.room + firstRoomIndex, worldCoordinate.abstractNode];
              if (visited)
                continue;
              float num4 = world.TotalShortCutLengthBetweenTwoConnectedRooms(room.index, connection) * shortcutRes;
              if (num4 > 0f && AddNode(ref worldCoordinate, ref start, current, num4 + roomRes, ref checkNext, ref foundStart, ref visited, owner))
                goto _found_path;
            }
            break;
          case 1:
            if (sideDisallowed)
              break;
            for (int i = 0; i < world.sideAccessNodes.Length; ++i)
            {
              if (world.sideAccessNodes[i].CompareDisregardingTile(current.pos))
                continue;
              WorldCoordinate worldCoordinate = world.sideAccessNodes[i];
              ref bool visited = ref alreadyChecked[worldCoordinate.room + firstRoomIndex, worldCoordinate.abstractNode];
              if (visited)
                continue;
              float num4 = world.SideHighwayDistanceBetweenNodes(current.pos, worldCoordinate) * sideRes;
              if (num4 > 0f && AddNode(ref worldCoordinate, ref start, current, num4 + roomRes, ref checkNext, ref foundStart, ref visited, owner))
                goto _found_path;
              if (!offscreenDenOptimization || sideFound || worldCoordinate.room != startNumber)
                continue;
              sideFound = true;
              if (skyFound && seaFound && regionFound)
                goto _offscreen_all_found;
            }
            break;
          case 2:
            if (skyDisallowed)
              break;
            for (int i = 0; i < world.skyAccessNodes.Length; ++i)
            {
              if (world.skyAccessNodes[i].CompareDisregardingTile(current.pos))
                continue;
              WorldCoordinate worldCoordinate = world.skyAccessNodes[i];
              ref bool visited = ref alreadyChecked[worldCoordinate.room + firstRoomIndex, worldCoordinate.abstractNode];
              if (visited)
                continue;
              float num4 = world.SkyHighwayDistanceBetweenNodes(current.pos, worldCoordinate) * skyRes;
              if (num4 > 0f && AddNode(ref worldCoordinate, ref start, current, num4 + roomRes, ref checkNext, ref foundStart, ref visited, owner))
                goto _found_path;
              if (!offscreenDenOptimization || skyFound || worldCoordinate.room != startNumber)
                continue;
              skyFound = true;
              if (sideFound && seaFound && regionFound)
                goto _offscreen_all_found;
            }
            break;
          case 3:
            if (seaDisallowed)
              break;
            for (int i = 0; i < world.seaAccessNodes.Length; ++i)
            {
              if (world.seaAccessNodes[i].CompareDisregardingTile(current.pos))
                continue;
              WorldCoordinate worldCoordinate = world.seaAccessNodes[i];
              ref bool visited = ref alreadyChecked[worldCoordinate.room + firstRoomIndex, worldCoordinate.abstractNode];
              if (visited)
                continue;
              float num4 = world.SeaHighwayDistanceBetweenNodes(current.pos, worldCoordinate) * seaRes;
              if (num4 > 0f && AddNode(ref worldCoordinate, ref start, current, num4 + roomRes, ref checkNext, ref foundStart, ref visited, owner))
                goto _found_path;
              if (!offscreenDenOptimization || seaFound || worldCoordinate.room != startNumber)
                continue;
              seaFound = true;
              if (sideFound && skyFound && regionFound)
                goto _offscreen_all_found;
            }
            break;
          case 4:
            if (regionDisallowed)
              break;
            for (int i = 0; i < world.regionAccessNodes.Length; ++i)
            {
              if (world.regionAccessNodes[i].CompareDisregardingTile(current.pos))
                continue;
              WorldCoordinate worldCoordinate = world.regionAccessNodes[i];
              ref bool visited = ref alreadyChecked[worldCoordinate.room + firstRoomIndex, worldCoordinate.abstractNode];
              if (visited)
                continue;
              float num4 = world.RegionTransportationDistanceBetweenNodes(current.pos, worldCoordinate) * sideRes;
              if (num4 > 0f && AddNode(ref worldCoordinate, ref start, current, num4 + roomRes, ref checkNext, ref foundStart, ref visited, owner))
                goto _found_path;
              if (!offscreenDenOptimization || regionFound || worldCoordinate.room != startNumber)
                continue;
              regionFound = true;
              if (sideFound && skyFound && seaFound)
                goto _offscreen_all_found;
            }
            break;
        }
      if (++attempts <= 500)
        continue;
      CLogWarning("EMERGENCY EXIT AbstractSpacePathFinder after ", attempts, " generations!");
      return null;
    _offscreen_all_found:
      // No need to check other nodes, all ways to offscreen den are found and can't be changed
      checkNext.RemoveAll(node => node.pos.room != start.room);
    }
    if (foundStart == null)
    {
      if (!ModManager.DevTools
        || ModManager.Expedition && world.game.rainWorld.ExpeditionMode && (ExpeditionGame.activeUnlocks.Contains("bur-hunted") || ExpeditionGame.activeUnlocks.Contains("bur-pursued")))
        return null;
      CLogWarning("Abstract path finder has given up after ", attempts, " generations");
      CLogWarning($"Tried to find path between r:{startRoom.name}, n:{startNode} and r:{goalRoom.name}, n:{goalNode}");
      return null;
    }
  _found_path:
    AbstractSpacePathFinder.Node node4 = foundStart;
    result.Add(node4.pos);
    while (!node4.pos.Equals(goal))
      result.Add((node4 = node4.oneStepCloserToGoal).pos);
    result.Reverse();
    return result;
  }
}