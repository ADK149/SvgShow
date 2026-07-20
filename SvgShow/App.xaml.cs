using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace SvgShow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string? StartupSvgFile { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                var arg = e.Args[0];
                if (File.Exists(arg) && arg.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    StartupSvgFile = arg;
                }
            }
        }
    }

}
