using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PCShutdownTimer
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex = null;
        private const string AppMutexName = "Global\\PCShutdownTimer_youyoubodragon_Mutex_2026";

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            try
            {
                _mutex = new Mutex(true, AppMutexName, out createdNew);
            }
            catch
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    "PCシャットダウン予約ツールは既に起動しています。\nタスクトレイ（画面右下の通知領域）をご確認ください。",
                    "多重起動防止",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                LogAndShowError("OnStartup", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
                catch { }
            }
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShowError("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogAndShowError("CurrentDomain_UnhandledException", ex);
            }
        }

        private void LogAndShowError(string source, Exception ex)
        {
            string msg = $"[{DateTime.Now}] {source}: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}";
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                File.AppendAllText(logPath, msg + "\n--------------------\n");
            }
            catch { }

            System.Windows.MessageBox.Show(msg, "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
