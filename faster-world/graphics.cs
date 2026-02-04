using UnityEngine;
using static faster_world.LogWrapper;

namespace faster_world;

public static class M_Graphics
{
  public static void Dangler_DrawSprite(Dangler self, int spriteIndex, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
  {
    float clampTimeStacker = Mathf.Clamp01(timeStacker), vpx, vpy;
    TriangleMesh mesh = sLeaser.sprites[spriteIndex] as TriangleMesh;
    mesh._isMeshDirty = true;
    Dangler.DanglerSegment segment = self.segments[0];
    {
      Vector2 conPos = self.ConPos(timeStacker);
      vpx = segment.lastPos.x + (segment.pos.x - segment.lastPos.x) * clampTimeStacker - camPos.x;
      vpy = segment.lastPos.y + (segment.pos.y - segment.lastPos.y) * clampTimeStacker - camPos.y;
      float
        v1x = conPos.x - camPos.x, v1y = conPos.y - camPos.y, nx = 0f, ny = 0f,
        dx = vpx - v1x, dy = vpy - v1y,
        l = Mathf.Sqrt(dx * dx + dy * dy);
      if (l > 1e-05f)
      {
        nx = dx / l;
        ny = dy / l;
      }
      float
        l2 = l * 0.2f,
        v3xs = ny * segment.stretchedRad, v3ys = nx * segment.stretchedRad;
      mesh.vertices[0].x = v1x + v3xs;
      mesh.vertices[0].y = v1y - v3ys;
      mesh.vertices[1].x = v1x - v3xs;
      mesh.vertices[1].y = v1y + v3ys;

      mesh.vertices[2].x = vpx + v3xs - nx * l2;
      mesh.vertices[2].y = vpy - v3ys - ny * l2;
      mesh.vertices[3].x = vpx - v3xs - nx * l2;
      mesh.vertices[3].y = vpy + v3ys - ny * l2;
    }
    for (int i = 1, k = 4; i < self.segments.Length; ++i, k += 4)
    {
      segment = self.segments[i];
      float
        v2x = segment.lastPos.x + (segment.pos.x - segment.lastPos.x) * clampTimeStacker - camPos.x,
        v2y = segment.lastPos.y + (segment.pos.y - segment.lastPos.y) * clampTimeStacker - camPos.y,
        dx = v2x - vpx, dy = v2y - vpy, nx = 0f, ny = 0f,
        l = Mathf.Sqrt(dx * dx + dy * dy);
      if (l > 1e-05f)
      {
        nx = dx / l;
        ny = dy / l;
      }
      float
        rs = (self.segments[i - 1].stretchedRad + segment.stretchedRad) * 0.5f,
        xl2 = nx * l * 0.2f, yl2 = ny * l * 0.2f;
      mesh.vertices[k].x = vpx + ny * rs + xl2;
      mesh.vertices[k].y = vpy - nx * rs + yl2;
      mesh.vertices[k + 1].x = vpx - ny * rs + xl2;
      mesh.vertices[k + 1].y = vpy + nx * rs + yl2;

      mesh.vertices[k + 2].x = v2x + ny * segment.stretchedRad - xl2;
      mesh.vertices[k + 2].y = v2y - nx * segment.stretchedRad - yl2;
      mesh.vertices[k + 3].x = v2x - ny * segment.stretchedRad - xl2;
      mesh.vertices[k + 3].y = v2y + nx * segment.stretchedRad - yl2;

      vpx = v2x;
      vpy = v2y;
    }
  }

  public static void DanglerSegment_Update(Dangler.DanglerSegment self)
  {
    ref Vector2 pos = ref self.pos, vel = ref self.vel;
    self.lastPos.x = pos.x;
    self.lastPos.y = pos.y;
    pos.x += vel.x;
    pos.y += vel.y;
    Dangler.DanglerProps props = self.dangler.Props;
    if (self.gModule.owner.room.PointSubmerged(pos))
    {
      vel.x *= props.waterFriction;
      vel.y *= props.waterFriction;
      vel.y -= props.waterGravity;
    }
    else
    {
      vel.x *= props.airFriction;
      vel.y *= props.airFriction;
      vel.y -= props.gravity;
    }
    Vector2 conPos = (self.gModule as HasDanglers).DanglerConnection(self.dangler.danglerNum, 1f);
    float elasticity = props.elasticity, conRad = self.conRad;
    if (self.index == 0)
    {
      float v2x = 0f, v2y = 0f,
        dx = conPos.x - pos.x,
        dy = conPos.y - pos.y,
        l = Mathf.Sqrt(dx * dx + dy * dy);
      if (dx != 0f || dy != 0f)
      {
        if (l > 1e-05f)
        {
          v2x = dx / l;
          v2y = dy / l;
        }
      }
      else
        v2y = 1f;

      v2x *= conRad - l;
      v2y *= conRad - l;
      pos.x -= v2x;
      pos.y -= v2y;
      vel.x -= v2x;
      vel.y -= v2y;
      self.stretchedRad = self.rad * Mathf.Clamp(Mathf.Sqrt(conRad / l) * 0.5f + 0.5f, 0.2f, 1.8f);
    }
    else
    {
      Dangler.DanglerSegment segment = self.dangler.segments[self.index - 1];

      float v3x = 0f, v3y = 0f,
        dx = segment.pos.x - pos.x,
        dy = segment.pos.y - pos.y,
        l = Mathf.Sqrt(dx * dx + dy * dy);
      if (dx != 0f || dy != 0f)
      {
        if (l > 1e-05f)
        {
          v3x = dx / l;
          v3y = dy / l;
        }
      }
      else
        v3y = 1f;

      float symmetryTendency = (segment.rad / (self.rad + segment.rad) + props.weightSymmetryTendency) * 0.5f,
        r0 = (conRad - l) * elasticity, r1 = r0 * symmetryTendency, r2 = r0 - r1;

      pos.x -= v3x * r1;
      pos.y -= v3y * r1;
      vel.x -= v3x * r1;
      vel.y -= v3y * r1;
      segment.pos.x += v3x * r2;
      segment.pos.y += v3y * r2;
      segment.vel.x += v3x * r2;
      segment.vel.y += v3y * r2;
      self.stretchedRad = self.rad * Mathf.Clamp(Mathf.Sqrt(conRad / l) * 0.5f + 0.5f, 0.2f, 1.8f);

      if (self.collideWithTerrain && self.index > 1)
        for (int i = self.index + 1; i < self.dangler.gModule.owner.bodyChunks.Length; ++i)
        {
          BodyChunk bodyChunk = self.dangler.gModule.owner.bodyChunks[i];
          if (bodyChunk.collideWithObjects)
          {
            float
              cdx = pos.x - bodyChunk.pos.x,
              cdy = pos.y - bodyChunk.pos.y,
              cl = Mathf.Sqrt(cdx * cdx + cdy * cdy);
            if (cl >= bodyChunk.rad)
              continue;

            float v4x = 0f, v4y = 0f;
            if (cdx != 0f || cdy != 0f)
            {
              if (l > 1e-05f)
              {
                v4x = cdx / cl;
                v4y = cdy / cl;
              }
            }
            else
              v4y = 1f;
            float r = bodyChunk.rad - cl;
            pos.x -= v4x * r;
            pos.y -= v4y * r;
            vel.x -= v4x * r;
            vel.y -= v4y * r;
          }
        }
    }
    // what was that abomination [FromBaseRadius]
    for (int i = 1; i <= self.index; ++i)
      conRad += self.dangler.segments[i].conRad;
    if (self.collideWithTerrain && !self.OnOtherSideOfTerrain(conPos, conRad * 1.2f))
      self.PushOutOfTerrain(self.gModule.owner.room, conPos);
  }
}