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
    self.coveredArea = new IntRect(minWidth, minHeight, maxWidth, maxHeight);
    maxWidth = aiMap.width - maxWidth - 1;
    maxHeight = aiMap.height - maxHeight - 1;
    int targetWidth = aiMap.width - maxWidth - minWidth, targetHeight = aiMap.height - maxHeight - minHeight;
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
    List<IntVector2> list = [];
    for (int i = 0; i < targetWidth; ++i)
      for (int n = 0; n < targetHeight; ++n)
        for (int k = 0; k < self.numberOfNodes; ++k)
          self.intGrid[i, n, k] = -1;
    for (int i = 0; i < aiMap.width; ++i)
      for (int n = 0; n < aiMap.height; ++n)
        if (accessibilityMap[i, n])
        {
          for (int k = 0; k < self.numberOfNodes; k++)
            self.intGrid[i - self.coveredArea.left, n - self.coveredArea.bottom, k] = 0;
          list.Add(new IntVector2(i, n));
        }
    self.accessableTiles = [.. list];
  }
}