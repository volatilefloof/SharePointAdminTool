using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace EntraGroupsApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsWebView2RuntimeAvailable())
            {
                MessageBox.Show(
                    "Microsoft Edge WebView2 Runtime is required. Please install it from https://developer.microsoft.com/en-us/microsoft-edge/webview2/.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            try
            {
                using (Form1 form = new Form1())
                {
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Startup Error:\n{ex.Message}\n\n" +
                    $"Inner Exception:\n{ex.InnerException?.Message}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}",
                    "Fatal Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ?? Keep these declared AFTER Main (or before if you prefer)
        private static bool IsWebView2RuntimeAvailable()
        {
            try
            {
                string versionInfo = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return !string.IsNullOrEmpty(versionInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 check failed: {ex.Message}");
                return false;
            }
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(args.Name);
            var path = assemblyName.Name + ".dll";

            if (!assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture))
            {
                path = $"{assemblyName.CultureInfo}\\{path}";
            }

            using var stream = executingAssembly.GetManifestResourceStream(path);
            if (stream == null)
                return null;

            var assemblyRawBytes = new byte[stream.Length];
            stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
            return Assembly.Load(assemblyRawBytes);
        }
    }
}
