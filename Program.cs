using System;
using System.Windows.Forms;

namespace SmartpageTimetableDuplicateV1
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
