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
            this.Robot = robot;
            this.Params = inputParams;
        }

        public void init()
        {
            var xml = XDocument.Load(Params.LevelFilePath);
            Levels = new LevelParser().Parse(xml, Params);
            Analyze(Levels);
            DrawLevels(Levels);
        }

        public void trade()
        {
            Robot.Print("Busy with trading");
        }

        private void Analyze(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                DateTime date = this.Robot.MarketSeries.OpenTime.LastValue.Date;
                var firstBarIndex = this.Robot.MarketSeries.OpenTime.GetIndexByTime(date);
                // this.Robot.MarketSeries[firstBarIndex].Price;
            }
        }

        private void DrawLevels(List<Level> Levels)
        {
            foreach (Level level in Levels)
            {
                this.Robot.Print(level);
                DrawLevel(level);
            }
        }

        private void DrawLevel(Level definition)
        {
            this.Robot.Chart.DrawText(definition.Label + "_label", definition.Label, definition.ValidFrom, definition.EntryPrice + 0.0002, Color.DarkBlue);
            this.Robot.Chart.DrawTrendLine(definition.Label, definition.ValidFrom, definition.EntryPrice, definition.ValidTo, definition.EntryPrice, Color.DarkBlue, 2, LineStyle.LinesDots);
        }
    }
}
