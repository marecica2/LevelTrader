using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using cAlgo.API;
using NLog;

namespace cAlgo
{
    public class Calendar
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static WebClient client = new WebClient();
        private static string URL = "http://cdn-nfs.faireconomy.media/ff_calendar_thisweek.xml";
        private Robot Robot { get; set; }
        private InputParams Params { get; set; }
        private List<CalendarEntry> Entries = new List<CalendarEntry>();
        public DateTime? PausedUntil { get; private set; }
        private bool Paused = false;

        public Calendar(Robot robot, InputParams inputParams)
        {
            this.Params = inputParams;
            this.Robot = robot;
        }

        public void Init()
        {
            XDocument xml = LoadXml();
            if(xml != null)
            {
                Entries = Parse(xml);
                foreach (CalendarEntry entry in Entries)
                {
                    entry.EventTimeBefore = entry.EventTime.AddMinutes(-Params.CalendarEventDuration);
                    entry.EventTimeAfter = entry.EventTime.AddMinutes(Params.CalendarEventDuration);
                }
                RefreshCalendar();
            } else
            {
                Entries = new List<CalendarEntry>();
            }
        }

        public void OnMinute()
        {
            DateTime time = Robot.Server.TimeInUtc;
            if (0 == time.Hour && 5 == time.Minute)
            {
                Robot.Print("Calendar refreshed automatically");
                logger.Info("Calendar refreshed automatically");
                Init();
            }
            RefreshCalendar();
        }

        public void RefreshCalendar()
        {
            List<CalendarEntry> events = UpcomingEvents(Robot.Symbol.Name, Robot.Server.Time);
            string text = "";
            foreach (var evt in events)
            {
                // if(evt.EventImpact >= Impact.MEDIUM)
                    text += evt.Country + " " + evt.EventImpact + " " + evt.EventTime + " " + Utils.Truncate(evt.Comment, 30) + "\r\n";
            }
            Robot.Chart.DrawStaticText("calendar", text, VerticalAlignment.Bottom, HorizontalAlignment.Left, Color.Gray);
        }

        public bool IsPaused(DateTime ?optTime)
        {
            DateTime time = optTime.HasValue? optTime.Value : Robot.Server.TimeInUtc;
            if(time.DayOfWeek == DayOfWeek.Friday && (time.Hour > 20 && time.Minute > 0 ) && ( time.Hour < 23 && time.Minute < 59))
                return true;

            DateTime ?pausedUntil = GetEventsInAdvance(Robot.Symbol.Name, time);
            if (pausedUntil != null)
            {
                PausedUntil = pausedUntil;
                logger.Debug(String.Format("Impact events until {0}", PausedUntil.Value));
                return true;
            }

            if(PausedUntil != null && time < PausedUntil)
            {
                return true;
            }

            if (PausedUntil != null && time > PausedUntil)
            {
                logger.Debug(String.Format("Impact Events finished"));
                PausedUntil = null;
                return false;
            }
            return false;
        }

        public List<CalendarEntry> UpcomingEvents(string symbol, DateTime time)
        {
            symbol = MapSymbolToCountry(symbol);
            int maxEvents = 7;
            int count = 0;
            List<CalendarEntry> events = new List<CalendarEntry>();
            foreach (CalendarEntry entry in Entries)
            {
                if (symbol.Contains(entry.Country) && time <= entry.EventTimeAfter && count < maxEvents)
                {
                    events.Add(entry);
                    count++;
                }
            }
            return events;
        }

        private DateTime? GetEventsInAdvance(string symbol, DateTime time)
        {
            symbol = MapSymbolToCountry(symbol);
            CalendarEntry last = null;
            List<CalendarEntry> group = new List<CalendarEntry>();
            foreach (CalendarEntry entry in Entries)
            {
                if (symbol.Contains(entry.Country) && DateTime.Compare(entry.EventTimeBefore, time) <= 0 && DateTime.Compare(time, entry.EventTimeAfter) <= 0)
                {
                    if (last == null || last.EventTime == entry.EventTime)
                        group.Add(entry);
                    last = entry;
                }
            }

            bool hasImpact = false;
            foreach (CalendarEntry entry in group)
            {
                if (hasImpact == false && entry.EventImpact >= Impact.MEDIUM)
                {
                    hasImpact = true;
                }
            }

            if (hasImpact && group.Count > 0)
            {
                //foreach (CalendarEntry entry in group)
                //    Robot.Print("Calendar entry " + entry.ToString());
                return group[0].EventTime.AddMinutes(Params.CalendarEventDuration * group.Count);
            }
            return null;
        }

        private string MapSymbolToCountry(string symbol)
        {
            if (symbol == "US30")
                return "USD";
            return symbol;
        }

        private XDocument LoadXml()
        {
            string filePath = Params.LevelFilePath;
            DateTime time = Robot.Server.TimeInUtc;
            int week = Utils.GetWeekOfYear(time);
            if(time.DayOfWeek == DayOfWeek.Sunday)
                week += 1;
            if (Robot.RunningMode != RunningMode.RealTime)
            {
                filePath = Params.BackTestPath + "\\calendar-" + time.Year + "-" + week + ".xml";
                if (!File.Exists(filePath))
                {
                    Robot.Print("Calendar on path {0} does not exist", filePath);
                    logger.Info(String.Format("Calendar on path {0} does not exist", filePath));
                    return null;
                }
                logger.Info(String.Format("Calendar file {0} initialized", filePath));
                Robot.Print("Calendar file {0} initialized", filePath);
                return XDocument.Load(filePath);
            } else
            {
                string xml = Fetch();
                logger.Info(String.Format("Calendar for year {0} week {1} initialized", time.Year, week));
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
            return Country + " " + EventImpact + " " + EventTime + " " + Comment + " <" + EventTimeBefore + " - " +  EventTimeAfter + ">";
        }
    }

    public enum Impact
    {
        HOLIDAY,
        LOW,
        MEDIUM,
        HIGH,
    }

}
