using System.Globalization;

namespace OrderManagementApp.Helpers
{
    public static class PersianDateHelper
    {
        private static readonly PersianCalendar _pc = new PersianCalendar();

        public static string ToPersianDate(this DateTime date)
        {
            return $"{_pc.GetYear(date):0000}/{_pc.GetMonth(date):00}/{_pc.GetDayOfMonth(date):00}";
        }

        public static DateTime ToGregorianDate(int year, int month, int day)
        {
            return _pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
    }
}