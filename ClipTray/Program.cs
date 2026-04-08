using System;
using System.Windows.Forms;

namespace ClipTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MessageBox.Show("ClipTray is running!", "ClipTray", MessageBoxButtons.OK);
        }
    }
}
