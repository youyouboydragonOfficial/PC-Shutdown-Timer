using System;
using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace PCShutdownTimer
{
    public partial class AlertWindow : Window
    {
        private readonly Action _onExtend;
        private readonly Action _onCancel;
        private readonly Func<TimeSpan> _getRemainingTime;
        private readonly string _actionName;
        private readonly DispatcherTimer _updateTimer;

        public AlertWindow(string actionName, Func<TimeSpan> getRemainingTime, Action onExtend, Action onCancel)
        {
            InitializeComponent();
            _actionName = actionName;
            _getRemainingTime = getRemainingTime;
            _onExtend = onExtend;
            _onCancel = onCancel;

            TxtMessage.Text = $"まもなく指定時刻になり、【{_actionName}】が実行されます。";

            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch { }

            UpdateDisplay();

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += (s, e) => UpdateDisplay();
            _updateTimer.Start();
        }

        private void UpdateDisplay()
        {
            var remaining = _getRemainingTime();
            if (remaining.TotalSeconds <= 0)
            {
                TxtRemaining.Text = "まもなく実行されます...";
                _updateTimer.Stop();
                Close();
                return;
            }

            if (remaining.TotalHours >= 1)
            {
                TxtRemaining.Text = $"実行まで あと {(int)remaining.TotalHours}時間 {remaining.Minutes:D2}分 {remaining.Seconds:D2}秒";
            }
            else
            {
                TxtRemaining.Text = $"実行まで あと {remaining.Minutes:D2}分 {remaining.Seconds:D2}秒";
            }
        }

        private void BtnExtend_Click(object sender, RoutedEventArgs e)
        {
            _updateTimer.Stop();
            _onExtend?.Invoke();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _updateTimer.Stop();
            _onCancel?.Invoke();
            Close();
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            _updateTimer.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer.Stop();
            base.OnClosed(e);
        }
    }
}
