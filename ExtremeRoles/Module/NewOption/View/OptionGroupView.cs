﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtremeRoles.Module.NewOption;

public sealed class OptionGroupView(in OptionGroup group)
{
	public int GroupId { get; } = group.Id;
	public string Name { get; } = group.Name;

	public IEnumerable<GameObject> AllObject => obj.Values;

	private readonly IReadOnlyDictionary<int, GameObject> obj = new Dictionary<int, GameObject>();
	private readonly OptionGroup group = group;

	public void Update(in float posOffset)
	{
		foreach (var option in this.group.AllOption)
		{
			if (!this.obj.TryGetValue(option.Id, out var obj))
			{
				continue;
			}
			bool enabled = option.IsActive();

			obj.SetActive(enabled);
		}
	}
}
