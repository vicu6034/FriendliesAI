using Stateless;
using System;
using UnityEngine;
using RagnarsRokare.MobAI;

namespace FriendliesAI.Behaviors
{
    internal class FightBehaviourF : IBehaviour
    {
        private const string Prefix = "RR_FIGHT";
        public float m_mobilityLevel;
        public float m_aggressionLevel;
        public float m_awarenessLevel;
        private float m_viewRange;
        private Vector3 m_startPosition;
        private float m_circleTargetDistance;
        private float m_searchTargetMovement;
        private MobAIBase m_aiBase;
        private ItemDrop.ItemData m_weapon;
        private float m_circleTimer;
        private float m_searchTimer;

        public string SuccessState { get; set; }

        public string FailState { get; set; }

        public string StartState => "RR_FIGHTMain";

        public bool IsBelowHealthThreshold(MobAIBase aiBase) => (double)aiBase.Character.GetHealthPercentage() < 0.3f;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            this.m_aiBase = aiBase;
            brain.Configure("RR_FIGHTMain").InitialTransition("RR_FIGHTIdentifyEnemy").PermitDynamic("RR_FIGHTFlee", (Func<string>)(() => this.FailState)).Permit("RR_FIGHTTargetLost", "RR_FIGHTIdentifyEnemy").SubstateOf(parentState).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_aiBase.UpdateAiStatus("Entered fighting behaviour");
                this.m_startPosition = aiBase.Instance.transform.position;
                this.m_viewRange = this.m_awarenessLevel * 5f;
                this.m_circleTargetDistance = this.m_mobilityLevel * 2f - this.m_aggressionLevel;
                this.m_searchTargetMovement = this.m_mobilityLevel;
            })).OnExit((Action<StateMachine<string, string>.Transition>)(t =>
            {
                aiBase.StopMoving();
                aiBase.TargetCreature = (Character)null;
            }));
            brain.Configure("RR_FIGHTIdentifyEnemy").SubstateOf("RR_FIGHTMain").Permit("RR_FIGHTFoundTarget", "RR_FIGHTSelectWeapon").Permit("RR_FIGHTNoTarget", "RR_FIGHTDoneFighting").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_searchTimer = this.m_aggressionLevel * 2f;
                if (!(aiBase.Attacker != null) || !aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                    return;
                aiBase.TargetCreature = aiBase.Attacker;
                aiBase.Brain.Fire("RR_FIGHTFoundTarget");
            }));
            brain.Configure("RR_FIGHTSelectWeapon").SubstateOf("RR_FIGHTMain").Permit("RR_FIGHTWeaponSelected", "RR_FIGHTTrackingEnemy").PermitDynamic("RR_FIGHTFailed", (Func<string>)(() => this.FailState)).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_weapon = (ItemDrop.ItemData)Common.Invoke<MonsterAI>(aiBase.Instance, "SelectBestAttack", (aiBase.Character as Humanoid), 1f);
                if (this.m_weapon == null)
                    brain.Fire("RR_FIGHTFailed");
                else
                    brain.Fire("RR_FIGHTWeaponSelected");
            }));
            brain.Configure("RR_FIGHTTrackingEnemy").SubstateOf("RR_FIGHTMain").Permit("RR_FIGHTAttack", "RR_FIGHTEngagingEnemy").Permit("RR_FIGHTNoTarget", "RR_FIGHTIdentifyEnemy").OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.m_searchTimer = this.m_aggressionLevel * 2f));
            brain.Configure("RR_FIGHTEngagingEnemy").SubstateOf("RR_FIGHTMain").Permit("RR_FIGHTAttack", "RR_FIGHTTrackingEnemy").Permit("RR_FIGHTNoTarget", "RR_FIGHTIdentifyEnemy").Permit("RR_FIGHTReposition", "RR_FIGHTCirclingEnemy").OnEntry((Action<StateMachine<string, string>.Transition>)(t => this.m_circleTimer = this.m_aggressionLevel));
            brain.Configure("RR_FIGHTCirclingEnemy").Permit("RR_FIGHTAttack", "RR_FIGHTTrackingEnemy").SubstateOf("RR_FIGHTMain").OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_circleTimer = 30f / this.m_aggressionLevel;
                aiBase.Character.Heal(aiBase.Character.GetMaxHealth() / 50f);
            }));
            brain.Configure("RR_FIGHTDoneFighting").SubstateOf("RR_FIGHTMain").PermitDynamic("RR_FIGHTDone", (Func<string>)(() => this.SuccessState)).OnEntry((Action<StateMachine<string, string>.Transition>)(t =>
            {
                this.m_aiBase.UpdateAiStatus("Done fighting.");
                aiBase.Character.Heal(aiBase.Character.GetMaxHealth() / 10f);
            })).OnExit((Action<StateMachine<string, string>.Transition>)(t =>
            {
                aiBase.Attacker = (Character)null;
                aiBase.TimeSinceHurt = 20f;
            }));
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (this.IsBelowHealthThreshold(aiBase))
                aiBase.Brain.Fire("RR_FIGHTFlee");
            else if (aiBase.Brain.IsInState("RR_FIGHTIdentifyEnemy"))
            {
                this.m_searchTimer -= dt;
                Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, this.m_startPosition, this.m_circleTargetDistance, true);
                if (Vector3.Distance(this.m_startPosition, aiBase.Character.transform.position) > this.m_viewRange - 5.0)
                    return;
                aiBase.TargetCreature = BaseAI.FindClosestEnemy(aiBase.Character, this.m_startPosition, this.m_viewRange);
                if (aiBase.TargetCreature != null && Vector3.Distance(this.m_startPosition, aiBase.TargetCreature.transform.position) < this.m_viewRange)
                {
                    Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    aiBase.Brain.Fire("RR_FIGHTFoundTarget");
                }
                else
                {
                    if ((double)this.m_searchTimer > 0.0)
                        return;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_FIGHTNoTarget");
                }
            }
            else if (aiBase.TargetCreature == null)
            {
                aiBase.Attacker = null;
                aiBase.Brain.Fire("RR_FIGHTTargetLost");
            }
            else if (aiBase.Brain.IsInState("RR_FIGHTTrackingEnemy"))
            {
                this.m_searchTimer -= dt;
                if (aiBase.Attacker != null && aiBase.TargetCreature != aiBase.Attacker && aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                    aiBase.TargetCreature = aiBase.Attacker;
                Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                if (Vector3.Distance(this.m_startPosition, aiBase.Character.transform.position) > this.m_viewRange && (aiBase.TargetCreature != aiBase.Attacker || this.m_aggressionLevel < 5.0))
                {
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_FIGHTNoTarget");
                }
                else if (aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, Math.Max(this.m_weapon.m_shared.m_aiAttackRange - 0.5f, 1f), true))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_FIGHTAttack");
                }
                else
                {
                    if ((double)this.m_searchTimer > 0.0)
                        return;
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire("RR_FIGHTNoTarget");
                }
            }
            else if (aiBase.Brain.IsInState("RR_FIGHTEngagingEnemy"))
            {
                this.m_circleTimer -= dt;
                bool flag = (bool)Common.Invoke<MonsterAI>(aiBase.Instance, "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                if (Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) >= this.m_weapon.m_shared.m_aiAttackRange)
                    aiBase.Brain.Fire("RR_FIGHTAttack");
                else if (!flag)
                    Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                else if (this.m_circleTimer <= 0.0)
                    aiBase.Brain.Fire("RR_FIGHTReposition");
                else
                    Common.Invoke<MonsterAI>((object)aiBase.Instance, "DoAttack", aiBase.TargetCreature, false);
            }
            else
            {
                if (aiBase.Brain.IsInState("RR_FIGHTCirclingEnemy"))
                {
                    this.m_circleTimer -= dt;
                    Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, this.m_circleTargetDistance, true);
                    if (this.m_circleTimer <= 0.0)
                    {
                        aiBase.Brain.Fire("RR_FIGHTAttack");
                        return;
                    }
                }
                if (!aiBase.Brain.IsInState("RR_FIGHTDoneFighting"))
                    return;
                aiBase.MoveAndAvoidFire(this.m_startPosition, dt, 0.5f);
                if ((double)Vector3.Distance(this.m_startPosition, aiBase.Character.transform.position) >= 1.0)
                    return;
                aiBase.Brain.Fire("RR_FIGHTDone");
            }
        }

        private class State
        {
            public const string Main = "RR_FIGHTMain";
            public const string IdentifyEnemy = "RR_FIGHTIdentifyEnemy";
            public const string SelectWeapon = "RR_FIGHTSelectWeapon";
            public const string TrackingEnemy = "RR_FIGHTTrackingEnemy";
            public const string EngagingEnemy = "RR_FIGHTEngagingEnemy";
            public const string CirclingEnemy = "RR_FIGHTCirclingEnemy";
            public const string AvoidFire = "RR_FIGHTAvoidFire";
            public const string DoneFighting = "RR_FIGHTDoneFighting";
        }

        private class Trigger
        {
            public const string Failed = "RR_FIGHTFailed";
            public const string Timeout = "RR_FIGHTTimeout";
            public const string WeaponSelected = "RR_FIGHTWeaponSelected";
            public const string FoundTarget = "RR_FIGHTFoundTarget";
            public const string NoTarget = "RR_FIGHTNoTarget";
            public const string TargetLost = "RR_FIGHTTargetLost";
            public const string Attack = "RR_FIGHTAttack";
            public const string Flee = "RR_FIGHTFlee";
            public const string Reposition = "RR_FIGHTReposition";
            public const string Done = "RR_FIGHTDone";
        }
    }
}
