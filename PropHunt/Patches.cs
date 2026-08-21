using HarmonyLib;
using UnityEngine;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;

namespace PropHunt
{
	[HarmonyPatch]
	public class Patches
	{
		private static CustomButton disguiseButton;
		private static CustomButton revertButton;
		private static CustomButton movePropButton;
		public static bool isMovingProp = false;

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
					GameObject closest = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
					if (closest != null)
					{
						for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
						{
							if (ShipStatus.Instance.AllConsoles[i] == closest.GetComponent<Console>())
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
					GameObject target = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
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
					if (PropManager.playerToProp.ContainsKey(player) && PropManager.playerToProp[player].sprite != null)
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
					   && PropManager.playerToProp.ContainsKey(PlayerControl.LocalPlayer)
					   && PropManager.playerToProp[PlayerControl.LocalPlayer].sprite != null
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
					   && PropManager.playerToProp.ContainsKey(PlayerControl.LocalPlayer)
					   && PropManager.playerToProp[PlayerControl.LocalPlayer].sprite != null
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

		[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
		[HarmonyPrefix]
		public static bool PlayerInputControlPatch(KeyboardJoystick __instance)
		{
			PlayerControl player = PlayerControl.LocalPlayer;

			if (!PropHuntPlugin.isPropHunt || player.Data.Role.IsImpostor || KeyboardJoystick.player == null
				|| AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
				return true;

			if (isMovingProp && PropManager.playerToProp.ContainsKey(player))
			{
				__instance.del = Vector2.zero;
				Vector2 inputDirection = Vector2.zero;

				if (KeyboardJoystick.player.GetButton(40)) inputDirection.x += 1f;
				if (KeyboardJoystick.player.GetButton(39)) inputDirection.x -= 1f;
				if (KeyboardJoystick.player.GetButton(44)) inputDirection.y += 1f;
				if (KeyboardJoystick.player.GetButton(42)) inputDirection.y -= 1f;

				Transform prop = PropManager.playerToProp[player].transform;
				Vector3 newPosition = new Vector3(
					prop.localPosition.x + inputDirection.x * PropHuntPlugin.propMoveSpeed * Time.deltaTime,
					prop.localPosition.y + inputDirection.y * PropHuntPlugin.propMoveSpeed * Time.deltaTime,
					-3);

				if (Vector2.Distance(Vector2.zero, newPosition) < PropHuntPlugin.maxPropDistance)
				{
					prop.localPosition = newPosition;
				}
				return false;
			}

			return true;
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
		[HarmonyPostfix]
		public static void PlayerControlStartPatch(PlayerControl __instance)
		{
			GameObject propObj = new GameObject("Prop")
			{
				layer = 11
			};
			SpriteRenderer propRenderer = propObj.AddComponent<SpriteRenderer>();
			propObj.transform.SetParent(__instance.transform);
			propObj.transform.localScale = Vector2.one;
			propObj.transform.localPosition = new Vector3(0, 0, -3);
			PropManager.playerToProp.Add(__instance, propRenderer);
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
		[HarmonyPostfix]
		public static void OnExitGame()
		{
			PropManager.playerToProp.Clear();
			isMovingProp = false;
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ResetAnimState))]
		[HarmonyPostfix]
		public static void PlayerPhysicsResetAnimationPatch(PlayerPhysics __instance)
		{
			if (!AmongUsClient.Instance.IsGameStarted || !PropHuntPlugin.isPropHunt || __instance.myPlayer == null)
				return;

			if (__instance.myPlayer.Visible && !__instance.myPlayer.Data.Role.IsImpostor && !__instance.myPlayer.Data.IsDead && PropManager.playerToProp.ContainsKey(__instance.myPlayer) && PropManager.playerToProp[__instance.myPlayer].sprite != null)
			{
				__instance.myPlayer.Visible = false;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
		[HarmonyPostfix]
		public static void OnPlayerDiePatch(PlayerControl __instance)
		{
			if (!PropHuntPlugin.isPropHunt || __instance.Data.Role.IsImpostor) return;

			SpriteRenderer prop = PropManager.playerToProp[__instance];
			if (prop != null)
			{
				prop.gameObject.Destroy();
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
		public static void IntroCuscenePatch()
		{
			ShadowCollab shadowCollab = Object.FindObjectOfType<ShadowCollab>();
			if (PropHuntPlugin.isPropHunt)
			{
				foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers)
				{
					player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, -5);
					player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, -5);
				}

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
				foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers)
				{
					player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, 0);
					player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, 0);
				}

				shadowCollab.ShadowQuad.gameObject.SetActive(true);
				shadowCollab.ShadowQuad.material.color = new Color(0.2745f, 0.2745f, 0.2745f, 1);
			}
		}
	}
}