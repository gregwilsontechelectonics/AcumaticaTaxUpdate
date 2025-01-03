
using System;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcumaticaTaxUpdate
{
    public partial class TaxUpdate : ServiceBase
    {
        private static DateTime runDate = DateTime.Now;
        private static bool executionFlag = true;
        private static readonly Thread taxCheck = new Thread(AcumaticaTaxUpdate);
        private static int MillisecondResponseRate = Convert.ToInt32(ConfigurationManager.AppSettings["MillisecondResponseRate"]);

        /// <summary>
        /// Checks the date and determines if tax updates are necessary.
        /// </summary>
        private static void AcumaticaTaxUpdate()
        {
            do
            {
                Thread.Sleep(MillisecondResponseRate);
                CheckDate();
            } while (executionFlag == true);
        }

        /// <summary>
        /// Checks the date to see if it is the last day of the month.
        /// If so, tax updates is processed.
        /// </summary>
        private static void CheckDate()
        {
            runDate = DateTime.Now;

            if (runDate.Day == DateTime.DaysInMonth(runDate.Year, runDate.Month))
            {
                try
                {
                    UpdateTaxes();
                }
                catch(Exception ex)
                {
                    LogHandler.LogData(ex.Message, "ERROR", ex.StackTrace);
                }
                
            }
        }

        /// <summary>
        /// OnStart for the service.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            taxCheck.Start();
        }

        /// <summary>
        /// OnStop for the service.
        /// </summary>
        protected override void OnStop()
        {
            executionFlag = false;
        }

        /// <summary>
        /// Sends a Post request to the specified URL. 
        /// </summary>
        /// <param name="requestContent">the request data being sent.</param>
        /// <param name="URL">The API URL the request is being sent to.</param>
        /// <param name="client">The HTTP Client.</param>
        /// <returns>Task<string></returns>
        public static async Task<string> PostRequest(HttpContent requestContent, string URL, HttpClient client)
        {
            HttpResponseMessage response = await client.PostAsync(URL, requestContent);
            var responseContent = response.Content;
            return responseContent.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Sends a Put request to the specified URL. 
        /// </summary>
        /// <param name="requestContent">the request data being sent.</param>
        /// <param name="URL">The API URL the request is being sent to.</param>
        /// <param name="client">The HTTP Client.</param>
        /// <returns>Task<string></returns>
        public static async Task<string> PutRequest(HttpContent requestContent, string URL, HttpClient client)
        {
            HttpResponseMessage response = await client.PutAsync(URL, requestContent);
            var responseContent = response.Content;
            return responseContent.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Service initializer.
        /// </summary>
        public TaxUpdate()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Retrieves the tax data from the database, and determines if updates are necessary.
        /// </summary>
        private async static void UpdateTaxes()
        {
            var context = new DWTaxData();

            if (context.vw_Taxes_To_Update.Any())
            {
                var baseURL = ConfigurationManager.AppSettings["APIBaseURL"];
                var loginURL = baseURL + ConfigurationManager.AppSettings["APIloginURL"];
                var updateURL = baseURL + ConfigurationManager.AppSettings["APIUpdateURL"];
                var data = "{\"name\":\"{uname}\",\"password\":\"{pword}\",\"locale\":\"en-US\",\"tenant\":\"{tenant}\"}";
                data.Replace("{uname}", ConfigurationManager.AppSettings["APIConUName"]);
                data.Replace("{pword}", ConfigurationManager.AppSettings["APIConPW"]);
                data.Replace("{tenant}", ConfigurationManager.AppSettings["APIConTenant"]);
                var client = new HttpClient();
                var requestContent = new StringContent(data, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "Anything");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                await PostRequest(requestContent, loginURL, client);
                context.vw_Taxes_To_Update.AsEnumerable().Select(r => UpdateTaxRecord(r, updateURL, client)).ToList();
            }
        }

        /// <summary>
        /// Parses the tax data, and sends an update to Acumatica. 
        /// </summary>
        /// <param name="record">The request data being sent.</param>
        /// <param name="URL">The API URL the request is being sent to.</param>
        /// <param name="client">The HTTP Client.</param>
        /// <returns>Task<string></returns>
        private async static Task<string> UpdateTaxRecord(vw_Taxes_To_Update record, string URL, HttpClient client)
        {
            try
            {
                // We may need to put the time in UTC format...
                // TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                // var startDate = TimeZoneInfo.ConvertTimeToUtc(DateTime.Today, tz);
                var data = "{\"TaxID\":{\"value\":\"{taxID}\"},\"TaxSchedule\":[{\"StartDate\":{\"value\": \"{startDate}\"},\"TaxRate\":{\"value\":{taxRate}},\"MinTaxableAmount\":{\"value\":0.0000},\"MaxTaxableAmount\":{\"value\":0.0000},\"ReportingGroup\":{\"value\":\"Default Output Group\"},\"custom\":{}}]}";
                data.Replace("{taxID}", record.TaxID);
                data.Replace("{startDate}", DateTime.Today.ToString());
                data.Replace("{taxRate}", record.TaxRate);
                LogHandler.LogData(data, "INFORMATION");
                var requestContent = new StringContent(data, Encoding.UTF8, "application/json");
                return await PutRequest(requestContent, URL, client);
            }
            catch (Exception ex)
            {
                LogHandler.LogData(ex.Message, "ERROR", ex.StackTrace);
                return "";
            }
        }
    }
}
