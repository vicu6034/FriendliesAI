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
            return prefabName == "RRRN_Tester" || prefabName == "Groot";
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            string prefabName = Common.GetPrefabName(mobType);
            if (!(prefabName == "RRRN_Tester"))
            {
                if (!(prefabName == "Groot"))
                    return (MobConfig)null;
                return new MobConfig()
                {
                    PostTameConsumables = GrootConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                    PostTameFeedDuration = (float)GrootConfig.PostTameFeedDuration.Value,
                    PreTameConsumables = GrootConfig.PreTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                    PreTameFeedDuration = (float)GrootConfig.PreTameFeedDuration.Value,
                    TamingTime = (float)GrootConfig.TamingTime.Value,
                    AIType = "Fixer",
                    AIConfig = JsonUtility.ToJson(new FixerAIConfig()
                    {
                        AssignmentSearchRadius = GrootConfig.AssignmentSearchRadius.Value,
                        ContainerSearchRadius = GrootConfig.ContainerSearchRadius.Value,
                        PostTameFeedDuration = GrootConfig.PostTameFeedDuration.Value,
                        IncludedContainers = GrootConfig.IncludedContainersList.Value.Split(','),
                        ItemSearchRadius = GrootConfig.ItemSearchRadius.Value,
                        MaxContainersInMemory = GrootConfig.MaxContainersInMemory.Value,
                        TimeLimitOnAssignment = GrootConfig.TimeLimitOnAssignment.Value
                    })
                };
            }
            return new MobConfig()
            {
                PostTameConsumables = NpcConfig.PostTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PostTameFeedDuration = (float)NpcConfig.PostTameFeedDuration.Value,
                PreTameConsumables = NpcConfig.PreTameConsumables.Select<string, ItemDrop>((Func<string, ItemDrop>)(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault<ItemDrop>())),
                PreTameFeedDuration = (float)NpcConfig.PreTameFeedDuration.Value,
                TamingTime = (float)NpcConfig.TamingTime.Value,
                AIType = "Test",
                AIConfig = JsonUtility.ToJson(new testConfig()
                {
                    InteractRange = NpcConfig.InteractionRange.Value
                })
            };
        }
    }
}
