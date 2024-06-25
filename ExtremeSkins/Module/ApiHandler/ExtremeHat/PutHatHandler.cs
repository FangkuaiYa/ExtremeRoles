﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using ExtremeRoles.Module.Interface;
using ExtremeRoles.Performance;
using ExtremeSkins.Core.API;
using ExtremeSkins.Core.ExtremeHats;
using ExtremeSkins.SkinManager;
using ExtremeSkins.Helper;

namespace ExtremeSkins.Module.ApiHandler.ExtremeHat;

#if WITHHAT

public sealed class PutHatHandler : IRequestHandler
{
	public Action<HttpListenerContext> Request => this.requestAction;

	private void requestAction(HttpListenerContext context)
	{
		var response = context.Response;
		NewCosmicData newHat = IRequestHandler.DeserializeJson<NewCosmicData>(context.Request);

		JsonSerializerOptions options = new()
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
		};
		using var jsonStream = new StreamReader(newHat.GetInfoJsonPath());
		HatInfo? info = JsonSerializer.Deserialize<HatInfo>(jsonStream.ReadToEnd(), options);
		var hatMng = FastDestroyableSingleton<HatManager>.Instance;

		if (info == null || hatMng == null)
		{
			IRequestHandler.SetStatusNG(response);
			response.Abort();
			return;
		}

		string folderPath = newHat.GetSkinFolderPath();

		CustomHat customHat = info.Animation == null ?
			new CustomHat(folderPath, info) : new AnimationHat(folderPath, info);

		Translation.AddTransData(newHat.AutherName, newHat.TransedAutherName);
		Translation.AddTransData(newHat.SkinName, newHat.TransedSkinName);

		string id = customHat.Id;

		if (!ExtremeHatManager.HatData.TryGetValue(id, out var hat))
		{
			IRequestHandler.SetStatusNG(response);
			response.Abort();
			return;
		}

		bool hasReloadHat =
			CachedPlayerControl.LocalPlayer != null &&
			CachedPlayerControl.LocalPlayer.PlayerControl.cosmetics.hat.Hat.ProductId == id;

		if (hasReloadHat)
		{
			CachedPlayerControl.LocalPlayer!.PlayerControl.RpcSetHat(HatData.EmptyId);
		}

		ExtremeHatManager.HatData[id] = customHat;

		List<HatData> hatData = hatMng.allHats.ToList();
		hatData.RemoveAll(x => x.ProductId == id);
		hatData.Add(customHat.Data);
		hatMng.allHats = hatData.ToArray();

		ExtremeSkinsPlugin.Logger.LogInfo($"Hat Reloaded :\n{customHat}");
		if (hasReloadHat)
		{
			CachedPlayerControl.LocalPlayer!.PlayerControl.RpcSetHat(id);
		}

		IRequestHandler.SetStatusOK(response);
		response.Close();
	}
}
#endif