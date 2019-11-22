using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace cAlgo
{
    class LevelParser
    {
        public List<Level> Parse(XDocument xml, InputParams parameters, DateTime time)
        {
            DateTime defaultValidFrom = time;
            DateTime defaultValidTo = time;
            if (parameters.StrategyType == StrategyType.SWING)
            {
                defaultValidFrom = Utils.StartOfWeek(time, DayOfWeek.Monday);
                defaultValidFrom = defaultValidFrom.AddHours(9);
                defaultValidTo = defaultValidFrom.AddDays(7).AddMinutes(-1);
            }
            if (parameters.StrategyType == StrategyType.INVEST)
            {
                defaultValidFrom = Utils.StartOfWeek(time, DayOfWeek.Monday);
                defaultValidFrom = defaultValidFrom.AddHours(9);
                defaultValidTo = defaultValidFrom.AddDays(14).AddMinutes(-1);
            }

            return (
                from c in xml.Root.Descendants("Level")
                where (string)c.Attribute("Instrument") == parameters.Instrument
                select new Level
                {
                    Symbol = (string)c.Attribute("Instrument"),
                    EntryPrice = (double)c.Attribute("Price"),
                    Label = (string)c.Element("Caption").Attribute("Text"),
                    ValidFrom = parameters.StrategyType == StrategyType.ID ? ParseDateTime(c.Element("StartTime").Value, parameters) : defaultValidFrom,
                    ValidTo = parameters.StrategyType == StrategyType.ID ? ParseDateTime(c.Element("EndTime").Value, parameters) : defaultValidTo,
                    StopLossPips = getStopLoss(parameters.StrategyType, c),
                    ProfitTargetPips = getProfit(parameters.StrategyType, c),
                }).ToList();
        }


        private int getStopLoss(StrategyType strategy, XElement e)
        {
            if (strategy == StrategyType.SWING)
                return (int) Math.Abs((double)e.Element("LinkedLevels").Descendants("LinkedLevel").ElementAt(0).Element("Distance") / 10);
            return 0;
        }

        private int getProfit(StrategyType strategy, XElement e)
        {
            if (strategy == StrategyType.SWING)
                return (int) Math.Abs((double) e.Element("LinkedLevels").Descendants("LinkedLevel").ElementAt(1).Element("Distance") / 10);
            return 0;
        }

        private DateTime ParseDateTime(string val, InputParams parameters)
        {
            DateTime dateTime = DateTime.ParseExact(val, "yyyy-MM-dd_HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            dateTime = dateTime.AddHours(parameters.TimeZoneOffset);
            return dateTime;
        }
    }
}
