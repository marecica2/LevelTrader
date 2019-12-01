﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using cAlgo.API;
using NLog;

namespace cAlgo
{
    public class LevelController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static int MIN_STOP_LOSS_PIPS = 5;

        private static double SL_DAILY_ATR_PERCENTAGE = 0.11;

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

        public void Init(double dailyAtr)
        {
            DailyAtr = dailyAtr;
            XDocument xml = LoadXml();
            Levels = new List<Level>();
            if(xml != null)
            {
                Levels = new LevelParser().Parse(xml, Params, Robot.Server.Time);
                Initialize(Levels);
                AnalyzeHistory(Levels);
                Renderer.Render(Levels);
                SendEmailNotification(dailyAtr);
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
                logger.Error("File {0} not found", filePath);
                Robot.Print("File {0} not found", filePath);
                return null;
            }
            logger.Info("Loading file {0}", filePath);
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
            if (Params.DailyReloadHour == time.Hour && Params.DailyReloadMinute == time.Minute && (time.DayOfWeek != DayOfWeek.Saturday || time.DayOfWeek != DayOfWeek.Sunday))
            {
                Init(dailyAtr);
                logger.Info(String.Format("Auto-reload on scheduled time {0} UTC executed successfully", time));
                Robot.Print("Auto-reload on scheduled time {0} UTC executed successfully", time);
                Robot.Notifications.SendEmail("marecica33@hotmail.com", "marek.balla@gmail.com", "Levels initialized", "test");
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
            int atrBasedPips = Params.UseAtrBasedStoppLossPips == true ? (int)Math.Round(Math.Max(DailyAtr * SL_DAILY_ATR_PERCENTAGE, MIN_STOP_LOSS_PIPS)) : Params.DefaultStopLossPips;
            if(Params.UseAtrBasedStoppLossPips == true)
            {
                logger.Info(String.Format("Using ATR based Stop Loss: Avg. Daily Atr (Pips): {0} * {1} percentage = {2} Pips", DailyAtr, SL_DAILY_ATR_PERCENTAGE, atrBasedPips));
                Robot.Print("Using ATR based Stop Loss: Avg. Daily Atr (Pips): {0} * {1} percentage = {2} Pips", DailyAtr, SL_DAILY_ATR_PERCENTAGE, atrBasedPips);
            }

            foreach (Level level in Levels)
            {
                logger.Info(level.ToString());
                Robot.Print(level);
                level.Id = Params.LevelFileName + "_" + idx;
                level.BeginBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                GetDirection(level, level.BeginBarIndex);
                level.EntryPrice = level.Direction == Direction.LONG ?
                    level.EntryPrice + Params.LevelOffset * Robot.Symbol.TickSize :
                    level.EntryPrice - Params.LevelOffset * Robot.Symbol.TickSize;
                if (level.StopLossPips == 0)
                {
                    level.StopLossPips = atrBasedPips;
                }
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
            logger.Info(String.Format("Number of levels loaded: {0}", Levels.Count));
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
                        logger.Info(String.Format("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]));
                        Robot.Print("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        level.LevelActivated = true;
                    }
                    if (Robot.MarketSeries.Low[i] >= level.DeactivatePrice && level.LevelActivated)
                    {
                        level.Traded = true;
                        logger.Info(String.Format("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]));
                        Robot.Print("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        break;
                    }
                    if (Robot.MarketSeries.Low[i] <= level.EntryPrice)
                    {
                        level.Traded = true;
                        logger.Info(String.Format("Level {0} marked as traded", level.Label));
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
                        logger.Info(String.Format("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]));
                        Robot.Print("Level {0} marked as activated at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        level.LevelActivated = true;
                    }
                    if (Robot.MarketSeries.High[i] <= level.DeactivatePrice && level.LevelActivated)
                    {
                        level.Traded = true;
                        logger.Info(String.Format("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]));
                        Robot.Print("Level {0} marked as cancelled at {1}", level.Label, Robot.MarketSeries.OpenTime[i]);
                        break;
                    }
                    if (Robot.MarketSeries.High[i] >= level.EntryPrice)
                    {
                        level.Traded = true;
                        logger.Info(String.Format("Level {0} marked as traded", level.Label));
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
                        double risk = Calculator.GetRisk(Params.RiskRewardRatio, Params.FixedRiskAmount);
                        double volume = Calculator.GetVolume(Robot.Symbol.Name, Params.PositionSize, Params.FixedRiskAmount, level.StopLossPips, trade);
                        string label = Utils.PositionLabel(Robot.SymbolName, Params.LevelFileName, Params.StrategyType.ToString());
                        string comment = "profit=" + risk + "&level=" + level.Label;
                        TradeResult result = Robot.PlaceLimitOrder(trade, Robot.Symbol.Name, volume, level.EntryPrice, label, level.StopLossPips, level.ProfitTargetPips, level.ValidTo, comment);

                        logger.Info(String.Format("Placing Limit Order Entry:{0} SL Pips:{1} Type: {2}", level.EntryPrice, level.StopLossPips, trade));
                        Robot.Print("Placing Limit Order Entry:{0} SL Pips:{1} Type: {2}", level.EntryPrice, level.StopLossPips, trade);

                        logger.Info(String.Format("Order placed for Level {0} Success: {1}  Error: {2}", result.PendingOrder.Label, result.IsSuccessful, result.Error));
                        Robot.Print("Order placed for Level {0} Success: {1}  Error: {2}", result.PendingOrder.Label, result.IsSuccessful, result.Error);
                    }
                    else
                    {
                        logger.Info(String.Format("Order skipped because of Calendar Event / Spike against"));
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
            bool isSpike = lastBarsVolatility >= DailyAtr * barsPercentage || lastBarVolatility > DailyAtr * barPercentage;
            if (isSpike)
            {
                logger.Info(String.Format("Spike detected. Volatility on last bar: {0} pips Volatility on last {1} bars: {2} pips. Avg Daily Atr was {3}", lastBarVolatility, barsCount, lastBarsVolatility, DailyAtr));
                Robot.Print("Spike detected. Volatility on last bar: {0} pips Volatility on last {1} bars: {2} pips. Avg Daily Atr was {3}", lastBarVolatility, barsCount, lastBarsVolatility, DailyAtr);
            }
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
                string label = Utils.PositionLabel(Robot.SymbolName, Params.LevelFileName, Params.StrategyType.ToString());
                Dictionary<String, String> attributes = Utils.ParseComment(order.Comment);
                if (order.Label == label && attributes["level"] == level.Label)
                {
                    logger.Info(String.Format("Order for level {0} cancelled. Reason: {1}", level.Label, reason));
                    Robot.Print("Order for level {0} cancelled. Reason: {1}", level.Label, reason);
                    Robot.CancelPendingOrder(order);
                }
            }
        }

        private void SendEmailNotification(double dailyAtr)
        {
            if (Robot.RunningMode == RunningMode.RealTime && Params.Email != null)
            {
                string body = "";
                body += "Levels initialized for Day " + Robot.Server.TimeInUtc + " UTC \r\n";
                body += "\r\n";
                body += "Level offset " + Params.LevelOffset + " ticks \r\n";
                body += "Level based on daily Atr " + dailyAtr + " pips \r\n";
                body += "Level offset " + Params.LevelOffset + " ticks \r\n";
                body += "\r\n";
                foreach (Level l in Levels)
                    body += l.ToString() + "\r\n";
                body += "\r\n";
                body += "Calendar events: \r\n";
                foreach(CalendarEntry c in Calendar.UpcomingEvents(Robot.SymbolName, Robot.Server.TimeInUtc))
                    body += c.ToString() + "\r\n";
                body += "\r\n";
                body += "Check Levels on Forex expert plus https://www.forex-zone.cz/forex-expert-plus/klientska-sekce \r\n";
                body += "Check Forex calendar https://www.forexfactory.com/calendar.php \r\n";
                body += "\r\n";
                body += "Notification sent by LevelTrader on " + Robot.Server.TimeInUtc + " UTC";
                Robot.Notifications.SendEmail("larecica2@gmail.com", Params.Email,"LevelTrader initialized " + Robot.SymbolName + " " + Params.StrategyType, body);
            }
        }
    }
}
