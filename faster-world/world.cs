using RWCustom;
using System;
using System.Collections.Generic;
using System.Text;
using static faster_world.LogWrapper;

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
    for (int i = 0; i < room.connections.Length; i++)
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
    StringBuilder sb = new($"{connMapGeneration}|{connMap.Length}|{connMap[0].connectivity.GetLength(0)}|");
    for (int i = 0; i < connMap.Length; ++i)
    {
      ref AbstractRoomNode node = ref connMap[i];
      if (node.type.Index == -1)
        continue;
      sb.Append($"{(int)node.type},{node.shortCutLength},{(node.submerged ? "1" : "0")},{node.viewedByCamera},{node.entranceWidth},");
      int[,,] connectivity = node.connectivity;
      for (int n = 0; n < connectivity.GetLength(0); ++n)
        for (int k = 0; k < connMap.Length; ++k)
          sb.Append($"{connectivity[n, k, 0]} {connectivity[n, k, 1]},");
      sb.Append('|');
    }
    return sb.ToString();
  }

  public static string RoomPreprocessor_CompressAIMapsToString(AImap aimap)
  {
    StringBuilder sb = new(RoomPreprocessor.IntArrayToString(aimap.GetCompressedVisibilityMap()));
    for (int i = 0; i < StaticWorld.preBakedPathingCreatures.Length; ++i)
      sb.Append($"<<DIV - A>>{RoomPreprocessor.IntArrayToString(aimap.creatureSpecificAImaps[i].ReturnCompressedIntGrid())}<<DIV - B>>{RoomPreprocessor.FloatArrayToString(aimap.creatureSpecificAImaps[i].ReturnCompressedFloatGrid())}");
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

  public static void AccessibilityDijkstraMapper_Update(AIdataPreprocessor.AccessibilityDijkstraMapper self)
  {
    if (self.procreateNextRound.Count == 0)
    {
      self.done = true;
      return;
    }
    bool removeNode;
    int x = 0, y = 0;
    float resistance = float.MaxValue, conResistance, targetResistance;
    PathCost.Legality legality = PathCost.Legality.Unallowed, conLegality, targetLegality;
    PathCost conCost;
    AIdataPreprocessor.DijkstraMapper.Cell resultCell = null, conCell, targetCell;
    for (int i = self.procreateNextRound.Count - 1; i >= 0; --i)
    {
      removeNode = true;
      targetCell = self.cellGrid[self.procreateNextRound[i].x, self.procreateNextRound[i].y];
      targetResistance = targetCell.cost.resistance;
      targetLegality = targetCell.cost.legality;
      foreach (MovementConnection movementConnection in self.aiMap.getAItile(targetCell.pos.x, targetCell.pos.y).outgoingPaths)
      {
        conCost = self.aiMap.ConnectionCostForCreature(movementConnection, self.crit);
        conResistance = targetResistance + conCost.resistance;
        conLegality = targetLegality > conCost.legality ? targetLegality : conCost.legality;
        conCell = self.cellGrid[movementConnection.destinationCoord.x, movementConnection.destinationCoord.y];
        if (conCell == null || (conCell.cost.legality == conLegality ? conCell.cost.resistance > conResistance : conCell.cost.legality > conLegality))
        {
          removeNode = false;
          if (conLegality == legality ? conResistance < resistance : conLegality < legality)
          {
            resultCell = targetCell;
            resistance = conResistance;
            legality = conLegality;
            x = movementConnection.destinationCoord.x;
            y = movementConnection.destinationCoord.y;
          }
        }
      }
      if (removeNode)
        self.procreateNextRound.RemoveAt(i);
    }
    if (resultCell != null)
    {
      self.AddCell(new(x, y), resultCell, new(resistance, legality), ++self.gen);
      return;
    }
    self.procreateNextRound.Clear();
    self.done = true;
  }
}