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

        public void Render(List<Level> Levels, bool paused)
        {
            foreach (Level level in Levels)
            {
                RenderLevel(level, paused);
            }
        }

        public void RenderLevel(Level level, bool paused = false)
        {
            // Robot.Print(level);
            bool inactive = level.Traded || level.Disabled || paused;
            string status = level.Traded ? "traded" : paused ? "paused" : level.Disabled ? "disabled" : "";
            string description = level.Label + " " + level.Direction + " " + status;
            string levelPrefix = level.Label + level.Uid;

            Color levelColor = inactive ? Color.Gray : Color.DarkBlue;
            Color zoneColor = inactive ? Color.LightGray : Color.LightBlue;
            Color slColor = inactive ? Color.LightGray : Color.LightCoral;
            Color ptColor = inactive ? Color.LightGray : Color.LimeGreen;

            Robot.Chart.DrawText(levelPrefix + "_label", description, level.ValidFrom, level.EntryPrice + 0.0002, levelColor);
            Robot.Chart.DrawTrendLine(levelPrefix, level.ValidFrom, level.EntryPrice, level.ValidTo, level.EntryPrice, levelColor, 2, LineStyle.LinesDots);

            Robot.Chart.DrawTrendLine(levelPrefix + "_labelActivate", level.ValidFrom, level.ActivatePrice, level.ValidTo, level.ActivatePrice, zoneColor, 2, LineStyle.Dots);
            Robot.Chart.DrawTrendLine(levelPrefix + "_labelDeactivate", level.ValidFrom, level.DeactivatePrice, level.ValidTo, level.DeactivatePrice, zoneColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(levelPrefix + "_SL_label", level.Label + " SL", level.ValidFrom, level.StopLossPrice + 0.0002, slColor);
            Robot.Chart.DrawTrendLine(levelPrefix + "_SL", level.ValidFrom, level.StopLossPrice, level.ValidTo, level.StopLossPrice, slColor, 2, LineStyle.Dots);

            Robot.Chart.DrawText(levelPrefix + "_PT_label", level.Label + " PT", level.ValidFrom, level.ProfitTargetPrice + 0.0002, ptColor);
            Robot.Chart.DrawTrendLine(levelPrefix + "_PT", level.ValidFrom, level.ProfitTargetPrice, level.ValidTo, level.ProfitTargetPrice, ptColor, 2, LineStyle.Dots);
        }
    }
}
