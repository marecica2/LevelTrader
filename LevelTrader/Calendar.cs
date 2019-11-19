using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using cAlgo.API;

namespace cAlgo
{
    class Calendar
    {
        private static WebClient client = new WebClient();
        private static string URL = "http://cdn-nfs.faireconomy.media/ff_calendar_thisweek.xml";

        private Robot Robot { get; set; }
        private InputParams Params { get; set; }
        private List<CalendarEntry> Entries;

        public Calendar(Robot robot, InputParams inputParams)
        {
            this.Params = inputParams;
            this.Robot = robot;
        }

        public void Init()
        {
            XDocument xml = LoadXml();
            Entries = Parse(xml);

            foreach (CalendarEntry entry in Entries)
            {
                entry.EventTimeBefore = entry.EventTime.AddMinutes(-Params.CalendarBeforeOffset);
                entry.EventTimeAfter = entry.EventTime.AddMinutes(Params.CalendarBeforeOffset);
                //if(entry.Country != country && entry.EventTime != time)
                //{
                //    time = entry.EventTime;
                //    country = entry.Country;
                //}
                //Robot.Print(entry);
            }
            OnBar();
        }

        public List<CalendarEntry> GetEvents(string symbol, DateTime time)
        {
            List<CalendarEntry> events = new List<CalendarEntry>();
            foreach (CalendarEntry entry in Entries)
            {
                if (symbol.Contains(entry.Country) &&
                    (entry.EventImpact == Impact.HIGH || entry.EventImpact == Impact.MEDIUM) &&
                    entry.EventTimeBefore <= time &&
                    entry.EventTimeAfter >= time
                   )
                    events.Add(entry);
            }
            if (events.Count == 0)
                return null;
            return events;
        }

        public void OnBar()
        {
            List<CalendarEntry> events = UpcomingEvents(Robot.Symbol.Name, DateTime.Now);

            string text = "";
            foreach (var evt in events)
            {
                text += evt.Country + " " + evt.EventImpact + " " + evt.EventTime + " " + Utils.Truncate(evt.Comment, 20) + "\r\n";
            }
            Robot.Chart.DrawStaticText("calendar", text, VerticalAlignment.Bottom, HorizontalAlignment.Left, Color.Gray);
            
        }

        public List<CalendarEntry> UpcomingEvents(string symbol, DateTime time)
        {
            int maxEvents = 5;
            int count = 0;
            List<CalendarEntry> events = new List<CalendarEntry>();
            foreach (CalendarEntry entry in Entries)
            {
                if (symbol.Contains(entry.Country) && time <= entry.EventTime && count < maxEvents)
                {
                    events.Add(entry);
                    count++;
                }
            }
            return events;
        }

        private XDocument LoadXml()
        {
            string filePath = Params.LevelFilePath;
            DateTime time = Robot.Server.TimeInUtc;
            int week = Utils.GetWeekOfYear(time);
            if (Robot.RunningMode != RunningMode.RealTime)
            {
                filePath = Params.BackTestPath + "\\calendar-" + time.Year + "-" + week + ".xml";
                Robot.Print("Calendar file {0} initialized", filePath);
                return XDocument.Load(filePath);
            } else
            {
                string xml = Fetch();
                Robot.Print("Calendar for year {0} week {1} initialized", time.Year, week);
                return XDocument.Parse(xml);
            }
        }

        private List<CalendarEntry> Parse(XDocument xml)
        {
            return (
                from c in xml.Root.Descendants("event")
                select new CalendarEntry
                {
                    Country = (string) c.Element("country").Value,
                    Comment = (string) c.Element("title").Value,
                    EventImpact = toImpact((string) c.Element("impact").Value),
                    EventTime = ParseDateTime(c.Element("date").Value + " " + c.Element("time").Value, Params),
                }).ToList();
        }

        private DateTime ParseDateTime(string val, InputParams parameters)
        {
            return DateTime.ParseExact(val, "MM-dd-yyyy h:mmtt", CultureInfo.InvariantCulture);
        }

        private Impact toImpact(string val)
        {
            if (val == "Low")
                return Impact.LOW;
            if (val == "Medium")
                return Impact.MEDIUM;
            if (val == "High")
                return Impact.HIGH;
            if (val == "Holiday")
                return Impact.HOLIDAY;
            return Impact.LOW;
        }

        private string Fetch()
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(URL);
            request.Method = "GET";
            String body = String.Empty;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                body = reader.ReadToEnd();
                reader.Close();
                dataStream.Close();
            }
            return body;
        }
    }


    public class CalendarEntry
    {
        public string Country { get; set; }
        public string Comment { get; set; }
        public Impact EventImpact { get; set; }
        public DateTime EventTime { get; set; }
        public DateTime EventTimeAfter { get; internal set; }
        public DateTime EventTimeBefore { get; internal set; }

        override public string ToString()
        {
            return Country + " " + Comment + " " + EventImpact + " " + EventTime;
        }
    }

    public enum Impact
    {
        LOW,
        MEDIUM,
        HIGH,
        HOLIDAY,
    }

}
