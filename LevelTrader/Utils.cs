using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo
{
    class Utils
    {
        public static DateTime ParseDateTime(string val, string pattern)
        {
            return DateTime.ParseExact(val, pattern, CultureInfo.InvariantCulture);
        }

        public static int GetWeekOfYear(DateTime time)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }


        public static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            DateTime start = dt.AddDays(-1 * diff).Date;
            return new DateTime(start.Year, start.Month, start.Day, 0, 0, 0);
        }


        public static string Truncate(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
        }


        public static string PositionLabel(string symbol, string input, string strategy)
        {
            return symbol + "_" + input + "_" + strategy;
        }

        public static string Pad(string value, int maxChars)
        {
            if(value.Length > maxChars)
            {
                value = Truncate(value, maxChars - 3);
            }
            return value.PadRight(maxChars, ' ');
        }

        public static Dictionary<String, String> ParseComment(String str)
        {
            Dictionary<String, String> map = new Dictionary<string, string>();
            String[] entries = str.Split('&');
            foreach (String entry in entries)
            {
                String[] kv = entry.Split('=');
                map.Add(kv[0], kv[1]);
            }
            return map;
        }


        public static double CalculateMargin(string symbolCode, double volume, string currency, double leverage, Robot robot)
        {
            double margin = 0.0;
            if (!_symbolExist(symbolCode, robot) || symbolCode.Length != 6) return margin;

            currency = currency.ToUpper();
            double symbolRate = _symbolRate(symbolCode, robot);
            if (symbolRate == 0) return margin;


            string baseCurrency = symbolCode.Substring(0, 3).ToUpper();
            string subaCurrency = symbolCode.Substring(3, 3).ToUpper();

            if (currency.Equals(baseCurrency))
            {
                margin = volume / leverage;
            }
            else if (currency.Equals(subaCurrency))
            {
                margin = volume / leverage * symbolRate;
            }
            else
            {
                symbolRate = _symbolRate(baseCurrency + currency, robot);
                if (symbolRate == 0)
                {
                    symbolRate = _symbolRate(currency + baseCurrency, robot);
                    if (symbolRate == 0) return margin;
                    symbolRate = 1 / symbolRate;
                }
                margin = volume / leverage * symbolRate;
            }
            return (margin > 0) ? Math.Round(margin, 2) : 0;
        }

        private static bool _symbolExist(string symbolCode, Robot robot)
        {
            try
            {
                bool exist = robot.MarketData.GetSymbol(symbolCode) != null;
                return exist;
            } catch
            {
                return false;
            }
        }

        private static double _symbolRate(string symbolCode, Robot robot)
        {
            try
            {
            robot.Print("X", robot.Symbols.GetSymbol(symbolCode));
                double rate = robot.Symbols.GetSymbol(symbolCode).Bid;
                robot.Print("Symbol rate for {0} :  {1}", symbolCode, rate);
                return rate;
            }
            catch
            {
                return 0;
            }
        }
    }
}
