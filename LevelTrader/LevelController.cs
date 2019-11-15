using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using cAlgo.API;

namespace cAlgo
{
    public class LevelController
    {
        public Robot Robot { get; set; }

        public InputParams Params { get; set; }

        private List<Level> Levels;

        public LevelController(Robot robot, InputParams inputParams)
        {
            Robot = robot;
            Params = inputParams;
        }

        public void Init()
        {
            var xml = XDocument.Load(Params.LevelFilePath);
            Levels = new LevelParser().Parse(xml, Params);
            Initialize(Levels);
            Analyze(Levels);
            DrawLevels(Levels);
        }



        public void Trade()
        {
            // Robot.Print("Busy with trading [Bid: " + Robot.Symbol.Bid + " Ask: " + Robot.Symbol.Ask + " ]");
        }

        private void Initialize(List<Level> levels)
        {
            foreach (Level level in Levels)
            {
                int levelFirstBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                CheckDirection(level, levelFirstBarIndex);
                if(level.Direction == Direction.LONG)
                {
                    level.StopLossPrice = level.EntryPrice - Params.StopLoss * Robot.Symbol.TickSize;
                    level.ProfitTargetPrice = level.EntryPrice + Params.StopLoss * Robot.Symbol.TickSize * Params.RiskRewardRatio;
                    level.ActivatePrice = level.EntryPrice + Params.StopLoss * Robot.Symbol.TickSize * (Params.LevelActivate / 100.0);
                    level.DeactivatePrice = level.EntryPrice + Params.StopLoss * Robot.Symbol.TickSize * (Params.LevelDeactivate / 100.0);
                }
                else
                {
                    level.StopLossPrice = level.EntryPrice + Params.StopLoss * Robot.Symbol.TickSize;
                    level.ProfitTargetPrice = level.EntryPrice - Params.StopLoss * Robot.Symbol.TickSize * Params.RiskRewardRatio;
                    level.ActivatePrice = level.EntryPrice - Params.StopLoss * Robot.Symbol.TickSize * (Params.LevelActivate / 100.0);
                    level.DeactivatePrice = level.EntryPrice - Params.StopLoss * Robot.Symbol.TickSize * (Params.LevelDeactivate / 100.0);
                }
            }
        }

        private void CheckDirection(Level level, int levelFirstBarIndex)
        {
            level.Direction = level.EntryPrice > Robot.MarketSeries.High[levelFirstBarIndex] ? Direction.SHORT : Direction.LONG;
        }

        private void Analyze(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                int levelFirstBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidFrom);
                int levelLastBarIndex = Robot.MarketSeries.OpenTime.GetIndexByTime(level.ValidTo);
                DateTime currentDate = Robot.MarketSeries.OpenTime.LastValue.Date;
                CheckTraded(level, levelFirstBarIndex, levelLastBarIndex);
            }
        }

        private void CheckTraded(Level level, int fromIndex, int toIndex)
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
                        Robot.Print("Level {0} marked as activated at {1}", level, Robot.MarketSeries.OpenTime[i]);
                        level.LevelActivated = true;
                    }
                    if (Robot.MarketSeries.High[i] <= level.DeactivatePrice && level.LevelActivated)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as cancelled at {1}", level, Robot.MarketSeries.OpenTime[i]);
                        break;
                    }
                    if (Robot.MarketSeries.High[i] >= level.EntryPrice)
                    {
                        level.Traded = true;
                        Robot.Print("Level {0} marked as traded", level);
                        break;
                    }
                }
            }
        }

        private void DrawLevels(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                Robot.Print(level);
                DrawLevelLine(level);
            }
        }

        private void DrawLevelLine(Level level)
        {
            string description = level.Label + " " + level.Direction + " " + (level.Traded ? "traded" : "");
            Robot.Chart.DrawText(level.Label + "_label", description , level.ValidFrom, level.EntryPrice + 0.0002, Color.DarkBlue);
            Robot.Chart.DrawTrendLine(level.Label, level.ValidFrom, level.EntryPrice, level.ValidTo, level.EntryPrice, Color.DarkBlue, 2, LineStyle.LinesDots);

            Robot.Chart.DrawTrendLine(level.Label + "_labelActivate", level.ValidFrom, level.ActivatePrice, level.ValidTo, level.ActivatePrice, Color.LightBlue, 2, LineStyle.Dots);

            Robot.Chart.DrawTrendLine(level.Label + "_labelDeactivate", level.ValidFrom, level.DeactivatePrice, level.ValidTo, level.DeactivatePrice, Color.LightBlue, 2, LineStyle.Dots);


            Robot.Chart.DrawText(level.Label + "_SL_label", level.Label + " SL", level.ValidFrom, level.StopLossPrice + 0.0002, Color.LightCoral);
            Robot.Chart.DrawTrendLine(level.Label + "_SL", level.ValidFrom, level.StopLossPrice, level.ValidTo, level.StopLossPrice, Color.LightCoral, 2, LineStyle.Dots);

            Robot.Chart.DrawText(level.Label + "_PT_label", level.Label + " PT" , level.ValidFrom, level.ProfitTargetPrice + 0.0002, Color.LimeGreen);
            Robot.Chart.DrawTrendLine(level.Label + "_PT", level.ValidFrom, level.ProfitTargetPrice, level.ValidTo, level.ProfitTargetPrice, Color.LimeGreen, 2, LineStyle.Dots);
        }
    }
}
