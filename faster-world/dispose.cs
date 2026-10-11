using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using Watcher;

namespace faster_world;

public static class M_Dispose
{
  public static bool finalizeRoomPreparers = false;
  public static List<Object> disposableObjects = [];

  public static void RoomPreparer_UpdateThread(RoomPreparer self)
  {
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
    RoomPreparer roomPreparer = self;
    for (; ; )
    {
      if (finalizeRoomPreparers)
        return;
      int status;
      bool requestShortcutsReady, requestReadyForAI;
      lock (roomPreparer)
      {
        status = self.status;
        requestShortcutsReady = self.requestShortcutsReady;
        requestReadyForAI = self.requestReadyForAI;
      }
      switch (status)
      {
        case 0:
          if (requestShortcutsReady)
            continue;
          self.scMapper.Update();
          if (!self.scMapper.done)
            continue;
          self.scMapper = null;
          if (self.shortcutsOnly)
          {
            lock (roomPreparer)
              self.requestShortcutsReady = self.threadFinished = true;
            return;
          }
          self.aiMapper = new(self.room);
          lock (roomPreparer)
            self.requestShortcutsReady = true;
          continue;
        case 1:
          break;
        case 2:
          if (self.loadAiHeatMaps)
          {
            self.room.aimap.creatureSpecificAImaps = RoomPreprocessor.DecompressStringToAImaps(self.aiHeatMapsData, self.room.aimap);
            lock (roomPreparer)
              self.status = 4;
            continue;
          }
          self.aiDataPreprocessor = new(self.room.aimap, self.falseBake);
          lock (roomPreparer)
            ++self.status;
          continue;
        case 3:
          self.aiDataPreprocessor.Update();
          if (!self.aiDataPreprocessor.done)
            continue;
          lock (roomPreparer)
            ++self.status;
          continue;
        case 4:
          lock (roomPreparer)
            if (!requestReadyForAI)
              self.requestReadyForAI = true;
          continue;
        case 5:
          lock (roomPreparer)
            self.threadFinished = true;
          return;
        default:
          continue;
      }
      self.aiMapper.Update();
      if (!self.aiMapper.done)
        continue;
      lock (roomPreparer)
        ++self.status;
      self.room.aimap = self.aiMapper.ReturnAIMap();
      self.aiMapper = null;
    }
  }

  public static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
  {
    finalizeRoomPreparers = true;
    List<RoomPreparer> roomPreparers = self.overWorld.activeWorld.loadingRooms;
    if (roomPreparers != null)
      foreach (RoomPreparer roomPreparer in roomPreparers)
        roomPreparer.thread?.Join();
    finalizeRoomPreparers = false;
    orig(self);
    foreach (RoomCamera camera in self.cameras)
    {
      Object.Destroy(camera.warpPointHoldFrame);
      Object.Destroy(camera.WarpPointHoldFrameMaterial);
      Object.Destroy(camera.restoreLightmapFirst);
      Object.Destroy(camera.restoreLightmapSecond);
      if (camera.rippleData != null)
        Object.Destroy(camera.rippleData.playerCamoMaskSaver);
    }
    foreach (var obj in disposableObjects)
      Object.Destroy(obj);
    disposableObjects.Clear();
  }

  public static void MaskSource_ctor_Material(On.Watcher.MaskSource.orig_ctor_Material orig, MaskSource self, Material material)
  {
    orig(self, material);
    disposableObjects.Add(material);
  }

  public static void DynamicLevelElement_AddLevelCombiner(RoomCamera camera)
  {
    if (DynamicLevelElement.levelCombinerAdded || camera.levelTexCombiner.bufferIDs.Contains("DynamicLevelElement"))
      return;
    Material material = new(Shader.Find("Futile/DynamicLevelElementCombiner"));
    disposableObjects.Add(material);
    camera.levelTexCombiner.AddPass(RenderTexture.GetTemporary(1, 1), material, "DynamicLevelElement", CameraEvent.AfterForwardOpaque);
    DynamicLevelElement.levelCombinerAdded = true;
  }
}