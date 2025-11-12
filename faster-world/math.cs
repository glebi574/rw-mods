using RWCustom;
using UnityEngine;
using static faster_world.LogWrapper;

namespace faster_world;

public static class M_Math
{
  public static void PhysicalObject_WeightedPush(PhysicalObject self, int A, int B, Vector2 dir, float frc)
  {
    BodyChunk a = self.bodyChunks[A], b = self.bodyChunks[B];
    float r = frc / (a.mass + b.mass), x = dir.x * r, y = dir.y * r;
    a.vel.x += x * b.mass;
    a.vel.y += y * b.mass;
    b.vel.x -= x * a.mass;
    b.vel.y -= y * a.mass;
  }

  public static bool PhysicalObject_IsTileSolid(PhysicalObject self, int bChunk, int relativeX, int relativeY)
  {
    BodyChunk bodyChunk = self.bodyChunks[bChunk];
    Room.Tile.TerrainType terrain = self.room.GetTile(relativeX + (int)(bodyChunk.pos.x / 20f + 1f) - 1, relativeY + (int)(bodyChunk.pos.y / 20f + 1f) - 1).Terrain;
    if (terrain == Room.Tile.TerrainType.Solid || terrain == Room.Tile.TerrainType.Floor && relativeY < 0 && !bodyChunk.goThroughFloors)
      return true;
    if (self.room.terrain == null)
      return false;
    Vector2 vector = new(bodyChunk.pos.x + relativeX * 20f, bodyChunk.pos.y + relativeY * 20f);
    return self.room.terrain.TrySnapToTerrain(vector, bodyChunk.rad, out Vector2 vector2, out _, false) && Vector2.Distance(vector2, vector) > bodyChunk.rad;
  }

  public static bool BodyPart_OnOtherSideOfTerrain(BodyPart self, Vector2 conPos, float minAffectRadius)
  {
    ref Vector2 pos = ref self.pos;
    Room room = self.owner.owner.room;
    float dx = conPos.x - pos.x, dy = conPos.y - pos.y, l = Mathf.Sqrt(dx * dx + dy * dy);
    if (l < minAffectRadius)
      return false;
    int tx = (int)(pos.x / 20f + 1f) - 1,
      ty = (int)(pos.y / 20f + 1f) - 1;
    if (room.GetTile(tx, ty).Solid)
      return true;
    TerrainManager terrain = room.terrain;
    if (terrain != null && terrain.TrySnapToTerrain(pos, 0f, out _, out _))
      return true;
    int idx = (int)(conPos.x / 20f + 1f) - 1 - tx,
      idy = (int)(conPos.y / 20f + 1f) - 1 - ty;
    if (idx < -1)
      idx = -1;
    else if (idx > 1)
      idx = 1;
    if (idy < -1)
      idy = -1;
    else if (idy > 1)
      idy = 1;
    if ((idx | idy) != 0)
    {
      if (Mathf.Abs(conPos.x - pos.x) > Mathf.Abs(conPos.y - pos.y))
        idy = 0;
      else
        idx = 0;
    }
    return room.GetTile(tx + idx, ty + idy).Solid;
  }

  public static void BodyPart_PushOutOfTerrain(BodyPart self, Room room, Vector2 basePoint)
  {
    if (room.terrain != null && self.owner.owner.Buried)
      return;
    self.terrainContact = false;
    if (room.terrain != null && room.terrain.TrySnapToTerrain(self.pos, self.rad, out Vector2 vector, out _))
    {
      self.terrainContact = true;
      self.pos = vector;
      self.vel.y = 0f;
      self.vel.x *= self.surfaceFric;
    }
    ref Vector2 pos = ref self.pos;
    IntVector2 baseTile = room.GetTilePosition(self.pos);
    for (int i = 0; i < 9; ++i)
    {
      int ix = Custom.eightDirectionsAndZero[i].x, iy = Custom.eightDirectionsAndZero[i].y, bx = baseTile.x + ix, by = baseTile.y + iy;
      float mx = bx * 20f + 10f, my = by * 20f + 10f, rad = self.rad, rad10 = rad + 10f;
      Room.Tile tile = room.GetTile(bx, by);
      if (tile.Terrain == Room.Tile.TerrainType.Solid)
      {
        float tx = 0f, ty = 0f;
        if (iy == 0)
        {
          if (self.lastPos.x < mx)
          {
            if (pos.x > mx - rad10 && room.GetTile(bx - 1, by).Terrain != Room.Tile.TerrainType.Solid)
              tx = mx - rad10;
          }
          else if (pos.x < mx + rad10 && room.GetTile(bx + 1, by).Terrain != Room.Tile.TerrainType.Solid)
            tx = mx + rad10;
        }
        if (ix == 0)
        {
          if (self.lastPos.y < my)
          {
            if (pos.y > my - rad10 && room.GetTile(bx, by - 1).Terrain != Room.Tile.TerrainType.Solid)
              ty = my - rad10;
          }
          else if (pos.y < my + rad10 && room.GetTile(bx, by + 1).Terrain != Room.Tile.TerrainType.Solid)
            ty = my + rad10;
        }
        if (tx != 0f && Mathf.Abs(pos.x - tx) < Mathf.Abs(pos.y - ty))
        {
          pos.x = tx;
          self.vel.x = tx - pos.x;
          self.vel.y *= self.surfaceFric;
          self.terrainContact = true;
        }
        else if (ty != 0f)
        {
          pos.y = ty;
          self.vel.y = ty - pos.y;
          self.vel.x *= self.surfaceFric;
          self.terrainContact = true;
        }
        else
        {
          float rx, ry;
          if (ix < 0)
            rx = mx - 10f;
          else if (ix > 0)
            rx = mx + 10f;
          else
            rx = pos.x;
          if (iy < 0)
            ry = my - 10f;
          else if (iy > 0)
            ry = my + 10f;
          else
            ry = pos.y;
          float dx = rx - pos.x, dy = ry - pos.y, l = Mathf.Sqrt(dx * dx + dy * dy);
          if (l < rad)
          {
            self.terrainContact = true;
            self.vel *= self.surfaceFric;
            if (dx == 0f && dy == 0f)
            {
              self.pos.y += l - rad;
              self.vel.y += l - rad;
            }
            else if (l > 1e-05f)
            {
              float v = rad / l - 1f;
              dx *= v;
              dy *= v;
              self.pos.x -= dx;
              self.pos.y -= dy;
              self.vel.x -= dx;
              self.vel.y -= dy;
            }
          }
        }
      }
      else if (ix == 0 && tile.Terrain == Room.Tile.TerrainType.Slope)
      {
        Room.SlopeDirection slopeType = room.IdentifySlope(new IntVector2(bx, by));
        float mpx = mx - pos.x;
        if (slopeType == Room.SlopeDirection.UpLeft)
        {
          float mry = my + rad - mpx;
          if (pos.y < mry)
          {
            pos.y = mry;
            self.vel.y = 0f;
            self.vel.x *= self.surfaceFric;
            self.terrainContact = true;
          }
        }
        else if (slopeType == Room.SlopeDirection.UpRight)
        {
          float mry = my + rad + mpx;
          if (pos.y < mry)
          {
            pos.y = mry;
            self.vel.y = 0f;
            self.vel.x *= self.surfaceFric;
            self.terrainContact = true;
          }
        }
        else if (slopeType == Room.SlopeDirection.DownLeft)
        {
          float mry = my - rad + mpx;
          if (pos.y > mry)
          {
            pos.y = mry;
            self.vel.y = 0f;
            self.vel.x *= self.surfaceFric;
            self.terrainContact = true;
          }
        }
        else if (slopeType == Room.SlopeDirection.DownRight)
        {
          float mry = my - rad - mpx;
          if (pos.y > mry)
          {
            pos.y = mry;
            self.vel.y = 0f;
            self.vel.x *= self.surfaceFric;
            self.terrainContact = true;
          }
        }
      }
    }
  }
}