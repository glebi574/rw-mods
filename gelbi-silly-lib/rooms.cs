using RWCustom;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace gelbi_silly_lib;

/// <summary>
/// Provides some information about the room parsed directly from its file for debugging purposes
/// </summary>
public class RoomRepresentation
{
  public enum State
  {
    Ok,
    NoExit,
    NoValidExit,
    InvalidFormatMerged,
    Empty,
  }

  public int width, height;
  public string representation;
  public string[,][] tiles;
  public HashSet<(int x, int y)> exitNodes;
  public State state = State.Ok;

  public RoomRepresentation(string path)
  {
    string[] data = File.ReadAllLines(path);
    if (data.Length == 0)
    {
      state = State.Empty;
      return;
    }
    string[] dimensions = data[1].Split('|')[0].Split('*');
    width = int.Parse(dimensions[0], CultureInfo.InvariantCulture);
    height = int.Parse(dimensions[1], CultureInfo.InvariantCulture);
    tiles = new string[width, height][];
    int tileDefIndex = 11;
    if (data.Length <= 9)
    {
      state = State.InvalidFormatMerged;
      if (!data[7].Contains("|"))
        return;
      tileDefIndex = 7;
    }
    string[] tileDefs = data[tileDefIndex].Split('|');
    for (int i = 0, x = 0, y = 0; i < tileDefs.Length - 1; ++i)
    {
      tiles[x, y] = tileDefs[i].Split(',');
      if (++y == height)
      {
        ++x;
        y = 0;
      }
    }
    Process();
  }

  char GetSlope(int x, int y)
  {
    return x == 0
      ? y == 0
        ? tiles[0, 1][0] == "1" ? tiles[1, 0][0] == "1" ? '▟' : '▙' : tiles[1, 0][0] == "1" ? '▜' : '▛'
        : tiles[0, y - 1][0] == "1" ? tiles[1, y][0] == "1" ? '▛' : '▜' : tiles[1, y][0] == "1" ? '▙' : '▟'
      : y == 0
        ? tiles[x, 1][0] == "1" ? tiles[x - 1, 0][0] == "1" ? '▟' : '▙' : tiles[x - 1, 0][0] == "1" ? '▜' : '▛'
        : tiles[x, y - 1][0] == "1" ? tiles[x - 1, y][0] == "1" ? '▛' : '▜' : tiles[x - 1, y][0] == "1" ? '▙' : '▟';
  }

  void Process()
  {
    bool hasExit = false;
    exitNodes = [];
    int[,] nodeMap = new int[width, height];
    StringBuilder sb = new(width);
    for (int y = 0; y < height; ++y)
    {
      for (int x = 0; x < width; ++x)
      {
        char c = tiles[x, y][0][0] switch
        {
          '0' => ' ',
          '1' => '█',
          '2' => GetSlope(x, y),
          '3' => '═',
          '4' => 'E',
          _ => '?',
        };
        if (c != ' ' && c != '═')
          nodeMap[x, y] = -1;
        bool hasBackground = false, is1 = false;
        for (int i = 1; i < tiles[x, y].Length; ++i)
        {
          switch (tiles[x, y][i])
          {
            case "1": // vertical beam
              if (c == '═')
                c = '╪';
              else if (c == ' ')
                c = '│';
              else if (c == '─')
                c = '┼';
              break;
            case "2": // horizontal beam
              if (c == ' ')
                c = '─';
              else if (c == '│')
                c = '┼';
              break;
            case "3": // 1
              if (is1)
                break;
              nodeMap[x, y] = 1;
              is1 = true;
              c = c switch
              {
                ' ' => '•',
                '█' => '◆',
                '▟' or '▛' => '▞',
                '▙' or '▜' => '▚',
                '═' => '╤',
                '│' => '┃',
                '─' => '━',
                '┼' => '╋',
                '╪' => '╬',
                _ => c
              };
              break;
            case "4": // room exit
              c = '#';
              nodeMap[x, y] = 5;
              exitNodes.Add((x, y));
              is1 = hasExit = true;
              break;
            case "5": // creature shortcut
              c = 'C';
              is1 = true;
              break;
            case "6": // background
              hasBackground = true;
              break;
            case "7": // hive
              if (c == ' ')
                c = 'H';
              break;
            case "8": // waterfall
              if (c == ' ')
                c = '║';
              break;
            case "9": // npc transportation
              if (c == 'E')
                c = 'N';
              break;
            case "10": // garbage worms
              c = 'G';
              break;
            case "11": // worm grass
              if (c == ' ')
                c = 'W';
              break;
            case "12": // region transportation
              c = 'R';
              is1 = true;
              break;
          }
          if (hasBackground)
          {
            if (c == ' ')
              c = '░';
          }
          if (c == 'E')
            nodeMap[x, y] = 4;
        }
        sb.Append(c);
      }
      sb.AppendLine();
    }
    representation = sb.ToString();

    if (state != State.Ok)
      return;
    if (!hasExit)
    {
      state = State.NoExit;
      return;
    }
    foreach ((int x, int y) in exitNodes)
    {
      HashSet<(int x, int y)> visited = [(x, y)];
      int cx = x, cy = y;
      for (; ; )
      {
      _nextNode:
        foreach (IntVector2 dir in Custom.fourDirections)
        {
          int kx = cx + dir.x, ky = cy + dir.y;
          if (kx != -1 && kx != width && ky != -1 && ky != height && !visited.Contains((kx, ky)))
          {
            if (nodeMap[kx, ky] == 4)
            {
              kx += dir.x;
              ky += dir.y;
              if (kx != -1 && kx != width && ky != -1 && ky != height)
                if (kx != -1 && kx != width && ky != -1 && ky != height && nodeMap[kx, ky] == 0)
                  goto _validExit;
            }
            if (nodeMap[kx, ky] == 1)
            {
              visited.Add((cx = kx, cy = ky));
              goto _nextNode;
            }
          }
        }
        break;
      }
    }
    state = State.NoValidExit;
  _validExit:;
  }

  /// <summary>
  /// Returns <c>true</c> if there are any issues with the room with <paramref name="message"/> containing description of the issue, otherwise returns <c>false</c>
  /// </summary>
  public bool GetError(out string message)
  {
    switch (state)
    {
      case State.NoExit:
        message = "Room has no exit";
        return true;
      case State.NoValidExit:
        message = "No room exit in the room is connected to a valid shortcut entrance or no valid path between any exists";
        return true;
      case State.InvalidFormatMerged:
        message = "Format of the room is invalid as it was merged instead of replacing original room";
        return true;
      case State.Empty:
        message = "Room is empty";
        return true;
      default:
        message = "";
        return false;
    }
  }
}