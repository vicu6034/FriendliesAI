using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;


namespace FriendliesAI
{
    [BepInPlugin("som.FriendliesAI", "FriendliesAI", "0.0.7")]
    [BepInDependency("som.Friendlies")]
    [BepInDependency("RagnarsRokare.MobAILib")]

    public class Plugin : BaseUnityPlugin
    {
        private const string GUID = "som.FriendliesAI";
        private const string NAME = "FriendliesAI";
        private const string VERSION = "0.0.7";
        internal static ManualLogSource log;

        private void Awake()
        {
            Plugin.log = this.Logger;
            NpcConfig.Init(this.Config);
            Type npcAI = new NpcAI().GetType();
            MobManager.RegisterMobAI(npcAI);
            //Debug.Log(MobManager.GetRegisteredMobAIs());
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(Character), "Damage")]
        private static class Character_Damaged_Patch
        {
            private static void Prefix(
                ref Character __instance,
                ref ZNetView ___m_nview,
                ref HitData hit)
            {
                string uniqueId = ___m_nview.GetZDO().GetString("RR_CharId");
                if (hit.GetAttacker() == null)
                    return;
                Character attacker = hit.GetAttacker();
                var touse = 1800f;
                if (___m_nview.GetZDO().GetFloat("TameTimeLeft", touse) < 1800f && attacker.IsTamed())
                {
                    hit.m_damage.Modify((0));
                    return;
                }
                    

                if (string.IsNullOrEmpty(uniqueId) || !MobManager.IsAliveMob(uniqueId))
                    return;
                
                if ( attacker != null && attacker.IsPlayer())
                    hit.m_damage.Modify(0.1f);
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        private static class Character_Awake_Patch
        {
            private static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name))
                    return;
                string uniqueId = GetOrCreateUniqueId(___m_nview);
                MobConfig mobConfig = MobConfigManager.GetMobConfig(__instance.name);
                //Tameable orAddTameable = __instance.GetComponent<Tameable>();
                //orAddTameable.m_tamingTime = mobConfig.TamingTime;
                //orAddTameable.m_commandable = true;
                AddVisualEquipmentCapability(__instance);
                ___m_nview.Register<string, string>("RR_UpdateCharacterHUD", new Action<long, string, string>(RPC_UpdateCharacterName));
                MonsterAI baseAi = __instance.GetBaseAI() as MonsterAI;
                if (__instance.IsTamed())
                {
                    try
                    {
                        MobManager.RegisterMob(__instance, uniqueId, mobConfig.AIType, mobConfig.AIConfig);
                    }
                    catch (ArgumentException ex)
                    {
                        Debug.LogError((object) ("Failed to register Mob AI (" + mobConfig.AIType + "). " +
                                                 ex.Message));
                        return;
                    }

                    //orAddTameable.m_fedDuration = mobConfig.PostTameFeedDuration;
                    //baseAi.m_consumeItems.Clear();
                    //baseAi.m_consumeItems.AddRange(mobConfig.PostTameConsumables);
                    baseAi.m_randomMoveRange = 5f;
                    baseAi.m_consumeSearchRange = 15f;
                    string str = ___m_nview?.GetZDO()?.GetString("RR_GivenName");
                    if (!string.IsNullOrEmpty(str))
                        __instance.m_name = str;
                }
                /*
                else
                {
                    orAddTameable.m_fedDuration = mobConfig.PreTameFeedDuration;
                    baseAi.m_consumeItems.Clear();
                    baseAi.m_consumeItems.AddRange(mobConfig.PreTameConsumables);
                }
                */
            }

            public static string GetOrCreateUniqueId(ZNetView ___m_nview)
            {
                string str = ___m_nview.GetZDO().GetString("RR_CharId");
                if (string.IsNullOrEmpty(str))
                {
                    str = Guid.NewGuid().ToString();
                    ___m_nview.GetZDO().Set("RR_CharId", str);
                }

                return str;
            }

            private static void AddVisualEquipmentCapability(Character __instance)
            {
                /*
                if (__instance.gameObject.GetComponent<VisEquipment>() == null)
                {
                    __instance.gameObject.AddComponent<VisEquipment>();
                }
                */
                __instance.gameObject.GetComponent<VisEquipment>().m_rightHand =
                    ((IEnumerable<Transform>) __instance.gameObject.GetComponentsInChildren<Transform>())
                    .Where<Transform>((Func<Transform, bool>) (c => c.name == "RightHand_Attach")).Single<Transform>();
            }

            public static void BroadcastUpdateCharacterName(ref ZNetView nview, string text) => nview.InvokeRPC(
                ZNetView.Everybody, "RR_UpdateCharacterHUD", (object) nview.GetZDO().GetString("RR_CharId"),
                (object) text);

            public static void RPC_UpdateCharacterName(long sender, string uniqueId, string text)
            {
                if (!MobManager.IsAliveMob(uniqueId))
                    return;
                Character character;
                try
                {
                    character = MobManager.AliveMobs[uniqueId].Character;
                }
                catch (Exception ex)
                {
                    return;
                }

                character.m_name = text;
                IDictionary dictionary =
                    EnemyHud.instance.GetType()
                        .GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField)
                        .GetValue((object) EnemyHud.instance) as IDictionary;
                if (!dictionary.Contains((object) character))
                    return;
                object obj = dictionary[(object) character];
                Text text1 = obj.GetType().GetField("m_name", BindingFlags.Instance | BindingFlags.Public).GetValue(obj) as Text;
                if ((UnityEngine.Object) text1 == (UnityEngine.Object) null)
                    return;
                text1.text = text;
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        private static class MonsterAI_MakeTame_Patch
        {
            private static void Postfix(
                MonsterAI __instance,
                ZNetView ___m_nview,
                Character ___m_character)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name))
                    return;
                MobConfig mobConfig = MobConfigManager.GetMobConfig(__instance.name);
                //__instance.m_consumeItems.Clear();
                //__instance.m_consumeItems.AddRange(mobConfig.PostTameConsumables);
                __instance.m_consumeSearchRange = 50f;
                try
                {
                    string uniqueId = ___m_nview.GetZDO().GetString("RR_CharId");
                    MobManager.RegisterMob(___m_character, uniqueId, mobConfig.AIType, mobConfig.AIConfig);
                }
                catch (ArgumentException ex)
                {
                    Debug.LogError((object) ("Failed to register Mob AI (" + mobConfig.AIType + "). " + ex.Message));
                }
            }
        }

        private class MyTextReceiver : TextReceiver
        {
            private ZNetView m_nview;
            private readonly Character m_character;

            public MyTextReceiver(Character character)
            {
                this.m_nview = character.GetComponent<ZNetView>();
                this.m_character = character;
            }

            public string GetText() => this.m_nview.GetZDO().GetString("RR_GivenName");

            public void SetText(string text)
            {
                this.m_nview.ClaimOwnership();
                this.m_nview.GetZDO().Set("RR_GivenName", text);
                Character_Awake_Patch.BroadcastUpdateCharacterName(ref this.m_nview, text);
            }
        }

        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        private static class Tameable_GetHoverName_Patch
        {
            private static bool Prefix(
                Tameable __instance,
                ref string __result,
                ZNetView ___m_nview,
                Character ___m_character)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name) || !___m_character.IsTamed())
                    return true;
                if (!___m_nview.IsValid())
                {
                    __result = string.Empty;
                    return true;
                }

                string str1 = ___m_nview.GetZDO().GetString("RR_AiStatus") ??
                              Traverse.Create((object) __instance).Method("GetStatusString").GetValue() as string;
                string str2 = (___m_character.GetHoverName() + " ( Tame " + str1 + " )");
                __result = (str2 +
                            "\n[<color=yellow><b>E</b></color>] Follow/Stay\n[<color=yellow>Hold E</color>] to change name");
                return false;
            }
        }

        [HarmonyPatch(typeof(Tameable), "Interact")]
        private static class Tameable_Interact_Patch
        {
            private static bool Prefix(
                Tameable __instance,
                ref bool __result,
                Humanoid user,
                bool hold,
                ZNetView ___m_nview,
                ref Character ___m_character,
                ref float ___m_lastPetTime)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name))
                    return true;
                if (!___m_nview.IsValid())
                {
                    __result = false;
                    return true;
                }

                string hoverName = ___m_character.GetHoverName();
                if (___m_character.IsTamed())
                {
                    if (hold)
                    {
                        TextInput.instance.RequestText((TextReceiver) new MyTextReceiver(___m_character), "Name", 15);
                        __result = false;
                        return false;
                    }

                    if ((double) Time.time - (double) ___m_lastPetTime > 1.0)
                    {
                        ___m_lastPetTime = Time.time;
                        __instance.m_petEffect.Create(___m_character.GetCenterPoint(), Quaternion.identity);
                        if (__instance.m_commandable)
                            typeof(Tameable).GetMethod("Command", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke((object) __instance, new object[1]
                                {
                                    (object) user
                                });
                        else
                            user.Message(MessageHud.MessageType.Center, hoverName + " $hud_tamelove");
                        __result = true;
                        return false;
                    }

                    __result = false;
                    return false;
                }

                __result = false;
                return false;
            }
        }
    }
}

