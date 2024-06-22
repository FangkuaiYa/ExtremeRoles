﻿using HarmonyLib;

using ExtremeRoles.Module.CustomOption;

namespace ExtremeRoles.Patches;

[HarmonyPatch(typeof(HashRandom), nameof(HashRandom.FastNext))]
public static class HashRandomFastNextPatch
{
    public static bool Prefix(
        ref int __result,
        [HarmonyArgument(0)] int maxInt)
    {
        if (OptionManager.Instance.GetValue<bool>(
            (int)OptionCreator.PresetOptionKey.UseStrongRandomGen))
        {
            __result = RandomGenerator.Instance.Next(maxInt);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(
    typeof(HashRandom),
    nameof(HashRandom.Next),
    [ typeof(int) ])]
public static class HashRandomNextPatch
{
    public static bool Prefix(
        ref int __result,
        [HarmonyArgument(0)] int maxInt)
    {
        if (OptionManager.Instance.GetValue<bool>(
            (int)OptionCreator.PresetOptionKey.UseStrongRandomGen))
        {
            __result = RandomGenerator.Instance.Next(maxInt);
            return false;
        }
        return true;
    }
}
