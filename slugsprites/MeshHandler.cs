using gelbi_silly_lib;
using gelbi_silly_lib.Converter;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static slugsprites.LogWrapper;

namespace slugsprites;

public class MeshHandler
{
  /// <summary>
  /// Event, used to add custom mesh handlers
  /// </summary>
  public static event Action OnInitialize;
  /// <summary>
  /// Managed mesh types
  /// </summary>
  public static Dictionary<string, CustomMesh> meshes = new();
  /// <summary>
  /// Update methods for each mesh type
  /// </summary>
  public static Dictionary<string, CustomMesh.UpdateHandler> meshHandlers = new();

  public static void Initialize()
  {
    meshHandlers["tailDefault"] = DefaultMeshHandlers.DefaultTailHandler;
    meshHandlers["emptyDefault"] = DefaultMeshHandlers.EmptyHandler;

    OnInitialize?.Invoke();
  }

  public static void LoadMeshes()
  {
    try
    {
      Log.LogInfo("[*] Loading meshes");
      meshes.Clear();

      List<string> paths = HandlerUtils.ListDirectoryE("slugsprites/meshes", out FileUtils.Result opResult);
      if (opResult != FileUtils.Result.Success)
        return;

      int failureCounter = 0;
      foreach (string path in paths)
      {
        Log.LogInfo($" Reading at: {path}");
        foreach (KeyValuePair<string, object> meshData in Json.Parser.Parse(File.ReadAllText(path)) as Dictionary<string, object>)
        {
          Log.LogInfo($"<> Loading mesh \"{meshData.Key}\"");
          try
          {
            if (meshes.ContainsKey(meshData.Key))
              throw new Exception($"mesh with name \"{meshData.Key}\" already exists");
            if (!meshHandlers.ContainsKey(meshData.Key))
              throw new Exception($"mesh lacks update handler - subcribe your method, assigning handler to this mesh type, to MeshHandler.OnInitialize");
            meshes[meshData.Key] = new(meshData.Value as Dictionary<string, object>) { handler = meshHandlers[meshData.Key], name = meshData.Key };
          }
          catch (Exception ei)
          {
            Log.LogError($"Failed to load mesh \"{meshData.Key}\": {ei}");
            ++failureCounter;
          }
        }
      }
      Log.LogInfo($"[+] Finished loading meshes: {meshes.Count}/{meshes.Count + failureCounter} meshes were successfully loaded");
    }
    catch (Exception e)
    {
      Log.LogError(e);
    }
  }
}

/// <summary>
/// Custom data, used to generate(and update via <see cref="handler"/>) TriangleMesh for defining sprite
/// <para>Don't modify any of its values, unless you know what you're doing. Changes in a single instance would affect all sprites with this mesh</para>
/// </summary>
public class CustomMesh
{
  public delegate void UpdateHandler(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData updatedSprite);

  public readonly TriangleMesh.Triangle[] triangles = null;
  public readonly bool mapUV = false, compatibleAsOther = true, customOrigin = false;
  /// <summary>
  /// Origin offset, relative to body sprite
  /// </summary>
  public readonly float originX = 0f, originY = 0f;
  public string name;
  public UpdateHandler handler;

  public CustomMesh(Dictionary<string, object> meshData)
  {
    meshData.TryUpdateValueWithType("mapUV", ref mapUV);
    meshData.TryUpdateValueWithType("compatibleAsOther", ref compatibleAsOther);
    meshData.TryUpdateNumber("originX", ref originX);
    meshData.TryUpdateNumber("originY", ref originY);
    if (!meshData.TryGetValueWithType("triangles", out List<object> triangleSets))
      throw new Exception($"mesh misses \"triangles\" field");
    triangles = new TriangleMesh.Triangle[triangleSets.Count];
    for (int i = 0; i < triangleSets.Count; ++i)
    {
      List<object> triangleSet = triangleSets[i] as List<object>;
      triangles[i] = new(Convert.ToInt32(triangleSet[0]), Convert.ToInt32(triangleSet[1]), Convert.ToInt32(triangleSet[2]));
    }
    customOrigin = originX != 0f || originY != 0f;
  }
}

public static class DefaultMeshHandlers
{
  public static void DefaultTailHandler(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData updatedSprite)
  {
    Vector2[] verticies = (sLeaser.sprites[_sprite.itail] as TriangleMesh).vertices;
    TriangleMesh tail = sLeaser.sprites[updatedSprite.realIndex] as TriangleMesh;
    for (int i = 0; i < verticies.Length; ++i)
      tail.MoveVertice(i, verticies[i]);
  }

  public static void EmptyHandler(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos, SlugcatSprites sprites, SlugSpriteData updatedSprite) { }
}