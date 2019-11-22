using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using cAlgo.API;

namespace cAlgo
{
    public class LevelController
    {
        private static int MIN_STOP_LOSS_PIPS = 5;

        private static double SL_DAILY_ATR_PERCENTAGE = 0.15;

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
            if(xml != null)
            {
                Levels = new List<Level>();
                Levels = new LevelParser().Parse(xml, Params, Robot.Server.Time);
                Initialize(Levels);
                AnalyzeHistory(Levels);
                Renderer.Render(Levels);
            }
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
            if(!File.Exists(filePath)) {
                Robot.Print("File {0} not found", filePath);
                return null;
            }
            Robot.Print("Loading file {0}", filePath);
            return XDocument.Load(filePath);
        }

        public void OnTick()
        {
            AnalyzeLevelsOnTick();
        }

        public void OnMinute(double dailyAtr)
        {
            DailyAtr = dailyAtr;
            DateTime time = Robot.Server.TimeInUtc;
            if (Params.DailyReloadHour == time.Hour && Params.DailyReloadMinute == time.Minute)
            {
                Init();
                Robot.Print("Auto-reload on scheduled time {0} UTC executed successfully", time);
            }

            if (Params.CalendarPause)
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
                Robot.Print(level);
                level.Id = Params.LevelFileName + "_" + idx;
                level.BeginBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                GetDirection(level, level.BeginBarIndex);
                level.EntryPrice = level.Direction == Direction.LONG ? 
                    level.EntryPrice + Params.LevelOffset * Robot.Symbol.TickSize : 
                    level.EntryPrice - Params.LevelOffset * Robot.Symbol.TickSize;
                if(level.StopLossPips == 0)
                    level.StopLossPips = Params.UseAtrBasedStoppLossPips == true ? 
                        (int) Math.Round(Math.Max(DailyAtr * SL_DAILY_ATR_PERCENTAGE, MIN_STOP_LOSS_PIPS)) : 
                        Params.DefaultStopLossPips;
                if (level.ProfitTargetPips == 0)
                    level.ProfitTargetPips = (int) (level.StopLossPips * Params.RiskRewardRatio * 100);

                if(level.Direction == Direction.LONG)
                {
                    level.StopLossPrice = level.EntryPrice - level.StopLossPips * Robot.Symbol.PipSize;
                    level.ProfitTargetPrice = level.EntryPrice + level.ProfitTargetPips * Params.RiskRewardRatio * 100 * Robot.Symbol.PipSize;
                    level.ActivatePrice = level.EntryPrice + level.ProfitTargetPips * Robot.Symbol.PipSize * Params.LevelActivate;
                    level.DeactivatePrice = level.EntryPrice + level.ProfitTargetPips * Robot.Symbol.PipSize * Params.LevelDeactivate;
                }
                else
                {
                    level.StopLossPrice = level.EntryPrice + level.StopLossPips * Robot.Symbol.PipSize;
                    level.ProfitTargetPrice = level.EntryPrice - level.ProfitTargetPips * Params.RiskRewardRatio * 100 * Robot.Symbol.PipSize;
                    level.ActivatePrice = level.EntryPrice - level.ProfitTargetPips * Robot.Symbol.PipSize * Params.LevelActivate;
                    level.DeactivatePrice = level.EntryPrice - level.ProfitTargetPips * Robot.Symbol.PipSize * Params.LevelDeactivate;
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
                    if (Robot.MarketSeries.Low[i] <= level.ActivatePrice && !level.LevelActivated)
                    {
                        Robot.Print("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        level.LevelActivated = true;
                    }
                    if (Robot.MarketSeries.Low[i] >= level.DeactivatePrice && level.LevelActivated)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        break;
                    }
                    if (Robot.MarketSeries.Low[i] <= level.EntryPrice)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as traded", level.Label);
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
                        double volume = Calculator.GetVolume(Robot.Symbol.Name, Params.PositionSize, Params.FixedRiskAmount, level.StopLossPips, trade);
                        TradeResult result = Robot.PlaceLimitOrder(trade, Robot.Symbol.Name, volume, level.EntryPrice, level.Label, level.StopLossPips, level.ProfitTargetPips, level.ValidTo);
                        Robot.Print("Placing Limit Order Entry:{0} SL Pips:{1} Type: {2}", level.EntryPrice, level.StopLossPips, trade);
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
            double barPercentage = 0.3;
            double barsPercentage = 0.5;
            if(Params.StrategyType == StrategyType.SWING)
            {
                barPercentage = 0.5;
                barsPercentage = 1;
            }
            if (Params.StrategyType == StrategyType.INVEST)
            {
                barPercentage = 1;
                barsPercentage = 1.5;
            }


            double lastBarVolatility = GetVolatilityPips(direction);
            double lastBarsVolatility = GetVolatilityPips(direction, barsCount);
            Robot.Print("aaaaa " + DailyAtr + " " + lastBarVolatility + " " + lastBarsVolatility);
            bool isSpike = lastBarsVolatility >= DailyAtr * barsPercentage || lastBarVolatility > DailyAtr * barPercentage;
            if (isSpike)
                Robot.Print("Spike detected. Volatility on last bar: {0} pips Volatility on last {1} bars: {2} pips. Avg Daily Atr was {3}", lastBarVolatility, barsCount, lastBarsVolatility, DailyAtr);
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
