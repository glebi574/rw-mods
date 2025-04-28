using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using Random = System.Random;

namespace casual_world
{
  public class Hooks
  {
    public class RandomFriendTracker
    {
      public static int tickLimit = 40 * 60;

      public FriendTracker ft;
      public int tick = 0;

      public RandomFriendTracker(FriendTracker ft)
      {
        this.ft = ft;
      }
    }

    public static CreatureTemplate.Relationship staticFriendRelationship = new(CreatureTemplate.Relationship.Type.Ignores, 1f);

    public PluginInterface pluginInterface;
    public Dictionary<EntityID, RandomFriendTracker> randomFriendTracker = new();
    public Random rand = new Random();

    public bool IgnoreEveryone => pluginInterface.ignoreEveryone.Value;
    public bool PlayerIsFriend => pluginInterface.playerIsFriend.Value;
    public bool RandomFriends => pluginInterface.updateFriendToRandom.Value;

    public Hooks(PluginInterface pluginInterface)
    {
      this.pluginInterface = pluginInterface;
    }

    public void Apply()
    {
      On.RelationshipTracker.DynamicRelationship.Update += DynamicRelationship_Update;
      On.Creature.Update += Creature_Update;
      On.CreatureCommunities.LikeOfPlayer += CreatureCommunities_LikeOfPlayer;
      On.ArtificialIntelligence.StaticRelationship += ArtificialIntelligence_StaticRelationship;
      On.LizardAI.DoIWantToBiteThisCreature += LizardAI_DoIWantToBiteThisCreature;
      On.CentipedeAI.DoIWantToShockCreature += CentipedeAI_DoIWantToShockCreature;
      On.Leech.LeechSchool.AddPrey += LeechSchool_AddPrey;
      On.MirosBirdAI.DoIWantToBiteCreature += MirosBirdAI_DoIWantToBiteCreature;
      On.PoleMimic.Act += PoleMimic_Act;
    }

    private void PoleMimic_Act(On.PoleMimic.orig_Act orig, PoleMimic self)
    {
      self.tipAttached = true;
      self.wantToWakeUp = false;
    }

    private bool MirosBirdAI_DoIWantToBiteCreature(On.MirosBirdAI.orig_DoIWantToBiteCreature orig, MirosBirdAI self, AbstractCreature creature)
    {
      return IgnoreCondition(creature) && orig(self, creature);
    }

    public void LeechSchool_AddPrey(On.Leech.LeechSchool.orig_AddPrey orig, Leech.LeechSchool self, Creature p)
    {
      if (IgnoreCondition(p.abstractCreature))
        orig(self, p);
    }

    public bool CentipedeAI_DoIWantToShockCreature(On.CentipedeAI.orig_DoIWantToShockCreature orig, CentipedeAI self, AbstractCreature critter)
    {
      return IgnoreCondition(critter) && orig(self, critter);
    }

    public CreatureTemplate.Relationship ArtificialIntelligence_StaticRelationship(On.ArtificialIntelligence.orig_StaticRelationship orig, ArtificialIntelligence self, AbstractCreature otherCreature)
    {
      return IgnoreCondition(otherCreature) ? orig(self, otherCreature) : staticFriendRelationship;
    }

    public bool LizardAI_DoIWantToBiteThisCreature(On.LizardAI.orig_DoIWantToBiteThisCreature orig, LizardAI self, Tracker.CreatureRepresentation otherCrit)
    {
      return IgnoreCondition(otherCrit.representedCreature) && orig(self, otherCrit);
    }

    // Returns false if creature should be ignored
    public bool IgnoreCondition(AbstractCreature otherCrit)
    {
      return !IgnoreEveryone && otherCrit.realizedCreature is not Player;
    }

    public void NewFriend(AbstractCreature self, AbstractCreature targetFriend)
    {
      FriendTracker ft = self.abstractAI.RealAI.friendTracker;
      ft.friend = targetFriend.realizedCreature;
      SocialMemory.Relationship relationship = self?.state?.socialMemory.GetRelationship(targetFriend.ID);
      if (relationship == null)
        ft.friendRel = new(targetFriend.ID) { like = 1f, know = 1f, tempLike = 1f };
      else
      {
        ft.friendRel = relationship;
        ft.friendRel.like = 1f;
        ft.friendRel.know = 1f;
        ft.friendRel.tempLike = 1f;
      }
    }

    public void NewRandomFriend(Creature self)
    {
      int amount = self.room?.abstractRoom?.creatures.Count ?? 0;
      if (amount < 2)
        return;
      int index = rand.Next(0, amount);
      if (self.room.abstractRoom.creatures[index].realizedCreature == self)
        index = (++index) % amount;
      randomFriendTracker.Remove(self.abstractCreature.ID);
      NewFriend(self.abstractCreature, self.room.abstractRoom.creatures[index]);
      randomFriendTracker.Add(self.abstractCreature.ID, new(self.abstractCreature.abstractAI.RealAI.friendTracker));
    }

    public float CreatureCommunities_LikeOfPlayer(On.CreatureCommunities.orig_LikeOfPlayer orig, CreatureCommunities self, CreatureCommunities.CommunityID commID, int region, int playerNumber)
    {
      return 1f;
    }

    public void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
    {
      orig(self, eu);
      if (self.abstractCreature?.abstractAI?.RealAI is not ArtificialIntelligence ai || ai.friendTracker == null)
        return;

      if (RandomFriends)
      {
        RandomFriendTracker creatureFriend;
        if (!randomFriendTracker.TryGetValue(self.abstractCreature.ID, out creatureFriend)
          || ++creatureFriend.tick > RandomFriendTracker.tickLimit)
          NewRandomFriend(self);
      }
      else if (PlayerIsFriend)
      {
        if (ai.friendTracker.friend != null || self?.room?.world?.game.Players?[0]?.realizedCreature is not Player player)
          return;
        NewFriend(self.abstractCreature, player.abstractCreature);
      }
    }

    public void DynamicRelationship_Update(On.RelationshipTracker.DynamicRelationship.orig_Update orig, RelationshipTracker.DynamicRelationship self)
    {
      if (IgnoreEveryone || self.trackerRep.representedCreature?.realizedCreature is Player player && !player.isNPC)
      {
        CreatureTemplate.Relationship relationship = new(CreatureTemplate.Relationship.Type.Ignores, 1f);
        (self.rt.AI as IUseARelationshipTracker).UpdateDynamicRelationship(self);
        if (relationship.type != self.currentRelationship.type)
          self.rt.SortCreatureIntoModule(self, relationship);
        self.trackerRep.priority = relationship.intensity * self.trackedByModuleWeigth;
        self.currentRelationship = relationship;
        return;
      }
      orig(self);
    }
  }
}
