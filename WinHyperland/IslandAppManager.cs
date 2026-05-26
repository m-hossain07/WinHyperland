using System;
using System.Collections.Generic;
using System.Linq;

namespace WinHyperland
{
    /// <summary>
    /// Identifies the type of "app" currently running in the Dynamic Island.
    /// Each represents a distinct view that the island can display.
    /// </summary>
    public enum IslandApp
    {
        None,
        Media,
        Weather,
        Notification
    }

    /// <summary>
    /// Manages the stack of active "apps" shown in the Dynamic Island.
    /// Supports cycling between active apps (like Apple's Dynamic Island).
    /// </summary>
    public sealed class IslandAppManager
    {
        private readonly List<IslandApp> _activeApps = new();
        private int _currentIndex = -1;

        public event Action<IslandApp>? OnActiveAppChanged;

        /// <summary>
        /// The currently displayed app.
        /// </summary>
        public IslandApp CurrentApp =>
            _currentIndex >= 0 && _currentIndex < _activeApps.Count
                ? _activeApps[_currentIndex]
                : IslandApp.None;

        /// <summary>
        /// All currently active apps.
        /// </summary>
        public IReadOnlyList<IslandApp> ActiveApps => _activeApps.AsReadOnly();

        /// <summary>
        /// How many apps are currently active.
        /// </summary>
        public int ActiveCount => _activeApps.Count;

        /// <summary>
        /// Whether there are multiple apps to cycle through.
        /// </summary>
        public bool HasMultipleApps => _activeApps.Count > 1;

        /// <summary>
        /// Register an app as active (e.g. media started playing, weather enabled).
        /// If the app is already active, this is a no-op.
        /// </summary>
        public void Activate(IslandApp app)
        {
            if (app == IslandApp.None || app == IslandApp.Notification) return;
            if (_activeApps.Contains(app)) return;

            _activeApps.Add(app);

            // If this is the first app, auto-select it
            if (_activeApps.Count == 1)
            {
                _currentIndex = 0;
                OnActiveAppChanged?.Invoke(app);
            }
        }

        /// <summary>
        /// Deactivate an app (e.g. media stopped, weather disabled).
        /// </summary>
        public void Deactivate(IslandApp app)
        {
            if (!_activeApps.Contains(app)) return;

            var wasCurrent = CurrentApp == app;
            _activeApps.Remove(app);

            if (_activeApps.Count == 0)
            {
                _currentIndex = -1;
                if (wasCurrent) OnActiveAppChanged?.Invoke(IslandApp.None);
            }
            else
            {
                _currentIndex = Math.Clamp(_currentIndex, 0, _activeApps.Count - 1);
                if (wasCurrent) OnActiveAppChanged?.Invoke(CurrentApp);
            }
        }

        /// <summary>
        /// Cycle to the next active app (on click/swipe of the pill).
        /// </summary>
        public IslandApp CycleNext()
        {
            if (_activeApps.Count <= 1) return CurrentApp;

            _currentIndex = (_currentIndex + 1) % _activeApps.Count;
            OnActiveAppChanged?.Invoke(CurrentApp);
            return CurrentApp;
        }

        /// <summary>
        /// Explicitly switch to a specific app.
        /// </summary>
        public void SwitchTo(IslandApp app)
        {
            int idx = _activeApps.IndexOf(app);
            if (idx < 0) return;

            _currentIndex = idx;
            OnActiveAppChanged?.Invoke(CurrentApp);
        }

        /// <summary>
        /// Check if a specific app is active.
        /// </summary>
        public bool IsActive(IslandApp app) => _activeApps.Contains(app);
    }
}
