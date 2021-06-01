using BepInEx.Configuration;
using System.Collections.Generic;

namespace FriendliesAI
{
    public static class GrootConfig
    {
        public static ConfigEntry<string> BrutePrefabName;
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> PreTameFeedDuration;
        public static ConfigEntry<int> PostTameFeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<int> AssignmentSearchRadius;
        public static ConfigEntry<int> ItemSearchRadius;
        public static ConfigEntry<int> ContainerSearchRadius;
        public static ConfigEntry<int> MaxContainersInMemory;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static ConfigEntry<string> IncludedContainersList;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;

        public static void Init(ConfigFile Config)
        {
            GrootConfig.BrutePrefabName = Config.Bind<string>("General", "Groot_PrefabName", "Groot",
                "The prefab to use the Groot ai with (repair structures)");
            GrootConfig.TamingItemList = Config.Bind<string>("General", "Groot_TamingItemList", "Dandelion",
                "Comma separated list if items used to tame Groot");
            GrootConfig.PreTameFeedDuration = Config.Bind<int>("General", "Groot_PreTameFeedDuration", 100,
                "Time before getting hungry after consuming one item during taming");
            GrootConfig.PostTameFeedDuration = Config.Bind<int>("General", "Groot_PostTameFeedDuration", 1000,
                "Time before getting hungry after consuming one item when tame");
            GrootConfig.TamingTime =
                Config.Bind<int>("General", "Brute_TamingTime", 5, "Total time it takes to tame Groot");
            GrootConfig.AssignmentSearchRadius = Config.Bind<int>("General", "Groot_AssignmentSearchRadius", 10,
                "Radius to search for new assignments within");
            GrootConfig.ItemSearchRadius = Config.Bind<int>("General", "Groot_ItemSearchRadius", 10,
                "Radius to search for items on the ground");
            GrootConfig.ContainerSearchRadius = Config.Bind<int>("General", "Groot_ContainerSearchRadius", 10,
                "Radius to search for containers");
            GrootConfig.MaxContainersInMemory = Config.Bind<int>("General", "Groot_MaxContainersInMemory", 3,
                "How many containers Groot should remember contents from");
            GrootConfig.TimeLimitOnAssignment = Config.Bind<int>("General", "Groot_TimeLimitOnAssignment", 30,
                "How long before moving on to next assignment");
            GrootConfig.IncludedContainersList = Config.Bind<string>("General", "Groot_IncludedContainersList",
                "piece_chest_wood", "Comma separated list of container piece names to be searchable by Groot");
            GrootConfig.PreTameConsumables = (IEnumerable<string>) GrootConfig.TamingItemList.Value.Split(',');
            GrootConfig.PostTameConsumables = (IEnumerable<string>) "Dandelion".Split(',');
        }
    }
}