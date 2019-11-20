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

        public void OnBar()
        {
            DateTime time = Robot.Server.TimeInUtc;
            if(Params.DailyReloadHour == time.Hour && Params.DailyReloadMinute == time.Minute)
            {
                Init();
                Calendar.Init();
                Robot.Print("Auto-reload on scheduled time {0} UTC executed successfully", time);
            }
 
            if(!Paused)
            {
                PausedUntil = Calendar.GetEventsInAdvance(Robot.Symbol.Name);
                if (!Paused && PausedUntil != null)
                {
                    Paused = true;
                    Robot.Print("Pausing execution until {0}", PausedUntil.Value);
                    Renderer.Render(Levels, true);
                    foreach (Level level in Levels)
                    {
                        CancelPendingOrder(level);
                    }
                } 
            }

            if (Paused && time >= PausedUntil.Value)
            {
                Paused = false;
                Robot.Print("Resuming execution");
                Renderer.Render(Levels, false);
            }

            Calendar.OnBar();
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
                level.StopLoss = Params.StopLossPips;
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
                Func<Level, bool> isLevelGoneBack = l => Robot.Symbol.Bid > l.DeactivatePrice;
                if (level.Direction == Direction.SHORT)
                {
                    isLevelCrossed = l => Robot.Symbol.Ask >= l.ActivatePrice && !l.LevelActivated;
                    isLevelGoneBack = l => Robot.Symbol.Ask < l.DeactivatePrice && l.LevelActivated;
                    trade = TradeType.Sell;
                }

                if (IsLevelTradeable(level) && isLevelCrossed(level))
                {
                    level.LevelActivated = true;
                    level.LevelActivatedIndex = idx;
                    level.Traded = true;
                    Renderer.RenderLevel(level, Paused);
                    if (!Paused)
                    {
                        double volume = Calculator.GetVolume(Robot.Symbol.Name, Params.PositionSize, Params.FixedRiskAmount, Params.StopLossPips, trade);
                        TradeResult result = Robot.PlaceLimitOrder(trade, Robot.Symbol.Name, volume, level.EntryPrice, level.Label, level.StopLoss, level.ProfitTarget, level.ValidTo);
                        Robot.Print("Order placed for Level {0} Success: {1}  Error: {2}", result.PendingOrder.Label, result.IsSuccessful, result.Error);
                    }
                }

                if (isLevelGoneBack(level) && level.Traded && !level.LevelDeactivated)
                {
                    level.LevelDeactivated = true;
                    CancelPendingOrder(level);
                }
            }
  
        }

        private bool IsLevelTradeable(Level level)
        {
            return level.ValidFrom < Robot.Server.TimeInUtc && Robot.Server.TimeInUtc < level.ValidTo && !level.Traded;
        }

        private void CancelPendingOrder(Level level)
        {
            foreach (var order in Robot.PendingOrders)
            {
                if (order.Label == level.Label)
                {
                    Robot.Print("Order for level {0} cancelled. Reason Deactivate Level reached", level.Label);
                    Robot.CancelPendingOrder(order);
                }
            }
        }


    }
}
