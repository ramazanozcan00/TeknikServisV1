using System;

namespace TeknikServis.Web.Extensions
{
    public static class DateTimeExtensions
    {
        // İş Günü Ekleme (Bitiş Tarihini Bulur)
        public static DateTime AddBusinessDays(this DateTime startDate, int days)
        {
            DateTime targetDate = startDate;
            int count = 0;

            while (count < days)
            {
                targetDate = targetDate.AddDays(1);
                if (targetDate.DayOfWeek != DayOfWeek.Saturday &&
                    targetDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    count++;
                }
            }
            return targetDate;
        }

        // Kalan İş Gününü Hesaplama (YENİ METOT)
        public static int GetRemainingBusinessDays(this DateTime endDate)
        {
            DateTime today = DateTime.Now.Date;
            if (today >= endDate.Date) return 0;

            int businessDays = 0;
            DateTime current = today.AddDays(1); // Bugünü saymıyorsak yarından başla

            while (current.Date <= endDate.Date)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday &&
                    current.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                current = current.AddDays(1);
            }

            return businessDays;
        }
    }
}