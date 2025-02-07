
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AcumaticaTaxUpdate
{
    public partial class TaxUpdate : ServiceBase
    {
        private Timer timer = new Timer();

        /// <summary>
        /// OnStart for the service.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            LogHandler.LogData("AcumaticaTaxUpdate service started.", "INFORMATION");
            timer.Elapsed += new ElapsedEventHandler(AcumaticaTaxUpdate);
            timer.Interval = Convert.ToInt32(ConfigurationManager.AppSettings["MillisecondResponseRate"]);
            timer.Start();
        }

        /// <summary>
        /// On the Timer elapsed event, this method will check the date and determine if tax updates are necessary.
        /// If so, it will kick off the process to update the necessary tax records.
        /// </summary>
        private static void AcumaticaTaxUpdate(object source, ElapsedEventArgs e)
        {
            // Check to see if it is the last day of the month.
            if (CheckDate())
            {
                try
                {
                   // Run the process to update the taxes.
                   //UpdateTaxes();
                }
                catch (Exception ex)
                {
                    LogHandler.LogData(ex.Message, "ERROR", ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Checks the date to see if it is the last day of the month.
        /// </summary>
        private static bool CheckDate()
        {
            DateTime runDate = DateTime.Now;

            if (runDate.Day == DateTime.DaysInMonth(runDate.Year, runDate.Month))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Decodes a base64 encoded string
        /// </summary>
        /// <param name="value">The base64 encoded string.</param>
        /// <returns>string</returns>
        private static string DecryptString(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                // The initial string is reversed.
                value = Reverse(value);

                // Append '=' to the end of the string to make it base64 compliant.
                switch (value.Length % 4)
                {
                    case 2:
                        value += "==";
                        break;
                    case 3:
                        value += "=";
                        break;
                }

                if (!IsB64String(value)) throw new Exception("Value is not base64 encoded");

                byte[] data = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(data);
            }

            throw new Exception("Cannot decode empty string");
        }

        /// <summary>
        /// Creates an HTTPClient instance.
        /// </summary>
        /// <returns>HttpClient</returns>
        private static HttpClient GetClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Anything");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        /// <summary>
        /// Get's the required data string from the App.config, and populates it with the passed in Key Value Pairs. 
        /// </summary>
        /// <param name="configKey">The App.config key for the data string.</param>
        /// <param name="kvpList">The KeyValuePair list of values to update in the data string.</param>
        /// <returns>Task<string></returns>
        private static string GetDataString(string configKey, List<KeyValuePair<string, string>> kvpList)
        {
            // Get and decrypt the data string from the App.config.
            string data = DecryptString(ConfigurationManager.AppSettings[configKey]);

            // Update each value in the KeyValuePair list in the data string.
            foreach (KeyValuePair<string, string> kvp in kvpList)
            {
                data = data.Replace(kvp.Key, kvp.Value);
            }

            return data;
        }

        /// <summary>
        /// Creates the data string necessary for the update request. 
        /// </summary>
        /// <returns>string</returns>
        private static string GetLoginDataString()
        {
            // Get and decrypt the login values from the App.config.
            var kvpList = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("{uname}", DecryptString(ConfigurationManager.AppSettings["APIConU"])),
                new KeyValuePair<string, string>("{pword}", DecryptString(ConfigurationManager.AppSettings["APIConP"])),
                new KeyValuePair<string, string>("{tenant}", ConfigurationManager.AppSettings["APIConTenant"])
            };

            //Create the data string
            return GetDataString("Login", kvpList);
        }

        /// <summary>
        /// Creates the data string necessary for the update request. 
        /// </summary>
        /// <param name="record">The request data being sent.</param>
        /// <returns>string</returns>
        private static string GetUpdateDataString(string taxID, string taxRate, string reportingGroup)
        {
            // Get the values necessary for the data string in the request.
            var kvpList = new List<KeyValuePair<string, string>>() {
                    new KeyValuePair<string, string>("{taxID}", taxID),
                    new KeyValuePair<string, string>("{startDate}", DateTime.Today.ToString()),
                    new KeyValuePair<string, string>("{taxRate}", taxRate),
                    new KeyValuePair<string, string>("{reportinggroup}", reportingGroup)
                };

            //Create the data string
            return GetDataString("Update", kvpList);
        }

        /// <summary>
        /// Checks is string is base64 encoded.
        /// </summary>
        /// <param name="b64String">The base64 encoded string.</param>
        /// <returns>bool</returns>
        private static bool IsB64String(string base64String)
        {
            if (string.IsNullOrEmpty(base64String) || base64String.Length % 4 != 0
                || base64String.Contains(" ") || base64String.Contains("\t") || base64String.Contains("\r") || base64String.Contains("\n"))
                return false;

            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch (Exception ex)
            {
                LogHandler.LogData(ex.Message, "INFORMATION");
                return false;
            }
        }

        /// <summary>
        /// Logs into the Acumatica API. 
        /// </summary>
        /// <param name="client">The HTTP Client we will be using.</param>
        /// <returns>Task<string></returns>
        private async static Task<string> LogIntoClient(HttpClient client)
        {
            //Create and Post the request
            string data = GetLoginDataString();
            var requestContent = new StringContent(data, Encoding.UTF8, "application/json");
            string loginURL = DecryptString(ConfigurationManager.AppSettings["APIlogin"]);
            var loginResult = await PostRequest(requestContent, loginURL, client);

            // If an error is returned, throw an exception, as we can't proceed.
            if (loginResult.Contains("error"))
            {
                LogHandler.LogData(loginResult, "ERROR");
                throw new Exception(loginResult);
            }

            return loginResult;
        }
        
        /// <summary>
        /// Sends a Post request to the specified URL. 
        /// </summary>
        /// <param name="requestContent">the request data being sent.</param>
        /// <param name="URL">The API URL the request is being sent to.</param>
        /// <param name="client">The HTTP Client.</param>
        /// <returns>Task<string></returns>
        private static async Task<string> PostRequest(HttpContent requestContent, string URL, HttpClient client)
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
        private static async Task<string> PutRequest(HttpContent requestContent, string URL, HttpClient client)
        {
            HttpResponseMessage response = await client.PutAsync(URL, requestContent);
            var responseContent = response.Content;
            return responseContent.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Reverses a string. 
        /// </summary>
        /// <param name="value">the string to reverse.</param>
        /// <returns>Task<string></returns>
        private static string Reverse(string value)
        {
            char[] charArray = value.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
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
        private static void UpdateTaxes()
        {
            HttpClient client = GetClient();
            LogIntoClient(client).Result.ToString();
            string taxID = "";
            string reportingGroup = "";
            string taxRate = "";
            string updateURL = DecryptString(ConfigurationManager.AppSettings["APIUpdate"]);
            var context = new DWTaxData();
            var lastmonth = DateTime.Today.AddDays(-21);

            // Tax level can only be the following values, and is case sensitive: State, County, SD, City
            // Pass through taxID, raxRate, and reportingGroup
            // Update State Taxes
            var records = context.tei_tax_rates.Where(r => r.StateUpdated > lastmonth);

            if (records.Any())
            {
                List<tei_tax_rates> stateTaxes = new List<tei_tax_rates>
                {
                    records.Where(r => r.TaxType == "Sales").FirstOrDefault(),
                    records.Where(r => r.TaxType == "Use").FirstOrDefault()
                };

                stateTaxes.Select(r => UpdateTaxRecord(r.State + r.TaxType, r.RateState, r.TaxType == "Use" ? "Use" : "State", updateURL, client)).ToList();
            }

            // Update County Taxes
            records = context.tei_tax_rates.Where(r => r.CountyUpdated > lastmonth);

            if (records.Any())
            {
                records.AsEnumerable().Select(r => UpdateTaxRecord(r.z2t_ID + "County" + r.TaxType, r.RateCounty, r.TaxType == "Use" ? "Use" : "Local", updateURL, client)).ToList();
            }

            // Update Special District Taxes
            records = context.tei_tax_rates.Where(r => r.SDUpdated > lastmonth);

            if (records.Any())
            {
                records.AsEnumerable().Select(r => UpdateTaxRecord(r.z2t_ID + "SD" + r.TaxType, r.RateSpecialDistrict, r.TaxType == "Use" ? "Use" : "Local", updateURL, client)).ToList();
            }

            // Update Special District Taxes
            records = context.tei_tax_rates.Where(r => r.CityUpdated > lastmonth);

            if (records.Any())
            {
                records.AsEnumerable().Select(r => UpdateTaxRecord(r.z2t_ID + "City" + r.TaxType, r.RateCity, r.TaxType == "Use" ? "Use" : "Local", updateURL, client)).ToList();
            }
        }
        
        /// <summary>
        /// Parses the tax data, and sends an update to Acumatica. 
        /// </summary>
        /// <param name="record">The request data being sent.</param>
        /// <param name="URL">The API URL the request is being sent to.</param>
        /// <param name="client">The HTTP Client.</param>
        /// <returns>Task<string></returns>
        private async static Task<string> UpdateTaxRecord(string taxID, string taxRate, string reportingGroup, string URL, HttpClient client)
        {
            try
            {
                
                // Create and Put the request
                string data = GetUpdateDataString(taxID, taxRate, reportingGroup);
                LogHandler.LogData("Updating tax rate: " + data, "INFORMATION");
                var requestContent = new StringContent(data, Encoding.UTF8, "application/json");
                var updateResult = await PutRequest(requestContent, URL, client);

                // If an error is returned, log it, and proceed to the next record.
                if (updateResult.Contains("error"))
                {
                    LogHandler.LogData("Error updating tax rate: " + updateResult, "ERROR");
                }

                return updateResult;
            }
            catch (Exception ex)
            {
                // If an error is returned, log it, and proceed to the next record.
                LogHandler.LogData(ex.Message, "ERROR", ex.StackTrace);
                return string.Empty;
            }
        }

        /// <summary>
        /// OnStop for the service.
        /// </summary>
        protected override void OnStop()
        {
            LogHandler.LogData("AcumaticaTaxUpdate service stopped.", "INFORMATION");
        }
    }
}
