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

        public void Render(List<Level> Levels, bool paused = false)
        {
            foreach (Level level in Levels)
            {
                RenderLevel(level, paused);
            }
        }

        public void RenderLevel(Level level, bool paused = false)
        {
            // Robot.Print(level);
            bool inactive = level.Traded || paused;
            string status = level.Traded ? "traded" : paused ? "paused" : "";
            string description = level.Label + " " + level.Direction + " " + status;

            Color levelColor = inactive ? Color.Gray : Color.DarkBlue;
            Color zoneColor = inactive ? Color.LightGray : Color.LightBlue;
            Color slColor = inactive ? Color.LightGray : Color.LightCoral;
            Color ptColor = inactive ? Color.LightGray : Color.LimeGreen;

            Robot.Chart.DrawText(level.Id + "_label", description, level.ValidFrom, level.EntryPrice + 0.0002, levelColor);
            Robot.Chart.DrawTrendLine(level.Id, level.ValidFrom, level.EntryPrice, level.ValidTo, level.EntryPrice, levelColor, 2, LineStyle.LinesDots);

            Robot.Chart.DrawTrendLine(level.Id + "_labelActivate", level.ValidFrom, level.ActivatePrice, level.ValidTo, level.ActivatePrice, zoneColor, 2, LineStyle.Dots);
            Robot.Chart.DrawTrendLine(level.Id + "_labelDeactivate", level.ValidFrom, level.DeactivatePrice, level.ValidTo, level.DeactivatePrice, zoneColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(level.Id + "_SL_label", level.Label + " SL", level.ValidFrom, level.StopLossPrice + 0.0002, slColor);
            Robot.Chart.DrawTrendLine(level.Id + "_SL", level.ValidFrom, level.StopLossPrice, level.ValidTo, level.StopLossPrice, slColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(level.Id + "_PT_label", level.Label + " PT", level.ValidFrom, level.ProfitTargetPrice + 0.0002, ptColor);
            Robot.Chart.DrawTrendLine(level.Id + "_PT", level.ValidFrom, level.ProfitTargetPrice, level.ValidTo, level.ProfitTargetPrice, ptColor, 2, LineStyle.Dots);
        }
    }
}
