using RagnarsRokare.MobAI;
using System;
using System.Linq;
using UnityEngine;

namespace FriendliesAI
{
    public static class MobConfigManager
    {
        public static bool IsControllableMob(string mobType)
        {
            string prefabName = Common.GetPrefabName(mobType);
            return prefabName == "RRRN_Tester" || prefabName == "RRRN_Tester2" || prefabName == "RRRN_Friendly05" || prefabName == "RRRN_Friendly1" || prefabName == "RRRN_Friendly15" || prefabName == "RRRN_Friendly2" || prefabName == "RRRN_Friendly25" || prefabName == "RRRN_Friendly3" || prefabName == "RRRN_Friendly35" || prefabName == "RRRN_Friendly4" || prefabName == "RRRN_Friendly45" || prefabName == "RRRN_FriendlyMelee_Fem_0" || prefabName == "RRRN_FriendlyMelee_Male_0";
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            string prefabName = Common.GetPrefabName(mobType);
            
            if (!(IsControllableMob(prefabName)))
                return (MobConfig)null;
            return new MobConfig()
            {
                /*
                //PostTameConsumables = GrootConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PostTameFeedDuration = (float)GrootConfig.PostTameFeedDuration.Value,
                //
                PreTameFeedDuration = (float)GrootConfig.PreTameFeedDuration.Value,
                AIConfig = JsonUtility.ToJson((object)new FixerAIConfig()
                {
                    AssignmentSearchRadius = GrootConfig.AssignmentSearchRadius.Value,
                    ContainerSearchRadius = GrootConfig.ContainerSearchRadius.Value,
                    PostTameFeedDuration = GrootConfig.PostTameFeedDuration.Value,
                    IncludedContainers = GrootConfig.IncludedContainersList.Value.Split(','),
                    ItemSearchRadius = GrootConfig.ItemSearchRadius.Value,
                    MaxContainersInMemory = GrootConfig.MaxContainersInMemory.Value,
                    TimeLimitOnAssignment = GrootConfig.TimeLimitOnAssignment.Value
                })
                */
                AIType = "NpcAI",
                PreTameConsumables = NpcConfig.PreTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PostTameConsumables = NpcConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                TamingTime = 0,
                AIConfig = JsonUtility.ToJson((object)new NpcAIConfig()
                {
                    InteractRange = 1
                })
            };
        }
    }
}
