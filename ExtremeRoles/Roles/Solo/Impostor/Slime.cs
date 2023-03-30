﻿using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Hazel;

using ExtremeRoles.Helper;
using ExtremeRoles.Module;
using ExtremeRoles.Resources;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;

namespace ExtremeRoles.Roles.Solo.Impostor;

public sealed class Slime : 
    SingleRoleBase,
    IRoleAbility,
    IRoleSpecialReset,
    IRoleKillAnimationChecker
{
    public enum SlimeRpc : byte
    {
        Morph,
        Reset,
    }

    public ExtremeAbilityButton Button { get; set; }

    public bool IsKillAnimating { get; set; } = false;

    private Console targetConsole;
    private GameObject consoleObj;

    public Slime() : base(
        ExtremeRoleId.Slime,
        ExtremeRoleType.Impostor,
        ExtremeRoleId.Slime.ToString(),
        Palette.ImpostorRed,
        true, false, true, true)
    { }

    public static void Ability(ref MessageReader reader)
    {
        SlimeRpc rpcId = (SlimeRpc)reader.ReadByte();
        byte rolePlayerId = reader.ReadByte();

        var rolePlayer = Player.GetPlayerControlById(rolePlayerId);
        var role = ExtremeRoleManager.GetSafeCastedRole<Slime>(rolePlayerId);
        if (role == null || rolePlayer == null) { return; }
        switch (rpcId)
        {
            case SlimeRpc.Morph:
                int index = reader.ReadPackedInt32();
                setPlayerSpriteToConsole(role, rolePlayer, index);
                break;
            case SlimeRpc.Reset:
                removeMorphConsole(role, rolePlayer);
                break;
            default:
                break;
        }
    }

    private static void setPlayerSpriteToConsole(Slime slime, PlayerControl player, int index)
    {
        Console console = CachedShipStatus.Instance.AllConsoles[index];
        
        if (console is null || console.Image is null) { return; }

        slime.consoleObj = new GameObject("MorphConsole");
        slime.consoleObj.transform.SetParent(player.transform);

        SpriteRenderer rend = slime.consoleObj.AddComponent<SpriteRenderer>();
        rend.sprite = console.Image.sprite;

        Vector3 scale = player.transform.localScale;
        slime.consoleObj.transform.position = player.transform.position;
        slime.consoleObj.transform.localScale =
            console.transform.lossyScale * (1.0f / scale.x);

        setPlayerObjActive(player, false);
    }

    private static void removeMorphConsole(Slime slime, PlayerControl player)
    {
        if (slime.consoleObj is not null)
        {
            Object.Destroy(slime.consoleObj);
        }
        setPlayerObjActive(player, true);
    }
    private static void setPlayerObjActive(PlayerControl player, bool active)
    {
        player.cosmetics.Visible = active;
        player.cosmetics.lockVisible = !active;
    }
    
    public void CreateAbility()
    {
        this.CreateReclickableAbilityButton(
            "carry",
            Loader.CreateSpriteFromResources(
               Path.CarrierCarry),
            checkAbility: IsAbilityActive,
            abilityOff: this.CleanUp);
    }

    public bool IsAbilityActive() => 
        CachedPlayerControl.LocalPlayer.PlayerControl.moveable ||
        this.IsKillAnimating;

    public bool IsAbilityUse()
    {
        PlayerControl localPlayer = CachedPlayerControl.LocalPlayer;
        
        this.targetConsole = Player.GetClosestConsole(
            localPlayer, localPlayer.MaxReportDistance);

        if (this.targetConsole is null) { return false; }

        return
            this.IsCommonUse() &&
            this.targetConsole.Image is not null &&
            GameSystem.IsValidConsole(localPlayer, this.targetConsole);
    }

    public void ResetOnMeetingEnd(GameData.PlayerInfo exiledPlayer = null)
    {
        this.IsKillAnimating = false;
    }

    public void ResetOnMeetingStart()
    {
        this.IsKillAnimating = false;
    }

    public bool UseAbility()
    {
        PlayerControl player = CachedPlayerControl.LocalPlayer;
        for (int i = 0; i < CachedShipStatus.Instance.AllConsoles.Length; ++i)
        {
            Console console = CachedShipStatus.Instance.AllConsoles[i];
            if (console != this.targetConsole) { continue; }

            using (var caller = RPCOperator.CreateCaller(
            RPCOperator.Command.SlimeAbility))
            {
                caller.WriteByte((byte)SlimeRpc.Morph);
                caller.WriteByte(player.PlayerId);
                caller.WritePackedInt(i);
            }
            setPlayerSpriteToConsole(this, player, i);
            this.IsKillAnimating = false;
            return true;
        }
        return false;
    }

    public void CleanUp()
    {
        PlayerControl player = CachedPlayerControl.LocalPlayer;

        using (var caller = RPCOperator.CreateCaller(
            RPCOperator.Command.SlimeAbility))
        {
            caller.WriteByte((byte)SlimeRpc.Reset);
            caller.WriteByte(player.PlayerId);
        }
        removeMorphConsole(this, player);
    }

    public override void RolePlayerKilledAction(
        PlayerControl rolePlayer, PlayerControl killerPlayer)
    {
        removeMorphConsole(this, rolePlayer);
    }

    protected override void CreateSpecificOption(
        IOption parentOps)
    {
        this.CreateCommonAbilityOption(
            parentOps, 30.0f);
    }

    protected override void RoleSpecificInit()
    {
        this.RoleAbilityInit();

        this.IsKillAnimating = false;
    }

    public void AllReset(PlayerControl rolePlayer)
    {
        removeMorphConsole(this, rolePlayer);
    }
}
