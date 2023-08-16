﻿using System;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;

using ExtremeRoles.Module.Interface;

namespace ExtremeSkins.Module.ApiHandler;

public sealed class GetStatusHandler : IRequestHandler
{
	private enum CurExSStatus
	{
		Booting,
		OK,
	}

	private readonly record struct ExSStatus(CurExSStatus RowStatus)
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
		public readonly CurExSStatus RowStatus = RowStatus;
		public string Status => RowStatus.ToString();
	}

	public Action<HttpListenerContext> Request => this.requestAction;

	private void requestAction(HttpListenerContext context)
	{
		var response = context.Response;
		IRequestHandler.SetStatusOK(response);
		IRequestHandler.SetContentsType(response);

		var curState = new ExSStatus(Patches.AmongUs.SplashManagerUpdatePatch.IsSkinLoad ? CurExSStatus.Booting : CurExSStatus.OK);

		IRequestHandler.Write(response, curState);
	}
}
