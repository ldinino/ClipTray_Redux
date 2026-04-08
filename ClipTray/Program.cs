using System;
using System.Threading;
using System.Windows.Forms;
using ClipTray.UI;

namespace ClipTray
{
    static class Program
    {
        private const string MutexName = "ClipTray_SingleInstance_Mutex";

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
        }
    }
}
