﻿using R2API;
using RoR2;
using RoR2.Orbs;
using SniperClassic.Modules;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;

namespace SniperClassic.Hooks
{
    public class OnHitEnemy
    {
        public static GameObject shockExplosionEffect;

        public static void GlobalEventManager_OnHitEnemy(On.RoR2.GlobalEventManager.orig_ProcessHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            CharacterBody victimBody = null;
            bool hadSpotter = false;
            bool hadSpotterScepter = false;
            if (NetworkServer.active && victim)
            {
                victimBody = victim.GetComponent<CharacterBody>();
                if (victimBody)
                {
                    hadSpotterScepter = victimBody.HasBuff(SniperContent.spotterScepterBuff);
                    hadSpotter = hadSpotterScepter || victimBody.HasBuff(SniperContent.spotterBuff);
                }
            }
            orig(self, damageInfo, victim);
            if (NetworkServer.active && !damageInfo.rejected)
            {
                bool victimPresent = victimBody && victimBody.healthComponent;
                bool victimAlive = victimPresent && victimBody.healthComponent.alive;
                if (damageInfo.HasModdedDamageType(SniperContent.SpotterDebuffOnHit) && victimAlive && damageInfo.procCoefficient > 0f)
                {
                    victimBody.AddTimedBuff(SniperContent.spotterStatDebuff, 2f);
                }
                if (hadSpotter && damageInfo.attacker)
                {
                    CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerBody && (!Config.spotterRequiresSniper || attackerBody.bodyIndex == BodyCatalog.FindBodyIndex("SniperClassicBody")))
                    {
                        if (damageInfo.procCoefficient > 0f && (damageInfo.HasModdedDamageType(SniperContent.FullCharge) || (damageInfo.damage / attackerBody.damage >= 10f)))
                        {
                            //Spotter Targeting/Recharge controller will apply the cooldown.
                            if (victimPresent)
                            {
                                if (victimAlive)
                                {
                                    if (victimBody.HasBuff(SniperContent.spotterBuff))
                                    {
                                        victimBody.RemoveBuff(SniperContent.spotterBuff);
                                    }
                                    if (victimBody.HasBuff(SniperContent.spotterScepterBuff))
                                    {
                                        victimBody.RemoveBuff(SniperContent.spotterScepterBuff);
                                    }
                                }

                                EnemySpotterReference esr = victim.GetComponent<EnemySpotterReference>();
                                if (esr.spotterOwner)
                                {
                                    SpotterRechargeController src = esr.spotterOwner.GetComponent<SpotterRechargeController>();
                                    if (src)
                                    {
                                        src.TriggerSpotter();
                                    }
                                }
                            }

                            List<HealthComponent> bouncedObjects = new List<HealthComponent>();
                            int targets = 20;
                            float range = 30f;

                            //Need to individually find all targets for the first bounce.
                            for (int i = 0; i < targets; i++)
                            {
                                LightningOrb spotterLightning = new LightningOrb
                                {
                                    bouncedObjects = bouncedObjects,
                                    attacker = damageInfo.attacker,
                                    inflictor = damageInfo.attacker,
                                    damageValue = damageInfo.damage * (hadSpotterScepter ? 1f : 0.5f),
                                    procCoefficient = 0.5f,
                                    teamIndex = attackerBody.teamComponent.teamIndex,
                                    isCrit = damageInfo.crit,
                                    procChainMask = damageInfo.procChainMask,
                                    lightningType = LightningOrb.LightningType.Tesla,
                                    damageColorIndex = DamageColorIndex.Nearby,
                                    bouncesRemaining = (hadSpotterScepter ? 2 : 1),
                                    targetsToFindPerBounce = (hadSpotterScepter ? 3 : 2),
                                    range = range,
                                    origin = damageInfo.position,
                                    damageType = (DamageType.SlowOnHit | (hadSpotterScepter ? DamageType.Shock5s : DamageType.Stun1s)),
                                    speed = 120f
                                };
                                spotterLightning.damageType.damageSource = DamageSource.Special;

                                HurtBox hurtBox = spotterLightning.PickNextTarget(damageInfo.position);

                                //Fire orb if HurtBox is found.
                                if (hurtBox)
                                {
                                    spotterLightning.target = hurtBox;
                                    spotterLightning.range *= 0.5f; //bounces have reduced range compared to the initial bounce
                                    OrbManager.instance.AddOrb(spotterLightning);
                                    spotterLightning.bouncedObjects.Add(hurtBox.healthComponent);
                                }
                            }

                            EffectManager.SpawnEffect(OnHitEnemy.shockExplosionEffect, new EffectData
                            {
                                origin = damageInfo.position,
                                scale = range
                            }, true);
                        }
                    }
                }
            }
        }
    }
}