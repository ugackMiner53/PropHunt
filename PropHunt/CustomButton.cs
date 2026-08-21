using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PropHunt
{
	public class CustomButton
	{
		public static List<CustomButton> buttons = new List<CustomButton>();
		public ActionButton actionButton;
		public GameObject actionButtonGameObject;
		public SpriteRenderer actionButtonRenderer;
		public Material actionButtonMat;
		public TextMeshPro actionButtonLabelText;
		public Vector3 PositionOffset;
		public float MaxTimer = float.MaxValue;
		public float Timer = 0f;
		public bool HasEffect;
		public bool isEffectActive;
		public float EffectDuration;
		public Sprite Sprite;
		public HudManager hudManager;
		public bool mirror;
		public KeyCode? hotkey;
		public string buttonText;

		private Action OnClick;
		private Action InitialOnClick;
		private Func<bool> HasButton;
		public Func<bool> CouldUse;
		private Action OnMeetingEnds;
		private Action OnEffectEnds;

		private static readonly int Desat = Shader.PropertyToID("_Desat");

		public CustomButton(
			Action onClick,
			Func<bool> hasButton,
			Func<bool> couldUse,
			Action onMeetingEnds,
			Sprite sprite,
			Vector3 positionOffset,
			HudManager hudManager,
			KeyCode? hotkey,
			bool hasEffect,
			float effectDuration,
			Action onEffectEnds,
			bool mirror = false,
			string buttonText = "")
		{
			this.hudManager = hudManager;
			this.OnClick = onClick;
			this.InitialOnClick = onClick;
			this.HasButton = hasButton;
			this.CouldUse = couldUse;
			this.PositionOffset = positionOffset;
			this.OnMeetingEnds = onMeetingEnds;
			this.HasEffect = hasEffect;
			this.EffectDuration = effectDuration;
			this.OnEffectEnds = onEffectEnds;
			this.Sprite = sprite;
			this.mirror = mirror;
			this.hotkey = hotkey;
			this.buttonText = buttonText;
			Timer = 16.2f;

			buttons.Add(this);

			actionButton = UnityEngine.Object.Instantiate(hudManager.KillButton, hudManager.KillButton.transform.parent);
			actionButtonGameObject = actionButton.gameObject;
			actionButtonRenderer = actionButton.graphic;
			actionButtonMat = actionButtonRenderer.material;
			actionButtonLabelText = actionButton.buttonLabelText;

			PassiveButton button = actionButton.GetComponent<PassiveButton>();
			button.OnClick = new Button.ButtonClickedEvent();
			button.OnClick.AddListener((UnityEngine.Events.UnityAction)onClickEvent);

			setActive(false);
		}

		public CustomButton(
			Action onClick,
			Func<bool> hasButton,
			Func<bool> couldUse,
			Action onMeetingEnds,
			Sprite sprite,
			Vector3 positionOffset,
			HudManager hudManager,
			KeyCode? hotkey,
			bool mirror = false,
			string buttonText = "")
			: this(onClick, hasButton, couldUse, onMeetingEnds, sprite, positionOffset, hudManager,
				  hotkey, false, 0f, () => { }, mirror, buttonText)
		{ }

		public void onClickEvent()
		{
			if (Timer >= 0f || !HasButton() || !CouldUse()) return;

			actionButtonRenderer.color = new Color(1f, 1f, 1f, 0.3f);
			OnClick();

			if (HasEffect && !isEffectActive)
			{
				Timer = EffectDuration;
				actionButton.cooldownTimerText.color = new Color(0F, 0.8F, 0F);
				isEffectActive = true;
			}
		}

		public static void HudUpdate()
		{
			buttons.RemoveAll(item => item.actionButton == null);
			foreach (var button in buttons)
			{
				try { button.Update(); }
				catch (NullReferenceException) { }
			}
		}

		public static void MeetingEndedUpdate()
		{
			buttons.RemoveAll(item => item.actionButton == null);
			foreach (var button in buttons)
			{
				try
				{
					button.OnMeetingEnds();
					button.Update();
				}
				catch (NullReferenceException) { }
			}
		}

		public void setActive(bool isActive)
		{
			actionButtonGameObject?.SetActive(isActive);
			actionButtonRenderer.enabled = isActive;
		}

		public void Update()
		{
			var localPlayer = PlayerControl.LocalPlayer;
			if (localPlayer.Data == null || MeetingHud.Instance || ExileController.Instance || !HasButton())
			{
				setActive(false);
				return;
			}
			setActive(hudManager.UseButton.isActiveAndEnabled || hudManager.PetButton.isActiveAndEnabled);

			if (isEffectActive && Timer >= 0f)
				Timer -= Time.deltaTime;
			else if (!localPlayer.inVent && localPlayer.moveable)
				Timer -= Time.deltaTime;

			if (Timer <= 0f && isEffectActive)
			{
				isEffectActive = false;
				actionButton.cooldownTimerText.color = Palette.EnabledColor;
				OnEffectEnds();
			}

			actionButtonRenderer.sprite = Sprite;
			if (!string.IsNullOrEmpty(buttonText))
				actionButton.OverrideText(buttonText);
			actionButtonLabelText.enabled = !string.IsNullOrEmpty(buttonText) || actionButtonRenderer.sprite != null;

			if (hudManager.UseButton != null)
			{
				Vector3 pos = hudManager.UseButton.transform.localPosition;
				if (mirror)
				{
					float aspect = Camera.main.aspect;
					float safeOrthographicSize = CameraSafeArea.GetSafeOrthographicSize(Camera.main);
					float xpos = 0.05f - safeOrthographicSize * aspect * 1.70f;
					pos = new Vector3(xpos, pos.y, pos.z);
				}
				actionButton.transform.localPosition = pos + PositionOffset;
			}

			if (CouldUse())
			{
				actionButtonRenderer.color = Palette.EnabledColor;
				actionButtonLabelText.color = Palette.EnabledColor;
				actionButtonMat.SetFloat(Desat, 0f);
			}
			else
			{
				actionButtonRenderer.color = Palette.DisabledClear;
				actionButtonLabelText.color = Palette.DisabledClear;
				actionButtonMat.SetFloat(Desat, 1f);
			}

			actionButton.SetCoolDown(Timer, isEffectActive ? EffectDuration : MaxTimer);

			if (hotkey.HasValue && Input.GetKeyDown(hotkey.Value))
				onClickEvent();
		}
	}
}
