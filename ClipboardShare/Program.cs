using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardShare
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool isNewInstance;
            string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            Mutex mtx = new Mutex(true, appName, out isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show("已经有一个程序实例正在运行");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
