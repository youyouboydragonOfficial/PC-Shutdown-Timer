using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;

namespace PCShutdownTimer
{
    public partial class MainWindow : Window
    {
        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        private Forms.NotifyIcon? _notifyIcon;
        private DispatcherTimer? _timer;
        private bool _isReserved = false;
        private DateTime _targetTime;
        private DateTime _startTime;
        private DateTime _noticeTime;
        private bool _noticeShown = false;
        private AlertWindow? _activeAlertWindow = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeFormElements();
            InitializeNotifyIcon();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void InitializeFormElements()
        {
            // Populate Hours (00 - 23)
            for (int i = 0; i < 24; i++)
            {
                CmbTargetHour.Items.Add(i.ToString("D2"));
                CmbDurationHours.Items.Add(i.ToString());
            }

            // Populate Minutes (00 - 59)
            for (int i = 0; i < 60; i++)
            {
                CmbTargetMinute.Items.Add(i.ToString("D2"));
                CmbDurationMinutes.Items.Add(i.ToString());
                CmbDurationSeconds.Items.Add(i.ToString());
            }

            // Default specific time: Today + 1 hour (rounded to 5 minutes)
            var now = DateTime.Now;
            DpTargetDate.SelectedDate = now.Date;
            var defaultTarget = now.AddHours(1);
            int roundedMinute = (defaultTarget.Minute / 5) * 5;
            CmbTargetHour.SelectedItem = defaultTarget.Hour.ToString("D2");
            CmbTargetMinute.SelectedItem = roundedMinute.ToString("D2");

            // Default countdown: 0h 30m 00s
            CmbDurationHours.SelectedItem = "0";
            CmbDurationMinutes.SelectedItem = "30";
            CmbDurationSeconds.SelectedItem = "0";
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "PCシャットダウン予約ツール (待機中)",
                Visible = true
            };

            // Load app icon for notify icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            var contextMenu = new Forms.ContextMenuStrip();
            var itemOpen = new Forms.ToolStripMenuItem("表示 (&O)", null, (s, e) => RestoreFromTray());
            var itemCancel = new Forms.ToolStripMenuItem("予約キャンセル (&C)", null, (s, e) => CancelReservation());
            var itemSeparator = new Forms.ToolStripSeparator();
            var itemExit = new Forms.ToolStripMenuItem("終了 (&X)", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                WpfApplication.Current.Shutdown();
            });

            contextMenu.Items.AddRange(new Forms.ToolStripItem[] { itemOpen, itemCancel, itemSeparator, itemExit });
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isReserved)
            {
                var result = WpfMessageBox.Show(
                    "シャットダウンの予約が実行中です。\nタスクトレイに最小化して予約を継続しますか？\n（[いいえ] を選ぶと予約を破棄してアプリを完全に終了します）",
                    "予約実行中の終了確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void HideToTray()
        {
            Hide();
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, "PCシャットダウン予約ツール", "バックグラウンドで予約を監視中です。ダブルクリックで再表示できます。", Forms.ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelSpecificTime == null || PanelCountdown == null) return;

            if (RbModeSpecificTime.IsChecked == true)
            {
                PanelSpecificTime.Visibility = Visibility.Visible;
                PanelCountdown.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelSpecificTime.Visibility = Visibility.Collapsed;
                PanelCountdown.Visibility = Visibility.Visible;
            }
        }

        private void ChkAdvanceNotice_Changed(object sender, RoutedEventArgs e)
        {
            if (CmbNoticeMinutes != null)
            {
                CmbNoticeMinutes.IsEnabled = ChkAdvanceNotice.IsChecked == true;
            }
        }

        // ================= PRESETS =================
        private void Preset_AddMinutes_30(object sender, RoutedEventArgs e) => SetSpecificTimeRelative(TimeSpan.FromMinutes(30));
        private void Preset_AddHours_1(object sender, RoutedEventArgs e) => SetSpecificTimeRelative(TimeSpan.FromHours(1));
        private void Preset_AddHours_2(object sender, RoutedEventArgs e) => SetSpecificTimeRelative(TimeSpan.FromHours(2));

        private void Preset_Today_2300(object sender, RoutedEventArgs e)
        {
            DpTargetDate.SelectedDate = DateTime.Now.Date;
            CmbTargetHour.SelectedItem = "23";
            CmbTargetMinute.SelectedItem = "00";
        }

        private void Preset_Tomorrow_0000(object sender, RoutedEventArgs e)
        {
            DpTargetDate.SelectedDate = DateTime.Now.Date.AddDays(1);
            CmbTargetHour.SelectedItem = "00";
            CmbTargetMinute.SelectedItem = "00";
        }

        private void Preset_Tomorrow_0200(object sender, RoutedEventArgs e)
        {
            DpTargetDate.SelectedDate = DateTime.Now.Date.AddDays(1);
            CmbTargetHour.SelectedItem = "02";
            CmbTargetMinute.SelectedItem = "00";
        }

        private void SetSpecificTimeRelative(TimeSpan offset)
        {
            var target = DateTime.Now.Add(offset);
            DpTargetDate.SelectedDate = target.Date;
            CmbTargetHour.SelectedItem = target.Hour.ToString("D2");
            CmbTargetMinute.SelectedItem = target.Minute.ToString("D2");
        }

        private void Preset_Cd_15(object sender, RoutedEventArgs e) => SetCountdown(0, 15, 0);
        private void Preset_Cd_30(object sender, RoutedEventArgs e) => SetCountdown(0, 30, 0);
        private void Preset_Cd_45(object sender, RoutedEventArgs e) => SetCountdown(0, 45, 0);
        private void Preset_Cd_60(object sender, RoutedEventArgs e) => SetCountdown(1, 0, 0);
        private void Preset_Cd_90(object sender, RoutedEventArgs e) => SetCountdown(1, 30, 0);
        private void Preset_Cd_120(object sender, RoutedEventArgs e) => SetCountdown(2, 0, 0);

        private void SetCountdown(int h, int m, int s)
        {
            CmbDurationHours.SelectedItem = h.ToString();
            CmbDurationMinutes.SelectedItem = m.ToString();
            CmbDurationSeconds.SelectedItem = s.ToString();
        }

        // ================= MAIN ACTION (START / CANCEL) =================
        private void BtnMainAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isReserved)
            {
                CancelReservation();
            }
            else
            {
                StartReservation();
            }
        }

        private void StartReservation()
        {
            DateTime now = DateTime.Now;
            DateTime target;

            if (RbModeSpecificTime.IsChecked == true)
            {
                if (!DpTargetDate.SelectedDate.HasValue)
                {
                    WpfMessageBox.Show("予約日を選択してください。", "入力確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int hour = int.Parse(CmbTargetHour.SelectedItem?.ToString() ?? "0");
                int minute = int.Parse(CmbTargetMinute.SelectedItem?.ToString() ?? "0");
                var date = DpTargetDate.SelectedDate.Value.Date;
                target = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

                if (target <= now)
                {
                    var ask = WpfMessageBox.Show(
                        "指定時刻が現在時刻よりも前になっています。\n明日の同じ時刻で予約しますか？",
                        "日時の確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (ask == MessageBoxResult.Yes)
                    {
                        target = target.AddDays(1);
                        DpTargetDate.SelectedDate = target.Date;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else
            {
                int h = int.Parse(CmbDurationHours.SelectedItem?.ToString() ?? "0");
                int m = int.Parse(CmbDurationMinutes.SelectedItem?.ToString() ?? "0");
                int s = int.Parse(CmbDurationSeconds.SelectedItem?.ToString() ?? "0");
                var duration = new TimeSpan(h, m, s);

                if (duration.TotalSeconds <= 0)
                {
                    WpfMessageBox.Show("1秒以上の時間を設定してください。", "入力確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                target = now.Add(duration);
            }

            _startTime = now;
            _targetTime = target;
            _isReserved = true;
            _noticeShown = false;

            // Notice time calculation
            int noticeMinutes = GetNoticeMinutes();
            if (ChkAdvanceNotice.IsChecked == true && noticeMinutes > 0)
            {
                _noticeTime = _targetTime.Subtract(TimeSpan.FromMinutes(noticeMinutes));
                if (_noticeTime <= now)
                {
                    // Notice time is already past or too soon
                    _noticeShown = true;
                    TxtNoticeTimeInfo.Text = "時間が近いため即時警告 (通知済み)";
                }
                else
                {
                    TxtNoticeTimeInfo.Text = $"{_noticeTime:HH:mm:ss} ({noticeMinutes}分前)";
                }
            }
            else
            {
                TxtNoticeTimeInfo.Text = "事前通知なし";
            }

            string actionName = GetSelectedActionName();
            TxtActiveActionTitle.Text = actionName;
            TxtTargetTimeInfo.Text = $"{_targetTime:yyyy/MM/dd HH:mm:ss}";

            // Update UI State
            CardActiveStatus.Visibility = Visibility.Visible;
            StatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(69, 10, 10));
            StatusBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
            StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            StatusText.Text = $"予約中 ({actionName})";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 202, 202));

            BtnMainAction.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
            TxtMainActionIcon.Text = "✕";
            TxtMainActionLabel.Text = "予約をキャンセルする";

            SetInputsEnabled(false);

            _timer?.Start();
            UpdateTimerDisplay();

            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"予約中: {_targetTime:HH:mm} に{actionName}";
                _notifyIcon.ShowBalloonTip(3000, "シャットダウン予約完了", $"{_targetTime:yyyy/MM/dd HH:mm:ss} に【{actionName}】が実行されます。", Forms.ToolTipIcon.Info);
            }
        }

        private void CancelReservation()
        {
            if (!_isReserved) return;

            _timer?.Stop();
            _isReserved = false;

            if (_activeAlertWindow != null)
            {
                _activeAlertWindow.Close();
                _activeAlertWindow = null;
            }

            // Restore UI State
            CardActiveStatus.Visibility = Visibility.Collapsed;
            StatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 78, 59));
            StatusBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
            StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            StatusText.Text = "待機中 (未予約)";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 231, 183));

            BtnMainAction.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199));
            TxtMainActionIcon.Text = "⏻";
            TxtMainActionLabel.Text = "予約を開始する";

            SetInputsEnabled(true);

            if (_notifyIcon != null)
            {
                _notifyIcon.Text = "PCシャットダウン予約ツール (待機中)";
                _notifyIcon.ShowBalloonTip(2000, "予約キャンセル", "シャットダウン予約をキャンセルしました。", Forms.ToolTipIcon.Warning);
            }
        }

        private void SetInputsEnabled(bool enabled)
        {
            RbShutdown.IsEnabled = enabled;
            RbRestart.IsEnabled = enabled;
            RbSleep.IsEnabled = enabled;
            RbSignOut.IsEnabled = enabled;
            ChkForce.IsEnabled = enabled;
            RbModeSpecificTime.IsEnabled = enabled;
            RbModeCountdown.IsEnabled = enabled;
            DpTargetDate.IsEnabled = enabled;
            CmbTargetHour.IsEnabled = enabled;
            CmbTargetMinute.IsEnabled = enabled;
            CmbDurationHours.IsEnabled = enabled;
            CmbDurationMinutes.IsEnabled = enabled;
            CmbDurationSeconds.IsEnabled = enabled;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isReserved) return;

            var now = DateTime.Now;
            var remaining = _targetTime - now;

            // Advance notice trigger check
            if (ChkAdvanceNotice.IsChecked == true && !_noticeShown && now >= _noticeTime && remaining.TotalSeconds > 5)
            {
                _noticeShown = true;
                ShowAdvanceNotice();
            }

            // Time expired
            if (remaining.TotalSeconds <= 0)
            {
                _timer?.Stop();
                _isReserved = false;
                ExecuteAction();
                return;
            }

            UpdateTimerDisplay();
        }

        private void UpdateTimerDisplay()
        {
            var remaining = _targetTime - DateTime.Now;
            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;

            TxtCountdownDisplay.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)remaining.TotalHours,
                remaining.Minutes,
                remaining.Seconds);

            // Calculate progress percentage
            double totalDuration = (_targetTime - _startTime).TotalSeconds;
            if (totalDuration > 0)
            {
                double elapsed = (DateTime.Now - _startTime).TotalSeconds;
                double progress = Math.Clamp((elapsed / totalDuration) * 100.0, 0.0, 100.0);
                PbCountdown.Value = progress;
            }

            if (_notifyIcon != null)
            {
                string actionName = GetSelectedActionName();
                _notifyIcon.Text = $"{actionName}まで 残り {(int)remaining.TotalHours}時間{remaining.Minutes}分{remaining.Seconds}秒";
            }
        }

        private void ShowAdvanceNotice()
        {
            string actionName = GetSelectedActionName();
            _activeAlertWindow = new AlertWindow(
                actionName,
                () => _targetTime - DateTime.Now,
                onExtend: () =>
                {
                    _targetTime = _targetTime.AddMinutes(10);
                    _noticeShown = false; // allow next notice or extend
                    TxtTargetTimeInfo.Text = $"{_targetTime:yyyy/MM/dd HH:mm:ss} (+10分延長)";
                    UpdateTimerDisplay();
                },
                onCancel: () =>
                {
                    CancelReservation();
                }
            );

            _activeAlertWindow.Owner = this;
            _activeAlertWindow.Show();
        }

        private void ExecuteAction()
        {
            bool force = ChkForce.IsChecked == true;
            string actionName = GetSelectedActionName();

            try
            {
                if (RbShutdown.IsChecked == true)
                {
                    // Shutdown
                    string args = force ? "/s /f /t 0" : "/s /t 0";
                    Process.Start("shutdown.exe", args);
                }
                else if (RbRestart.IsChecked == true)
                {
                    // Restart
                    string args = force ? "/r /f /t 0" : "/r /t 0";
                    Process.Start("shutdown.exe", args);
                }
                else if (RbSleep.IsChecked == true)
                {
                    // Sleep
                    SetSuspendState(false, force, false);
                }
                else if (RbSignOut.IsChecked == true)
                {
                    // Sign out
                    string args = force ? "/l /f" : "/l";
                    Process.Start("shutdown.exe", args);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"コマンドの実行中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            CancelReservation();
        }

        private string GetSelectedActionName()
        {
            if (RbShutdown.IsChecked == true) return "シャットダウン";
            if (RbRestart.IsChecked == true) return "再起動";
            if (RbSleep.IsChecked == true) return "スリープ";
            if (RbSignOut.IsChecked == true) return "サインアウト";
            return "シャットダウン";
        }

        private int GetNoticeMinutes()
        {
            int index = CmbNoticeMinutes.SelectedIndex;
            return index switch
            {
                0 => 1,
                1 => 3,
                2 => 5,
                3 => 10,
                4 => 15,
                5 => 30,
                _ => 5
            };
        }

        private void BtnMinimizeTray_Click(object sender, RoutedEventArgs e)
        {
            HideToTray();
        }

        private void BtnTestNotification_Click(object sender, RoutedEventArgs e)
        {
            string actionName = GetSelectedActionName();
            var testAlert = new AlertWindow(
                $"{actionName} (通知テスト)",
                () => TimeSpan.FromMinutes(GetNoticeMinutes()),
                onExtend: () => WpfMessageBox.Show("「10分延長」ボタンがクリックされました。", "通知テスト", MessageBoxButton.OK, MessageBoxImage.Information),
                onCancel: () => WpfMessageBox.Show("「予約キャンセル」ボタンがクリックされました。", "通知テスト", MessageBoxButton.OK, MessageBoxImage.Information)
            );
            testAlert.Owner = this;
            testAlert.ShowDialog();
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            WpfMessageBox.Show(
                "PCシャットダウン予約ツール (PC Shutdown Timer)\n\n" +
                "バージョン: 1.0.0\n" +
                "開発元・著作権: © youyoubodragon\n\n" +
                "指定時刻やカウントダウンによるPCの自動シャットダウン・再起動・スリープ機能を提供します。\n" +
                "事前通知機能により、シャットダウン前に作業内容の保存猶予や延長が行えます。",
                "バージョン情報 - © youyoubodragon",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}