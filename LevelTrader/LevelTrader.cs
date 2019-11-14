using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {

        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }
        
        [Parameter(DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST\\2019-11-14 (46)\\FE_NT8.xml")]
        public string LevelFilePath { get; set; }

        private LevelDefinitions levels;

        protected override void OnStart()
        {
            LevelDefinitions levels = new LevelDefinitions(this, LevelFilePath, Symbol.Name);
            levels.init();
        }

        protected override void OnTick()
        {
            // Put your core logic here
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }


    public class LevelDefinitions
    {
        public Robot Robot { get; set; }
        public string LevelFilePath { get; set; }
        public string Instrument { get; set; }

        public LevelDefinitions(Robot robot, string LevelFilePath, string instrument)
        {
            this.Robot = robot;
            this.Instrument = instrument;
            this.LevelFilePath = LevelFilePath;
        }

        public void init()
        {
            var xml = XDocument.Load(LevelFilePath);

            List<LevelDefinition> levels = ( 
                from c in xml.Root.Descendants("Level")
                where (string)c.Attribute("Instrument") == Instrument
                select new LevelDefinition {
                    Symbol = (string) c.Attribute("Instrument") ,
                    EntryPrice = (double) c.Attribute("Price"),
                    Label = (string) c.Element("Caption").Attribute("Text"),
                    ValidFrom = parseValidity(c.Element("StartTime").Value),
                    ValidTo = parseValidity(c.Element("EndTime").Value)
            }).ToList();

            foreach (LevelDefinition level in levels)
            {
                this.Robot.Print(level);
                drawLevel(level);
            }
        }


        private void drawLevel(LevelDefinition definition)
        {
            this.Robot.Chart.DrawHorizontalLine(definition.Label, definition.EntryPrice, Color.AliceBlue, 2, LineStyle.Lines);
        }

        private DateTime parseValidity(string val)
        {
            return DateTime.ParseExact(val, "yyyy-MM-dd_HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public class LevelDefinition
    {
        public string Label { get; set; }
        public string Symbol { get; set; }
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitTargetPrice { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        override
        public string ToString()
        {
            return Label + " " + Symbol + " " + EntryPrice + " " + ValidFrom + " " + ValidTo;
        }
    }
}
