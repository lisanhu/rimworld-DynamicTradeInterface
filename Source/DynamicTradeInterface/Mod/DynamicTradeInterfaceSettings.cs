﻿using DynamicTradeInterface.Attributes;
using DynamicTradeInterface.Defs;
using DynamicTradeInterface.InterfaceComponents.TableBox;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace DynamicTradeInterface.Mod
{
	internal class DynamicTradeInterfaceSettings : ModSettings
	{
		const float DEFAULT_TRADE_WIDTH = 0.75f;
		const float DEFAULT_TRADE_HEIGHT = 0.8f;

		private HashSet<TradeColumnDef> _validColumnDefs;
		private List<TradeColumnDef> _visibleColumns;


		bool _profilingEnabled;
		Dictionary<TradeColumnDef, Queue<long>> _tradeColumnProfilings;
		float _tradeWidthPercentage = DEFAULT_TRADE_WIDTH;
		float _tradeHeightPercentage = DEFAULT_TRADE_HEIGHT;
		bool _excludeUnwillingItems;

		public DynamicTradeInterfaceSettings()
		{
			_validColumnDefs = new HashSet<TradeColumnDef>();
			_visibleColumns = new List<TradeColumnDef>();
			_tradeColumnProfilings = new Dictionary<TradeColumnDef, Queue<long>>();
		}

		internal HashSet<TradeColumnDef> ValidColumns => _validColumnDefs;
		internal List<TradeColumnDef> VisibleColumns => _visibleColumns;

		internal bool ProfilingEnabled
		{
			get => _profilingEnabled;
			set => _profilingEnabled = value;
		}

		internal Dictionary<TradeColumnDef, Queue<long>> TradeColumnProfilings => _tradeColumnProfilings;


		public float TradeWidthPercentage 
		{ 
			get => _tradeWidthPercentage; 
			set => _tradeWidthPercentage = value; 
		}

		public float TradeHeightPercentage 
		{ 
			get => _tradeHeightPercentage; 
			set => _tradeHeightPercentage = value; 
		}

		public bool ExcludeUnwillingItems
		{
			get => _excludeUnwillingItems;
			set => _excludeUnwillingItems = value;
		}


		public override void ExposeData()
		{
			base.ExposeData();


			Scribe_Values.Look(ref _tradeWidthPercentage, nameof(TradeWidthPercentage), DEFAULT_TRADE_WIDTH);
			Scribe_Values.Look(ref _tradeHeightPercentage, nameof(TradeHeightPercentage), DEFAULT_TRADE_HEIGHT);

			Scribe_Values.Look(ref _excludeUnwillingItems, nameof(ExcludeUnwillingItems), false);

			if (_tradeWidthPercentage < 0.01)
				_tradeWidthPercentage = DEFAULT_TRADE_WIDTH;

			if (_tradeHeightPercentage < 0.01)
				_tradeHeightPercentage = DEFAULT_TRADE_HEIGHT;

			Scribe_Collections.Look(ref _visibleColumns, "visibleColumns");
			if (_visibleColumns != null)
				_visibleColumns = _visibleColumns.Where(x => x != null).ToList();
		}


		internal void Initialize()
		{
			_validColumnDefs.Clear();
			List<TradeColumnDef> tradeColumns = DefDatabase<TradeColumnDef>.AllDefsListForReading;

			foreach (TradeColumnDef columnDef in tradeColumns)
			{
				if (string.IsNullOrWhiteSpace(columnDef.callbackHandler) == false)
				{
					try
					{
						columnDef._callback = AccessTools.MethodDelegate<TradeColumnDef.TradeColumnCallback>(columnDef.callbackHandler);
						if (columnDef._callback == null)
							continue;
						_validColumnDefs.Add(columnDef);
					}
					catch (Exception e)
					{
						Logging.Error($"Unable to locate draw callback '{columnDef.callbackHandler}' for column {columnDef.defName}.\nEnsure referenced method has following arguments: 'ref Rect, Tradeable, TradeAction'");
						Logging.Error(e);
						continue;
					}
				}

				columnDef._orderValueCallback = ParseCallbackHandler<TradeColumnDef.TradeColumnOrderValueCallback>(columnDef.orderValueCallbackHandler,
					$"Unable to locate order value callback '{columnDef.orderValueCallbackHandler}' for column {columnDef.defName}.\nEnsure referenced method has argument of 'List<Tradeable>' and return type of 'Func<Tradeable, object>'");

				columnDef._postOpenCallback = ParseCallbackHandler<TradeColumnDef.TradeColumnEventCallback>(columnDef.postOpenCallbackHandler,
					$"Unable to locate order value callback '{columnDef.postOpenCallbackHandler}' for column {columnDef.defName}.\nEnsure referenced method has arguments matching 'IEnumerable<Tradeable> rows, Transactor transactor'");

				columnDef._postClosedCallback = ParseCallbackHandler<TradeColumnDef.TradeColumnEventCallback>(columnDef.postClosedCallbackHandler,
					$"Unable to locate order value callback '{columnDef.postClosedCallbackHandler}' for column {columnDef.defName}.\nEnsure referenced method has arguments matching 'IEnumerable<Tradeable> rows, Transactor transactor'");
			}

			// Default visible columns
			if (_visibleColumns.Count == 0)
				_visibleColumns.AddRange(_validColumnDefs.Where(x => x.defaultVisible));
		}

		private T? ParseCallbackHandler<T>(string? handler, string error) where T : Delegate
		{
			T? result = null;
			if (string.IsNullOrWhiteSpace(handler) == false)
			{
				try
				{
					result = AccessTools.MethodDelegate<T>(handler);
				}
				catch (Exception e)
				{
					Logging.Error(error);
					Logging.Error(e);
				}
			}
			return result;
		}

		internal IEnumerable<TradeColumnDef> GetVisibleTradeColumns()
		{
			foreach (TradeColumnDef columnDef in _visibleColumns)
			{
				if (_validColumnDefs.Contains(columnDef))
					yield return columnDef;
			}
		}
	}
}
