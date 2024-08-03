﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;



using ExtremeRoles.Module.CustomOption.Interfaces;
using ExtremeRoles.Helper;


#nullable enable

namespace ExtremeRoles.Module.CustomOption.Implemented;

public abstract class CustomOptionBase<OutType, SelectionType> :
	IValueOption<OutType>
	where OutType :
		struct, IComparable, IConvertible,
		IComparable<OutType>, IEquatable<OutType>
	where SelectionType :
		notnull, IComparable, IConvertible,
		IComparable<SelectionType>, IEquatable<SelectionType>
{
	public abstract OutType Value { get; }
	public IOptionInfo Info { get; init; }

	public IOptionRelation Relation { get; init; }

	public bool IsEnable => OptionRange.Selection != config.DefaultValue;

	public bool IsActiveAndEnable
	{
		get
		{
			if (Info.IsHidden)
			{
				return false;
			}

			if (Relation is not IOptionChain hasParent)
			{
				return true;
			}
			return hasParent.IsChainEnable;
		}
	}

	public string Title => Tr.GetString(Info.Name);

	public string ValueString
	{
		get
		{
			string? value = OptionRange.Value.ToString();
			if (string.IsNullOrEmpty(value))
			{
				value = "NOT_SUPPORT";
			}
			if (typeof(SelectionType) == typeof(string))
			{
				value = Tr.GetString(value);
			}
			string format = this.Info.Format;
			return string.IsNullOrEmpty(format) ?
				value : Tr.GetString(format, value);
		}
	}

	public int Range => OptionRange.Range;

	public int Selection
	{
		get => OptionRange.Selection;

		set
		{
			OptionRange.Selection = value;

			foreach (var withUdate in withUpdate)
			{
				withUdate.Update(Value);
			}

			var amongUs = AmongUsClient.Instance;
			if (amongUs != null &&
				amongUs.AmHost)
			{
				config.Value = OptionRange.Selection;
			}
		}
	}

	private readonly ConfigBinder config;
	protected IOptionRange<SelectionType> OptionRange;
	private readonly List<IDynamismOption<OutType>> withUpdate = new List<IDynamismOption<OutType>>();

	public CustomOptionBase(
		IOptionInfo info,
		IOptionRange<SelectionType> range,
		IOptionRelation relation,
		SelectionType defaultValue)
	{
		Info = info;
		OptionRange = range;
		Relation = relation;

		int defaultIndex = OptionRange.GetIndex(defaultValue);

		if (relation is IOptionParent parentRelation)
		{
			parentRelation.Parent.Relation.Children.Add(this);
		}
		config = new ConfigBinder(Info.CodeRemovedName, defaultIndex);

		OptionRange.Selection = config.Value;

		ExtremeRolesPlugin.Logger.LogInfo($"---- Create new Option ----\n{this}\n--------");
	}

	public override string ToString()
	{
		var builder = new StringBuilder();
		builder
			.AppendLine(Info.ToString())
			.Append(OptionRange.ToString());
		return builder.ToString();
	}

	public void AddWithUpdate(IDynamismOption<OutType> option)
	{
		withUpdate.Add(option);
		option.Update(Value);
	}

	public void SwitchPreset()
	{
		config.Rebind();
		Selection = config.Value;
	}
}
