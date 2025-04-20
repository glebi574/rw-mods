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
    public const string PLUGIN_VERSION = "1.0.1";

    public void OnEnable()
    {
      On.LizardAI.DoIWantToBiteThisCreature += LizardAI_DoIWantToBiteThisCreature;
      On.RelationshipTracker.DynamicRelationship.Update += DynamicRelationship_Update;
    }

    public bool GetCommunityLikeOfPlayer(CreatureCommunities communities, AbstractCreature creature, AbstractCreature player)
    {
      return creature.creatureTemplate.communityID is CreatureCommunities.CommunityID communityID
          && communityID != CreatureCommunities.CommunityID.All
          && communities.LikeOfPlayer(communityID, player.world.region.regionNumber, player.ID.number) > 0.5f;
    }

    public bool GetLikeOfCreature(SocialMemory social, AbstractCreature creature, float likeValue = 0.8f)
    {
      return social.GetLike(creature.ID) > likeValue;
    }

    public bool IsFriend(AbstractCreature creature1, AbstractCreature creature2)
    {
      if (creature1.world.game.Players.Count < 1 || !creature2.state.alive)
        return false;
      CreatureTemplate.Relationship.Type relationship_type = creature1.abstractAI?.RealAI?.tracker?.RepresentationForCreature(creature2, false)
        .dynamicRelationship.currentRelationship.type;
      if (relationship_type == CreatureTemplate.Relationship.Type.Ignores || relationship_type == CreatureTemplate.Relationship.Type.Pack)
        return true;

      SocialMemory social1 = creature1.state.socialMemory, social2 = creature2.state.socialMemory;
      CreatureCommunities communities = creature1.world.game.GetStorySession?.creatureCommunities;

      foreach (AbstractCreature player in creature1.world.game.Players)
      {
        if (player == creature2 || social1 != null && !GetLikeOfCreature(social1, creature2, -0.1f))
          continue;

        if ((social1 == null || !GetLikeOfCreature(social1, player))
          && (communities == null || !GetCommunityLikeOfPlayer(communities, creature1, player)))
          continue;

        if (social2 != null && GetLikeOfCreature(social2, player)
          || communities != null && GetCommunityLikeOfPlayer(communities, creature2, player))
          return true;
      }
      return false;
    }

    public void DynamicRelationship_Update(On.RelationshipTracker.DynamicRelationship.orig_Update orig, RelationshipTracker.DynamicRelationship self)
    {
      if (IsFriend(self.rt.AI.creature, self.trackerRep.representedCreature))
      {
        CreatureTemplate.Relationship relationship = new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.5f);
        (self.rt.AI as IUseARelationshipTracker).UpdateDynamicRelationship(self);
        if (relationship.type != self.currentRelationship.type)
        {
          self.rt.SortCreatureIntoModule(self, relationship);
        }
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