# MES Breakout Bot

NinjaTrader 8 strategy that trades MES breakouts in two windows (Eastern Time):

- **9:45–11:00**: breakout of the 9:30–9:45 opening range
- **1:00–2:00 PM**: breakout of the 11:30–1:00 midday range

Each trade uses 1 MES with a 10 pt stop ($50) and a 10 pt target ($50), and the position is flattened when the window ends.
Limits: max 2 trades per window, 4 per day, stop for the day at -$200, halt at a 10% drawdown.
Fade mode (off by default) trades against each break instead. Filters: longs only above VWAP, shorts only below it (VWAP from 9:30). Switches in the strategy settings turn the AM window, PM window, longs or shorts on and off.

## Install
1. Set NinjaTrader's time zone to Eastern (Tools > Options > General), or edit the time inputs.
2. Copy `Strategies/MesBreakoutBot.cs` to `Documents\NinjaTrader 8\bin\Custom\Strategies\`.
3. In NinjaTrader, open New > NinjaScript Editor and press F5 to compile.

## Test before live
1. **Backtest:** New > Strategy Analyzer, MES 1-minute, 1+ year.
2. **Sim:** add it to a 1-minute MES chart on the Sim101 account for 4+ weeks.
3. **Live:** only after sim matches the backtest. Start with 1 contract.

Note: the drawdown halt and daily counters reset if the strategy is restarted.

## MesVwapPullbackBot (v2 strategy)
`Strategies/MesVwapPullbackBot.cs` trades pullbacks to today's VWAP in the trend direction, 9:45–11:00 ET.
Long when VWAP is rising and a bar dips to VWAP then closes green above it; short is the mirror.
Defaults: 1 MES, 8 pt stop, 12 pt target, max 3 trades/day, -$200 daily stop.
(The original MesBreakoutBot failed out-of-sample testing and is kept for reference only.)

## MesCloseMomentumBot (Sim101 forward test)
`Strategies/MesCloseMomentumBot.cs`: at 15:30 ET, if MES is 10+ pts away from the 9:30 open, trade that direction; 10 pt stop; exit 15:59. One trade per day.
Backtest (Oct 2025–Sep 2026, $1.24 RT + 1 tick slippage): tuning months PF 0.80, unseen Jun–Sep PF 1.13 on 39 trades. Not proven; running on Sim101 to collect live evidence.
The breakout, VWAP pullback, gap fill, VWAP band and prior-day level ideas all failed out-of-sample.
