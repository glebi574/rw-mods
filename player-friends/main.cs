using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace player_friends
{
  [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string PLUGIN_GUID = "gelbi.player_friends";
    public const string PLUGIN_NAME = "Player Friends";
    public const string PLUGIN_VERSION = "1.0.5";

    public void OnEnable()
    {
      On.LizardAI.DoIWantToBiteThisCreature += LizardAI_DoIWantToBiteThisCreature;
      On.RelationshipTracker.DynamicRelationship.Update += DynamicRelationship_Update;
    }

    public float GetCommunityLikeOfPlayer(CreatureCommunities communities, AbstractCreature creature, AbstractCreature player)
    {
      return
        communities != null
        && creature.creatureTemplate.communityID is CreatureCommunities.CommunityID communityID
        && communityID != CreatureCommunities.CommunityID.All
        ? communities.LikeOfPlayer(communityID, player.world.region.regionNumber, player.ID.number)
        : 0f;
    }

    public float GetLikeOfCreature(SocialMemory social, AbstractCreature creature)
    {
      return  social != null ? social.GetLike(creature.ID) : 0f;
    }

    public bool isPlayerFriend(AbstractCreature creature, AbstractCreature player)
    {
      return creature.abstractAI?.RealAI?.friendTracker?.friendRel?.subjectID == player.ID;
    }

    public bool IsFriend(AbstractCreature creature1, AbstractCreature creature2)
    {
      if (creature1?.state == null || creature2?.state == null || creature1.world.game.Players.Count < 1 || !creature2.state.alive)
        return false;
      CreatureTemplate.Relationship.Type relationship_type = creature1.abstractAI?.RealAI?.tracker?.RepresentationForCreature(creature2, false)
        ?.dynamicRelationship?.currentRelationship.type;
      if (relationship_type == CreatureTemplate.Relationship.Type.Ignores || relationship_type == CreatureTemplate.Relationship.Type.Pack)
        return true;

      SocialMemory social1 = creature1.state.socialMemory, social2 = creature2.state.socialMemory;
      CreatureCommunities communities = creature1.world.game.GetStorySession?.creatureCommunities;

      if (GetLikeOfCreature(social1, creature2) < -0.1f)
        return false;

      foreach (AbstractCreature player in creature1.world.game.Players)
      {
        if (player == creature2)
          continue;

        float communityLike1 = GetCommunityLikeOfPlayer(communities, creature1, player),
          playerLike1 = GetLikeOfCreature(social1, player);
        if (creature1.realizedCreature is not Player
          && !isPlayerFriend(creature1, player)
          && (communityLike1 < 0.7 || playerLike1 < -0.1)
          && playerLike1 < 0.8)
          continue;

        float communityLike2 = GetCommunityLikeOfPlayer(communities, creature2, player),
          playerLike2 = GetLikeOfCreature(social2, player);
        if (creature2.realizedCreature is Player
          || isPlayerFriend(creature2, player)
          || communityLike2 > 0.7 && playerLike2 > -0.1
          || playerLike2 > 0.8)
          return true;
      }
      return false;
    }

    public void DynamicRelationship_Update(On.RelationshipTracker.DynamicRelationship.orig_Update orig, RelationshipTracker.DynamicRelationship self)
    {
      AbstractCreature creature1 = self.rt.AI.creature, creature2 = self.trackerRep.representedCreature;
      if ((creature1.realizedCreature is not Player || creature2.realizedCreature is not Player) && IsFriend(creature1, creature2))
      {
        CreatureTemplate.Relationship relationship = new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.5f);
        (self.rt.AI as IUseARelationshipTracker).UpdateDynamicRelationship(self);
        if (relationship.type != self.currentRelationship.type)
          self.rt.SortCreatureIntoModule(self, relationship);
        self.trackerRep.priority = relationship.intensity * self.trackedByModuleWeigth;
        self.currentRelationship = relationship;
      }
      else
        orig(self);
    }

    public bool LizardAI_DoIWantToBiteThisCreature(On.LizardAI.orig_DoIWantToBiteThisCreature orig, LizardAI self, Tracker.CreatureRepresentation otherCrit)
    {
      return orig(self, otherCrit) && !IsFriend(self.creature, otherCrit.representedCreature);
    }
  }
}