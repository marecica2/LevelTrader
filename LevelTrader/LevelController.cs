using System;
using System.Collections.Generic;
using System.Xml.Linq;
using cAlgo.API;

namespace cAlgo
{
    public class LevelController
    {
        private Robot Robot { get; set; }

        private InputParams Params { get; set; }

        private Calendar Calendar { get; set; }

        private List<Level> Levels;

        private LevelRenderer Renderer;

        private RiskCalculator Calculator;

        private double DailyAtr { get; set; }
        
        private bool Paused = false;

        private DateTime ?PausedUntil;

        public LevelController(Robot robot, InputParams inputParams, Calendar calendar)
        {
            Robot = robot;
            Params = inputParams;
            Calendar = calendar;
            Renderer = new LevelRenderer(Robot);
            Calculator = new RiskCalculator(Robot);
        }

        public void Init()
        {
            XDocument xml = LoadXml();
            Levels = new LevelParser().Parse(xml, Params);
            Initialize(Levels);
            AnalyzeHistory(Levels);
            Renderer.Render(Levels);
        }

        private XDocument LoadXml()
        {
            string filePath = Params.LevelFilePath;
            if (Robot.RunningMode != RunningMode.RealTime)
            {
                DateTime time = Robot.Server.TimeInUtc;
                int week = Utils.GetWeekOfYear(time);
                filePath = Params.BackTestPath + "\\" + time.Year + "-" + time.Month + "-" + time.Day + " (" + week + ")\\";
            }
            filePath += Params.LevelFileName;
            Robot.Print("Loading file {0}", filePath);
            return XDocument.Load(filePath);
        }

        public void OnTick()
        {
            AnalyzeLevelsOnTick();
        }

        public void OnBar(double dailyAtr)
        {
            DailyAtr = dailyAtr;
            DateTime time = Robot.Server.TimeInUtc;
            if (Params.DailyReloadHour == time.Hour && Params.DailyReloadMinute == time.Minute)
            {
                Init();
                Robot.Print("Auto-reload on scheduled time {0} UTC executed successfully", time);
            }

            if(Params.CalendarPause)
            {
                bool paused = Calendar.IsPaused();
                if (paused != Paused)
                {
                    Renderer.Render(Levels, paused);
                    Paused = paused;
                    if (Paused)
                        foreach (Level level in Levels)
                            CancelPendingOrder(level, "Ongoing calendar event with expected impact");
                }

            }
        }

        private void Initialize(List<Level> levels)
        {
            int idx = 0;
            foreach (Level level in Levels)
            {
                level.Id = Params.LevelFileName + "_" + idx;
                level.BeginBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                GetDirection(level, level.BeginBarIndex);
                level.EntryPrice = level.Direction == Direction.LONG ? 
                    level.EntryPrice + Params.LevelOffset * Robot.Symbol.TickSize : 
                    level.EntryPrice - Params.LevelOffset * Robot.Symbol.TickSize;
                level.StopLoss = Params.UseAtrBasedStoppLossPips == true ? 
                    (int) Math.Max(Math.Ceiling(DailyAtr * 0.15), 5) : 
                    Params.StopLossPips;
                level.ProfitTarget = Params.StopLossPips * Params.RiskRewardRatio * 100;

                if(level.Direction == Direction.LONG)
                {
                    level.StopLossPrice = level.EntryPrice - Params.StopLossPips * Robot.Symbol.PipSize;
                    level.ProfitTargetPrice = level.EntryPrice + Params.StopLossPips * Params.RiskRewardRatio * 100 * Robot.Symbol.PipSize;
                    level.ActivatePrice = level.EntryPrice + Params.StopLossPips * Robot.Symbol.PipSize * Params.LevelActivate;
                    level.DeactivatePrice = level.EntryPrice + Params.StopLossPips * Robot.Symbol.PipSize * Params.LevelDeactivate;
                }
                else
                {
                    level.StopLossPrice = level.EntryPrice + Params.StopLossPips * Robot.Symbol.PipSize;
                    level.ProfitTargetPrice = level.EntryPrice - Params.StopLossPips * Params.RiskRewardRatio * 100 * Robot.Symbol.PipSize;
                    level.ActivatePrice = level.EntryPrice - Params.StopLossPips * Robot.Symbol.PipSize * Params.LevelActivate;
                    level.DeactivatePrice = level.EntryPrice - Params.StopLossPips * Robot.Symbol.PipSize * Params.LevelDeactivate;
                }
                idx++;
            }
            Robot.Print("Number of levels loaded: {0}", Levels.Count);
        }

        private void GetDirection(Level level, int levelFirstBarIndex)
        {
            level.Direction = level.EntryPrice > Robot.MarketSeries.High[levelFirstBarIndex] ? Direction.SHORT : Direction.LONG;
        }

        private void AnalyzeHistory(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                int levelFirstBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                int levelLastBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidTo);
                DateTime currentDate = Robot.MarketSeries.OpenTime.LastValue.Date;
                HasBeenTraded(level, levelFirstBarIndex, levelLastBarIndex);
            }
        }

        private void HasBeenTraded(Level level, int fromIndex, int toIndex)
        {
            if (level.Direction == Direction.LONG)
            {
                for (int i = fromIndex; i <= toIndex; i++)
                {
                    if (Robot.MarketSeries.Low[i] <= level.EntryPrice)
                    {
                        level.Traded = true;
                        break;
                    }
                }
            }
            else
            {
                for (int i = fromIndex; i <= toIndex; i++)
                {
                    if (Robot.MarketSeries.High[i] >= level.ActivatePrice && !level.LevelActivated)
                    {
                        Robot.Print("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        level.LevelActivated = true;
                    }
                    if (Robot.MarketSeries.High[i] <= level.DeactivatePrice && level.LevelActivated)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        break;
                    }
                    if (Robot.MarketSeries.High[i] >= level.EntryPrice)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as traded", level.Label);
                        break;
                    }
                }
            }
        }


        private void AnalyzeLevelsOnTick()
        {
            int idx = Robot.MarketSeries.Close.Count - 1;
            foreach (Level level in Levels)
            {
                TradeType trade = TradeType.Buy;
                Func<Level, bool> isLevelCrossed = l => Robot.Symbol.Bid <= l.ActivatePrice;
                Func<Level, bool> isLevelGoneAway = l => Robot.Symbol.Bid > l.DeactivatePrice;
                if (level.Direction == Direction.SHORT)
                {
                    isLevelCrossed = l => Robot.Symbol.Ask >= l.ActivatePrice && !l.LevelActivated;
                    isLevelGoneAway = l => Robot.Symbol.Ask < l.DeactivatePrice && l.LevelActivated;
                    trade = TradeType.Sell;
                }

                if (IsLevelTradeable(level) && isLevelCrossed(level))
                {
                    level.LevelActivated = true;
                    level.LevelActivatedIndex = idx;
                    level.Traded = true;
                    Renderer.RenderLevel(level, Paused);
                    if (!Paused && !IsSpike(level.Direction == Direction.LONG ? TradeType.Sell : TradeType.Buy))
                    {
                        double volume = Calculator.GetVolume(Robot.Symbol.Name, Params.PositionSize, Params.FixedRiskAmount, Params.StopLossPips, trade);
                        TradeResult result = Robot.PlaceLimitOrder(trade, Robot.Symbol.Name, volume, level.EntryPrice, level.Label, level.StopLoss, level.ProfitTarget, level.ValidTo);
                        Robot.Print("Placing Limit Order Entry:{0} SL Pips:{1} Type: {2}", level.EntryPrice, Params.StopLossPips, trade);
                        Robot.Print("Order placed for Level {0} Success: {1}  Error: {2}", result.PendingOrder.Label, result.IsSuccessful, result.Error);
                    }
                    else
                    {
                        Robot.Print("Order skipped because of Calendar Event / Spike against");
                    }
                }

                if (isLevelGoneAway(level) && level.Traded && !level.LevelDeactivated)
                {
                    level.LevelDeactivated = true;
                    CancelPendingOrder(level, "Deactivate Level reached");
                }
            }
  
        }

        private bool IsSpike(TradeType direction)
        {
            if (Params.PreventSpikes == false)
                return false;

            int barsCount = 4;
            double pipsBars = GetVolatilityPips(direction, barsCount);
            double pipsBar = GetVolatilityPips(direction);
            bool isSpike = pipsBars >= DailyAtr * 0.5 || pipsBar > DailyAtr * 0.3;
            if (isSpike)
                Robot.Print("Spike detected. Volatility on last bar: {0} pips Volatility on last {0} bars: {1} pips", pipsBar, barsCount, pipsBars);
            return isSpike;
        }

        private double GetVolatilityPips(TradeType direction, int lastBarsCount = 1)
        {
            int currentBar = Robot.MarketSeries.Close.Count - 1;
            int firstBar = Robot.MarketSeries.Close.Count - lastBarsCount;
            double distanceInPips = 0;
            if(direction == TradeType.Buy)
            {
                double minLow = Robot.MarketSeries.Low[currentBar];
                for (int i = firstBar; i <= currentBar; i++)
                    if (Robot.MarketSeries.Low[i] < minLow)
                        minLow = Robot.MarketSeries.Low[i];

                distanceInPips = Robot.MarketSeries.High[currentBar] - minLow;
            } else
            {
                double maxHigh = Robot.MarketSeries.High[currentBar];
                for (int i = firstBar; i <= currentBar; i++)
                    if (Robot.MarketSeries.Low[i] > maxHigh)
                        maxHigh = Robot.MarketSeries.High[i];

                distanceInPips = maxHigh - Robot.MarketSeries.Low[currentBar];
            }

            return Math.Round(distanceInPips, Robot.Symbol.Digits) / Robot.Symbol.PipSize;
        }

        private bool IsLevelTradeable(Level level)
        {
            return level.ValidFrom < Robot.Server.TimeInUtc && Robot.Server.TimeInUtc < level.ValidTo && !level.Traded;
        }

        private void CancelPendingOrder(Level level, string reason)
        {
            foreach (var order in Robot.PendingOrders)
            {
                if (order.Label == level.Label)
                {
                    Robot.Print("Order for level {0} cancelled. Reason: {1}", level.Label, reason);
                    Robot.CancelPendingOrder(order);
                }
            }
        }


    }
}
