﻿using System;
using System.Linq;
using System.Net;

using ExtremeRoles.Module.Interface;
using ExtremeSkins.Core.API;
using ExtremeSkins.SkinManager;

namespace ExtremeSkins.Module.ApiHandler.ExtremeNamePlate;

public sealed class GetNamePlateHandler : IRequestHandler
{

	public Action<HttpListenerContext> Request => this.requestAction;

	private void requestAction(HttpListenerContext context)
	{
		var response = context.Response;
		IRequestHandler.SetStatusOK(response);
		IRequestHandler.SetContentsType(response);

		var curData = SkinContainer<CustomNamePlate>.GetValues().Select(
			x => new ExportData(x.Id, x.Name, x.Author));

		IRequestHandler.Write(response, curData);
	}
}