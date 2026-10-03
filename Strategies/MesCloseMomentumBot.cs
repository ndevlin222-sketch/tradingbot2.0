// MesCloseMomentumBot - NinjaTrader 8 strategy for MES (Micro E-mini S&P 500).
//
// Intraday momentum into the close: if MES moved at least MinMovePoints from the 9:30 open
// to 10:00, trade in that direction at 15:30 and exit at 15:59. One trade per day.
//
// Times are in the time zone NinjaTrader is set to (Tools > Options > General).
// Run on a 1-minute MES chart with the Sim101 account until it proves itself.

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class MesCloseMomentumBot : Strategy
	{
		private DateTime currentDay = DateTime.MinValue;
		private double dayOpen;          // first trade price of the RTH session (9:30)
		private double morningMove;      // 9:30 open to 10:00 close, set at 10:00
		private bool tradedToday;
		private double peakProfit;
		private bool haltedForDrawdown;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name                         = "MesCloseMomentumBot";
				Description                  = "MES intraday momentum: trade the 9:30-10:00 direction from 15:30 to 15:59.";
				Calculate                    = Calculate.OnBarClose;
				EntriesPerDirection          = 1;
				EntryHandling                = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds    = 30;
				BarsRequiredToTrade          = 1;
				StartBehavior                = StartBehavior.WaitUntilFlat;
				IsInstantiatedOnEachOptimizationIteration = true;

				Contracts          = 1;
				StopPoints         = 10;
				MinMovePoints      = 10;
				StartingCapital    = 10000;
				MaxDrawdownPercent = 10;

				OpenTime   = 93000;
				SignalTime = 100000;
				EntryTime = 153000;
				ExitTime  = 155900;
			}
			else if (State == State.Configure)
			{
				// Points to ticks (MES tick = 0.25, so 4 ticks per point). No target: the exit is time-based.
				SetStopLoss(CalculationMode.Ticks, StopPoints * 4);
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0 || CurrentBar < BarsRequiredToTrade)
				return;

			// Bar timestamps are the bar's close time, so the 9:31 bar's Open is the 9:30 open.
			int t = ToTime(Time[0]);

			if (Time[0].Date != currentDay)
			{
				currentDay  = Time[0].Date;
				dayOpen     = 0;
				morningMove = 0;
				tradedToday = false;
			}

			if (dayOpen == 0 && t > OpenTime)
				dayOpen = Open[0];
			if (t == SignalTime && dayOpen != 0)
				morningMove = Close[0] - dayOpen;

			// Drawdown guard: stop trading for good if equity falls MaxDrawdownPercent below its peak.
			double realized = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
			peakProfit = Math.Max(peakProfit, realized);
			if (StartingCapital + realized <= (StartingCapital + peakProfit) * (1 - MaxDrawdownPercent / 100.0))
				haltedForDrawdown = true;

			if (t >= ExitTime && Position.MarketPosition != MarketPosition.Flat)
			{
				ExitLong();
				ExitShort();
				return;
			}

			if (t != EntryTime || tradedToday || haltedForDrawdown || dayOpen == 0)
				return;
			if (Position.MarketPosition != MarketPosition.Flat)
				return;

			if (morningMove >= MinMovePoints)
			{
				EnterLong(Contracts, "MomLong");
				tradedToday = true;
			}
			else if (morningMove <= -MinMovePoints)
			{
				EnterShort(Contracts, "MomShort");
				tradedToday = true;
			}
		}

		#region Properties
		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Contracts", Order = 1, GroupName = "1. Trade")]
		public int Contracts { get; set; }

		[NinjaScriptProperty, Range(1, int.MaxValue)]
		[Display(Name = "Stop (points)", Order = 2, GroupName = "1. Trade")]
		public int StopPoints { get; set; }

		[NinjaScriptProperty, Range(0, double.MaxValue)]
		[Display(Name = "Min move from open (points)", Order = 3, GroupName = "1. Trade")]
		public double MinMovePoints { get; set; }

		[NinjaScriptProperty, Range(1, double.MaxValue)]
		[Display(Name = "Starting capital ($)", Order = 1, GroupName = "2. Risk")]
		public double StartingCapital { get; set; }

		[NinjaScriptProperty, Range(1, 100)]
		[Display(Name = "Max drawdown (%)", Order = 2, GroupName = "2. Risk")]
		public double MaxDrawdownPercent { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "RTH open (HHmmss)", Order = 1, GroupName = "3. Times")]
		public int OpenTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Signal time (end of morning move)", Order = 2, GroupName = "3. Times")]
		public int SignalTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Entry time", Order = 3, GroupName = "3. Times")]
		public int EntryTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Exit time", Order = 4, GroupName = "3. Times")]
		public int ExitTime { get; set; }
		#endregion
	}
}
