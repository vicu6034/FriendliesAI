using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RagnarsRokare.MobAI;

namespace FriendliesAI.Behaviors
{
    internal class SearchForItemsBehaviour : IBehaviour
    {
        private const string Prefix = "RR_SFI";
        private StateMachine<string, string>.TriggerWithParameters<ItemDrop> FoundGroundItemTrigger;
        private ItemDrop m_groundItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;
        private int m_searchRadius;

        public IEnumerable<ItemDrop.ItemData> Items { get; set; }

        public MaxStack<Container> KnownContainers { get; set; }

        public string[] AcceptedContainerNames { get; set; }

        public ItemDrop.ItemData FoundItem { get; private set; }

        public float OpenChestDelay { get; private set; } = 1f;

        public float MaxSearchTime { get; set; } = 60f;

        public string StartState => "RR_SFIMain";

        public string SuccessState { get; set; }

        public string FailState { get; set; }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            this.m_aiBase = aiBase;
            this.FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>("RR_SFIFoundGroundItem");
            this.m_searchRadius = aiBase.Awareness * 5;
            brain.Configure("RR_SFIMain").InitialTransition("RR_SFISearchItemsOnGround").SubstateOf(parentState).PermitDynamic("RR_SFITimeout", (Func<string>)(() => this.FailState)).OnEntry((Action<StateMachine<string, string>.Transition>)(t => { })).OnExit((Action<StateMachine<string, string>.Transition>)(t => this.KnownContainers.Peek()?.SetInUse(false)));
            brain.Configure("RR_SFISearchItemsOnGround").SubstateOf("RR_SFIMain").Permit(this.FoundGroundItemTrigger.Trigger, "RR_SFIMoveToGroundItem").Permit("RR_SFIFailed", "RR_SFISearchForRandomContainer").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                ItemDrop nearbyItem = Common.GetNearbyItem(this.m_aiBase.Instance, this.Items, this.m_searchRadius);
                if ((UnityEngine.Object)nearbyItem != (UnityEngine.Object)null)
                {
                    this.m_aiBase.UpdateAiStatus("Look, there is a " + nearbyItem.m_itemData.m_shared.m_name + " on da grund");
                    brain.Fire<ItemDrop>(this.FoundGroundItemTrigger, nearbyItem);
                }
                else
                {
                    this.m_aiBase.UpdateAiStatus("I seen nottin on da ground.");
                    brain.Fire("RR_SFIFailed");
                }
            }));
            brain.Configure("RR_SFISearchForRandomContainer").SubstateOf("RR_SFIMain").Permit("RR_SFIContainerFound", "RR_SFIMoveToContainer").PermitDynamic("RR_SFIContainerNotFound", (Func<string>)(() => this.FailState)).PermitDynamic("RR_SFIFailed", (Func<string>)(() => this.FailState)).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                if (this.KnownContainers.Any<Container>())
                {
                    Container container = this.KnownContainers.Where<Container>((Func<Container, bool>)(c => c.GetInventory().GetAllItems().Any<ItemDrop.ItemData>((Func<ItemDrop.ItemData, bool>)(i => this.Items.Any<ItemDrop.ItemData>((Func<ItemDrop.ItemData, bool>)(it => i.m_shared.m_name == it.m_shared.m_name)))))).RandomOrDefault<Container>();
                    if ((UnityEngine.Object)container != (UnityEngine.Object)null)
                    {
                        this.KnownContainers.Remove(container);
                        this.KnownContainers.Push(container);
                        this.m_aiBase.UpdateAiStatus("I seen this in that a bin");
                        brain.Fire("RR_SFIContainerFound");
                        return;
                    }
                }
                Container randomNearbyContainer = Common.FindRandomNearbyContainer(this.m_aiBase.Instance, this.KnownContainers, this.AcceptedContainerNames, (float)this.m_searchRadius);
                if ((UnityEngine.Object)randomNearbyContainer != (UnityEngine.Object)null)
                {
                    this.KnownContainers.Push(randomNearbyContainer);
                    this.m_aiBase.UpdateAiStatus("Look a bin!");
                    this.m_aiBase.Brain.Fire("RR_SFIContainerFound");
                }
                else
                {
                    this.m_aiBase.UpdateAiStatus("Me give up, nottin found!");
                    this.KnownContainers.Clear();
                    this.m_aiBase.Brain.Fire("RR_SFIContainerNotFound");
                }
            }));
            brain.Configure("RR_SFIMoveToGroundItem").SubstateOf("RR_SFIMain").Permit("RR_SFIGroundItemIsClose", "RR_SFIPickUpItemFromGround").Permit("RR_SFIFailed", "RR_SFISearchItemsOnGround").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_groundItem = t.Parameters[0] as ItemDrop;
                int num;
                if (!((UnityEngine.Object)this.m_groundItem == (UnityEngine.Object)null))
                {
                    ZNetView nview = Common.GetNView<ItemDrop>(this.m_groundItem);
                    num = nview != null ? (!nview.IsValid() ? 1 : 0) : 1;
                }
                else
                    num = 1;
                if (num != 0)
                    brain.Fire("RR_SFIFailed");
                else
                    this.m_aiBase.UpdateAiStatus("Heading to " + this.m_groundItem.m_itemData.m_shared.m_name);
            }));
            brain.Configure("RR_SFIPickUpItemFromGround").SubstateOf("RR_SFIMain").PermitDynamic("RR_SFIItemFound", (Func<string>)(() => this.SuccessState)).Permit("RR_SFIFailed", "RR_SFISearchItemsOnGround").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.FoundItem = this.m_groundItem.m_itemData;
                int num;
                if (!((UnityEngine.Object)this.m_groundItem == (UnityEngine.Object)null))
                {
                    ZNetView nview = Common.GetNView<ItemDrop>(this.m_groundItem);
                    num = nview != null ? (!nview.IsValid() ? 1 : 0) : 1;
                }
                else
                    num = 1;
                if (num != 0)
                {
                    brain.Fire("RR_SFIFailed");
                }
                else
                {
                    this.m_aiBase.UpdateAiStatus("Got a " + this.FoundItem.m_shared.m_name + " from the ground");
                    if (this.m_groundItem.RemoveOne())
                        brain.Fire("RR_SFIItemFound");
                    else
                        brain.Fire("RR_SFIFailed");
                }
            }));
            brain.Configure("RR_SFIMoveToContainer").SubstateOf("RR_SFIMain").Permit("RR_SFIContainerIsClose", "RR_SFIOpenContainer").Permit("RR_SFIFailed", "RR_SFISearchItemsOnGround").PermitDynamic("RR_SFIContainerNotFound", (Func<string>)(() => this.FailState)).OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.m_aiBase.UpdateAiStatus("Heading to that a bin")));
            brain.Configure("RR_SFIOpenContainer").SubstateOf("RR_SFIMain").Permit("RR_SFIContainerOpened", "RR_SFISearchForItem").Permit("RR_SFIFailed", "RR_SFISearchItemsOnGround").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                if ((UnityEngine.Object)this.KnownContainers.Peek() == (UnityEngine.Object)null || this.KnownContainers.Peek().IsInUse())
                {
                    this.KnownContainers.Pop();
                    brain.Fire("RR_SFIFailed");
                }
                else
                {
                    this.KnownContainers.Peek().SetInUse(true);
                    this.m_openChestTimer = 0.0f;
                }
            }));
            brain.Configure("RR_SFISearchForItem").SubstateOf("RR_SFIMain").PermitDynamic("RR_SFIItemFound", (Func<string>)(() => this.SuccessState)).Permit("RR_SFIFailed", "RR_SFISearchItemsOnGround").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                if ((UnityEngine.Object)this.KnownContainers.Peek() == (UnityEngine.Object)null)
                {
                    brain.Fire("RR_SFIFailed");
                }
                else
                {
                    this.FoundItem = this.KnownContainers.Peek().GetInventory().GetAllItems().Where<ItemDrop.ItemData>((Func<ItemDrop.ItemData, bool>)(i => this.Items.Any<ItemDrop.ItemData>((Func<ItemDrop.ItemData, bool>)(it => i.m_shared.m_name == it.m_shared.m_name)))).RandomOrDefault<ItemDrop.ItemData>();
                    if (this.FoundItem != null)
                    {
                        this.m_aiBase.UpdateAiStatus("Found " + this.FoundItem.m_shared.m_name + " in this a bin!");
                        this.KnownContainers.Peek().GetInventory().RemoveItem(this.FoundItem, 1);
                        Common.Invoke<Container>((object)this.KnownContainers.Peek(), "Save");
                        Common.Invoke<Inventory>((object)this.KnownContainers.Peek().GetInventory(), "Changed");
                        brain.Fire("RR_SFIItemFound");
                    }
                    else
                    {
                        this.m_aiBase.UpdateAiStatus("Nottin in this a bin..");
                        brain.Fire("RR_SFIFailed");
                    }
                }
            })).OnExit((Action<StateMachine<string, string>.Transition>)(t => this.KnownContainers.Peek().SetInUse(false)));
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if ((double)(this.m_currentSearchTime += dt) > (double)this.MaxSearchTime)
            {
                this.m_currentSearchTime = 0.0f;
                aiBase.Brain.Fire("RR_SFITimeout");
            }
            if (aiBase.Brain.IsInState("RR_SFIMoveToContainer"))
            {
                if ((UnityEngine.Object)this.KnownContainers.Peek() == (UnityEngine.Object)null)
                {
                    aiBase.StopMoving();
                    this.KnownContainers.Pop();
                    aiBase.Brain.Fire("RR_SFIFailed");
                }
                else
                {
                    aiBase.MoveAndAvoidFire(this.KnownContainers.Peek().transform.position, dt, 0.5f);
                    if ((double)Vector3.Distance(aiBase.Instance.transform.position, this.KnownContainers.Peek().transform.position) >= 2.0)
                        return;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_SFIContainerIsClose");
                }
            }
            else if (aiBase.Brain.IsInState("RR_SFIMoveToGroundItem"))
            {
                int num;
                if (!((UnityEngine.Object)this.m_groundItem == (UnityEngine.Object)null))
                {
                    ItemDrop groundItem = this.m_groundItem;
                    if (groundItem == null)
                    {
                        num = 1;
                    }
                    else
                    {
                        bool? nullable = groundItem.GetComponent<ZNetView>()?.IsValid();
                        bool flag = true;
                        num = !(nullable.GetValueOrDefault() == flag & nullable.HasValue) ? 1 : 0;
                    }
                }
                else
                    num = 1;
                if (num != 0)
                {
                    this.m_groundItem = (ItemDrop)null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_SFIFailed");
                }
                else
                {
                    aiBase.MoveAndAvoidFire(this.m_groundItem.transform.position, dt, 0.5f);
                    if ((double)Vector3.Distance(aiBase.Instance.transform.position, this.m_groundItem.transform.position) >= 1.5)
                        return;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_SFIGroundItemIsClose");
                }
            }
            else
            {
                if (!aiBase.Brain.IsInState("RR_SFIOpenContainer") || (double)(this.m_openChestTimer += dt) <= (double)this.OpenChestDelay)
                    return;
                aiBase.Brain.Fire("RR_SFIContainerOpened");
            }
        }

        private class State
        {
            public const string Main = "RR_SFIMain";
            public const string SearchItemsOnGround = "RR_SFISearchItemsOnGround";
            public const string MoveToGroundItem = "RR_SFIMoveToGroundItem";
            public const string SearchForRandomContainer = "RR_SFISearchForRandomContainer";
            public const string MoveToContainer = "RR_SFIMoveToContainer";
            public const string OpenContainer = "RR_SFIOpenContainer";
            public const string SearchForItem = "RR_SFISearchForItem";
            public const string PickUpItemFromGround = "RR_SFIPickUpItemFromGround";
            public const string AvoidFire = "RR_SFIAvoidFire";
        }

        private class Trigger
        {
            public const string ItemFound = "RR_SFIItemFound";
            public const string ContainerFound = "RR_SFIContainerFound";
            public const string ContainerNotFound = "RR_SFIContainerNotFound";
            public const string ContainerIsClose = "RR_SFIContainerIsClose";
            public const string Failed = "RR_SFIFailed";
            public const string ContainerOpened = "RR_SFIContainerOpened";
            public const string Timeout = "RR_SFITimeout";
            public const string GroundItemIsClose = "RR_SFIGroundItemIsClose";
            public const string FoundGroundItem = "RR_SFIFoundGroundItem";
        }
    }
}
