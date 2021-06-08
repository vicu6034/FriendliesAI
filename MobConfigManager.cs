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
            return prefabName == "RRRN_Tester" || prefabName == "RRRN_Tester2";
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            string prefabName = Common.GetPrefabName(mobType);
            if (!(prefabName == "RRRN_Tester"))
            {
                if (!(prefabName == "RRRN_Tester2"))
                    return (MobConfig)null;
                return new MobConfig()
                {
                    /*
                    //PostTameConsumables = GrootConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                    PostTameFeedDuration = (float)GrootConfig.PostTameFeedDuration.Value,
                    //PreTameConsumables = GrootConfig.PreTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                    PreTameFeedDuration = (float)GrootConfig.PreTameFeedDuration.Value,
                    TamingTime = (float)GrootConfig.TamingTime.Value,
                    AIType = "Fixer",
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
                    PostTameConsumables = NpcConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                    TamingTime = NpcConfig.TamingTime.Value,
                    AIConfig = JsonUtility.ToJson((object)new NpcAIConfig()
                    {
                        InteractRange = 1
                    })
                };
            }
            return new MobConfig()
            {
                PostTameConsumables = NpcConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PostTameFeedDuration = (float)NpcConfig.FeedDuration.Value,
                //PreTameConsumables = NpcConfig.PreTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PreTameFeedDuration = (float)NpcConfig.FeedDuration.Value,
                TamingTime = (float)NpcConfig.TamingTime.Value,
                AIType = "Worker",
                AIConfig = JsonUtility.ToJson((object)new WorkerAIConfig()
                {
                    AssignmentSearchRadius = NpcConfig.AssignmentSearchRadius.Value,
                    ContainerSearchRadius = NpcConfig.ContainerSearchRadius.Value,
                    FeedDuration = NpcConfig.FeedDuration.Value,
                    IncludedContainers = NpcConfig.IncludedContainersList.Value.Split(','),
                    ItemSearchRadius = NpcConfig.ItemSearchRadius.Value,
                    MaxContainersInMemory = NpcConfig.MaxContainersInMemory.Value,
                    TimeBeforeAssignmentCanBeRepeated = NpcConfig.TimeBeforeAssignmentCanBeRepeated.Value,
                    TimeLimitOnAssignment = NpcConfig.TimeLimitOnAssignment.Value
                })
            };
        }
    }
}
