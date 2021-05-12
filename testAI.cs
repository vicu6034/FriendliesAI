using Stateless;
using System;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class TestAI : MobAIBase, IMobAIType
    {
        private testConfig m_config;
        private ItemDrop m_groundItem;
        private float m_updateTimer;
        private float m_boredTimer;
        private StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;

        public MobAIInfo GetMobAIInfo() => new MobAIInfo()
        {
            Name = "Test",
            AIType = this.GetType(),
            ConfigType = typeof (testConfig)
        };

        public TestAI()
        {
        }

        public TestAI(MonsterAI instance, testConfig config)
          : base((BaseAI)instance, "Idle")
        {
            Debug.Log((object)"Test Config");
            this.m_config = config;
            this.ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            this.UpdateTrigger = this.Brain.SetTriggerParameters<float>("Update");
            this.Brain.Configure("Idle").Permit("Bored", "FindItem").OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.UpdateAiStatus("Just hanging around..")));
            this.Brain.Configure("FindItem").Permit("FoundItem", "MoveToItem").Permit("Failed", "Idle").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_groundItem = Common.GetNearbyItem(this.Instance, ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "").Select<ItemDrop, ItemDrop.ItemData>((Func<ItemDrop, ItemDrop.ItemData>)(i => i.m_itemData)));
                if ((UnityEngine.Object)this.m_groundItem != (UnityEngine.Object)null)
                {
                    this.Brain.Fire("FoundItem");
                }
                else
                {
                    this.UpdateAiStatus("No items to be seen");
                    this.Brain.Fire("Failed");
                }
            }));
            this.Brain.Configure("MoveToItem").PermitIf<float>(this.UpdateTrigger, "Idle", (Func<float, bool>)(dt => this.MoveAndAvoidFire(this.m_groundItem.transform.position, dt, 0.5f))).Permit("Failed", "Idle").OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.UpdateAiStatus("Moving to " + this.m_groundItem.m_itemData.m_shared.m_name)));
        }

        public override void UpdateAI(float dt)
        {
            base.UpdateAI(dt);
            if ((double)(this.m_updateTimer += dt) < 0.0500000007450581)
                return;
            this.m_updateTimer = 0.0f;
            if (this.Brain.State == "Idle" && (double)(this.m_boredTimer += dt) > (double)UnityEngine.Random.Range(3f, 6f))
            {
                this.m_boredTimer = 0.0f;
                this.Brain.Fire("Bored");
            }
            if (!(this.Brain.State == "MoveToItem"))
                return;
            if ((UnityEngine.Object)this.m_groundItem == (UnityEngine.Object)null)
            {
                this.StopMoving();
                this.Brain.Fire("Failed");
            }
            else
                this.Brain.Fire<float>(this.UpdateTrigger, dt);
        }

        public override void Follow(Player player)
        {
        }
        
        public override void GotShoutedAtBy(MobAIBase mob) => this.Instance.m_alertedEffects.Create(this.Instance.transform.position, Quaternion.identity);

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
        }

        private class State
        {
            public const string Idle = "Idle";
            public const string FindItem = "FindItem";
            public const string MoveToItem = "MoveToItem";
            public const string PickupItem = "PickupItem";
        }

        private class Trigger
        {
            public const string Bored = "Bored";
            public const string FoundItem = "FoundItem";
            public const string Failed = "Failed";
            public const string Update = "Update";
        }
    }
}
