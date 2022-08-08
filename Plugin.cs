using BepInEx;
using BepInEx.Configuration;
using Dungeonator;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace QoLMod
{
    [BepInDependency(ETGModMainBehaviour.GUID)]
    [BepInPlugin(GUID, NAME, VERSION)]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "spapi.etg.qolmod";
        public const string NAME = "SpecialAPI's QoL";
        public const string VERSION = "1.0.0";

        //misc category
        public static ConfigEntry<bool> RatStealsAmmo;
        public static ConfigEntry<bool> RatStealsArmor;
        public static ConfigEntry<bool> DisableExplosionQueue;
        public static ConfigEntry<bool> SprunTellsTheTrigger;
        public static ConfigEntry<bool> EnableDualGunRefill;

        //high dragunfire buff
        public static ConfigEntry<bool> HighDragunfireBuffEnabled;
        public static ConfigEntry<int> HighDragunfireMaxAmmo;
        public static ConfigEntry<float> HighDragunfireReloadTime;
        public static ConfigEntry<float> HighDragunfireFirerate;
        public static ConfigEntry<float> HighDragunfireProjectileDamage;
        public static ConfigEntry<float> HighDragunfireProjectileForce;
        public static ConfigEntry<float> HighDragunfireProjectileFireChance;
        public static ConfigEntry<bool> HighDragunfireBreakDamageCaps;
        public static ConfigEntry<bool> HighDragunfireInfinitePenetration;
        public static ConfigEntry<bool> HighDragunfirePierceGunjurerShields;

        [HarmonyPatch(typeof(Exploder), nameof(Exploder.DoExplode))]
        [HarmonyPrefix]
        public static void IgnoreQueue(ref bool ignoreQueues)
        {
            ignoreQueues |= DisableExplosionQueue.Value;
        }

        [HarmonyPatch(typeof(SprenOrbitalItem), nameof(SprenOrbitalItem.AssignTrigger))]
        [HarmonyPostfix]
        public static void TellTheTrigger(SprenOrbitalItem __instance)
        {
            if (!SprunTellsTheTrigger.Value)
            {
                return;
            }
            GameObject orbital = __instance.m_extantOrbital;
            if(orbital == null)
            {
                return;
            }
            string text = StringTableManager.GetString("#SPRUN_CURRENTTRIGGER_TEXT").Replace("%SPRUN_TRIGGER", StringTableManager.GetString("#SPRUN_" + __instance.m_trigger));
            TextBoxManager.ShowTextBox(orbital.transform.position + Vector3.up / 2, orbital.transform, 5, text, "", false, TextBoxManager.BoxSlideOrientation.NO_ADJUSTMENT, false, false);
        }

        [HarmonyPatch(typeof(AmmoPickup), nameof(AmmoPickup.Interact))]
        [HarmonyPrefix]
        public static bool Replace(AmmoPickup __instance, PlayerController interactor)
        {
            if (interactor != null && interactor.inventory != null && interactor.inventory.DualWielding)
            {
                if (!__instance)
                {
                    return false;
                }
                if ((interactor.CurrentGun == null || interactor.CurrentGun.ammo == interactor.CurrentGun.AdjustedMaxAmmo || interactor.CurrentGun.InfiniteAmmo || interactor.CurrentGun.RequiresFundsToShoot) && 
                    (interactor.CurrentSecondaryGun == null || interactor.CurrentSecondaryGun.ammo == interactor.CurrentSecondaryGun.AdjustedMaxAmmo || interactor.CurrentSecondaryGun.InfiniteAmmo || interactor.CurrentSecondaryGun.RequiresFundsToShoot))
                {
                    if (interactor.CurrentGun != null || interactor.CurrentSecondaryGun != null)
                    {
                        GameUIRoot.Instance.InformNeedsReload(interactor, new Vector3(interactor.specRigidbody.UnitCenter.x - interactor.transform.position.x, 1.25f, 0f), 1f, "#RELOAD_FULL");
                    }
                    return false;
                }
                if (RoomHandler.unassignedInteractableObjects.Contains(__instance))
                {
                    RoomHandler.unassignedInteractableObjects.Remove(__instance);
                }
                SpriteOutlineManager.RemoveOutlineFromSprite(__instance.sprite, true);
                __instance.Pickup(interactor);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(AmmoPickup), nameof(AmmoPickup.Pickup))]
        [HarmonyPrefix]
        public static bool Replace2(AmmoPickup __instance, PlayerController player)
        {
            if (player != null && player.inventory != null && player.inventory.DualWielding)
            {
                if (__instance.m_pickedUp)
                {
                    return false;
                }
                player.ResetTarnisherClipCapacity();
                var wasPickedUp = false;
                if (!(player.CurrentGun == null) && player.CurrentGun.ammo != player.CurrentGun.AdjustedMaxAmmo && player.CurrentGun.CanGainAmmo)
                {
                    switch (__instance.mode)
                    {
                        case AmmoPickup.AmmoPickupMode.ONE_CLIP:
                            player.CurrentGun.GainAmmo(player.CurrentGun.ClipCapacity);
                            break;
                        case AmmoPickup.AmmoPickupMode.FULL_AMMO:
                            if (player.CurrentGun.AdjustedMaxAmmo > 0)
                            {
                                player.CurrentGun.GainAmmo(player.CurrentGun.AdjustedMaxAmmo);
                                player.CurrentGun.ForceImmediateReload(false);
                                string @string = StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_HEADER");
                                string description = player.CurrentGun.GetComponent<EncounterTrackable>().journalData.GetPrimaryDisplayName(false) + " " + StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_BODY");
                                tk2dBaseSprite sprite = player.CurrentGun.GetSprite();
                                if (!GameUIRoot.Instance.BossHealthBarVisible)
                                {
                                    GameUIRoot.Instance.notificationController.DoCustomNotification(@string, description, sprite.Collection, sprite.spriteId, UINotificationController.NotificationColor.SILVER, false, false);
                                }
                            }
                            break;
                        case AmmoPickup.AmmoPickupMode.SPREAD_AMMO:
                            {
                                player.CurrentGun.GainAmmo(Mathf.CeilToInt((float)player.CurrentGun.AdjustedMaxAmmo * __instance.SpreadAmmoCurrentGunPercent));
                                player.CurrentGun.ForceImmediateReload(false);
                                string string2 = StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_HEADER");
                                string string3 = StringTableManager.GetString("#AMMO_SPREAD_REFILLED_BODY");
                                tk2dBaseSprite sprite2 = __instance.sprite;
                                if (!GameUIRoot.Instance.BossHealthBarVisible)
                                {
                                    GameUIRoot.Instance.notificationController.DoCustomNotification(string2, string3, sprite2.Collection, sprite2.spriteId, UINotificationController.NotificationColor.SILVER, false, false);
                                }
                                break;
                            }
                    }
                    wasPickedUp = true;
                }
                if (!(player.CurrentSecondaryGun == null) && player.CurrentSecondaryGun.ammo != player.CurrentSecondaryGun.AdjustedMaxAmmo && player.CurrentSecondaryGun.CanGainAmmo)
                {
                    switch (__instance.mode)
                    {
                        case AmmoPickup.AmmoPickupMode.ONE_CLIP:
                            player.CurrentSecondaryGun.GainAmmo(player.CurrentSecondaryGun.ClipCapacity);
                            break;
                        case AmmoPickup.AmmoPickupMode.FULL_AMMO:
                            if (player.CurrentSecondaryGun.AdjustedMaxAmmo > 0)
                            {
                                player.CurrentSecondaryGun.GainAmmo(player.CurrentSecondaryGun.AdjustedMaxAmmo);
                                player.CurrentSecondaryGun.ForceImmediateReload(false);
                                string @string = StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_HEADER");
                                string description = player.CurrentSecondaryGun.GetComponent<EncounterTrackable>().journalData.GetPrimaryDisplayName(false) + " " + StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_BODY");
                                tk2dBaseSprite sprite = player.CurrentSecondaryGun.GetSprite();
                                if (!GameUIRoot.Instance.BossHealthBarVisible)
                                {
                                    GameUIRoot.Instance.notificationController.DoCustomNotification(@string, description, sprite.Collection, sprite.spriteId, UINotificationController.NotificationColor.SILVER, false, false);
                                }
                            }
                            break;
                        case AmmoPickup.AmmoPickupMode.SPREAD_AMMO:
                            {
                                player.CurrentSecondaryGun.GainAmmo(Mathf.CeilToInt((float)player.CurrentSecondaryGun.AdjustedMaxAmmo * __instance.SpreadAmmoCurrentGunPercent));
                                player.CurrentSecondaryGun.ForceImmediateReload(false);
                                string string2 = StringTableManager.GetString("#AMMO_SINGLE_GUN_REFILLED_HEADER");
                                string string3 = StringTableManager.GetString("#AMMO_SPREAD_REFILLED_BODY");
                                tk2dBaseSprite sprite2 = __instance.sprite;
                                if (!GameUIRoot.Instance.BossHealthBarVisible)
                                {
                                    GameUIRoot.Instance.notificationController.DoCustomNotification(string2, string3, sprite2.Collection, sprite2.spriteId, UINotificationController.NotificationColor.SILVER, false, false);
                                }
                                break;
                            }
                    }
                    wasPickedUp = true;
                }
                if (wasPickedUp)
                {
                    if(__instance.mode == AmmoPickup.AmmoPickupMode.SPREAD_AMMO)
                    {
                        for (int i = 0; i < player.inventory.AllGuns.Count; i++)
                        {
                            if (player.inventory.AllGuns[i] && player.CurrentGun != player.inventory.AllGuns[i] && player.CurrentSecondaryGun != player.inventory.AllGuns[i])
                            {
                                player.inventory.AllGuns[i].GainAmmo(Mathf.FloorToInt((float)player.inventory.AllGuns[i].AdjustedMaxAmmo * __instance.SpreadAmmoOtherGunsPercent));
                            }
                        }
                        if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                        {
                            PlayerController otherPlayer = GameManager.Instance.GetOtherPlayer(player);
                            if (!otherPlayer.IsGhost)
                            {
                                for (int j = 0; j < otherPlayer.inventory.AllGuns.Count; j++)
                                {
                                    if (otherPlayer.inventory.AllGuns[j])
                                    {
                                        otherPlayer.inventory.AllGuns[j].GainAmmo(Mathf.FloorToInt((float)otherPlayer.inventory.AllGuns[j].AdjustedMaxAmmo * __instance.SpreadAmmoOtherGunsPercent));
                                    }
                                }
                                otherPlayer.CurrentGun.ForceImmediateReload(false);
                                if(otherPlayer.CurrentSecondaryGun != null)
                                {
                                    otherPlayer.CurrentSecondaryGun.ForceImmediateReload(false);
                                }
                            }
                        }
                    }
                    __instance.m_pickedUp = true;
                    __instance.m_isBeingEyedByRat = false;
                    __instance.GetRidOfMinimapIcon();
                    if (__instance.pickupVFX != null)
                    {
                        player.PlayEffectOnActor(__instance.pickupVFX, Vector3.zero, true, false, false);
                    }
                    Destroy(__instance.gameObject);
                    AkSoundEngine.PostEvent("Play_OBJ_ammo_pickup_01", __instance.gameObject);
                }
                return false;
            }
            return true;
        }

        public void Awake()
        {
            ETGMod.Databases.Strings.Core.Set("#SPRUN_CURRENTTRIGGER_TEXT",         "Current Trigger: %SPRUN_TRIGGER.");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_USED_LAST_BLANK",             "Player uses their last blank");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_LOST_LAST_ARMOR",             "Player loses their last piece of armor");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_REDUCED_TO_ONE_HEALTH",       "Player goes down to half a heart");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_GUN_OUT_OF_AMMO",             "Player's gun runs out of ammo");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_SET_ON_FIRE",                 "Player is set on fire");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_ELECTROCUTED_OR_POISONED",    "Player takes damage from electrocution or poison");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_FELL_IN_PIT",                 "Player falls into a pit");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_TOOK_ANY_HEART_DAMAGE",       "Player takes damage to hearts");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_FLIPPED_TABLE",               "Player flips a table");
            ETGMod.Databases.Strings.Core.Set("#SPRUN_ACTIVE_ITEM_USED",            "Player uses an active item");

            //misc category
            RatStealsAmmo           = Config.Bind("Misc", "RatStealsAmmo",              false,  "If false, the rat will no longer steal ammo pickups.");
            RatStealsArmor          = Config.Bind("Misc", "RatStealsArmor",             false,  "If false, the rat will no longer steal armor pickups.");
            DisableExplosionQueue   = Config.Bind("Misc", "DisableExplosionQueue",      true,   "If true, the explosion queue will be disabled and all explosions will be triggered without any wait.");
            SprunTellsTheTrigger    = Config.Bind("Misc", "SprunTellsTheTrigger",       true,   "If true, the sprun will tell the trigger when picked up.");
            EnableDualGunRefill     = Config.Bind("Misc", "EnableDualGunRefill",        true,   "If true, picking up an ammo pickup while dual wielding will refill both guns.");

            new Harmony(GUID).PatchAll();

            //high dragunfire buff
            HighDragunfireBuffEnabled           = Config.Bind("HighDragunfireBuff", "Enabled",                  true, 
                "If true, all of the high dragunfire buff configs will be enabled.");
            HighDragunfireMaxAmmo               = Config.Bind("HighDragunfireBuff", "MaxAmmo",                  1200, 
                "New max ammo for the High Dragunfire. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireReloadTime            = Config.Bind("HighDragunfireBuff", "ReloadTime",               0.8f, 
                "New reload time for the High Dragunfire (in seconds). Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireFirerate              = Config.Bind("HighDragunfireBuff", "FireRate",                 0.025f, 
                "New fire rate for the High Dragunfire. Fire rate is the cooldown time between shots (in seconds). Lower number means faster firing gun. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireProjectileDamage      = Config.Bind("HighDragunfireBuff", "ProjectileDamage",         10f, 
                "New projectile damage for the High Dragunfire. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireProjectileForce       = Config.Bind("HighDragunfireBuff", "ProjectileKnockback",      9f, 
                "New projectile knockback for the High Dragunfire. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireProjectileFireChance  = Config.Bind("HighDragunfireBuff", "ProjectileFireChance",     100f, 
                "New % chance for High Dragunfire's projectiles to set enemies on fire. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireBreakDamageCaps       = Config.Bind("HighDragunfireBuff", "BreakDamageCaps",          true, 
                "If true, High Dragunfire's projectiles will break both the damage cap and the boss DPS cap. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfireInfinitePenetration   = Config.Bind("HighDragunfireBuff", "InfinitePenetration",      true, 
                "If true, High Dragunfire's projectiles will infinitely pierce enemies. Only works when the High Dragunfire Buff is enabled.");
            HighDragunfirePierceGunjurerShields = Config.Bind("HighDragunfireBuff", "PierceGunjurerShields",    true, 
                "If true, High Dragunfire's projectiles will pierce the shields of Gunjurers like Gunther's last stage. Only works when the High Dragunfire Buff is enabled.");
        }

        public void GMStart(GameManager instance)
        {
            void UpdateAmmo()
            {
                PickupObjectDatabase.GetById(78).IgnoredByRat = !RatStealsAmmo.Value;
                PickupObjectDatabase.GetById(600).IgnoredByRat = !RatStealsAmmo.Value;
            }
            void UpdateArmor()
            {
                PickupObjectDatabase.GetById(120).IgnoredByRat = !RatStealsArmor.Value;
            }

            //misc
            RatStealsAmmo.SettingChanged += (x, x2) => UpdateAmmo();
            RatStealsArmor.SettingChanged += (x, x2) => UpdateArmor();

            UpdateAmmo();
            UpdateArmor();

            //high dragunfire buff
            EventHandler buffHandler = (x, x2) => UpdateHighDragunfireBuffs();
            HighDragunfireBuffEnabled.SettingChanged += buffHandler;
            HighDragunfireMaxAmmo.SettingChanged += buffHandler;
            HighDragunfireReloadTime.SettingChanged += buffHandler;
            HighDragunfireFirerate.SettingChanged += buffHandler;
            HighDragunfireProjectileDamage.SettingChanged += buffHandler;
            HighDragunfireProjectileForce.SettingChanged += buffHandler;
            HighDragunfireProjectileFireChance.SettingChanged += buffHandler;
            HighDragunfireBreakDamageCaps.SettingChanged += buffHandler;
            HighDragunfireInfinitePenetration.SettingChanged += buffHandler;
            HighDragunfirePierceGunjurerShields.SettingChanged += buffHandler;
            UpdateHighDragunfireBuffs();
        }

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }

        public static void UpdateHighDragunfireBuffs()
        {
            var hd = PickupObjectDatabase.GetById(670) as Gun;
            var mod = hd.DefaultModule;
            var proj = mod.projectiles[0];
            var pierce = proj.gameObject.GetOrAddComponent<PierceProjModifier>();
            if (HighDragunfireBuffEnabled.Value)
            {
                hd.ammo = hd.maxAmmo = HighDragunfireMaxAmmo.Value;
                hd.reloadTime = HighDragunfireReloadTime.Value;
                mod.cooldownTime = HighDragunfireFirerate.Value;
                proj.baseData.damage = HighDragunfireProjectileDamage.Value;
                proj.baseData.force = HighDragunfireProjectileForce.Value;
                proj.FireApplyChance = HighDragunfireProjectileFireChance.Value / 100f;
                proj.ignoreDamageCaps = HighDragunfireBreakDamageCaps.Value;
                pierce.penetration = (pierce.penetratesBreakables = HighDragunfireInfinitePenetration.Value) ? 9999 : 0;
                pierce.BeastModeLevel = HighDragunfirePierceGunjurerShields.Value ? PierceProjModifier.BeastModeStatus.BEAST_MODE_LEVEL_ONE : PierceProjModifier.BeastModeStatus.NOT_BEAST_MODE;
            }
            else
            {
                hd.ammo = hd.maxAmmo = 600;
                hd.reloadTime = 1.6f;
                mod.cooldownTime = 0.0666666f;
                proj.baseData.damage = 5;
                proj.baseData.force = 27;
                proj.FireApplyChance = 0.08f;
                proj.ignoreDamageCaps = false;
                pierce.penetration = 0;
                pierce.penetratesBreakables = false;
                pierce.BeastModeLevel = PierceProjModifier.BeastModeStatus.NOT_BEAST_MODE;
            }
        }
    }
}
