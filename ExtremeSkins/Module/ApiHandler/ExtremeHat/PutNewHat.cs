﻿using ExtremeRoles.Module.Interface;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using ExtremeRoles.Performance;
using ExtremeSkins.Core.ExtremeHats;
using ExtremeSkins.SkinManager;

using DataStructure = ExtremeSkins.Core.ExtremeHats.DataStructure;

namespace ExtremeSkins.Module.ApiHandler.ExtremeHat;

public sealed class PutNewHatHandler : IRequestHandler
{
	private readonly record struct NewExHData(string ParentPath, string SkinName)
	{

		[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
		public string FolderPath => DataStructure.GetHatPath(ParentPath, SkinName);

		[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
		public string InfoJsonPath => DataStructure.GetHatInfoPath(ParentPath, SkinName);
	}

	public Action<HttpListenerContext> Request => this.requestAction;

	private void requestAction(HttpListenerContext context)
	{
		var response = context.Response;
		string jsonString = IRequestHandler.GetJsonString(context.Request);
		NewExHData newHat = JsonSerializer.Deserialize<NewExHData>(jsonString);

		using var jsonStream = new StreamReader(newHat.InfoJsonPath);
		JsonSerializerOptions options = new()
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
		};
		HatInfo? info = JsonSerializer.Deserialize<HatInfo>(jsonStream.ReadToEnd(), options);
		var hatMng = FastDestroyableSingleton<HatManager>.Instance;

		if (info == null || hatMng == null)
		{
			IRequestHandler.SetStatusNG(response);
			response.Abort();
			return;
		}
		string folderPath = newHat.FolderPath;
		CustomHat customHat = info.Animation == null ?
			new CustomHat(folderPath, info) : new AnimationHat(folderPath, info);
		if (ExtremeHatManager.HatData.TryAdd(customHat.Id, customHat))
		{
			ExtremeSkinsPlugin.Logger.LogInfo($"Hat Loaded :\n{customHat}");
		}
		else
		{
			IRequestHandler.SetStatusNG(response);
			response.Abort();
			return;
		}
		List<HatData> hatData = hatMng.allHats.ToList();
		hatData.Add(customHat.Data);
		hatMng.allHats = hatData.ToArray();

		IRequestHandler.SetStatusOK(response);
	}
}