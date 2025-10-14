using AntdUI;
using StarResonanceDpsAnalysis.Forms;
using StarResonanceDpsAnalysis.Forms.ModuleForm;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.LaunchFunction;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // Temporarily do nothing
            };
            Application.ThreadException += (sender, e) =>
            {
                // Temporarily do nothing
            };

            //Console.OutputEncoding = Encoding.UTF8;
            //Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // Key

           // Set AntdUI global DPI scaling based on primary screen resolution: 1080p=1.0, 2K≈1.33, 4K=2.0
           // float dpiScale = GetPrimaryResolutionScale();
            AppConfig.dpi = AntdUI.Config.Dpi;

            if (!AppConfig.GetConfigExists()) 
            {
                AppConfig.Language = "en";
            }
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(AppConfig.Language);

            AntdUI.Config.TextRenderingHighQuality = true;

            

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.

            ApplicationConfiguration.Initialize();
            FormManager.dpsStatistics = new DpsStatisticsForm();
            Application.Run(FormManager.dpsStatistics);

            //Application.Run(new ModuleCalculationForm());
        }

        private static float GetPrimaryResolutionScale()
        {
            try
            {
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                // Determine by height: 1080->1.0, 1440->1.33, >=2160->2.0
                if (bounds.Height >= 2160) return 2.0f;       // 4K
                if (bounds.Height >= 1440) return 1.3333f;    // 2K
                return 1.0f;                                   // 1080p and below
            }
            catch
            {
                return 1.0f;
            }
        }
    }
}