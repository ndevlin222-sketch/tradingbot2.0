// MesVwapPullbackBot - NinjaTrader 8 strategy for MES (Micro E-mini S&P 500).
//
// Trades pullbacks to today's VWAP in the direction of the trend, 9:45-11:00.
//   Uptrend:   price above VWAP and VWAP rising. Buy when a bar dips to VWAP and closes back above it, green.
//   Downtrend: price below VWAP and VWAP falling. Sell when a bar pops to VWAP and closes back below it, red.
//
// Times are in the time zone NinjaTrader is set to (Tools > Options > General).
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
	public class MesVwapPullbackBot : Strategy
	{
		private DateTime currentDay = DateTime.MinValue;
		private double dayStartProfit;   // realized P&L at start of today
		private double peakProfit;       // highest realized P&L seen, for drawdown
		private bool haltedForDrawdown;
		private int tradesToday;

		private double cumPriceVolume, cumVolume;
		private Series<double> vwap;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name                         = "MesVwapPullbackBot";
				Description                  = "MES pullback-to-VWAP in the direction of the trend, with daily risk limits.";
				Calculate                    = Calculate.OnBarClose;
				EntriesPerDirection          = 1;
				EntryHandling                = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds    = 30;
				BarsRequiredToTrade          = 20;
				StartBehavior                = StartBehavior.WaitUntilFlat;
				IsInstantiatedOnEachOptimizationIteration = true;

				Contracts          = 1;
				StopPoints         = 8;
				TargetPoints       = 12;
				TouchPoints        = 1;
				SlopeBars          = 15;
				MaxTradesPerDay    = 3;
				DailyLossLimit     = 200;
				StartingCapital    = 10000;
				MaxDrawdownPercent = 10;
				AllowLongs         = true;
				AllowShorts        = true;

				VwapStart  = 93000;
				TradeStart = 94500;
				TradeEnd   = 110000;
			}
			else if (State == State.Configure)
			{
				// Points to ticks (MES tick = 0.25, so 4 ticks per point).
				SetStopLoss(CalculationMode.Ticks, StopPoints * 4);
				SetProfitTarget(CalculationMode.Ticks, TargetPoints * 4);
			}
			else if (State == State.DataLoaded)
			{
				vwap = new Series<double>(this);
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0)
				return;

			// Bar timestamps are the bar's close time, so a 1-min bar stamped 9:31 covers 9:30-9:31.
			int t = ToTime(Time[0]);
			double realized = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

			if (Time[0].Date != currentDay)
				StartNewDay(realized);

			if (t > VwapStart)
			{
				cumPriceVolume += (High[0] + Low[0] + Close[0]) / 3 * Volume[0];
				cumVolume      += Volume[0];
			}
			vwap[0] = cumVolume > 0 ? cumPriceVolume / cumVolume : Close[0];

			if (CurrentBar < BarsRequiredToTrade)
				return;

			// Drawdown guard: stop trading for good if equity falls MaxDrawdownPercent below its peak.
			peakProfit = Math.Max(peakProfit, realized);
			double peakEquity = StartingCapital + peakProfit;
			if (StartingCapital + realized <= peakEquity * (1 - MaxDrawdownPercent / 100.0))
				haltedForDrawdown = true;

			bool inWindow = t > TradeStart && t <= TradeEnd;

			// Flatten anything still open once the window ends.
			if (!inWindow && Position.MarketPosition != MarketPosition.Flat)
			{
				ExitLong();
				ExitShort();
				return;
			}

			// No new entries on the window's last bar, since the fill would land after it closes.
			if (!inWindow || t >= TradeEnd)
				return;
			if (Position.MarketPosition != MarketPosition.Flat || haltedForDrawdown)
				return;
			if (tradesToday >= MaxTradesPerDay)
				return;
			if (realized - dayStartProfit <= -DailyLossLimit)
				return;

			// VWAP slope is measured over SlopeBars, all within today (the window starts 15 min after VWAP does).
			bool vwapRising  = vwap[0] > vwap[SlopeBars];
			bool vwapFalling = vwap[0] < vwap[SlopeBars];

			bool longSignal = AllowLongs && vwapRising
				&& Low[0] <= vwap[0] + TouchPoints
				&& Close[0] > vwap[0]
				&& Close[0] > Open[0];

			bool shortSignal = AllowShorts && vwapFalling
				&& High[0] >= vwap[0] - TouchPoints
				&& Close[0] < vwap[0]
				&& Close[0] < Open[0];

			if (longSignal)
			{
				EnterLong(Contracts, "VwapLong");
				tradesToday++;
			}
			else if (shortSignal)
			{
				EnterShort(Contracts, "VwapShort");
				tradesToday++;
			}
		}

		private void StartNewDay(double realized)
		{
			currentDay     = Time[0].Date;
			dayStartProfit = realized;
			tradesToday    = 0;
			cumPriceVolume = 0;
			cumVolume      = 0;
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

		[NinjaScriptProperty, Range(0, double.MaxValue)]
		[Display(Name = "VWAP touch distance (points)", Order = 4, GroupName = "1. Trade")]
		public double TouchPoints { get; set; }

		[NinjaScriptProperty, Range(1, 15)]
		[Display(Name = "VWAP slope lookback (bars)", Order = 5, GroupName = "1. Trade")]
		public int SlopeBars { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Max trades per day", Order = 1, GroupName = "2. Risk")]
		public int MaxTradesPerDay { get; set; }

		[NinjaScriptProperty, Range(1, double.MaxValue)]
		[Display(Name = "Daily loss limit ($)", Order = 2, GroupName = "2. Risk")]
		public double DailyLossLimit { get; set; }

		[NinjaScriptProperty, Range(1, double.MaxValue)]
		[Display(Name = "Starting capital ($)", Order = 3, GroupName = "2. Risk")]
		public double StartingCapital { get; set; }

		[NinjaScriptProperty, Range(1, 100)]
		[Display(Name = "Max drawdown (%)", Order = 4, GroupName = "2. Risk")]
		public double MaxDrawdownPercent { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow longs", Order = 1, GroupName = "3. Switches")]
		public bool AllowLongs { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow shorts", Order = 2, GroupName = "3. Switches")]
		public bool AllowShorts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "VWAP start (HHmmss)", Order = 1, GroupName = "4. Times")]
		public int VwapStart { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Trading start", Order = 2, GroupName = "4. Times")]
		public int TradeStart { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Trading end", Order = 3, GroupName = "4. Times")]
		public int TradeEnd { get; set; }
		#endregion
	}
}
