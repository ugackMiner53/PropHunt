using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PowerTools;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;
using UnityEngine;

namespace PropHunt
{
    [HarmonyPatch]
    public class Patches
    {
        private static CustomButton disguiseButton;
        private static CustomButton revertButton;
        private static CustomButton movePropButton;
        public static bool isMovingProp = false;

        public static AnimationClip seekerTransformClip;

        public static bool bypassSeekerAnimStretch = false;

        private static readonly HashSet<PlayerControl> transformedPlayers = new HashSet<PlayerControl>();
        private static readonly HashSet<PlayerControl> morphFinishedPlayers = new HashSet<PlayerControl>();

        private const float SeekerMorphJump = 7.2f;

        private static float SeekerMorphJumpTime(AnimationClip clip)
        {
            if (clip == null) return 0f;
            return Mathf.Max(0f, Mathf.Min(SeekerMorphJump, clip.length - 2f));
        }

        private sealed class MorphWatchState
        {
            public float End;
            public float Next;
            public int Attempts;
            public bool HandedBack;
            public Vector3 LastPos;
            public float MorphStartedAt;
        }
        private static readonly Dictionary<PlayerControl, MorphWatchState> morphWatch = new Dictionary<PlayerControl, MorphWatchState>();

        private static GameObject propPreviewHolder;
        private static SpriteRenderer propPreviewRenderer;

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
        [HarmonyPostfix]
        public static void HudManagerStartPatch(HudManager __instance)
        {
            disguiseButton = new CustomButton(
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor) return;
                    Console closest = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
                    if (closest != null)
                    {
                        for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
                        {
                            if (ShipStatus.Instance.AllConsoles[i] == closest)
                            {
                                Logger<PropHuntPlugin>.Info("Task of index " + i + " being sent out");
                                RPCHandler.RPCPropSync(PlayerControl.LocalPlayer, i + "");
                                // Start the disguise cooldown (configurable in the settings)
                                disguiseButton.Timer = Mathf.Max(0.01f, PropHuntPlugin.disguiseCooldown);
                                disguiseButton.MaxTimer = Mathf.Max(0.01f, PropHuntPlugin.disguiseCooldown);
                                break;
                            }
                        }
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor
                        || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
                        return false;
                    Console target = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
                    if (target != null)
                    {
                        Sprite s = target.GetComponent<SpriteRenderer>()?.sprite
                                ?? target.GetComponentInChildren<SpriteRenderer>()?.sprite;
                        if (s != null)
                        {
                            propPreviewRenderer.sprite = s;
                            propPreviewHolder.transform.localScale = Vector3.one;
                            float max = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
                            if (max > 0) propPreviewHolder.transform.localScale /= max;
                            return true;
                        }
                    }
                    // No prop in range → clear the stale preview so it doesn't linger
                    if (propPreviewRenderer != null) propPreviewRenderer.sprite = null;
                    return false;
                },
                () => { },
                null,
                new Vector3(0f, 1f, 0f),
                __instance,
                KeyCode.R,
                buttonText: "DISGUISE"
            );

            propPreviewHolder = new GameObject("PropPreview");
            propPreviewRenderer = propPreviewHolder.AddComponent<SpriteRenderer>();
            propPreviewHolder.transform.SetParent(disguiseButton.actionButton.transform, false);
            propPreviewHolder.transform.localPosition = new Vector3(0, 0, -2f);

            // Ready to disguise immediately (override the button's default cooldown value)
            disguiseButton.Timer = -1f;

            revertButton = new CustomButton(
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor) return;
                    PlayerControl player = PlayerControl.LocalPlayer;
                    if (TryGetAliveProp(player, out SpriteRenderer revertProp) && revertProp.sprite != null)
                    {
                        Logger<PropHuntPlugin>.Info("Reverting to crewmate");
                        RPCHandler.RPCRevert(player);
                        player.Visible = true;

                        // Reverting the disguise also releases the "move prop"
                        // lock so the player regains normal movement control.
                        if (isMovingProp)
                        {
                            isMovingProp = false;
                            if (movePropButton != null)
                            {
                                movePropButton.buttonText = "MOVE PROP";
                                movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                                movePropButton.Timer = 0f;
                                movePropButton.isEffectActive = false;
                            }
                        }
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && TryGetAliveProp(PlayerControl.LocalPlayer, out SpriteRenderer revertProp) && revertProp.sprite != null
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () => true,
                () => { },
                Utility.LoadSprite("PropHunt.Resources.RevertButton.png", 150f),
                new Vector3(-2f, 1f, 0f),
                __instance,
                KeyCode.C,
                buttonText: "REVERT"
            );
            revertButton.Timer = -1f;

            movePropButton = new CustomButton(
                () =>
                {
                    if (isMovingProp)
                    {
                        PlayerControl player = PlayerControl.LocalPlayer;
                        if (PropManager.playerToProp.ContainsKey(player))
                        {
                            // Sync the final (fine-tuned) position to every client
                            RPCHandler.RPCPropPos(player, PropManager.playerToProp[player].transform.localPosition);
                        }
                        isMovingProp = false;
                        movePropButton.buttonText = "MOVE PROP";
                        movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                        movePropButton.Timer = 0f;
                        movePropButton.isEffectActive = false;
                    }
                    else
                    {
                        isMovingProp = true;
                        movePropButton.buttonText = "FIX PROP";
                        movePropButton.actionButtonRenderer.color = new Color(0F, 0.8F, 0F);
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && TryGetAliveProp(PlayerControl.LocalPlayer, out SpriteRenderer moveCheckProp) && moveCheckProp.sprite != null
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () => true,
                () =>
                {
                    if (isMovingProp)
                    {
                        PlayerControl player = PlayerControl.LocalPlayer;
                        if (PropManager.playerToProp.ContainsKey(player))
                        {
                            RPCHandler.RPCPropPos(player, PropManager.playerToProp[player].transform.localPosition);
                        }
                        isMovingProp = false;
                        movePropButton.buttonText = "MOVE PROP";
                    }
                },
                Utility.LoadSprite("PropHunt.Resources.MovePropButton.png", 150f),
                new Vector3(-1f, 1f, 0f),
                __instance,
                KeyCode.LeftShift,
                hasEffect: false,
                effectDuration: 0f,
                onEffectEnds: () => { },
                buttonText: "MOVE PROP"
            );
            movePropButton.Timer = -1f;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        [HarmonyPostfix]
        public static void MovePropPatch(PlayerPhysics __instance)
        {
            if (!PropHuntPlugin.isPropHunt || !__instance.AmOwner) return;
            if (!isMovingProp || !TryGetAliveProp(__instance.myPlayer, out SpriteRenderer movePropRenderer)) return;

            Vector2 input = DestroyableSingleton<HudManager>.Instance.joystick.DeltaL;

            Transform prop = movePropRenderer.transform;
            Vector3 newPosition = new Vector3(
                prop.localPosition.x + input.x * PropHuntPlugin.propMoveSpeed * Time.fixedDeltaTime,
                prop.localPosition.y + input.y * PropHuntPlugin.propMoveSpeed * Time.fixedDeltaTime,
                0);

            if (Vector2.Distance(Vector2.zero, newPosition) < PropHuntPlugin.maxPropDistance)
            {
                prop.localPosition = newPosition;
            }
            __instance.SetNormalizedVelocity(Vector2.zero);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        [HarmonyPostfix]
        public static void PlayerControlStartPatch(PlayerControl __instance)
        {
            GameObject propObj = new GameObject("Prop")
            {
                layer = 8
            };
            SpriteRenderer propRenderer = propObj.AddComponent<SpriteRenderer>();
            propObj.transform.SetParent(__instance.transform);
            propObj.transform.localScale = Vector2.one;
            // z = 0 puts the prop exactly on the vanilla player body plane, so every
            // shadow/occluder that covers a real player covers the prop too.
            propObj.transform.localPosition = new Vector3(0, 0, 0);
            PropManager.playerToProp.Add(__instance, propRenderer);
        }

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
        [HarmonyPostfix]
        public static void OnExitGame()
        {
            PropManager.playerToProp.Clear();
            isMovingProp = false;
        }

        [HarmonyPatch(typeof(LogicOptionsHnS), nameof(LogicOptionsHnS.GetCrewmateLeadTime))]
        [HarmonyPostfix]
        public static void CrewmateLeadTimePatch(ref int __result)
        {
            if (!PropHuntPlugin.isPropHunt) return;
            __result = (int)PropHuntPlugin.seekerWaitTime;
        }

        [HarmonyPatch(typeof(PlayerControl), "set_Visible")]
        [HarmonyPrefix]
        public static void PlayerVisibleGuardPatch(PlayerControl __instance, ref bool value)
        {
            if (!value || !PropHuntPlugin.isPropHunt) return;
            if (!TryGetAliveProp(__instance, out SpriteRenderer guardProp) || guardProp.sprite == null) return;
            if (__instance.Data == null || __instance.Data.Role == null) return;
            if (__instance.Data.Role.IsImpostor || __instance.Data.IsDead) return;
            value = false;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        [HarmonyPostfix]
        public static void PropVentVisibilityPatch(PlayerPhysics __instance)
        {
            if (!PropHuntPlugin.isPropHunt || __instance.myPlayer == null) return;
            if (!TryGetAliveProp(__instance.myPlayer, out SpriteRenderer ventProp) || ventProp.sprite == null) return;
            bool shouldShow = !__instance.myPlayer.inVent;
            if (ventProp.enabled != shouldShow) ventProp.enabled = shouldShow;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
        [HarmonyPostfix]
        public static void OnPlayerDiePatch(PlayerControl __instance)
        {
            if (!PropHuntPlugin.isPropHunt || __instance.Data.Role.IsImpostor) return;

            if (PropManager.playerToProp.TryGetValue(__instance, out SpriteRenderer prop))
            {
                if (prop != null)
                {
                    prop.gameObject.Destroy();
                }
                PropManager.playerToProp.Remove(__instance);
            }
            if (__instance == PlayerControl.LocalPlayer)
            {
                isMovingProp = false;
                if (movePropButton != null)
                {
                    movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                    movePropButton.Timer = 0f;
                    movePropButton.isEffectActive = false;
                }
            }

            if (PropHuntPlugin.infectionMode)
            {
                Logger<PropHuntPlugin>.Info("Infection conversion: player " + __instance.PlayerId
                    + (__instance.AmOwner ? " (local)" : ""));
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, RoleTypes.Impostor);
                __instance.Data.Role.TeamType = RoleTeamTypes.Impostor;
                __instance.Visible = true;
                __instance.cosmetics.SetPetVisible(true);

                foreach (DeadBody body in Object.FindObjectsOfType<DeadBody>())
                {
                    if (body.ParentId == __instance.PlayerId)
                    {
                        body.gameObject.SetActive(false);
                        break;
                    }
                }

                RegisterSeekerThreat(__instance);

                ApplySeekerTransformVisual(__instance, true);

                if (__instance.AmOwner)
                {
                    DestroyableSingleton<HudManager>.Instance.ShadowQuad.material.color = new Color(0f, 0f, 0f, 1f);
                    RPCHandler.RPCSeekerTransform(__instance);
                }
            }
        }

        private static bool IsInfectionVictim(PlayerControl player)
        {
            return PropHuntPlugin.isPropHunt && PropHuntPlugin.infectionMode
                && player != null && player.Data != null && !player.Data.Role.IsImpostor;
        }

        [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRoleOnDeath))]
        [HarmonyPrefix]
        public static bool AssignRoleOnDeathPatch(PlayerControl player, bool specialRolesAllowed)
        {
            return !IsInfectionVictim(player);
        }

        [HarmonyPatch(typeof(HideAndSeekManager), nameof(HideAndSeekManager.OnPlayerDeath))]
        [HarmonyPrefix]
        public static bool HideAndSeekOnPlayerDeathPatch(PlayerControl player, bool assignGhostRole)
        {
            return !IsInfectionVictim(player);
        }

        private static void RegisterSeekerThreat(PlayerControl player)
        {
            try
            {
                HideAndSeekManager hnsManager = GameManager.Instance != null ? GameManager.Instance.Cast<HideAndSeekManager>() : null;
                if (hnsManager == null || hnsManager.LogicDangerLevel == null) return;
                var impostors = hnsManager.LogicDangerLevel.impostors;
                if (impostors == null || impostors.Contains(player)) return;
                impostors.Add(player);
                Logger<PropHuntPlugin>.Info("Threat list now includes converted seeker " + player.PlayerId + " (count " + impostors.Count + ")");
            }
            catch (System.Exception e)
            {
                Logger<PropHuntPlugin>.Warning("RegisterSeekerThreat failed: " + e);
            }
        }

        public static void ApplySeekerTransformVisual(PlayerControl player, bool force)
        {
            if (!PropHuntPlugin.isPropHunt || !PropHuntPlugin.infectionMode) return;
            if (player == null || player.Data == null) return;

            if (transformedPlayers.Contains(player))
            {
                if (morphWatch.ContainsKey(player))
                {
                    Logger<PropHuntPlugin>.Info("SeekerTransform duplicate skipped for player "
                        + player.PlayerId + " (morph already playing)");
                    return;
                }
                if (morphFinishedPlayers.Contains(player))
                {
                    Logger<PropHuntPlugin>.Info("SeekerTransform duplicate skipped for player "
                        + player.PlayerId + " (morph already finished)");
                    return;
                }
            }
            transformedPlayers.Add(player);

            try
            {
                if (seekerTransformClip == null)
                {
                    seekerTransformClip = FindSeekerTransformClip();
                }

                Logger<PropHuntPlugin>.Info("SeekerTransform: player " + player.PlayerId
                    + " clip=" + (seekerTransformClip != null ? seekerTransformClip.name : "NULL")
                    + " force=" + force);

                GameManager.Instance.SetSpecialCosmetics(player);

                if (player.MyPhysics != null)
                {
                    PlayerBodyTypes bodyType = AprilFoolsMode.ShouldLongAround()
                        ? PlayerBodyTypes.LongSeeker
                        : PlayerBodyTypes.Seeker;
                    player.MyPhysics.SetBodyType(bodyType);
                    Logger<PropHuntPlugin>.Info("SeekerTransform: player " + player.PlayerId
                        + " bodyType=" + bodyType);
                }

                if (seekerTransformClip != null)
                {
                    DiagnoseAnimator(player, "before play");

                    if (player.NetTransform != null)
                    {
                        player.NetTransform.Halt();
                    }

                    bypassSeekerAnimStretch = true;
                    try
                    {
                        float morphJump = SeekerMorphJumpTime(seekerTransformClip);
                        Logger<PropHuntPlugin>.Info("SeekerTransform: player " + player.PlayerId
                            + " morph segment " + morphJump.ToString("0.00") + "s.."
                            + seekerTransformClip.length.ToString("0.00") + "s");

                        SpriteAnim physAnim = player.MyPhysics != null && player.MyPhysics.Animations != null
                            ? player.MyPhysics.Animations.Animator
                            : null;
                        if (physAnim != null)
                        {
                            physAnim.Play(seekerTransformClip, 1f);
                            physAnim.SetTime(morphJump);
                        }

                        if (player.cosmetics != null)
                        {
                            player.cosmetics.SetBodyCosmeticsVisible(false);
                        }
                    }
                    finally
                    {
                        bypassSeekerAnimStretch = false;
                    }

                    DiagnoseAnimator(player, "after play");
                    EnsureMorphWatchdog(player);
                }
            }
            catch (System.Exception e)
            {
                Logger<PropHuntPlugin>.Warning("SeekerTransform failed for player " + player.PlayerId + ": " + e);
            }
        }

        private static void DiagnoseAnimator(PlayerControl player, string tag)
        {
            try
            {
                PlayerAnimations anims = player.MyPhysics != null ? player.MyPhysics.Animations : null;
                SpriteAnim physAnim = anims != null ? anims.Animator : null;
                SpriteAnim skinAnim = player.cosmetics != null ? player.cosmetics.GetSkinSpriteAnim() : null;
                Logger<PropHuntPlugin>.Info("MorphState[" + tag + "] player " + player.PlayerId
                    + " physCur=" + (physAnim != null && physAnim.GetCurrentAnimation() != null ? physAnim.GetCurrentAnimation().name : "null")
                    + " physPlaying=" + (physAnim != null && physAnim.Playing)
                    + " someAnim=" + (anims != null && anims.IsPlayingSomeAnimation())
                    + " runAnim=" + (anims != null && anims.IsPlayingRunAnimation())
                    + " skinCur=" + (skinAnim != null && skinAnim.GetCurrentAnimation() != null ? skinAnim.GetCurrentAnimation().name : "null")
                    + " spawnGuard=" + (anims != null && anims.IsPlayingSpawnAnimation())
                    + " groupSpawn=" + (anims != null && anims.group != null && anims.group.SpawnAnim != null ? anims.group.SpawnAnim.name : "null")
                    + " playerGO=" + (player.gameObject != null && player.gameObject.activeInHierarchy)
                    + " physGO=" + (player.MyPhysics != null && player.MyPhysics.gameObject != null && player.MyPhysics.gameObject.activeInHierarchy)
                    + " isDead=" + player.Data.IsDead
                    + " visible=" + player.Visible);
            }
            catch (System.Exception e)
            {
                Logger<PropHuntPlugin>.Warning("DiagnoseAnimator failed: " + e);
            }
        }

        private static void EnsureMorphWatchdog(PlayerControl player)
        {
            if (morphWatch.ContainsKey(player)) return;
            morphWatch[player] = CreateMorphWatchState();
        }

        private static MorphWatchState CreateMorphWatchState()
        {
            AnimationClip clip = seekerTransformClip;
            float duration = clip != null
                ? Mathf.Max(0.5f, clip.length - SeekerMorphJumpTime(clip))
                : 3f;
            return new MorphWatchState
            {
                End = Time.time + duration,
                Next = Time.time + 0.3f,
                Attempts = 0,
                MorphStartedAt = Time.time,
            };
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
        [HarmonyPostfix]
        public static void MorphWatchdogTick(HudManager __instance)
        {
            if (morphWatch.Count == 0) return;
            List<PlayerControl> finished = null;
            foreach (KeyValuePair<PlayerControl, MorphWatchState> entry in morphWatch)
            {
                PlayerControl player = entry.Key;
                MorphWatchState state = entry.Value;
                if (player == null)
                {
                    (finished ??= new List<PlayerControl>()).Add(player);
                    continue;
                }

                PlayerAnimations anims = player.MyPhysics != null ? player.MyPhysics.Animations : null;
                SpriteAnim anim = anims != null ? anims.Animator : null;
                AnimationClip clip = seekerTransformClip;
                if (anim == null || clip == null)
                {
                    Logger<PropHuntPlugin>.Info("Morph watch abort for player " + player.PlayerId
                        + " (anim=" + (anim != null) + " clip=" + (clip != null) + ")");
                    (finished ??= new List<PlayerControl>()).Add(player);
                    continue;
                }

                AnimationClip current = anim.GetCurrentAnimation();
                bool morphCurrent = current != null && current.name == clip.name;

                if (state.HandedBack)
                {
                    if (Time.time >= state.End)
                    {
                        (finished ??= new List<PlayerControl>()).Add(player);
                        continue;
                    }
                    if (Time.time < state.Next) continue;
                    state.Next = Time.time + 0.4f;

                    if (anims.IsPlayingRunAnimation())
                    {
                        if (state.Attempts > 0)
                        {
                            Logger<PropHuntPlugin>.Info("Post-restore run animation recovered for player " + player.PlayerId);
                        }
                        (finished ??= new List<PlayerControl>()).Add(player);
                        continue;
                    }

                    Vector3 pos = player.transform.position;
                    if ((pos - state.LastPos).sqrMagnitude > 0.01f)
                    {
                        if (state.Attempts < 3)
                        {
                            state.Attempts++;
                            Logger<PropHuntPlugin>.Info("Post-restore player " + player.PlayerId
                                + " moving but still on idle - forcing run, attempt " + state.Attempts
                                + " (someAnim=" + anims.IsPlayingSomeAnimation()
                                + " cur=" + (current != null ? current.name : "null") + ")");
                            anims.PlayRunAnimation();
                        }
                        else
                        {
                            Logger<PropHuntPlugin>.Info("Post-restore player " + player.PlayerId
                                + " still sliding after 3 forced runs (someAnim="
                                + anims.IsPlayingSomeAnimation()
                                + " cur=" + (current != null ? current.name : "null") + ")");
                            (finished ??= new List<PlayerControl>()).Add(player);
                        }
                        state.LastPos = pos;
                        continue;
                    }
                    state.LastPos = pos;
                    continue;
                }

                if (Time.time < state.End)
                {
                    if (Time.time < state.Next) continue;
                    state.Next = Time.time + 0.3f;
                    if (morphCurrent) continue;

                    if (state.Attempts >= 6)
                    {
                        // Out of retries - move on to handing the animator back.
                        state.End = Time.time;
                        continue;
                    }

                    state.Attempts++;
                    float morphJump = SeekerMorphJumpTime(clip);
                    float resumeAt = Mathf.Min(morphJump + (Time.time - state.MorphStartedAt),
                        Mathf.Max(morphJump, clip.length - 0.1f));
                    Logger<PropHuntPlugin>.Info("Morph interrupted (cur=" + (current != null ? current.name : "null")
                        + "), replay " + state.Attempts + " for player " + player.PlayerId
                        + " resume " + resumeAt.ToString("0.00") + "s");
                    bypassSeekerAnimStretch = true;
                    try
                    {
                        anim.Play(clip, 1f);
                        anim.SetTime(resumeAt);
                        // Watch window covers whatever is left of the segment.
                        state.End = Time.time + Mathf.Max(0.3f, clip.length - resumeAt);
                        state.Next = Time.time + 0.3f;
                    }
                    finally
                    {
                        bypassSeekerAnimStretch = false;
                    }
                    continue;
                }

                bool timedOut = Time.time >= state.End + 8f;
                if (morphCurrent && anim.Playing && !timedOut)
                {
                    // Still running - keep waiting for it to finish.
                    if (Time.time >= state.Next) state.Next = Time.time + 0.3f;
                    continue;
                }

                if (morphCurrent)
                {
                    RestoreIdle(player, timedOut ? "timeout" : "finished");
                }
                else
                {
                    Logger<PropHuntPlugin>.Info("Morph ended for player " + player.PlayerId
                        + " (cur=" + (current != null ? current.name : "null") + ")");
                }
                // Linger for a moment in phase 3 in case the run transition was
                // missed while the morph held the animator.
                state.HandedBack = true;
                state.Attempts = 0;
                state.End = Time.time + 2.5f;
                state.Next = Time.time + 0.4f;
                state.LastPos = player.transform.position;
            }
            if (finished != null)
            {
                foreach (PlayerControl player in finished)
                {
                    morphWatch.Remove(player);
                    if (player != null) morphFinishedPlayers.Add(player);
                }
            }
        }

        private static void RestoreIdle(PlayerControl player, string reason)
        {
            try
            {
                PlayerAnimations anims = player.MyPhysics != null ? player.MyPhysics.Animations : null;
                if (anims == null) return;
                anims.PlayIdleAnimation();
                SpriteAnim after = anims.Animator;
                Logger<PropHuntPlugin>.Info("Morph over (" + reason + "), idle restored for player " + player.PlayerId
                    + " (now=" + (after != null && after.GetCurrentAnimation() != null ? after.GetCurrentAnimation().name : "null")
                    + " someAnim=" + anims.IsPlayingSomeAnimation()
                    + " runAnim=" + anims.IsPlayingRunAnimation() + ")");
            }
            catch (System.Exception e)
            {
                Logger<PropHuntPlugin>.Warning("RestoreIdle failed: " + e);
            }
        }

        private static AnimationClip FindSeekerTransformClip()
        {
            try
            {
                AnimationClip best = null;
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<AnimationClip>()))
                {
                    AnimationClip clip = obj as AnimationClip;
                    if (clip == null || string.IsNullOrEmpty(clip.name) || !clip.name.StartsWith("HnSSeekerSpawn")) continue;
                    if (clip.name == "HnSSeekerSpawn") return clip;
                    if (best == null || clip.name.Length < best.name.Length) best = clip;
                }
                if (best != null)
                {
                    Logger<PropHuntPlugin>.Info("Found seeker transform clip by search: " + best.name);
                }
                return best;
            }
            catch (System.Exception e)
            {
                Logger<PropHuntPlugin>.Warning("FindSeekerTransformClip failed: " + e);
                return null;
            }
        }

        [HarmonyPatch(typeof(LogicHnSDangerLevel), nameof(LogicHnSDangerLevel.FixedUpdate))]
        [HarmonyPrefix]
        public static bool DangerLevelSeekerGuardPatch(LogicHnSDangerLevel __instance)
        {
            if (!PropHuntPlugin.isPropHunt || !PropHuntPlugin.infectionMode) return true;
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null || !local.Data.Role.IsImpostor) return true;

            __instance.dangerLevel1 = 0f;
            __instance.dangerLevel2 = 0f;
            if (__instance.dangerMeter != null)
            {
                __instance.dangerMeter.SetDangerValue(0f, 0f);
                __instance.dangerMeter.gameObject.SetActive(false);
            }

            LogicHnSMusic music = __instance.hnsManager != null ? __instance.hnsManager.LogicMusic : null;
            if (music != null)
            {
                music.SetMusicValues(0f, 0f);
                music.dangerLevel1Volume = 0f;
                music.dangerLevel2Volume = 0f;
                if (music.dangerLevel1Source != null) music.dangerLevel1Source.volume = 0f;
                if (music.dangerLevel2Source != null) music.dangerLevel2Source.volume = 0f;
            }
            return false;
        }

        [HarmonyPatch(typeof(SpriteAnim), nameof(SpriteAnim.Play))]
        [HarmonyPrefix]
        public static void SpriteAnimPlayPatch(AnimationClip anim, ref float speed)
        {
            if (!PropHuntPlugin.isPropHunt || anim == null || !anim.name.StartsWith("HnSSeekerSpawn")) return;
            if (seekerTransformClip == null)
            {
                seekerTransformClip = anim;
                Logger<PropHuntPlugin>.Info("Captured seeker transform clip " + anim.name);
            }
            if (bypassSeekerAnimStretch) return;
            speed = anim.length / Mathf.Max(1f, PropHuntPlugin.seekerWaitTime);
            Logger<PropHuntPlugin>.Info("Intro seek anim " + anim.name + ": " + anim.length.ToString("0.00") + "s -> speed " + speed.ToString("0.00"));
        }

        [HarmonyPatch(typeof(LogicGameFlowHnS), nameof(LogicGameFlowHnS.SeekerAdminMapEnabled))]
        [HarmonyPostfix]
        static void SeekerAdminMapEnabledPatch(LogicGameFlowHnS __instance, PlayerControl player, ref bool __result)
        {
            if (PropHuntPlugin.isPropHunt && !__instance.hideAndSeekManager.LogicOptionsHnS.GetSeekerFinalMap())
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
        [HarmonyPostfix]
        public static void KillButtonHighlightPatch(ActionButton __instance)
        {
            if (PropHuntPlugin.isPropHunt)
            {
                __instance.SetEnabled();
            }
        }

        [HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.IsValidTarget))]
        [HarmonyPrefix]
        public static bool ValidKillTargetPatch(ImpostorRole __instance, ref bool __result, NetworkedPlayerInfo target)
        {
            if (PropHuntPlugin.isPropHunt)
            {
                __result = !(target == null) && !target.Disconnected && !target.IsDead && target.PlayerId != __instance.Player.PlayerId && !(target.Role == null) && !(target.Object == null) && !target.Object.inVent && !target.Object.inMovingPlat && target.Role.CanBeKilled;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
        [HarmonyPrefix]
        public static void KillButtonClickPatch(KillButton __instance)
        {
            if (PropHuntPlugin.isPropHunt && __instance.currentTarget == null && !__instance.isCoolingDown && !PlayerControl.LocalPlayer.Data.IsDead && !PlayerControl.LocalPlayer.inVent)
            {
                RPCHandler.RPCFailedKill(PlayerControl.LocalPlayer);
                PlayerControl.LocalPlayer.SetKillTimer(3f);
            }
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.CheckClick))]
        [HarmonyPrefix]
        static bool KillButtonCheckClick(PlayerControl target)
        {
            return !PropHuntPlugin.isPropHunt;
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void MinPlayerPatch(GameStartManager __instance)
        {
            __instance.MinPlayers = PropHuntPlugin.isPropHunt ? 2 : 4;
        }

        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
        [HarmonyPostfix]
        public static void PreventZeroImpPatch(ref int __result)
        {
            if (__result <= 0)
            {
                __result = 1;
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
        [HarmonyPostfix]
        public static void IntroCuscenePatch(IntroCutscene __instance)
        {
            ShadowCollab shadowCollab = Object.FindObjectOfType<ShadowCollab>();
            if (PropHuntPlugin.isPropHunt)
            {
                transformedPlayers.Clear();
                morphFinishedPlayers.Clear();
                morphWatch.Clear();
                if (AprilFoolsMode.ShouldHorseAround())
                {
                    seekerTransformClip = __instance.HnSSeekerSpawnHorseInGameAnim;
                }
                else if (AprilFoolsMode.ShouldLongAround())
                {
                    seekerTransformClip = __instance.HnSSeekerSpawnLongInGameAnim;
                }
                else
                {
                    seekerTransformClip = __instance.HnSSeekerSpawnAnim;
                }
                Logger<PropHuntPlugin>.Info("Intro cached seeker clip: "
                    + (seekerTransformClip != null ? seekerTransformClip.name : "NULL"));
                
                if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
                {
                    shadowCollab.ShadowQuad.material.color = new Color(0, 0, 0, 1);
                    shadowCollab.ShadowQuad.gameObject.SetActive(true);
                }
                else
                {
                    shadowCollab.ShadowQuad.gameObject.SetActive(false);
                }

                DestroyableSingleton<HudManager>.Instance.Chat.SetVisible(true);
                DestroyableSingleton<HudManager>.Instance.MatchInfoButton.gameObject.SetActive(false);
            }
            else
            {
                shadowCollab.ShadowQuad.gameObject.SetActive(true);
                shadowCollab.ShadowQuad.material.color = new Color(0.2745f, 0.2745f, 0.2745f, 1);
            }
        }

        private static bool TryGetAliveProp(PlayerControl player, out SpriteRenderer renderer)
        {
            return PropManager.playerToProp.TryGetValue(player, out renderer) && renderer != null;
        }
    }
}