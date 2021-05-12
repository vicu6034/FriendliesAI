using BepInEx.Configuration;
using System.Collections.Generic;

namespace FriendliesAI
{
    public static class NpcConfig
    {
        public static ConfigEntry<string> NpcPrefabName;
        public static ConfigEntry<int> InteractionRange;
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> PreTameFeedDuration;
        public static ConfigEntry<int> PostTameFeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;


        public static void Init(ConfigFile Config)
        {
            NpcConfig.NpcPrefabName = Config.Bind<string>("General", "NPCTester_PrefabName", "RRRN_Tester", "The prefab to use the Npc ai with");
            NpcConfig.InteractionRange = Config.Bind<int>("General", "NPCTester_InteractionRange", 1, "The distance to interact");
            NpcConfig.TamingItemList = Config.Bind<string>("General", "NPCTester_TamingItemList", "Coins", "Comma separated list if items used to tame NPCs");
            NpcConfig.PreTameFeedDuration = Config.Bind<int>("General", "NPCTester_PreTameFeedDuration", 100, "Time before getting hungry after consuming one item during taming");
            NpcConfig.PostTameFeedDuration = Config.Bind<int>("General", "NPCTester_PostTameFeedDuration", 1000, "Time before getting hungry after consuming one item when tame");
            NpcConfig.TamingTime = Config.Bind<int>("General", "NPCTester_TamingTime", 5, "Total time it takes to tame NPCs");
            NpcConfig.PreTameConsumables = (IEnumerable<string>)NpcConfig.TamingItemList.Value.Split(',');
            NpcConfig.PostTameConsumables = (IEnumerable<string>)"Dandelion".Split(',');
        }
    }
}