﻿using System;

using ExtremeRoles.Resources;

using UnityEngine;

namespace ExtremeRoles.Test.Img;

internal abstract class AssetImgLoadRunner
	: TestRunnerBase
{
	protected void LoadFromExR(string asset, string img)
	{
		try
		{
			var sprite = Loader.GetUnityObjectFromExRResources<Sprite>(asset, img);
			Log.LogInfo($"Img Loaded:{asset} | {img}");
			if (sprite == null)
			{
				throw new Exception("Sprite is Null");
			}
		}
		catch (Exception ex)
		{
			Log.LogError(
				$"Img:{asset} | {img} not load   {ex.Message}");
		}
	}
}
