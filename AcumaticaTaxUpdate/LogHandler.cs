using System;

namespace AcumaticaTaxUpdate
{
    public static class LogHandler
    {
        public static void LogData(string descr, string errorLevel = "", string stackTrace = "")
        {
            var context = new DWTaxLog();
            var log = new tei_tax_update_logs
            {
                Descr = descr,
                ErrorLevel = errorLevel,
                StackTrace = stackTrace,
                LogDateTime = DateTime.Now
            };
            context.tei_tax_update_logs.Add(log);
            context.SaveChanges();
        }

    }
}
