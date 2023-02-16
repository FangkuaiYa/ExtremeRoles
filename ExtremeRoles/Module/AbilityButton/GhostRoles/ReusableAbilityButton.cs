﻿using System;
using UnityEngine;

using ExtremeRoles.GhostRoles;


namespace ExtremeRoles.Module.AbilityButton.GhostRoles
{

    public sealed class ReusableAbilityButton : GhostRoleAbilityButtonBase
    {

        public ReusableAbilityButton(
            AbilityType abilityType,
            Action<RPCOperator.RpcCaller> ability,
            Func<bool> abilityPreCheck,
            Func<bool> canUse,
            Sprite sprite,
            Action rpcHostCallAbility = null,
            Action abilityCleanUp = null,
            Func<bool> abilityCheck = null,
            KeyCode hotkey = KeyCode.F) : base(
                abilityType,
                ability, abilityPreCheck,
                canUse, sprite,
                rpcHostCallAbility, abilityCleanUp,
                abilityCheck, hotkey)
        { }

        protected override void DoClick()
        {
            if (IsEnable() &&
                Timer <= 0f &&
                this.State == AbilityState.Ready &&
                UseAbility())
            {
                this.SetStatus(
                    this.HasCleanUp() ?
                    AbilityState.Activating :
                    AbilityState.CoolDown);
            }
        }

        protected override bool IsEnable() =>
            CanUse.Invoke() && !IsComSabNow();

        protected override void UpdateAbility()
        { }
    }
}
