// MesBreakoutBot - NinjaTrader 8 strategy for MES (Micro E-mini S&P 500).
//
// Morning:   Opening Range Breakout. Range = 9:30-9:45, trade 9:45-11:00.
// Afternoon: Midday Range Breakout.  Range = 11:30-13:00, trade 13:00-14:00.
//
// Times are in the time zone NinjaTrader is set to (Tools > Options > General).
// Set it to Eastern Time, or change the time inputs to match your zone.
//
// Fade mode (off by default) takes the opposite side of each break instead.
// Filters: breakouts only in the direction of today's VWAP (from 9:30), and
// on/off switches for each window and direction so each piece can be tested alone.
//
// Run on a 1-minute MES chart. Test in Strategy Analyzer and Sim101 before going live.

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class MesBreakoutBot : Strategy
	{
		private DateTime currentDay = DateTime.MinValue;
		private double dayStartProfit;   // realized P&L at start of today
		private double peakProfit;       // highest realized P&L seen, for drawdown
		private bool haltedForDrawdown;

		private int tradesToday;
		private int amTrades;
		private int pmTrades;

		private double amHigh, amLow;
		private double pmHigh, pmLow;

		private double cumPriceVolume, cumVolume;  // for session VWAP from AmRangeStart

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name                         = "MesBreakoutBot";
				Description                  = "MES opening-range and midday-range breakout with daily risk limits.";
				Calculate                    = Calculate.OnBarClose;
				EntriesPerDirection          = 1;
				EntryHandling                = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds    = 30;
				BarsRequiredToTrade          = 2;
				StartBehavior                = StartBehavior.WaitUntilFlat;
				IsInstantiatedOnEachOptimizationIteration = true;

				Contracts          = 1;
				StopPoints         = 10;
				TargetPoints       = 10;
				MaxTradesPerWindow = 2;
				MaxTradesPerDay    = 4;
				DailyLossLimit     = 200;
				StartingCapital    = 10000;
				MaxDrawdownPercent = 10;

				UseVwapFilter = true;
				FadeBreakouts = false;
				TradeAm       = true;
				TradePm       = true;
				AllowLongs    = true;
				AllowShorts   = true;

				AmRangeStart = 93000;  AmRangeEnd = 94500;  AmTradeEnd = 110000;
				PmRangeStart = 113000; PmRangeEnd = 130000; PmTradeEnd = 140000;
			}
			else if (State == State.Configure)
			{
				// Points to ticks (MES tick = 0.25, so 4 ticks per point).
				SetStopLoss(CalculationMode.Ticks, StopPoints * 4);
				SetProfitTarget(CalculationMode.Ticks, TargetPoints * 4);
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0 || CurrentBar < BarsRequiredToTrade)
				return;

			// Bar timestamps are the bar's close time, so a 1-min bar stamped 9:31 covers 9:30-9:31.
			int t = ToTime(Time[0]);
			double realized = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

			if (Time[0].Date != currentDay)
				StartNewDay(realized);

			UpdateRanges(t);

			// Drawdown guard: stop trading for good if equity falls MaxDrawdownPercent below its peak.
			peakProfit = Math.Max(peakProfit, realized);
			double peakEquity = StartingCapital + peakProfit;
			if (StartingCapital + realized <= peakEquity * (1 - MaxDrawdownPercent / 100.0))
				haltedForDrawdown = true;

			bool inAmWindow = t > AmRangeEnd && t <= AmTradeEnd;
			bool inPmWindow = t > PmRangeEnd && t <= PmTradeEnd;

			// Flatten anything still open once a trading window ends.
			if (!inAmWindow && !inPmWindow && Position.MarketPosition != MarketPosition.Flat)
			{
				ExitLong();
				ExitShort();
				return;
			}

			if (Position.MarketPosition != MarketPosition.Flat || haltedForDrawdown)
				return;
			if (tradesToday >= MaxTradesPerDay)
				return;
			if (realized - dayStartProfit <= -DailyLossLimit)
				return;

			// No new entries on a window's last bar, since the fill would land after the window closes.
			if (TradeAm && inAmWindow && t < AmTradeEnd && amTrades < MaxTradesPerWindow)
			{
				if (TryBreakout(amHigh, amLow, "AM"))
					amTrades++;
			}
			else if (TradePm && inPmWindow && t < PmTradeEnd && pmTrades < MaxTradesPerWindow)
			{
				if (TryBreakout(pmHigh, pmLow, "PM"))
					pmTrades++;
			}
		}

		private void StartNewDay(double realized)
		{
			currentDay     = Time[0].Date;
			dayStartProfit = realized;
			tradesToday    = 0;
			amTrades       = 0;
			pmTrades       = 0;
			amHigh = pmHigh = double.MinValue;
			amLow  = pmLow  = double.MaxValue;
			cumPriceVolume = 0;
			cumVolume      = 0;
		}

		private void UpdateRanges(int t)
		{
			if (t > AmRangeStart)
			{
				cumPriceVolume += (High[0] + Low[0] + Close[0]) / 3 * Volume[0];
				cumVolume      += Volume[0];
			}
			if (t > AmRangeStart && t <= AmRangeEnd)
			{
				amHigh = Math.Max(amHigh, High[0]);
				amLow  = Math.Min(amLow, Low[0]);
			}
			if (t > PmRangeStart && t <= PmRangeEnd)
			{
				pmHigh = Math.Max(pmHigh, High[0]);
				pmLow  = Math.Min(pmLow, Low[0]);
			}
		}

		// Enters only on a fresh break: the prior bar closed inside the range and this one closed outside.
		private bool TryBreakout(double rangeHigh, double rangeLow, string tag)
		{
			if (rangeHigh == double.MinValue || rangeLow == double.MaxValue)
				return false;  // range never formed (holiday, missing data)

			bool breakUp   = Close[0] > rangeHigh && Close[1] <= rangeHigh;
			bool breakDown = Close[0] < rangeLow && Close[1] >= rangeLow;
			if (!breakUp && !breakDown)
				return false;

			// Fade mode trades against the break (sell a break up, buy a break down).
			bool goLong = FadeBreakouts ? breakDown : breakUp;

			// The VWAP filter only applies to breakout mode.
			double vwap = cumVolume > 0 ? cumPriceVolume / cumVolume : Close[0];
			bool vwapOk = FadeBreakouts || !UseVwapFilter || (goLong ? Close[0] > vwap : Close[0] < vwap);
			if (!vwapOk || (goLong && !AllowLongs) || (!goLong && !AllowShorts))
				return false;

			if (goLong)
				EnterLong(Contracts, tag + "Long");
			else
				EnterShort(Contracts, tag + "Short");
			tradesToday++;
			return true;
		}

		#region Properties
		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Contracts", Order = 1, GroupName = "1. Trade")]
		public int Contracts { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Stop (points)", Order = 2, GroupName = "1. Trade")]
		public int StopPoints { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Target (points)", Order = 3, GroupName = "1. Trade")]
		public int TargetPoints { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Max trades per window", Order = 1, GroupName = "2. Risk")]
		public int MaxTradesPerWindow { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Max trades per day", Order = 2, GroupName = "2. Risk")]
		public int MaxTradesPerDay { get; set; }

		[NinjaScriptProperty, Range(1, double.MaxValue)]
		[Display(Name = "Daily loss limit ($)", Order = 3, GroupName = "2. Risk")]
		public double DailyLossLimit { get; set; }

		[NinjaScriptProperty, Range(1, double.MaxValue)]
		[Display(Name = "Starting capital ($)", Order = 4, GroupName = "2. Risk")]
		public double StartingCapital { get; set; }

		[NinjaScriptProperty, Range(1, 100)]
		[Display(Name = "Max drawdown (%)", Order = 5, GroupName = "2. Risk")]
		public double MaxDrawdownPercent { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Only trade with VWAP", Order = 6, GroupName = "2. Risk")]
		public bool UseVwapFilter { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Fade breakouts (trade against the break)", Order = 7, GroupName = "2. Risk")]
		public bool FadeBreakouts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Trade AM window", Order = 1, GroupName = "4. Switches")]
		public bool TradeAm { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Trade PM window", Order = 2, GroupName = "4. Switches")]
		public bool TradePm { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow longs", Order = 3, GroupName = "4. Switches")]
		public bool AllowLongs { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow shorts", Order = 4, GroupName = "4. Switches")]
		public bool AllowShorts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "AM range start (HHmmss)", Order = 1, GroupName = "3. Times")]
		public int AmRangeStart { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "AM range end / trading start", Order = 2, GroupName = "3. Times")]
		public int AmRangeEnd { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "AM trading end", Order = 3, GroupName = "3. Times")]
		public int AmTradeEnd { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "PM range start", Order = 4, GroupName = "3. Times")]
		public int PmRangeStart { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "PM range end / trading start", Order = 5, GroupName = "3. Times")]
		public int PmRangeEnd { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "PM trading end", Order = 6, GroupName = "3. Times")]
		public int PmTradeEnd { get; set; }
		#endregion
	}
}
