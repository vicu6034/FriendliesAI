using Stateless;
using System;
using System.Linq;
using UnityEngine;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Reflection;
using Stateless.Graph;
using FriendliesAI.Behaviors;
using HarmonyLib;
using Random = System.Random;

namespace FriendliesAI
{
    public class NpcAI : MobAIBase, IMobAIType
    {
        public MaxStack<Piece> m_assignment = new MaxStack<Piece>(100);
        public MaxStack<Assignment> m_assignmentA;
        public MaxStack<Container> m_containers;
        public ItemDrop.ItemData m_carrying;
        private float m_searchForNewAssignmentTimer;
        private float m_triggerTimer;
        private float m_assignedTimer;
        private float m_closeEnoughTimer;
        private float m_repairTimer;
        private float m_roarTimer;
        private float m_lastSuccessfulFindAssignment;
        private float m_lastFailedFindAssignment;
        private float m_stuckInIdleTimer;
        public Vector3 m_startPosition;
        private readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        //private readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> workerUpdateTrigger;
        private readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        private readonly SearchForItemsBehaviour searchForItemsBehaviour;
        private readonly FightBehaviour fightBehaviour;
        private readonly EatingBehaviour eatingBehaviour;
        private readonly NpcAIConfig m_config;
        private string m_lastState = "";

        public float CloseEnoughTimeout { get; private set; } = 10f;

        public float RepairTimeout { get; private set; } = 5f;

        public float RoarTimeout { get; private set; } = 10f;

        public float RepairMinDist { get; private set; } = 2f;

        public float AdjustAssignmentStackSizeTime { get; private set; } = 60f;

        public NpcAI()
        {
        }

        public NpcAI(MonsterAI instance, object config)
          : this(instance, config as MobAIBaseConfig)
        {
        }

        public NpcAI(MonsterAI instance, MobAIBaseConfig config)
          : base((BaseAI)instance, "Idle", config)
        {
            this.m_config = config as NpcAIConfig;
            this.m_containers = new MaxStack<Container>(this.Intelligence);
            if ((double)instance.m_consumeHeal == 0.0)
                instance.m_consumeHeal = this.Character.GetMaxHealth() * 0.25f;
            if (this.m_startPosition == Vector3.zero)
                this.m_startPosition = instance.transform.position;
            string str = this.NView.GetZDO().GetString("RR_SavedAssignmentList");
            if (!string.IsNullOrEmpty(str))
            {
                string[] strArray = str.Split(',');
                Dictionary<string, Piece> dictionary = (typeof(Piece).GetField("m_allPieces", BindingFlags.Static | BindingFlags.NonPublic).GetValue((object)null) as IEnumerable<Piece>).Where<Piece>((Func<Piece, bool>)(p =>
                {
                    ZNetView nview = Common.GetNView<Piece>(p);
                    return nview != null && nview.IsValid();
                })).ToDictionary<Piece, string>((Func<Piece, string>)(p => Common.GetOrCreateUniqueId(Common.GetNView<Piece>(p))));
                Common.Dbgl(string.Format("Loading {0} assignments", (object)((IEnumerable<string>)strArray).Count<string>()));
                foreach (string key in strArray)
                {
                    if (dictionary.ContainsKey(key))
                        this.m_assignment.Push(dictionary[key]);
                }
            }
            this.RegisterRPCMethods();
            this.RegisterWorkerRPCMethods();
            this.UpdateTrigger = this.Brain.SetTriggerParameters<(MonsterAI, float)>("Update");
            this.LookForItemTrigger = this.Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>("ItemFound");
            this.m_assignmentA = new MaxStack<Assignment>(this.Intelligence);
            this.m_containers = new MaxStack<Container>(this.Intelligence);
            this.m_carrying = null;
            this.searchForItemsBehaviour = new SearchForItemsBehaviour();
            this.searchForItemsBehaviour.Configure((MobAIBase)this, this.Brain, "SearchForItems");
            this.fightBehaviour = new FightBehaviour();
            this.fightBehaviour.Configure((MobAIBase)this, this.Brain, "Fight");
            this.eatingBehaviour = new EatingBehaviour();
            this.eatingBehaviour.Configure((MobAIBase)this, this.Brain, "Hungry");
            //this.eatingBehaviour.HungryTimeout = (float)this.m_config.PostTameFeedDuration;
            this.eatingBehaviour.HungryTimeout = 500f;
            this.eatingBehaviour.SearchForItemsState = "SearchForItems";
            this.eatingBehaviour.SuccessState = "Idle";
            this.eatingBehaviour.FailState = "Idle";
            this.eatingBehaviour.HealPercentageOnConsume = 0.1f;
            this.m_trainedAssignments.AddRange((IEnumerable<string>)this.NView.GetZDO().GetString("RR_trainedAssignments").Split());
            this.ConfigureRoot();
            this.ConfigureIdle();
            this.ConfigureFollow();
            this.ConfigureSearchForItems();
            this.ConfigureAssigned();
            this.ConfigureWorkerAssigned();
            this.ConfigureFlee();
            this.ConfigureFight();
            this.ConfigureHungry();
            this.ConfigureMoveToWorkerAssignment();
            this.ConfigureCheckingWorkerAssignment();
            this.ConfigureDoneWithWorkerAssignment();
            this.ConfigureUnloadToWorkerAssignment();
            StateGraph stateGraph = new StateGraph(this.Brain.GetInfo());
        }

        private void RegisterRPCMethods() => this.NView.Register<string>("RR_AddAssignment", (Action<long, string>)((source, assignment) =>
        {
            if (this.NView.IsOwner())
            {
                Common.Dbgl(string.Format("Saving {0} assignments", (object)this.m_assignment.Count<Piece>()));
                Common.Dbgl(string.Format("Removed {0} invalid assignments", (object)this.m_assignment.Where<Piece>((Func<Piece, bool>)(p => !Common.GetNView<Piece>(p).IsValid())).Count<Piece>()));
                foreach (Piece piece in this.m_assignment.Where<Piece>((Func<Piece, bool>)(p => !Common.GetNView<Piece>(p).IsValid())).ToList<Piece>())
                    this.m_assignment.Remove(piece);
                this.NView.GetZDO().Set("RR_SavedAssignmentList", string.Join(",", this.m_assignment.Select<Piece, string>((Func<Piece, string>)(p => p.GetUniqueId()))));
            }
            else
            {
                Common.Dbgl("Push new assignment");
                Piece piece = (typeof(Piece).GetField("m_allPieces", BindingFlags.Static | BindingFlags.NonPublic).GetValue((object)null) as IEnumerable<Piece>).Where<Piece>((Func<Piece, bool>)(p => p.GetUniqueId() == assignment)).FirstOrDefault<Piece>();
                if ((UnityEngine.Object)null != (UnityEngine.Object)piece && !this.m_assignment.Contains(piece))
                    this.m_assignment.Push(piece);
            }
        }));

        private void RegisterWorkerRPCMethods() => this.NView.Register<string, string>("RR_updateTrainedAssignments", (Action<long, string, string>)((source, uniqueID, trainedAssignments) =>
        {
            if (this.NView.IsOwner() || !(this.UniqueID == uniqueID))
                return;
            this.m_trainedAssignments.Clear();
            this.m_trainedAssignments.AddRange((IEnumerable<string>)trainedAssignments.Split());
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "A worker learned a new skill.", 0, (Sprite)null);
        }));

        private void ConfigureRoot() => this.Brain.Configure("Root").InitialTransition("Idle").PermitIf("TakeDamage", "Fight", (Func<bool>)(() =>
        {
            if (this.Brain.IsInState("Fight"))
                return false;
            return (double)this.TimeSinceHurt < 20.0 || Common.Alarmed(this.Instance, (float)this.Awareness);
        })).PermitIf("Follow", "Follow", (Func<bool>)(() => !this.Brain.IsInState("Follow") && (bool)(UnityEngine.Object)(this.Instance as MonsterAI).GetFollowTarget()));
        
        private void ConfigureHungry() => this.Brain.Configure("Hungry").SubstateOf("Root");

        private void ConfigureIdle() => this.Brain.Configure("Idle").SubstateOf("Root").PermitIf("Hungry", this.eatingBehaviour.StartState, (Func<bool>)(() => this.eatingBehaviour.IsHungry(this.IsHurt))).Permit("NoWorkerTasks", "Assigned").Permit("HasWorkerTasks", "WorkerAssigned").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.m_stuckInIdleTimer = 0.0f;
            this.UpdateAiStatus("Nothing to do, bored");
            AddNewAssignment(Instance.transform.position);
            AddNewWorkerAssignment(Instance, this.m_assignmentA);
            Random rand = new Random();
            var rando = rand.Next(0, 2);
            if (rando == 0)
            {
                this.Brain.Fire("HasWorkerTasks");
            }
            else 
            {
                this.Brain.Fire("NoWorkerTasks");
            }
        }));

        private void ConfigureFight() => this.Brain.Configure("Fight").SubstateOf("Root").Permit("Fight", this.fightBehaviour.StartState).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.fightBehaviour.SuccessState = "Idle";
            this.fightBehaviour.FailState = "Flee";
            this.fightBehaviour.m_mobilityLevel = (float)this.Mobility;
            this.fightBehaviour.m_agressionLevel = (float)this.Agressiveness;
            this.fightBehaviour.m_awarenessLevel = (float)this.Awareness;
            this.Brain.Fire("Fight");
        })).OnExit((Action<StateMachine<string, string>.Transition>)(t =>
        {
            ItemDrop.ItemData currentWeapon = (this.Character as Humanoid).GetCurrentWeapon();
            if (currentWeapon != null)
                (this.Character as Humanoid).UnequipItem(currentWeapon);
            MobAIBase.Invoke<MonsterAI>((object)this.Instance, "SetAlerted", (object)false);
        }));


        private void ConfigureFlee() => this.Brain.Configure("Flee").SubstateOf("Root").PermitIf<(MonsterAI, float)>(this.UpdateTrigger, "Idle", (Func<(MonsterAI, float), bool>)(args => Common.Alarmed((BaseAI)args.Item1, (float)Mathf.Max(1, this.Awareness - 1)))).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.UpdateAiStatus("Got hurt, flee!");
            this.Instance.Alert();
        })).OnExit((Action<StateMachine<string, string>.Transition>)(t =>
        {
            MobAIBase.Invoke<MonsterAI>((object)this.Instance, "SetAlerted", (object)false);
            this.Attacker = (Character)null;
            this.StopMoving();
        }));

        private void ConfigureFollow() => this.Brain.Configure("Follow").PermitIf<(MonsterAI, float)>(this.UpdateTrigger, "Idle", (Func<(MonsterAI, float), bool>)(args => !(bool)(UnityEngine.Object)args.Item1.GetFollowTarget())).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.UpdateAiStatus("Follow");
            this.Attacker = (Character)null;
            MobAIBase.Invoke<MonsterAI>((object)this.Instance, "SetAlerted", (object)false);
        })).OnExit((Action<StateMachine<string, string>.Transition>)(t => this.HomePosition = this.m_startPosition = this.eatingBehaviour.LastKnownFoodPosition = this.Instance.transform.position));

        private void ConfigureSearchForItems() => this.Brain.Configure("SearchForItems").SubstateOf("Root").Permit("SearchForItems", this.searchForItemsBehaviour.StartState).Permit("ShoutedAt", "MoveAwayFrom").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.searchForItemsBehaviour.KnownContainers = this.m_containers;
            this.searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
            //this.searchForItemsBehaviour.AcceptedContainerNames = this.m_config.IncludedContainers;
            this.searchForItemsBehaviour.AcceptedContainerNames = new String[1]{"piece_chest_wood"};
            this.searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
            this.searchForItemsBehaviour.FailState = t.Parameters[2] as string;
            this.Brain.Fire("SearchForItems");
        }));

        private void ConfigureAssigned()
        {
            this.Brain.Configure("Assigned").SubstateOf("Idle").InitialTransition("MoveToAssignment").Permit("AssignmentTimedOut", "Idle").OnEntry(
                    (Action<StateMachine<string, string>.Transition>) (t =>
                    {
                        this.UpdateAiStatus("Looking for broken structures");
                        this.m_assignedTimer = 0.0f;
                    }));
            this.Brain.Configure("MoveToAssignment").SubstateOf("Assigned").Permit("Failed", "Idle").PermitIf<(MonsterAI, float)>(this.UpdateTrigger, "CheckRepairState", (Func<(MonsterAI, float), bool>) (arg => this.MoveToAssignment(1))).OnEntry((Action<StateMachine<string, string>.Transition>) (t =>
                    {
                        ZNetView nview = Common.GetNView<Piece>(this.m_assignment.Peek());
                        if (nview == null || !nview.IsValid())
                        {
                            this.Brain.Fire("Failed");
                            this.m_assignment.Pop();
                        }
                        else
                        {
                            this.UpdateAiStatus("Moving to assignment " + this.m_assignment.Peek().m_name);
                            this.m_closeEnoughTimer = 0.0f;
                        }
                    })).OnExit((Action<StateMachine<string, string>.Transition>) (t => this.StopMoving()));
            this.Brain.Configure("TurnToFaceAssignment").SubstateOf("Assigned").PermitIf<(MonsterAI, float)>(this.UpdateTrigger, "CheckRepairState", (Func<(MonsterAI, float), bool>) (arg => Common.TurnToFacePosition((MobAIBase) this, this.m_assignment.Peek().transform.position)));
            this.Brain.Configure("CheckRepairState").SubstateOf("Assigned").Permit("Failed", "Idle").Permit("RepairDone", "Idle").Permit("RepairNeeded", "RepairAssignment").OnEntry((Action<StateMachine<string, string>.Transition>) (t =>
                    {
                        ZNetView nview = Common.GetNView<Piece>(this.m_assignment.Peek());
                        if (nview == null || !nview.IsValid())
                        {
                            this.Brain.Fire("Failed");
                            this.m_assignment.Pop();
                        }
                        else
                        {
                            this.NView.InvokeRPC(ZNetView.Everybody, "RR_AddAssignment",
                                (object) this.m_assignment.Peek().GetUniqueId());
                            WearNTear component = this.m_assignment.Peek().GetComponent<WearNTear>();
                            if (component == null)
                            {
                                this.Brain.Fire("Failed");
                                this.m_assignment.Pop();
                            }
                            else if ((double) component.GetHealthPercentage() <= 0.95)
                            {
                                this.UpdateAiStatus("Hm, this needs fixing!");
                                this.m_startPosition = this.Instance.transform.position;
                                this.Brain.Fire("RepairNeeded");
                            }
                            else if (component.GetHealthPercentage() > 0.95)
                            {
                                this.UpdateAiStatus("The " + this.m_assignment.Peek().m_name +
                                                    " does not need to be repaired yet");
                                this.Brain.Fire("Failed");
                            }
                            else
                            {
                                this.UpdateAiStatus("The " + this.m_assignment.Peek().m_name + " is repaired");
                                this.Brain.Fire("RepairDone");
                            }
                        }
                    }));
            bool hammerAnimationStarted = false;
            this.Brain.Configure("RepairAssignment").SubstateOf("Assigned").Permit("Failed", "Idle").PermitIf<(MonsterAI, float)>(this.UpdateTrigger, "Idle", (Func<(MonsterAI, float), bool>) (args =>
                {
                    this.m_repairTimer += args.Item2;
                    if ((double) this.m_repairTimer < (double) this.RepairTimeout - 0.5)
                        return false;
                    if (!hammerAnimationStarted)
                    {
                        ZSyncAnimation zsyncAnimation =
                            typeof(Character).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic)
                                .GetValue((object) this.Character) as ZSyncAnimation;
                        ItemDrop.ItemData itemData = (this.Character as Humanoid).GetCurrentWeapon();
                        if (itemData == null)
                        {
                            itemData = (this.Character as Humanoid).GetInventory().GetAllItems()
                                .FirstOrDefault<ItemDrop.ItemData>();
                            (this.Character as Humanoid).EquipItem(itemData);
                        }

                        zsyncAnimation.SetTrigger(itemData.m_shared.m_attack.m_attackAnimation);
                        hammerAnimationStarted = true;
                    }

                    return (double) this.m_repairTimer >= (double) this.RepairTimeout;
                })).OnEntry((Action<StateMachine<string, string>.Transition>) (t =>
                {
                    ZNetView nview = Common.GetNView<Piece>(this.m_assignment.Peek());
                    if (nview == null || !nview.IsValid())
                    {
                        this.Brain.Fire("Failed");
                        this.m_assignment.Pop();
                    }
                    else
                    {
                        this.UpdateAiStatus("Fixing this " + this.m_assignment.Peek().m_name);
                        this.m_repairTimer = 0.0f;
                        hammerAnimationStarted = false;
                    }
                })).OnExit((Action<StateMachine<string, string>.Transition>) (t =>
                {
                    int num;
                    if (!(t.Trigger == "Failed"))
                    {
                        ZNetView nview = Common.GetNView<Piece>(this.m_assignment.Peek());
                        num = nview != null ? (!nview.IsValid() ? 1 : 0) : 1;
                    }
                    else
                        num = 1;

                    if (num != 0)
                        return;
                    this.m_stuckInIdleTimer = 0.0f;
                    Debug.LogWarning((object) ("Trigger:" + t.Trigger));
                    Piece piece = this.m_assignment.Peek();
                    this.UpdateAiStatus("This " + this.m_assignment.Peek().m_name + " is good as new!");
                    WearNTear component = piece.GetComponent<WearNTear>();
                    if (!(bool) (UnityEngine.Object) component || !component.Repair())
                        return;
                    piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
                }));
        }

        public bool MoveToAssignment(float dt)
        {
            Piece piece = this.m_assignment.Peek();
            int num;
            if (piece == null)
            {
                num = 1;
            }
            else
            {
                bool? nullable = piece.GetComponent<ZNetView>()?.IsValid();
                bool flag = true;
                num = !(nullable.GetValueOrDefault() == flag & nullable.HasValue) ? 1 : 0;
            }
            if (num != 0)
            {
                this.m_assignment.Pop();
                return true;
            }
            if ((double)(this.m_roarTimer += dt) > (double)this.RoarTimeout)
            {
                IEnumerable<MobAIBase> source = MobManager.AliveMobs.Values.Where<MobAIBase>((Func<MobAIBase, bool>)(c => c.HasInstance())).Where<MobAIBase>((Func<MobAIBase, bool>)(c => (double)Vector3.Distance(c.Instance.transform.position, this.Instance.transform.position) < 1.0)).Where<MobAIBase>((Func<MobAIBase, bool>)(m => m.UniqueID != this.UniqueID));
                if (source.Any<MobAIBase>())
                {
                    this.Instance.m_alertedEffects.Create(this.Instance.transform.position, Quaternion.identity);
                    foreach (MobAIBase mobAiBase in source)
                        mobAiBase.GotShoutedAtBy((MobAIBase)this);
                    this.m_roarTimer = 0.0f;
                }
            }
            float distance = (double)(this.m_closeEnoughTimer += dt) > (double)this.CloseEnoughTimeout ? this.RepairMinDist : this.RepairMinDist + 2f;
            return this.MoveAndAvoidFire(this.m_assignment.Peek().FindClosestPoint(this.Instance.transform.position), dt, distance);
        }

        public override void UpdateAI(float dt)
        {
            if (this.Brain.State != this.m_lastState)
            {
                Common.Dbgl("State:" + this.Brain.State);
                this.m_lastState = this.Brain.State;
            }
            base.UpdateAI(dt);
            this.m_triggerTimer += dt;
            if ((double)this.m_triggerTimer < 0.100000001490116)
                return;
            this.m_triggerTimer = 0.0f;
            MonsterAI instance = this.Instance as MonsterAI;
            this.eatingBehaviour.Update((MobAIBase)this, dt);
            this.Brain.Fire("Follow");
            this.Brain.Fire("TakeDamage");
            this.Brain.Fire("Hungry");
            this.Brain.Fire<(MonsterAI, float)>(this.UpdateTrigger, (instance, dt));
            this.m_assignedTimer += dt;
            if (this.m_assignedTimer > 60f)
                this.Brain.Fire("AssignmentTimedOut");
            if (this.Brain.IsInState("Follow"))
                MobAIBase.Invoke<MonsterAI>((object)this.Instance, "Follow", (object)instance.GetFollowTarget(), (object)dt);
            else if (this.Brain.IsInState("Flee"))
            {
                Vector3 vector3 = (UnityEngine.Object)this.Attacker == (UnityEngine.Object)null ? this.Character.transform.position : this.Attacker.transform.position;
                MobAIBase.Invoke<MonsterAI>((object)this.Instance, "Flee", (object)dt, (object)vector3);
            }
            else if (this.Brain.IsInState("SearchForItems"))
                this.searchForItemsBehaviour.Update((MobAIBase)this, dt);
            else if (this.Brain.IsInState("Fight"))
            {
                this.fightBehaviour.Update((MobAIBase)this, dt);
            }
            else
            {
                if (!(this.Brain.State == "Idle"))
                    return;
                Common.Invoke<BaseAI>((object)this.Instance, "RandomMovement", (object)dt, (object)this.m_startPosition);
            }
        }

        private bool AddNewAssignment(Vector3 position)
        {
            Common.Dbgl("Enter AddNewAssignment");
            List<Piece> pieceList = new List<Piece>();
            DateTime now = DateTime.Now;
            Piece.GetAllPiecesInRadius(position, (float)(this.m_config.Awareness * 5), pieceList);
            Piece piece = pieceList.Where<Piece>((Func<Piece, bool>)(p => p.m_category == Piece.PieceCategory.Building || p.m_category == Piece.PieceCategory.Crafting)).Where<Piece>((Func<Piece, bool>)(p => !this.m_assignment.Contains(p))).Where<Piece>((Func<Piece, bool>)(p =>
            {
                ZNetView nview = Common.GetNView<Piece>(p);
                return nview != null && nview.IsValid();
            })).Where<Piece>((Func<Piece, bool>)(p => Common.CanSeeTarget(this.Instance, p.gameObject))).OrderBy<Piece, float>((Func<Piece, float>)(p => Vector3.Distance(p.GetCenter(), position))).FirstOrDefault<Piece>();
            Common.Dbgl(string.Format("Selecting piece took {0}ms", (object)(DateTime.Now - now).TotalMilliseconds));
            if ((UnityEngine.Object)piece != (UnityEngine.Object)null && !string.IsNullOrEmpty(Common.GetOrCreateUniqueId(Common.GetNView<Piece>(piece))))
            {
                this.m_lastSuccessfulFindAssignment = Time.time;
                if ((double)Time.time - (double)this.m_lastFailedFindAssignment > (double)this.AdjustAssignmentStackSizeTime)
                {
                    this.m_lastFailedFindAssignment = Time.time;
                    int num1 = Math.Min(100, (int)((double)this.m_assignment.MaxSize * 1.20000004768372));
                    int num2 = this.m_assignment.Count<Piece>();
                    Common.Dbgl(string.Format("Increased Assigned stack from {0} to {1} and copied {2} pieces", (object)this.m_assignment.MaxSize, (object)num1, (object)num2));
                    this.m_assignment.MaxSize = num1;
                }
                this.m_assignment.Push(piece);
                return true;
            }
            this.m_lastFailedFindAssignment = Time.time;
            if ((double)Time.time - (double)this.m_lastSuccessfulFindAssignment > (double)this.AdjustAssignmentStackSizeTime)
            {
                this.m_lastSuccessfulFindAssignment = Time.time;
                int num1 = Math.Max(1, (int)((double)this.m_assignment.Count<Piece>() * 0.800000011920929));
                int num2 = this.m_assignment.Count<Piece>();
                Common.Dbgl(string.Format("Decreased Assigned stack from {0} to {1} pushing {2} pieces", (object)this.m_assignment.MaxSize, (object)num1, (object)num2));
                this.m_assignment.MaxSize = num1;
            }
            return false;
        }

        private void ConfigureWorkerAssigned()
        {
            this.Brain.Configure("WorkerAssigned").SubstateOf("Idle").InitialTransition("MoveToWorkerAssignment").PermitIf("TakeDamage", "Fight", (Func<bool>)(() => (double)this.TimeSinceHurt < 20.0)).PermitIf("Follow", "Follow", (Func<bool>)(() => (bool)(UnityEngine.Object)(this.Instance as MonsterAI).GetFollowTarget())).PermitIf("Hungry", this.eatingBehaviour.StartState, (Func<bool>)(() => this.eatingBehaviour.IsHungry(this.IsHurt))).Permit("AssignmentTimedOut", "DoneWithWorkerAssignment").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.UpdateAiStatus("I'm on it Boss");
                this.m_assignedTimer = 0.0f;
                this.m_startPosition = this.Instance.transform.position;
            })).OnExit((Action<StateMachine<string, string>.Transition>)(t =>
            {
                if (this.m_carrying == null)
                    return;
                (this.Character as Humanoid).DropItem((this.Character as Humanoid).GetInventory(), this.m_carrying, 1);
                this.m_carrying = (ItemDrop.ItemData)null;
            }));
            this.Brain.Configure("HaveAssignmentItem").SubstateOf("WorkerAssigned").Permit("ItemFound", "MoveToWorkerAssignment").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.UpdateAiStatus("Trying to Pickup " + this.searchForItemsBehaviour.FoundItem.m_shared.m_name);
                ItemDrop.ItemData itemData = (this.Character as Humanoid).PickupPrefab(this.searchForItemsBehaviour.FoundItem.m_dropPrefab);
                (this.Character as Humanoid).EquipItem(itemData);
                this.m_carrying = itemData;
                this.Brain.Fire("ItemFound");
            }));
            this.Brain.Configure("HaveNoAssignmentItem").SubstateOf("WorkerAssigned").PermitIf("ItemNotFound", "DoneWithWorkerAssignment").OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.Brain.Fire("ItemNotFound")));
        }

        private void ConfigureMoveToWorkerAssignment()
        {
            string nextState = "Idle";
            this.Brain.Configure("MoveToWorkerAssignment").SubstateOf("WorkerAssigned").PermitDynamicIf<(MonsterAI, float)>(this.UpdateTrigger, (Func<(MonsterAI, float), string>)(args => nextState), (Func<(MonsterAI, float), bool>)(arg => this.MoveToWorkerAssignment(arg.Item2))).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.UpdateAiStatus("Moving to assignment " + this.m_assignmentA.Peek().TypeOfAssignment.Name);
                this.m_closeEnoughTimer = 0.0f;
                if (t.Source == "HaveAssignmentItem")
                    nextState = "UnloadToWorkerAssignment";
                else
                    nextState = "CheckingWorkerAssignment";
            }));
        }

        private void ConfigureCheckingWorkerAssignment() => this.Brain.Configure("CheckingWorkerAssignment").SubstateOf("WorkerAssigned").Permit(this.LookForItemTrigger.Trigger, "SearchForItems").Permit("WorkerAssignmentFinished", "DoneWithWorkerAssignment").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.StopMoving();
            this.UpdateAiStatus("Checking assignment for task");
            ItemDrop.ItemData needFuel = this.m_assignmentA.Peek().NeedFuel;
            IEnumerable<ItemDrop.ItemData> needOre = this.m_assignmentA.Peek().NeedOre;
            List<ItemDrop.ItemData> source = new List<ItemDrop.ItemData>();
            Common.Dbgl("Ore:" + needOre.Join<ItemDrop.ItemData>((Func<ItemDrop.ItemData, string>)(j => j.m_shared.m_name)) + ", Fuel:" + needFuel?.m_shared.m_name);
            if (needFuel != null)
            {
                source.Add(needFuel);
                this.UpdateAiStatus("Adding " + needFuel.m_shared.m_name + " to search list");
            }
            if (needOre.Any<ItemDrop.ItemData>())
            {
                source.AddRange(needOre);
                this.UpdateAiStatus("Adding " + needOre.Join<ItemDrop.ItemData>((Func<ItemDrop.ItemData, string>)(o => o.m_shared.m_name)) + " to search list");
            }
            if (!source.Any<ItemDrop.ItemData>())
                this.Brain.Fire("WorkerAssignmentFinished");
            else
                this.Brain.Fire<IEnumerable<ItemDrop.ItemData>, string, string>(this.LookForItemTrigger, (IEnumerable<ItemDrop.ItemData>)source, "HaveAssignmentItem", "HaveNoAssignmentItem");
        }));

        private void ConfigureUnloadToWorkerAssignment() => this.Brain.Configure("UnloadToWorkerAssignment").SubstateOf("WorkerAssigned").Permit("WorkerAssignmentFinished", "CheckingWorkerAssignment").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            this.StopMoving();
            ItemDrop.ItemData needFuel = this.m_assignmentA.Peek().NeedFuel;
            IEnumerable<ItemDrop.ItemData> needOre = this.m_assignmentA.Peek().NeedOre;
            bool flag1 = this.m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
            bool flag2 = needOre != null && needOre.Any<ItemDrop.ItemData>((Func<ItemDrop.ItemData, bool>)(c => this.m_carrying.m_shared.m_name == c?.m_shared?.m_name));
            if (flag1)
            {
                this.UpdateAiStatus("Unload to " + this.m_assignmentA.Peek().TypeOfAssignment.Name + " -> Fuel");
                this.m_assignmentA.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel");
                (this.Character as Humanoid).GetInventory().RemoveOneItem(this.m_carrying);
            }
            else if (flag2)
            {
                this.UpdateAiStatus("Unload to " + this.m_assignmentA.Peek().TypeOfAssignment.Name + " -> Ore");
                this.m_assignmentA.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", (object)Common.GetPrefabName(this.m_carrying.m_dropPrefab.name));
                (this.Character as Humanoid).GetInventory().RemoveOneItem(this.m_carrying);
            }
            else
            {
                this.UpdateAiStatus("Dropping " + this.m_carrying.m_shared.m_name + " on the ground");
                (this.Character as Humanoid).DropItem((this.Character as Humanoid).GetInventory(), this.m_carrying, 1);
            }
            (this.Character as Humanoid).UnequipItem(this.m_carrying, false);
            this.m_carrying = (ItemDrop.ItemData)null;
            this.Brain.Fire("WorkerAssignmentFinished");
        }));

        private void ConfigureDoneWithWorkerAssignment() => this.Brain.Configure("DoneWithWorkerAssignment").SubstateOf("WorkerAssigned").Permit("LeaveWorkerAssignment", "Idle").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
        {
            if (this.m_carrying != null)
            {
                this.UpdateAiStatus("Dropping " + this.m_carrying.m_shared.m_name + " on the ground");
                (this.Character as Humanoid).DropItem((this.Character as Humanoid).GetInventory(), this.m_carrying, 1);
                this.m_carrying = (ItemDrop.ItemData)null;
            }
            this.UpdateAiStatus("Done with assignment!");
            this.m_containers.Peek()?.SetInUse(false);
            this.Brain.Fire("LeaveWorkerAssignment");
        }));
        public bool AddNewWorkerAssignment(BaseAI instance, MaxStack<Assignment> KnownAssignments)
        {
            Assignment nearbyAssignment = Common.FindRandomNearbyAssignment(instance, this.m_trainedAssignments, KnownAssignments, (float)(this.Awareness * 5));
            if ((nearbyAssignment == null) || (nearbyAssignment == KnownAssignments.Peek()))
                return false;
            KnownAssignments.Push(nearbyAssignment);
            return true;
        }
        public bool MoveToWorkerAssignment(float dt)
        {
            Assignment assignment = this.m_assignmentA.Peek();
            int num;
            if (assignment == null)
            {
                num = 0;
            }
            else
            {
                bool? nullable = assignment.AssignmentObject?.GetComponent<ZNetView>()?.IsValid();
                bool flag = false;
                num = nullable.GetValueOrDefault() == flag & nullable.HasValue ? 1 : 0;
            }
            if (num != 0)
            {
                this.m_assignmentA.Pop();
                return true;
            }
            float distance = (double)(this.m_closeEnoughTimer += dt) > (double)this.CloseEnoughTimeout ? this.m_assignmentA.Peek().TypeOfAssignment.InteractDist : this.m_assignmentA.Peek().TypeOfAssignment.InteractDist + 1f;
            return this.MoveAndAvoidFire(this.m_assignmentA.Peek().Position, dt, distance);
        }
        public override void Follow(Player player) => this.NView.InvokeRPC(ZNetView.Everybody, "RR_MobCommand", (object)player.GetZDOID(), (object)nameof(Follow));

        public MobAIInfo GetMobAIInfo() => new MobAIInfo()
        {
            Name = "NpcAI",
            AIType = this.GetType(),
            ConfigType = typeof(NpcAIConfig)
        };

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
            Player player = this.GetPlayer(playerId);
            if ((UnityEngine.Object)player == (UnityEngine.Object)null || !(command == "Follow"))
                return;
            (this.Instance as MonsterAI).ResetPatrolPoint();
            (this.Instance as MonsterAI).SetFollowTarget(player.gameObject);
        }

        public override void GotShoutedAtBy(MobAIBase mob) => this.Instance.m_alertedEffects.Create(this.Instance.transform.position, Quaternion.identity);

        public class State
        {
            public const string Idle = "Idle";
            public const string Follow = "Follow";
            public const string Fight = "Fight";
            public const string Flee = "Flee";
            public const string Assigned = "Assigned";
            public const string SearchForItems = "SearchForItems";
            public const string MoveToAssignment = "MoveToAssignment";
            public const string CheckRepairState = "CheckRepairState";
            public const string RepairAssignment = "RepairAssignment";
            public const string Root = "Root";
            public const string Hungry = "Hungry";
            public const string TurnToFaceAssignment = "TurnToFaceAssignment";
            public const string WorkerAssigned = "WorkerAssigned";
            public const string MoveToWorkerAssignment = "MoveToWorkerAssignment";
            public const string CheckWorkerAssignment = "CheckWorkerAssignment";
            public const string DoneWithWorkerAssignment = "DoneWithWorkerAssignment";
            public const string UnloadToWorkerAssignment = "UnloadToWorkerAssignment";
            public const string HaveAssignmentItem = "HaveAssignmentItem";
            public const string HaveNoAssignmentItem = "HaveNoAssignmentItem";
        }

        private class Trigger
        {
            public const string Update = "Update";
            public const string TakeDamage = "TakeDamage";
            public const string Follow = "Follow";
            public const string Hungry = "Hungry";
            public const string ItemFound = "ItemFound";
            public const string ConsumeItem = "ConsumeItem";
            public const string ItemNotFound = "ItemNotFound";
            public const string SearchForItems = "SearchForItems";
            public const string AssignmentTimedOut = "AssignmentTimedOut";
            public const string RepairNeeded = "RepairNeeded";
            public const string RepairDone = "RepairDone";
            public const string Failed = "Failed";
            public const string Fight = "Fight";
            public const string EnterEatBehaviour = "EnterEatBehaviour";
            public const string WorkerAssignmentFinished = "WorkerAssignmentFinished";
            public const string LeaveWorkerAssignment = "LeaveWorkerAssignment";
            public const string HasWorkerTasks = "HasWorkerTasks";
            public const string NoWorkerTasks = "NoWorkerTasks";
            public const string Bored = "Bored";
        }
    }
}
