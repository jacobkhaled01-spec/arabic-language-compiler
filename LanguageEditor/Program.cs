using System;
using System.Text;
using System.Windows.Forms;

namespace LanguageEditor
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length > 0 && args[0] == "--capture")
            {
                CaptureHelper.RunCapture(args.Length > 1 ? args[1] : "");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
