﻿using System;
using System.Collections.Generic;

using UnityEngine;
using TMPro;

using ExtremeRoles.Helper;
using ExtremeRoles.Module;
using ExtremeRoles.Module.Ability;
using ExtremeRoles.Module.Ability.Behavior;
using ExtremeRoles.Module.Ability.AutoActivator;
using ExtremeRoles.Module.CustomOption.Factory;
using ExtremeRoles.Module.SystemType.Roles;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.Resources;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;
using ExtremeRoles.GameMode;

#nullable enable

namespace ExtremeRoles.Roles.Solo.Neutral;

public sealed class Tucker : SingleRoleBase, IRoleAbility, IRoleSpecialReset
{
	private sealed record RemoveInfo(int Target, Vector2 StartPos);

	public enum Option
	{
		Range,
		ShadowTimer,
		ShadowOffset,
		RemoveShadowTime,
		KillCoolReduceOnRemoveShadow,
		IsReduceInitKillCoolOnRemove,
		ChimeraCanUseVent,
		ChimeraReviveTime,
		ChimeraDeathKillCoolOffset,
		TuckerDeathKillCoolOffset,
	}

	public ExtremeAbilityButton? Button
	{
		get => this.internalButton;
		set
		{
			if (value is not ExtremeMultiModalAbilityButton button)
			{
				throw new ArgumentException("This role using multimodal ability");
			}
			this.internalButton = button;
		}
	}
	private ExtremeMultiModalAbilityButton? internalButton;

	private float range;

	private Chimera.Option? option;
	private TuckerShadowSystem? system;
	private CountBehavior? createBehavior;

	private byte target;
	private int targetShadowId;
	private RemoveInfo? removeInfo;

	private HashSet<byte> chimera = new HashSet<byte>();

	public Tucker() : base(
		ExtremeRoleId.Tucker,
		ExtremeRoleType.Neutral,
		ExtremeRoleId.Tucker.ToString(),
		ColorPalette.GamblerYellowGold,
		false, false, false, false)
	{ }

	public static void TargetToChimera(byte rolePlayerId, byte targetPlayerId)
	{
		PlayerControl targetPlayer = Player.GetPlayerControlById(targetPlayerId);
		PlayerControl rolePlayer = Player.GetPlayerControlById(rolePlayerId);
		if (rolePlayer == null ||
			targetPlayer == null ||
			!ExtremeRoleManager.TryGetSafeCastedRole<Tucker>(rolePlayerId, out var tucker) ||
			tucker.option is null)
		{
			return;
		}
		IRoleSpecialReset.ResetRole(targetPlayerId);

		var chimera = new Chimera(rolePlayer.Data, tucker.option);
		ExtremeRoleManager.SetNewRole(targetPlayerId, chimera);
		chimera.SetControlId(tucker.GameControlId);
		IRoleSpecialReset.ResetLover(targetPlayerId);

		var local = PlayerControl.LocalPlayer;
		if (local != null &&
			rolePlayerId == local.PlayerId)
		{
			tucker.chimera.Add(targetPlayerId);
		}

		if (AmongUsClient.Instance.AmHost &&
			tucker.system is not null)
		{
			tucker.system.Enable(rolePlayerId);
		}
	}

	public void CreateAbility()
	{
		var loader = this.Loader;

		float coolTime = loader.GetValue<RoleAbilityCommonOption, float>(
			RoleAbilityCommonOption.AbilityCoolTime);

		var img = UnityObjectLoader.LoadSpriteFromResources(ObjectPath.TestButton);

		this.createBehavior = new CountBehavior(
			"createChimera", img,
			isCreateChimera,
			createChimera);
		this.createBehavior.SetCoolTime(coolTime);
		this.createBehavior.SetAbilityCount(
			loader.GetValue<RoleAbilityCommonOption, int>(
				RoleAbilityCommonOption.AbilityCount));

		var summonAbility = new ReusableActivatingBehavior(
			"removeShadow", img,
			isRemoveShadow,
			startRemove,
			isRemoving,
			remove,
			() => { });
		summonAbility.SetCoolTime(coolTime);
		summonAbility.ActiveTime = loader.GetValue<Option, float>(Option.RemoveShadowTime);

		this.Button = new ExtremeMultiModalAbilityButton(
			new RoleButtonActivator(),
			KeyCode.F,
			this.createBehavior,
			summonAbility);
	}

	public void ResetOnMeetingEnd(NetworkedPlayerInfo? exiledPlayer = null)
	{ }

	public void ResetOnMeetingStart()
	{ }

	public void OnResetChimera(byte chimeraId, float killCoolTime)
	{
		this.chimera.Remove(chimeraId);

		if (this.chimera.Count == 0 &&
			this.createBehavior == null)
		{
			this.CanKill = true;
			this.HasOtherKillCool = false;
			this.KillCoolTime = killCoolTime;
		}
	}

	public override Color GetTargetRoleSeeColor(SingleRoleBase targetRole, byte targetPlayerId)
	{
		if (targetRole.Id is ExtremeRoleId.Chimera &&
			this.IsSameControlId(targetRole) &&
			this.chimera.Contains(targetPlayerId))
		{
			return this.NameColor;
		}
		return base.GetTargetRoleSeeColor(targetRole, targetPlayerId);
	}

	public override bool IsSameTeam(SingleRoleBase targetRole)
	{
		if (this.isSameTuckerTeam(targetRole))
		{
			if (ExtremeGameModeManager.Instance.ShipOption.IsSameNeutralSameWin)
			{
				return true;
			}
			else
			{
				return this.IsSameControlId(targetRole);
			}
		}
		else
		{
			return base.IsSameTeam(targetRole);
		}
	}

	protected override void CreateSpecificOption(AutoParentSetOptionCategoryFactory factory)
	{
		IRoleAbility.CreateAbilityCountOption(factory, 1, 10);

		factory.CreateFloatOption(
			Option.Range,
			0.75f, 0.1f, 1.2f, 0.1f);

		factory.CreateFloatOption(
			Option.ShadowTimer,
			15.0f, 0.5f, 60.0f, 0.1f,
			format: OptionUnit.Second);
		factory.CreateFloatOption(
			Option.ShadowOffset,
			0.5f, 0.0f, 2.5f, 0.1f);
		factory.CreateFloatOption(
			Option.RemoveShadowTime,
			3.0f, 0.1f, 15.0f, 0.1f,
			format: OptionUnit.Second);
		factory.CreateFloatOption(
			Option.KillCoolReduceOnRemoveShadow,
			2.5f, 0.1f, 30.0f, 0.1f,
			format: OptionUnit.Second);
		factory.CreateBoolOption(
			Option.IsReduceInitKillCoolOnRemove, false);

		CreateKillerOption(factory, ignorePrefix: false);

		factory.CreateBoolOption(
			Option.ChimeraCanUseVent, false);
		factory.CreateFloatOption(
			Option.ChimeraReviveTime,
			5.0f, 4.0f, 10.0f, 0.1f,
			format: OptionUnit.Second);
		factory.CreateFloatOption(
			Option.ChimeraDeathKillCoolOffset,
			2.5f, -30.0f, 30.0f, 0.1f,
			format: OptionUnit.Second);
		factory.CreateFloatOption(
			Option.TuckerDeathKillCoolOffset,
			2.5f, -30.0f, 30.0f, 0.1f,
			format: OptionUnit.Second);
	}

	protected override void RoleSpecificInit()
	{
		var loader = this.Loader;

		float killCool = loader.GetValue<KillerCommonOption, bool>(KillerCommonOption.HasOtherKillCool) ?
			loader.GetValue<KillerCommonOption, float>(KillerCommonOption.KillCoolDown) :
			GameManager.Instance.LogicOptions.GetKillCooldown();

		this.option = new Chimera.Option(
			killCool,
			loader.GetValue<KillerCommonOption, bool>(KillerCommonOption.HasOtherKillRange),
			loader.GetValue<KillerCommonOption, int>(KillerCommonOption.KillRange),
			loader.GetValue<Option, float>(Option.TuckerDeathKillCoolOffset),
			loader.GetValue<Option, float>(Option.ChimeraDeathKillCoolOffset),
			loader.GetValue<Option, float>(Option.ChimeraReviveTime),
			loader.GetValue<Option, bool>(Option.ChimeraCanUseVent));

		this.system = ExtremeSystemTypeManager.Instance.CreateOrGet(
			TuckerShadowSystem.Type, () => new TuckerShadowSystem(
				loader.GetValue<Option, float>(Option.ShadowOffset),
				loader.GetValue<Option, float>(Option.ShadowTimer),
				loader.GetValue<Option, float>(Option.KillCoolReduceOnRemoveShadow),
				loader.GetValue<Option, bool>(Option.IsReduceInitKillCoolOnRemove)));

		this.range = loader.GetValue<Option, float>(Option.Range);

		this.removeInfo = null;
		this.chimera = new HashSet<byte>();
	}

	private bool isCreateChimera()
	{
		this.target = byte.MaxValue;

		var targetPlayer = Player.GetClosestPlayerInRange(
			PlayerControl.LocalPlayer,
			this, this.range);
		if (targetPlayer == null)
		{
			return false;
		}
		this.target = targetPlayer.PlayerId;

		return IRoleAbility.IsCommonUse();
	}

	private bool createChimera()
	{
		var local = PlayerControl.LocalPlayer;
		if (this.createBehavior is null ||
			this.target == byte.MaxValue ||
			local == null)
		{
			return false;
		}

		byte rolePlayerId = local.PlayerId;
		using (var caller = RPCOperator.CreateCaller(
			RPCOperator.Command.ReplaceRole))
		{
			caller.WriteByte(rolePlayerId);
			caller.WriteByte(this.target);
			caller.WriteByte(
				(byte)ExtremeRoleManager.ReplaceOperation.ForceRelaceToChimera);
		}
		TargetToChimera(rolePlayerId, this.target);

		this.target = byte.MaxValue;

		if (this.createBehavior.AbilityCount <= 1 &&
			this.internalButton is not null)
		{
			this.internalButton.Remove(this.createBehavior);
			this.createBehavior = null;
		}

		return true;
	}

	private bool isRemoveShadow()
	{
		this.targetShadowId = int.MaxValue;
		var local = PlayerControl.LocalPlayer;

		if (local == null ||
			this.system is null ||
			!this.system.TryGetClosedShadowId(local, this.range, out this.targetShadowId))
		{
			return false;
		}
		return IRoleAbility.IsCommonUse();
	}

	private bool startRemove()
	{
		this.removeInfo = null;
		var local = PlayerControl.LocalPlayer;

		if (local == null)
		{
			return false;
		}
		this.removeInfo = new RemoveInfo(this.target, local.GetTruePosition());
		return false;
	}

	private bool isRemoving()
	{
		var local = PlayerControl.LocalPlayer;
		if (this.removeInfo is null ||
			local == null)
		{
			return false;
		}
		return local.GetTruePosition() == this.removeInfo.StartPos;
	}

	private void remove()
	{
		var local = PlayerControl.LocalPlayer;
		if (this.removeInfo is null ||
			local == null)
		{
			return;
		}

		ExtremeSystemTypeManager.RpcUpdateSystem(
			TuckerShadowSystem.Type, x =>
			{
				x.Write((byte)TuckerShadowSystem.Ops.Remove);
				x.Write(local.PlayerId);
				x.WritePacked(this.removeInfo.Target);
			});

		this.removeInfo = null;

	}


	public void AllReset(PlayerControl rolePlayer)
	{
		if (this.system != null)
		{
			this.system.Disable(rolePlayer.PlayerId);
		}

		// Tuckerが消えるので関係性を解除
		var local = PlayerControl.LocalPlayer;
		if (local == null)
		{
			return;
		}
		byte localPlayerId = local.PlayerId;
		foreach (byte chimera in this.chimera)
		{
			if (chimera != localPlayerId ||
				!ExtremeRoleManager.TryGetSafeCastedLocalRole<Chimera>(out var role))
			{
				continue;
			}
			role.RemoveTucker();
		}
	}

	private bool isSameTuckerTeam(SingleRoleBase targetRole)
	{
		return ((targetRole.Id == this.Id) || (targetRole.Id == ExtremeRoleId.Chimera));
	}
}

public sealed class Chimera : SingleRoleBase, IRoleUpdate, IRoleSpecialReset, IRoleHasParent
{
	public sealed record Option(
		float KillCool,
		bool HasOtherRange,
		int Range,
		float TukerKillCoolOffset,
		float RevieKillCoolOffset,
		float ResurrectTime,
		bool Vent);

	private NetworkedPlayerInfo? tuckerPlayer;
	private readonly float reviveKillCoolOffset;
	private readonly float resurrectTime;
	private readonly float tuckerDeathKillCoolOffset;
	private readonly float initCoolTime;

	private TextMeshPro? resurrectText;
	private float resurrectTimer;
	private bool isReviveNow;
	private bool isTuckerDead;

	public byte Parent { get; }

	public Chimera(
		NetworkedPlayerInfo tuckerPlayer,
		Option option) : base(
		ExtremeRoleId.Chimera,
		ExtremeRoleType.Neutral,
		ExtremeRoleId.Chimera.ToString(),
		ColorPalette.GamblerYellowGold,
		true, false, option.Vent, false)
	{
		this.Parent = tuckerPlayer.PlayerId;
		this.tuckerPlayer = tuckerPlayer;
		this.reviveKillCoolOffset = option.RevieKillCoolOffset;
		this.tuckerDeathKillCoolOffset = option.TukerKillCoolOffset;
		this.resurrectTime = option.ResurrectTime;
		this.resurrectTimer = this.resurrectTime;

		this.HasOtherKillCool = true;
		this.initCoolTime = option.KillCool;
		this.KillCoolTime = this.initCoolTime;
		this.isTuckerDead = tuckerPlayer.IsDead;

		this.isReviveNow = false;
	}

	protected override void CreateSpecificOption(AutoParentSetOptionCategoryFactory factory)
	{
		throw new Exception("Don't call this class method!!");
	}

	protected override void RoleSpecificInit()
	{
		throw new Exception("Don't call this class method!!");
	}

	public void RemoveTucker()
	{
		this.tuckerPlayer = null;
	}

	public void OnRemoveShadow(byte tuckerPlayerId,
		float reduceTime, bool isReduceInitKillCool)
	{
		if (this.tuckerPlayer == null ||
			tuckerPlayerId != this.tuckerPlayer.PlayerId)
		{
			return;
		}

		float min = isReduceInitKillCool ? 0.01f : this.initCoolTime;
		updateKillCoolTime(-reduceTime, min);
	}

	public void Update(PlayerControl rolePlayer)
	{
		if (CachedShipStatus.Instance == null ||
			GameData.Instance == null ||
			!CachedShipStatus.Instance.enabled ||
			rolePlayer == null ||
			rolePlayer.Data == null ||
			MeetingHud.Instance != null ||
			ExileController.Instance != null)
		{
			if (this.resurrectText != null)
			{
				this.resurrectText.gameObject.SetActive(false);
			}
			return;
		}

		if (!this.isTuckerDead)
		{
			this.isTuckerDead = this.tuckerPlayer == null || this.tuckerPlayer.IsDead;
			if (this.isTuckerDead)
			{
				updateKillCoolTime(this.KillCoolTime, this.tuckerDeathKillCoolOffset);
			}
		}


		// 復活処理
		if (this.tuckerPlayer == null ||
			!rolePlayer.Data.IsDead ||
			this.tuckerPlayer.Disconnected ||
			this.tuckerPlayer.IsDead ||
			this.isReviveNow)
		{
			if (this.resurrectText != null)
			{
				this.resurrectText.gameObject.SetActive(false);
			}
			return;
		}


		if (this.resurrectText == null)
		{
			this.resurrectText = UnityEngine.Object.Instantiate(
				FastDestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText,
				Camera.main.transform, false);
			this.resurrectText.transform.localPosition = new Vector3(0.0f, 0.0f, -250.0f);
			this.resurrectText.enableWordWrapping = false;
		}

		this.resurrectText.gameObject.SetActive(true);
		this.resurrectTimer -= Time.deltaTime;
		this.resurrectText.text = string.Format(
			Translation.GetString("resurrectText"),
			Mathf.CeilToInt(this.resurrectTimer));

		if (this.resurrectTimer <= 0.0f)
		{
			revive(rolePlayer);
		}
	}

	public override Color GetTargetRoleSeeColor(SingleRoleBase targetRole, byte targetPlayerId)
	{
		if (this.tuckerPlayer != null &&
			targetRole.Id is ExtremeRoleId.Tucker &&
			targetPlayerId == this.tuckerPlayer.PlayerId)
		{
			return this.NameColor;
		}
		return base.GetTargetRoleSeeColor(targetRole, targetPlayerId);
	}

	public override bool IsSameTeam(SingleRoleBase targetRole)
	{
		if (this.isSameChimeraTeam(targetRole))
		{
			if (ExtremeGameModeManager.Instance.ShipOption.IsSameNeutralSameWin)
			{
				return true;
			}
			else
			{
				return this.IsSameControlId(targetRole);
			}
		}
		else
		{
			return base.IsSameTeam(targetRole);
		}
	}

	public override bool IsBlockShowMeetingRoleInfo() => this.infoBlock();

	public override bool IsBlockShowPlayingRoleInfo() => this.infoBlock();

	private bool infoBlock()
		=> !this.isTuckerDead;

	private void revive(PlayerControl rolePlayer)
	{
		if (rolePlayer == null || this.tuckerPlayer == null) { return; }
		this.isReviveNow = true;
		this.resurrectTimer = this.resurrectTime;

		byte playerId = rolePlayer.PlayerId;

		updateKillCoolTime(this.KillCoolTime, this.reviveKillCoolOffset);
		Player.RpcUncheckRevive(playerId);

		if (rolePlayer.Data == null ||
			rolePlayer.Data.IsDead ||
			rolePlayer.Data.Disconnected) { return; }

		List<Vector2> randomPos = new List<Vector2>();
		Map.AddSpawnPoint(randomPos, playerId);
		Player.RpcUncheckSnap(playerId, randomPos[
			RandomGenerator.Instance.Next(randomPos.Count)]);

		rolePlayer.killTimer = this.KillCoolTime;

		FastDestroyableSingleton<HudManager>.Instance.Chat.chatBubblePool.ReclaimAll();
		if (this.resurrectText != null)
		{
			this.resurrectText.gameObject.SetActive(false);
		}

		ExtremeSystemTypeManager.RpcUpdateSystem(
			TuckerShadowSystem.Type, x =>
			{
				x.Write((byte)TuckerShadowSystem.Ops.ChimeraRevive);
				x.Write(this.tuckerPlayer.PlayerId);
			});

		this.isReviveNow = false;
	}

	public void AllReset(PlayerControl rolePlayer)
	{
		if (PlayerControl.LocalPlayer == null ||
			rolePlayer.PlayerId != PlayerControl.LocalPlayer.PlayerId)
		{
			return;
		}
		// 累積していたキルクールのオフセットを無効化しておく
		this.KillCoolTime = this.initCoolTime;
	}

	private void updateKillCoolTime(float offset, float min=0.1f)
	{
		this.KillCoolTime = Mathf.Clamp(this.KillCoolTime + offset, min, float.MaxValue);
	}

	private bool isSameChimeraTeam(SingleRoleBase targetRole)
	{
		return ((targetRole.Id == this.Id) || (targetRole.Id == ExtremeRoleId.Tucker));
	}

	public void RemoveParent(byte rolePlayerId)
	{
		if (!ExtremeRoleManager.TryGetSafeCastedRole<Tucker>(this.Parent, out var tucker))
		{
			return;
		}
		tucker.OnResetChimera(rolePlayerId, this.KillCoolTime);
	}
}