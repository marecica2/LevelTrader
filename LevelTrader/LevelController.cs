﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using cAlgo.API;

namespace cAlgo
{
    public class LevelController
    {
        public Robot Robot { get; set; }

        public InputParams Params { get; set; }

        private List<Level> Levels;

        private LevelRenderer Renderer;

        private RiskCalculator Calculator;

        public LevelController(Robot robot, InputParams inputParams)
        {
            Robot = robot;
            Params = inputParams;
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
            if(Params.DailyReloadHour == time.Hour && Params.DailyReloadMinute == time.Minute && time.Second == 0)
            {
                Init();
                Robot.Print("Auto-reload on scheduled time {0} UTC executed successfully", time);
            }
        }

        private void Initialize(List<Level> levels)
        {
            int idx = 0;
            foreach (Level level in Levels)
            {
                level.Id = Params.LevelFileName + "_" + idx;
                level.BeginBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                CheckDirection(level, level.BeginBarIndex);
                level.EntryPrice = level.Direction == Direction.LONG ? level.EntryPrice + Params.LevelOffset * Robot.Symbol.TickSize : level.EntryPrice - Params.LevelOffset * Robot.Symbol.TickSize;
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

        private void CheckDirection(Level level, int levelFirstBarIndex)
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
                if(levelIsTradeable(level))
                {
                    if (level.Direction == Direction.LONG)
                    {
                        if (Robot.MarketSeries.Close[idx] <= level.ActivatePrice && !level.LevelActivated)
                        {
                            level.LevelActivated = true;
                            level.LevelActivatedIndex = idx;
                            double orderVolume = Calculator.GetVolume(Robot.Symbol.Name, Params.RiskRewardRatio, Params.StopLossPips, TradeType.Buy);
                            TradeResult result = Robot.PlaceLimitOrder(TradeType.Buy, Robot.Symbol.Name, orderVolume, level.EntryPrice, level.Label, level.StopLoss, level.ProfitTarget, level.ValidTo);
                            Robot.Print("Placed Limit order {0} {1}", result.IsSuccessful, result.PendingOrder.Label);
                            level.OrderCreated = true;
                            Renderer.RenderLevel(level);
                        }
                        if (Robot.MarketSeries.Close[idx] > level.DeactivatePrice && level.LevelActivated)
                        {
                            level.Traded = true;
                            Renderer.RenderLevel(level);
                            CancelPendingOrder(level);
                        }
                    }
                    else
                    {
                        if (Robot.MarketSeries.Close[idx] >= level.ActivatePrice && !level.LevelActivated)
                        {
                            level.LevelActivated = true;
                            level.LevelActivatedIndex = idx;
                            double orderVolume = Calculator.GetVolume(Robot.Symbol.Name, Params.RiskRewardRatio, Params.StopLossPips, TradeType.Sell);
                            TradeResult result = Robot.PlaceLimitOrder(TradeType.Sell, Robot.Symbol.Name, orderVolume, level.EntryPrice, level.Label, level.StopLoss, level.ProfitTarget, level.ValidTo);
                            Robot.Print("Placed Limit order {0} {1}", result.IsSuccessful, result.PendingOrder.Label);
                            level.OrderCreated = true;
                            Renderer.RenderLevel(level);
                        }
                        if (Robot.MarketSeries.Close[idx] < level.DeactivatePrice && level.LevelActivated)
                        {
                            level.Traded = true;
                            Renderer.RenderLevel(level);
                            CancelPendingOrder(level);
                        }
                    }
                }
            }
        }

        private bool levelIsTradeable(Level level)
        {
            return level.ValidFrom < Robot.Server.TimeInUtc && Robot.Server.TimeInUtc < level.ValidTo && !level.Traded;
        }

        private void CancelPendingOrder(Level level)
        {
            foreach (var order in Robot.PendingOrders)
            {
                if (order.Label == level.Label)
                {
                    Robot.Print("Order for level {0} cancelled. Reason OFF level reached", level.Label);
                    Robot.CancelPendingOrder(order);
                }
            }
        }


    }
}
