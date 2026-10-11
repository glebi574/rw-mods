using RWCustom;
using System.IO;
using System.Text;

namespace faster_world;

public static class M_Merging
{
  public static void BackwardsCompability_IndexMapWorld(int worldVersion, PlayerProgression prog, string overrideOutputPath)
  {
    StringBuilder roomIndexMap = new(), spawnerIndexMap = new();
    for (int i = 0, roomLineCounter = 0; i < prog.regionNames.Length; ++i)
    {
      string regionName = prog.regionNames[i];
      string[] lines = File.ReadAllLines(AssetManager.ResolveFilePath(string.Concat("world\\", regionName, "\\world_", regionName, ".txt")));
      bool isRoomsBlock = false, isCreaturesBlock = false;
      int spawnerCounter = 0, roomBlockBase;
      foreach (string line in lines)
        if (isRoomsBlock && (roomBlockBase = line.IndexOf(" : ")) != -1)
          roomIndexMap.Append(roomLineCounter++).Append(' ').AppendLine(line.Substring(0, roomBlockBase));
        else if (isCreaturesBlock && line.Length > 2 && (line[0] != '/' || line[1] != '/'))
        {
          string baseLine = line, condition = "";
          if (line[0] == '(')
          {
            int c = line.IndexOf(')');
            if (c == -1)
              condition = line;
            else
            {
              condition = line.Substring(0, c + 1);
              baseLine = line.Substring(c + 1, line.Length - c - 1);
            }
          }
          string[] defs = baseLine.Split(" : ");
          if (defs.Length < 2)
            continue;
          string param0 = defs[0], param1 = defs[1];
          if (param0 == "LINEAGE")
            spawnerIndexMap.Append(i * 1000 + spawnerCounter++).Append(' ').Append(param0).Append(condition)
              .Append(param1.Length > 3 && param1.Substring(0, 3) == "OFF" ? regionName + param1 : param1).AppendLine(defs[2]);
          else
            foreach (string value in Custom.ValidateSpacedDelimiter(param1, ",").Split(", "))
              if (value.Length != 0)
                spawnerIndexMap.Append(i * 1000 + spawnerCounter++).Append(' ')
                  .Append(param0.Length > 3 && param0.Substring(0, 3) == "OFF" ? regionName + param0 : param0).Append(condition).AppendLine(value);
        }
        else switch (line)
          {
            case "ROOMS":
              isRoomsBlock = true;
              break;
            case "CREATURES":
              isCreaturesBlock = true;
              break;
            case "END ROOMS":
              isRoomsBlock = false;
              roomIndexMap.Append(roomLineCounter++).Append(' ').Append(regionName).AppendLine("OffscreenDen");
              break;
            case "END CREATURES":
              isCreaturesBlock = false;
              break;
          }
    }

    StringBuilder regionsIndexMap = new();
    int roomCounter = 0;
    foreach (string regionName in prog.regionNames)
    {
      int roomsInRegion = Region.NumberOfRoomsInRegion(regionName);
      regionsIndexMap.Append(roomCounter).Append(' ').AppendLine(roomsInRegion.ToString());
      roomCounter += roomsInRegion;
    }
    string basePath = overrideOutputPath ?? (Custom.rootFolderDirectory + "\\World\\IndexMaps");
    File.AppendAllText($"{basePath}\\roomIndexMap{worldVersion}.txt", roomIndexMap.ToString());
    File.AppendAllText($"{basePath}\\spawnerIndexMap{worldVersion}.txt", spawnerIndexMap.ToString());
    File.AppendAllText($"{basePath}\\regionsIndexMap{worldVersion}.txt", regionsIndexMap.ToString());
  }
}