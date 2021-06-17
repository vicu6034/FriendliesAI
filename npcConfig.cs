﻿using BepInEx.Configuration;
using System.Collections.Generic;

namespace FriendliesAI
{
    public static class NpcConfig
    {
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> FeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<int> AssignmentSearchRadius;
        public static ConfigEntry<int> ItemSearchRadius;
        public static ConfigEntry<int> ContainerSearchRadius;
        public static ConfigEntry<string> IncludedContainersList;
        public static ConfigEntry<int> MaxContainersInMemory;
        public static ConfigEntry<int> TimeBeforeAssignmentCanBeRepeated;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;

        public static void Init(ConfigFile Config)
        {
            TamingItemList = Config.Bind<string>("General", "Npc_TamingItemList", "Coins", "Comma separated list if items used to tame NPCs");
            FeedDuration = Config.Bind<int>("General", "Npc_FeedDuration", 500, "Time before getting hungry after consuming one item");
            TamingTime = Config.Bind<int>("General", "Npc_TamingTime", 0, "Total time it takes to tame an NPC");
            AssignmentSearchRadius = Config.Bind<int>("General", "Npc_AssignmentSearchRadius", 30, "Radius to search for new assignments within");
            ItemSearchRadius = Config.Bind<int>("General", "Npc_ItemSearchRadius", 10, "Radius to search for items on the ground");
            ContainerSearchRadius = Config.Bind<int>("General", "Npc_ContainerSearchRadius", 10, "Radius to search for containers");
            IncludedContainersList = Config.Bind<string>("General", "Npc_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by NPCs");
            MaxContainersInMemory = Config.Bind<int>("General", "Npc_MaxContainersInMemory", 3, "How many containers NPCs should remember contents from");
            TimeBeforeAssignmentCanBeRepeated = Config.Bind<int>("General", "Npc_TimeBeforeAssignmentCanBeRepeated", 120, "How long before assignment can be done again");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Npc_TimeLimitOnAssignment", 60, "How long before moving on to next assignment");
            PreTameConsumables = (IEnumerable<string>)"Coins".Split(',');
            PostTameConsumables = (IEnumerable<string>)"CookedMeat".Split(',');
        }
    }
}
