# MES Breakout Bot

NinjaTrader 8 strategy that trades MES breakouts in two windows (Eastern Time):

- **9:45–11:00**: breakout of the 9:30–9:45 opening range
- **1:00–2:00 PM**: breakout of the 11:30–1:00 midday range

Each trade uses 1 MES with a 10 pt stop ($50) and a 20 pt target ($100), and the position is flattened when the window ends.
Limits: max 2 trades per window, 4 per day, stop for the day at -$200, halt at a 10% drawdown.

## Install
1. Set NinjaTrader's time zone to Eastern (Tools > Options > General), or edit the time inputs.
2. Copy `Strategies/MesBreakoutBot.cs` to `Documents\NinjaTrader 8\bin\Custom\Strategies\`.
3. In NinjaTrader, open New > NinjaScript Editor and press F5 to compile.

## Test before live
1. **Backtest:** New > Strategy Analyzer, MES 1-minute, 1+ year.
2. **Sim:** add it to a 1-minute MES chart on the Sim101 account for 4+ weeks.
3. **Live:** only after sim matches the backtest. Start with 1 contract.

Note: the drawdown halt and daily counters reset if the strategy is restarted.
