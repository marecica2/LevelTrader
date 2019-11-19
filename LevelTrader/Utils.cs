using System;
using System.Globalization;

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


        public static string Truncate(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
        }

        public static string Pad(string value, int maxChars)
        {
            if(value.Length > maxChars)
            {
                value = Truncate(value, maxChars - 3);
            }
            return value.PadRight(maxChars, ' ');
        }
    }
}
