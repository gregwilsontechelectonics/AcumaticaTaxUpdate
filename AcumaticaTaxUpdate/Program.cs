using System.ServiceProcess;


namespace AcumaticaTaxUpdate
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;

            ServicesToRun = new ServiceBase[]
            {
                new TaxUpdate()
            };

            ServiceBase.Run(ServicesToRun);
        }
    }
}
