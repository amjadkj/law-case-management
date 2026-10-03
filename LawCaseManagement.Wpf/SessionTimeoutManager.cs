using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf
{
    public class SessionTimeoutManager
    {
        private readonly DispatcherTimer _timer;
        private readonly TimeSpan _timeoutThreshold;
        private DateTime _lastActivityTime;
        private readonly Action _onTimeoutAction;

        public SessionTimeoutManager(TimeSpan timeoutThreshold, Action onTimeoutAction)
        {
            _timeoutThreshold = timeoutThreshold;
            _onTimeoutAction = onTimeoutAction;
            _lastActivityTime = DateTime.Now;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _lastActivityTime = DateTime.Now;
            EventManager.RegisterClassHandler(typeof(Window), UIElement.MouseMoveEvent, new MouseEventHandler(OnUserActivity));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.KeyDownEvent, new KeyEventHandler(OnUserActivity));
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnUserActivity(object? sender, RoutedEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (AuthService.CurrentUser != null && (DateTime.Now - _lastActivityTime) >= _timeoutThreshold)
            {
                Stop();
                MessageBox.Show("Your session has timed out due to 15 minutes of inactivity. Please log in again.", "Session Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
                _onTimeoutAction?.Invoke();
            }
        }
    }
}
