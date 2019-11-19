using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace cAlgo
{
    class LevelParser
    {
        public List<Level> Parse(XDocument xml, InputParams parameters)
        {
            return (
                from c in xml.Root.Descendants("Level")
                where (string)c.Attribute("Instrument") == parameters.Instrument
                select new Level
                {
                    Symbol = (string)c.Attribute("Instrument"),
                    EntryPrice = (double)c.Attribute("Price"),
                    Label = (string)c.Element("Caption").Attribute("Text"),
                    ValidFrom = ParseDateTime(c.Element("StartTime").Value, parameters),
                    ValidTo = ParseDateTime(c.Element("EndTime").Value, parameters)
                }).ToList();
        }

        private DateTime ParseDateTime(string val, InputParams parameters)
        {
            DateTime dateTime = DateTime.ParseExact(val, "yyyy-MM-dd_HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            dateTime = dateTime.AddHours(parameters.TimeZoneOffset);
            return dateTime;
        }
    }
}
