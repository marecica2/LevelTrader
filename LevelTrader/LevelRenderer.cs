using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo
{
    class LevelRenderer
    {
        private Robot Robot { get; set; }

        public LevelRenderer(Robot robot)
        {
            this.Robot = robot;
        }

        public void Render(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                RenderLevel(level);
            }
        }

        public void RenderLevel(Level level)
        {
            //Robot.Print(level);
            string description = level.Label + " " + level.Direction + " " + (level.Traded ? "traded" : "");

            Color levelColor = level.Traded ? Color.Gray : Color.DarkBlue;
            Color zoneColor = level.Traded ? Color.LightGray : Color.LightBlue;
            Color slColor = level.Traded ? Color.LightGray : Color.LightCoral;
            Color ptColor = level.Traded ? Color.LightGray : Color.LimeGreen;

            Robot.Chart.DrawText(level.Label + "_label", description, level.ValidFrom, level.EntryPrice + 0.0002, levelColor);
            Robot.Chart.DrawTrendLine(level.Label, level.ValidFrom, level.EntryPrice, level.ValidTo, level.EntryPrice, levelColor);

            Robot.Chart.DrawTrendLine(level.Label + "_labelActivate", level.ValidFrom, level.ActivatePrice, level.ValidTo, level.ActivatePrice, zoneColor, 2, LineStyle.Dots);
            Robot.Chart.DrawTrendLine(level.Label + "_labelDeactivate", level.ValidFrom, level.DeactivatePrice, level.ValidTo, level.DeactivatePrice, zoneColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(level.Label + "_SL_label", level.Label + " SL", level.ValidFrom, level.StopLossPrice + 0.0002, slColor);
            Robot.Chart.DrawTrendLine(level.Label + "_SL", level.ValidFrom, level.StopLossPrice, level.ValidTo, level.StopLossPrice, slColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(level.Label + "_PT_label", level.Label + " PT", level.ValidFrom, level.ProfitTargetPrice + 0.0002, ptColor);
            Robot.Chart.DrawTrendLine(level.Label + "_PT", level.ValidFrom, level.ProfitTargetPrice, level.ValidTo, level.ProfitTargetPrice, ptColor, 2, LineStyle.Dots);
        }
    }
}
