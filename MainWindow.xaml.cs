using System.Globalization;
using System.Text;
using System.Media;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace Quick_Alt_Tab
{
    public partial class MainWindow : Window
    {
        private System.Windows.Threading.DispatcherTimer? hideStatusTimer;
        private System.Windows.Threading.DispatcherTimer? trialCountdownTimer;
        private TaskCompletionSource<TrialConfirmationResult>? trialConfirmation;
        private HwndSource? hwndSource;
        private DevMode resetBackupMode;
        private bool resetDisplayModeTrialActive;
        private int trialSecondsRemaining;
        private int trialOverlayVersion;
        private List<ResolutionOption> resolutionOptions = [];
        private List<MonitorOption> monitorOptions = [];
        private MonitorOption? selectedMonitor;

        private const int EmergencyHotkeyId = 0x5154;
        private const int ConfirmHotkeyId = 0x5155;
        private const int RestoreNativeHotkeyId = 0x5156;
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private const uint ModShift = 0x0004;
        private const uint VkN = 0x4E;
        private const uint VkO = 0x4F;
        private const uint VkY = 0x59;
        private const int EnumCurrentSettings = -1;
        private const int DispChangeSuccessful = 0;
        private const int CdsTest = 0x00000002;
        private const int CdsUpdateRegistry = 0x00000001;
        private const int DmPelsWidth = 0x00080000;
        private const int DmPelsHeight = 0x00100000;
        private const int DmDisplayFrequency = 0x00400000;
        private const int DisplayDeviceActive = 0x00000001;
        private const int DisplayDevicePrimaryDevice = 0x00000004;

        private enum TrialConfirmationResult
        {
            Confirmed,
            Cancelled,
            Timeout
        }

        private sealed record ResolutionOption(int Width, int Height, List<uint> RefreshRates, bool IsCustomPreset, bool IsOwnResolution, uint SelectedRefreshX1K);
        private sealed record MonitorOption(string DeviceName, string DisplayName, bool IsPrimary, uint? NvidiaDisplayId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(
            string? deviceName,
            ref DevMode devMode,
            IntPtr hwnd,
            int flags,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DevMode
        {
            private const int CchDevName = 32;
            private const int CchFormName = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDevName)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DisplayDevice
        {
            public uint cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        public MainWindow()
        {
            InitializeComponent();
            MonitorButton.Content = "\u25A3";
            SettingsButton.Content = "\u2699";
            RefreshMonitorOptions();

            Logger.Log("Application started");

            this.Loaded += (s, e) =>
            {
                Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, ActualWidth, ActualHeight),
                    14,
                    14);
                Logger.Log("Window loaded and rounded clip applied");
            };

            SourceInitialized += (_, _) =>
            {
                hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                hwndSource?.AddHook(WndProc);
                RegisterRestoreNativeHotkey();
            };

            Logger.Log("Checking for NVIDIA GPU...");
            if (!NvidiaAPI.IsNvidiaGPUPresent())
            {
                Logger.Log("NVIDIA GPU not detected");
                ApplyButton.IsEnabled = false;
            }
            else
            {
                Logger.Log("NVIDIA GPU detected successfully");
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (MonitorPanel.Visibility == Visibility.Visible)
            {
                MonitorPanel.Visibility = Visibility.Collapsed;
                MainFormPanel.Effect = SettingsPanel.Visibility == Visibility.Visible ? new BlurEffect { Radius = 3 } : null;
                return;
            }

            SettingsPanel.Visibility = Visibility.Collapsed;
            RefreshMonitorOptions();
            RenderMonitorList();
            MainFormPanel.Effect = new BlurEffect { Radius = 3 };
            MonitorPanel.Visibility = Visibility.Visible;
        }

        private void RefreshMonitorOptions()
        {
            var windowsDisplays = EnumerateWindowsDisplays().ToList();
            var nvidiaDisplays = NvidiaAPI.GetNvidiaDisplays();

            monitorOptions = windowsDisplays
                .Select((display, index) =>
                {
                    uint? nvidiaDisplayId = index < nvidiaDisplays.Count ? nvidiaDisplays[index].DisplayId : null;
                    return new MonitorOption(display.DeviceName, display.DisplayName, display.IsPrimary, nvidiaDisplayId);
                })
                .ToList();

            selectedMonitor ??= monitorOptions.FirstOrDefault(display => display.IsPrimary) ?? monitorOptions.FirstOrDefault();
            if (selectedMonitor?.NvidiaDisplayId is uint displayId)
            {
                NvidiaAPI.SetSelectedDisplayId(displayId);
            }
        }

        private void RenderMonitorList()
        {
            MonitorListPanel.Children.Clear();
            foreach (var monitor in monitorOptions)
            {
                var row = new Grid
                {
                    Height = 30,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = Brushes.Transparent
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock
                {
                    Text = monitor.DisplayName,
                    Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var marker = new TextBlock
                {
                    Text = selectedMonitor?.DeviceName == monitor.DeviceName ? "selected" : monitor.IsPrimary ? "primary" : string.Empty,
                    Foreground = new SolidColorBrush(Color.FromRgb(113, 113, 122)),
                    FontSize = 10,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(marker, 1);
                row.Children.Add(marker);

                row.MouseLeftButtonDown += (_, _) => SelectMonitor(monitor);
                MonitorListPanel.Children.Add(row);
            }
        }

        private void SelectMonitor(MonitorOption monitor)
        {
            selectedMonitor = monitor;
            if (monitor.NvidiaDisplayId is uint displayId)
            {
                NvidiaAPI.SetSelectedDisplayId(displayId);
            }

            SyncNativeFieldsWithSelectedMonitor();
            RefreshResolutionSettings();
            RenderMonitorList();
            MonitorPanel.Visibility = Visibility.Collapsed;
            MainFormPanel.Effect = null;
            Logger.Log($"Selected monitor {monitor.DeviceName} ({monitor.DisplayName}), NVIDIA displayId: {(monitor.NvidiaDisplayId.HasValue ? $"0x{monitor.NvidiaDisplayId.Value:X8}" : "none")}.");
        }

        private void SyncNativeFieldsWithSelectedMonitor()
        {
            DevMode current = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref current) ||
                current.dmPelsWidth == 0 ||
                current.dmPelsHeight == 0 ||
                current.dmDisplayFrequency == 0)
            {
                return;
            }

            NativeWidth.Text = current.dmPelsWidth.ToString(CultureInfo.InvariantCulture);
            NativeHeight.Text = current.dmPelsHeight.ToString(CultureInfo.InvariantCulture);
            NativeRefreshRate.Text = current.dmDisplayFrequency.ToString(CultureInfo.InvariantCulture);
            Logger.Log($"Native fields synced with selected monitor {SelectedDisplayDeviceName}: {current.dmPelsWidth}x{current.dmPelsHeight} @ {current.dmDisplayFrequency} Hz.");
        }

        private static IEnumerable<MonitorOption> EnumerateWindowsDisplays()
        {
            for (uint index = 0; ; index++)
            {
                var device = CreateDisplayDevice();
                if (!EnumDisplayDevices(null, index, ref device, 0))
                {
                    break;
                }

                bool isActive = (device.StateFlags & DisplayDeviceActive) != 0;
                if (!isActive || string.IsNullOrWhiteSpace(device.DeviceName))
                {
                    continue;
                }

                bool isPrimary = (device.StateFlags & DisplayDevicePrimaryDevice) != 0;
                string monitorName = GetMonitorDisplayName(device.DeviceName);
                string name = string.IsNullOrWhiteSpace(monitorName)
                    ? device.DeviceName
                    : monitorName;
                yield return new MonitorOption(device.DeviceName, name, isPrimary, null);
            }
        }

        private static string GetMonitorDisplayName(string adapterDeviceName)
        {
            var monitor = CreateDisplayDevice();
            if (!EnumDisplayDevices(adapterDeviceName, 0, ref monitor, 0))
            {
                return string.Empty;
            }

            string edidName = GetEdidMonitorName(monitor.DeviceID);
            if (!string.IsNullOrWhiteSpace(edidName))
            {
                return edidName;
            }

            return string.IsNullOrWhiteSpace(monitor.DeviceString)
                ? string.Empty
                : CleanMonitorName(monitor.DeviceString);
        }

        private static string GetEdidMonitorName(string monitorDeviceId)
        {
            if (string.IsNullOrWhiteSpace(monitorDeviceId))
            {
                return string.Empty;
            }

            const string monitorPrefix = @"MONITOR\";
            string id = monitorDeviceId.Trim();
            if (id.StartsWith(monitorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                id = id[monitorPrefix.Length..];
            }

            string[] parts = id.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                using RegistryKey? displayKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[0]}");
                if (displayKey == null)
                {
                    return string.Empty;
                }

                foreach (string subKeyName in GetPreferredMonitorRegistryKeys(displayKey, parts))
                {
                    using RegistryKey? parametersKey = displayKey.OpenSubKey($@"{subKeyName}\Device Parameters");
                    if (parametersKey?.GetValue("EDID") is not byte[] edid)
                    {
                        continue;
                    }

                    string name = ReadMonitorNameFromEdid(edid);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read monitor EDID name for {monitorDeviceId}: {ex.Message}");
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetPreferredMonitorRegistryKeys(RegistryKey displayKey, string[] deviceIdParts)
        {
            string[] subKeyNames = displayKey.GetSubKeyNames();
            if (deviceIdParts.Length > 1 && subKeyNames.Contains(deviceIdParts[1], StringComparer.OrdinalIgnoreCase))
            {
                yield return deviceIdParts[1];
            }

            foreach (string subKeyName in subKeyNames)
            {
                if (deviceIdParts.Length <= 1 || !subKeyName.Equals(deviceIdParts[1], StringComparison.OrdinalIgnoreCase))
                {
                    yield return subKeyName;
                }
            }
        }

        private static string ReadMonitorNameFromEdid(byte[] edid)
        {
            if (edid.Length < 128)
            {
                return string.Empty;
            }

            for (int offset = 54; offset <= 108; offset += 18)
            {
                if (edid[offset] == 0x00 &&
                    edid[offset + 1] == 0x00 &&
                    edid[offset + 2] == 0x00 &&
                    edid[offset + 3] == 0xFC &&
                    edid[offset + 4] == 0x00)
                {
                    string rawName = Encoding.ASCII.GetString(edid, offset + 5, 13);
                    return CleanMonitorName(rawName);
                }
            }

            return string.Empty;
        }

        private static string CleanMonitorName(string name)
        {
            return string.Join(" ", name
                .Replace('\0', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Trim();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                MainFormPanel.Effect = null;
                return;
            }

            MonitorPanel.Visibility = Visibility.Collapsed;
            RefreshResolutionSettings();
            MainFormPanel.Effect = new BlurEffect { Radius = 3 };
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void RefreshResolutionSettings()
        {
            ResolutionListPanel.Children.Clear();

            var customPresets = NvidiaAPI.EnumSavedCustomDisplays();
            var remembered = LoadRememberedPresets().ToHashSet();
            var customSet = customPresets.Concat(remembered).ToHashSet();
            var ownResolution = GetOwnResolutionFromInputOrCurrent();
            var currentMode = GetCurrentDisplayMode();

            var displayModes = EnumerateWindowsDisplayModes().ToList();
            SyncNativeRefreshWithCurrentMode();

            var grouped = displayModes
                .Concat(customSet)
                .GroupBy(mode => (mode.Width, mode.Height))
                .Select(group =>
                {
                    var refreshRates = group
                        .Select(mode => mode.RefreshX1K)
                        .Distinct()
                        .OrderBy(rate => rate)
                        .ToList();
                    var customRefreshRates = customSet
                        .Where(mode => mode.Width == group.Key.Width && mode.Height == group.Key.Height)
                        .Select(mode => mode.RefreshX1K)
                        .Distinct()
                        .OrderBy(rate => rate)
                        .ToList();
                    bool isCustomPreset = customRefreshRates.Count > 0;
                    uint selectedRefresh = 0;

                    if (isCustomPreset)
                    {
                        selectedRefresh = customRefreshRates
                            .OrderByDescending(rate => rate % 1000)
                            .ThenByDescending(rate => rate)
                            .First();
                    }
                    else if (currentMode.HasValue &&
                        currentMode.Value.Width == group.Key.Width &&
                        currentMode.Value.Height == group.Key.Height)
                    {
                        selectedRefresh = currentMode.Value.RefreshX1K;
                    }

                    return new ResolutionOption(
                        group.Key.Width,
                        group.Key.Height,
                        refreshRates,
                        isCustomPreset,
                        ownResolution.HasValue &&
                            ownResolution.Value.Width == group.Key.Width &&
                            ownResolution.Value.Height == group.Key.Height,
                        selectedRefresh);
                })
                .OrderByDescending(option => option.IsCustomPreset)
                .ThenByDescending(option => option.IsOwnResolution)
                .ThenByDescending(option => option.Width * option.Height)
                .ThenBy(option => option.Width)
                .ThenBy(option => option.Height)
                .ToList();

            resolutionOptions = grouped;

            foreach (var option in grouped)
            {
                ResolutionListPanel.Children.Add(CreateResolutionRow(option));
            }
        }

        private FrameworkElement CreateResolutionRow(ResolutionOption option)
        {
            var row = new Grid
            {
                Height = 28,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

            var resolutionText = new TextBlock
            {
                Text = $"{option.Width}x{option.Height}",
                Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(resolutionText, 0);
            row.Children.Add(resolutionText);

            var marker = new TextBlock
            {
                Text = option.IsCustomPreset ? "custom" : option.IsOwnResolution ? "own" : string.Empty,
                Foreground = new SolidColorBrush(Color.FromRgb(113, 113, 122)),
                FontSize = 10,
                Margin = new Thickness(6, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(marker, 1);
            row.Children.Add(marker);

            var refreshBox = new ComboBox
            {
                Width = 70,
                Height = 22,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(23, 21, 25)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 81, 99))
            };

            foreach (uint refreshRate in option.RefreshRates)
            {
                refreshBox.Items.Add(new ComboBoxItem
                {
                    Content = FormatRefreshX1K(refreshRate),
                    Tag = refreshRate
                });
            }

            int selectedRefreshIndex = option.SelectedRefreshX1K > 0
                ? option.RefreshRates.FindIndex(rate => rate == option.SelectedRefreshX1K)
                : -1;
            if (selectedRefreshIndex < 0 && option.SelectedRefreshX1K > 0)
            {
                selectedRefreshIndex = option.RefreshRates.FindIndex(rate => Math.Abs((long)rate - option.SelectedRefreshX1K) <= 1);
            }
            refreshBox.SelectedIndex = selectedRefreshIndex >= 0
                ? selectedRefreshIndex
                : option.RefreshRates.Count > 0 ? option.RefreshRates.Count - 1 : -1;
            refreshBox.SelectionChanged += (_, _) => SelectResolutionOption(option, refreshBox);
            Grid.SetColumn(refreshBox, 2);
            row.Children.Add(refreshBox);

            if (option.IsCustomPreset)
            {
                var deleteButton = new Button
                {
                    Content = "\u00D7",
                    Width = 22,
                    Height = 22,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderBrush = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Delete custom preset"
                };
                deleteButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    uint refreshX1K = GetSelectedRefresh(refreshBox);
                    DeleteSelectedCustomPreset(option.Width, option.Height, refreshX1K, option.IsOwnResolution);
                };
                Grid.SetColumn(deleteButton, 3);
                row.Children.Add(deleteButton);
            }

            row.MouseLeftButtonDown += (_, e) =>
            {
                SelectResolutionOption(option, refreshBox);
                if (e.ClickCount >= 2)
                {
                    ApplySelectedDisplayMode(option, refreshBox);
                }
            };
            return row;
        }

        private void SelectResolutionOption(ResolutionOption option, ComboBox refreshBox)
        {
            CustomWidth.Text = option.Width.ToString(CultureInfo.InvariantCulture);
            CustomHeight.Text = option.Height.ToString(CultureInfo.InvariantCulture);
        }

        private void DeleteSelectedCustomPreset(int width, int height, uint refreshX1K, bool isOwnResolution)
        {
            if (refreshX1K == 0)
            {
                return;
            }

            if (isOwnResolution)
            {
                TrySwitchToOwnFallbackMode(width, height, refreshX1K);
            }

            int deleted = NvidiaAPI.DeleteSavedCustomDisplays([new NvidiaAPI.CustomDisplayMode(width, height, refreshX1K)]);
            if (deleted >= 0)
            {
                RemoveRememberedPreset(width, height, refreshX1K);
                RefreshResolutionSettings();
                ShowSettingsStatus(deleted > 0 ? "Custom preset deleted" : "No matching preset");
            }
            else
            {
                ShowError(NvidiaAPI.LastErrorMessage.Length > 0 ? NvidiaAPI.LastErrorMessage : "Failed to delete custom preset");
            }
        }

        private void TrySwitchToOwnFallbackMode(int width, int height, uint deletingRefreshX1K)
        {
            var fallback = EnumerateWindowsDisplayModes()
                .Where(mode => mode.Width == width && mode.Height == height)
                .Where(mode => Math.Abs((long)mode.RefreshX1K - deletingRefreshX1K) > 1)
                .OrderByDescending(mode => mode.RefreshX1K == 120000)
                .ThenBy(mode => mode.RefreshX1K)
                .FirstOrDefault();

            if (fallback.Width <= 0 || fallback.Height <= 0 || fallback.RefreshX1K == 0)
            {
                Logger.Log($"No fallback own display mode found before deleting {width}x{height} custom preset.");
                return;
            }

            uint refresh = (uint)Math.Round(fallback.RefreshX1K / 1000.0);
            DevMode mode = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref mode))
            {
                Logger.Error("Failed to read current display mode before deleting own custom preset.");
                return;
            }

            mode.dmPelsWidth = (uint)width;
            mode.dmPelsHeight = (uint)height;
            mode.dmDisplayFrequency = refresh;
            mode.dmFields |= DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

            int testResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (testResult != DispChangeSuccessful)
            {
                Logger.Log($"Fallback mode {width}x{height} @ {refresh} Hz was rejected before deleting own custom preset. Code: {testResult}.");
                return;
            }

            int applyResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            Logger.Log(applyResult == DispChangeSuccessful
                ? $"Switched to own fallback mode {width}x{height} @ {refresh} Hz before deleting custom preset."
                : $"Failed to switch to own fallback mode before deleting custom preset. Code: {applyResult}.");
        }

        private bool TryBeginDisplayModeTrial(int width, int height, uint refresh, out string error)
        {
            error = string.Empty;
            resetBackupMode = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref resetBackupMode))
            {
                error = "Failed to read current display mode.";
                Logger.Error(error);
                return false;
            }

            if (!TryFindExactDisplayMode(width, height, refresh, out DevMode mode))
            {
                error = $"{width}x{height} @ {refresh} Hz is not available right now.";
                Logger.Error(error);
                return false;
            }

            int testResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (testResult != DispChangeSuccessful)
            {
                error = $"Windows rejected {width}x{height} @ {refresh} Hz. Code: {testResult}.";
                Logger.Error(error);
                return false;
            }

            int applyResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            if (applyResult != DispChangeSuccessful)
            {
                error = $"Failed to apply {width}x{height} @ {refresh} Hz. Code: {applyResult}.";
                Logger.Error(error);
                return false;
            }

            resetDisplayModeTrialActive = true;
            Logger.Log($"Display mode trial is active: {width}x{height} @ {refresh} Hz.");
            return true;
        }

        private bool TryFindExactDisplayMode(int width, int height, uint refresh, out DevMode mode)
        {
            for (int index = 0; ; index++)
            {
                mode = CreateDevMode();
                if (!EnumDisplaySettings(SelectedDisplayDeviceName, index, ref mode))
                {
                    mode = default;
                    return false;
                }

                if (mode.dmPelsWidth == width &&
                    mode.dmPelsHeight == height &&
                    mode.dmDisplayFrequency == refresh)
                {
                    mode.dmFields |= DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
                    return true;
                }
            }
        }

        private async void ApplySelectedDisplayMode(ResolutionOption option, ComboBox refreshBox)
        {
            uint refreshX1K = GetSelectedRefresh(refreshBox);
            if (refreshX1K == 0)
            {
                return;
            }

            uint refresh = (uint)Math.Round(refreshX1K / 1000.0);
            if (!TryBeginDisplayModeTrial(option.Width, option.Height, refresh, out string error))
            {
                ShowSettingsStatus(error);
                return;
            }

            TrialConfirmationResult confirmation = await WaitForResetConfirmationAsync(option.Width, option.Height, refresh, 15, "preset");
            if (confirmation != TrialConfirmationResult.Confirmed)
            {
                if (confirmation == TrialConfirmationResult.Timeout)
                {
                    RestoreResetBackupMode();
                    Logger.Log("Display mode trial from settings timed out and was reverted.");
                }
                else
                {
                    Logger.Log("Display mode trial from settings was cancelled with N and reverted.");
                }

                ApplyButton.IsEnabled = true;
                return;
            }

            resetDisplayModeTrialActive = false;
            ApplyButton.IsEnabled = true;
            SyncNativeRefreshWithCurrentMode();
            RefreshResolutionSettings();

            ShowSettingsStatus($"Applied {option.Width}x{option.Height} @ {refresh} Hz");
            Logger.Log($"Applied display mode from settings: {option.Width}x{option.Height} @ {refresh} Hz.");
        }

        private void ShowSettingsStatus(string message)
        {
            Logger.Log($"Settings status: {message}");
            SettingsStatusText.Visibility = Visibility.Collapsed;
        }

        private void ShowTrialOverlay(string title, string body)
        {
            trialOverlayVersion++;
            TrialOverlay.BeginAnimation(OpacityProperty, null);
            TrialOverlayTitle.Text = title;
            TrialOverlayBody.Text = body;
            TrialOverlay.Visibility = Visibility.Visible;
            MainFormPanel.Opacity = 0.55;
            SettingsPanel.Opacity = 0.55;
            ContentGrid.Effect = new BlurEffect { Radius = 4 };

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            TrialOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void HideTrialOverlay()
        {
            int hideVersion = ++trialOverlayVersion;
            TrialOverlay.BeginAnimation(OpacityProperty, null);
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            fadeOut.Completed += (_, _) =>
            {
                if (hideVersion != trialOverlayVersion)
                {
                    return;
                }

                TrialOverlay.Visibility = Visibility.Collapsed;
                MainFormPanel.Opacity = 1.0;
                SettingsPanel.Opacity = 1.0;
                ContentGrid.Effect = null;
            };
            TrialOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private static uint GetSelectedRefresh(ComboBox refreshBox)
        {
            return refreshBox.SelectedItem is ComboBoxItem item && item.Tag is uint refreshX1K ? refreshX1K : 0;
        }

        private static string FormatRefreshX1K(uint refreshX1K)
        {
            return (refreshX1K / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string FormatRefreshForMainInput(uint refreshX1K)
        {
            return Math.Round(refreshX1K / 1000.0).ToString("0", CultureInfo.InvariantCulture);
        }

        private (int Width, int Height)? GetOwnResolutionFromInputOrCurrent()
        {
            if (int.TryParse(NativeWidth.Text, out int nativeWidth) &&
                int.TryParse(NativeHeight.Text, out int nativeHeight) &&
                nativeWidth > 0 &&
                nativeHeight > 0)
            {
                return (nativeWidth, nativeHeight);
            }

            DevMode current = CreateDevMode();
            if (EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref current) &&
                current.dmPelsWidth > 0 &&
                current.dmPelsHeight > 0)
            {
                return ((int)current.dmPelsWidth, (int)current.dmPelsHeight);
            }

            return null;
        }

        private IEnumerable<NvidiaAPI.CustomDisplayMode> EnumerateWindowsDisplayModes()
        {
            var modes = new HashSet<NvidiaAPI.CustomDisplayMode>();

            for (int index = 0; ; index++)
            {
                DevMode mode = CreateDevMode();
                if (!EnumDisplaySettings(SelectedDisplayDeviceName, index, ref mode))
                {
                    break;
                }

                if (mode.dmPelsWidth < 640 ||
                    mode.dmPelsHeight < 480 ||
                    mode.dmDisplayFrequency == 0)
                {
                    continue;
                }

                modes.Add(new NvidiaAPI.CustomDisplayMode(
                    (int)mode.dmPelsWidth,
                    (int)mode.dmPelsHeight,
                    mode.dmDisplayFrequency * 1000u));
            }

            return modes;
        }

        private void SyncNativeRefreshWithCurrentMode()
        {
            if (!int.TryParse(NativeWidth.Text, out int nativeWidth) ||
                !int.TryParse(NativeHeight.Text, out int nativeHeight) ||
                nativeWidth <= 0 ||
                nativeHeight <= 0)
            {
                return;
            }

            DevMode current = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref current))
            {
                return;
            }

            if (current.dmPelsWidth == nativeWidth &&
                current.dmPelsHeight == nativeHeight &&
                current.dmDisplayFrequency > 0)
            {
                NativeRefreshRate.Text = current.dmDisplayFrequency.ToString(CultureInfo.InvariantCulture);
                Logger.Log($"Native refresh field synced with current display mode: {nativeWidth}x{nativeHeight} @ {current.dmDisplayFrequency} Hz.");
            }
        }

        private NvidiaAPI.CustomDisplayMode? GetCurrentDisplayMode()
        {
            DevMode current = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref current) ||
                current.dmPelsWidth == 0 ||
                current.dmPelsHeight == 0 ||
                current.dmDisplayFrequency == 0)
            {
                return null;
            }

            return new NvidiaAPI.CustomDisplayMode(
                (int)current.dmPelsWidth,
                (int)current.dmPelsHeight,
                current.dmDisplayFrequency * 1000u);
        }

        private bool EnsureNativeModeForPresetCreation(int width, int height, double refreshRate, out string error)
        {
            error = string.Empty;
            DevMode current = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref current))
            {
                error = "Failed to read current display mode before creating presets.";
                Logger.Error(error);
                return false;
            }

            uint requestedRefresh = (uint)Math.Round(refreshRate);
            uint refresh = ResolveAvailableRefresh(width, height, requestedRefresh);
            if (refresh == 0)
            {
                error = $"No available native base mode found for {width}x{height}.";
                Logger.Error(error);
                return false;
            }

            if (refresh != requestedRefresh)
            {
                NativeRefreshRate.Text = refresh.ToString(CultureInfo.InvariantCulture);
                Logger.Log($"Requested native refresh {requestedRefresh} Hz is not available. Using real available refresh {refresh} Hz.");
            }

            if (current.dmPelsWidth == width &&
                current.dmPelsHeight == height &&
                current.dmDisplayFrequency == refresh)
            {
                Logger.Log($"Current display mode already matches native base mode {width}x{height} @ {refresh} Hz.");
                return true;
            }

            var mode = current;
            mode.dmPelsWidth = (uint)width;
            mode.dmPelsHeight = (uint)height;
            mode.dmDisplayFrequency = refresh;
            mode.dmFields |= DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

            int testResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (testResult != DispChangeSuccessful)
            {
                error = $"Windows rejected native base mode {width}x{height} @ {refresh} Hz. Code: {testResult}.";
                Logger.Error(error);
                return false;
            }

            int applyResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref mode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            if (applyResult != DispChangeSuccessful)
            {
                error = $"Failed to apply native base mode {width}x{height} @ {refresh} Hz. Code: {applyResult}.";
                Logger.Error(error);
                return false;
            }

            Logger.Log($"Switched to native base mode {width}x{height} @ {refresh} Hz before creating custom presets.");
            return true;
        }

        private uint ResolveAvailableRefresh(int width, int height, uint preferredRefresh)
        {
            var refreshes = EnumerateWindowsDisplayModes()
                .Where(mode => mode.Width == width && mode.Height == height)
                .Select(mode => (uint)Math.Round(mode.RefreshX1K / 1000.0))
                .Where(refresh => refresh > 0)
                .Distinct()
                .OrderBy(refresh => refresh)
                .ToList();

            if (refreshes.Count == 0)
            {
                return 0;
            }

            if (refreshes.Contains(preferredRefresh))
            {
                return preferredRefresh;
            }

            return refreshes[^1];
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Application closing");
            UnregisterEmergencyHotkey();
            UnregisterRestoreNativeHotkey();
            NvidiaAPI.RevertCurrentTrial();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            UnregisterEmergencyHotkey();
            UnregisterRestoreNativeHotkey();
            if (hwndSource != null)
            {
                hwndSource.RemoveHook(WndProc);
                hwndSource = null;
            }

            NvidiaAPI.RevertCurrentTrial();
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && wParam.ToInt32() == EmergencyHotkeyId)
            {
                handled = true;
                EmergencyRevertTrial("Reject hotkey N pressed.");
            }
            else if (msg == WmHotkey && wParam.ToInt32() == ConfirmHotkeyId)
            {
                handled = true;
                ConfirmVisibleTrial("Confirm hotkey Y pressed.");
            }
            else if (msg == WmHotkey && wParam.ToInt32() == RestoreNativeHotkeyId)
            {
                handled = true;
                RestoreNativeModeFromHotkey();
            }

            return IntPtr.Zero;
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (trialConfirmation != null)
            {
                trialConfirmation.TrySetResult(TrialConfirmationResult.Confirmed);
                return;
            }

            Logger.Log("=== Apply button clicked ===");

            if (!ValidateInputs())
            {
                Logger.Log("Input validation failed");
                return;
            }

            ApplyButton.IsEnabled = false;
            ApplyButton.Content = "Applying...";

            try
            {
                int nativeWidth = int.Parse(NativeWidth.Text, CultureInfo.InvariantCulture);
                int nativeHeight = int.Parse(NativeHeight.Text, CultureInfo.InvariantCulture);
                double nativeRefresh = ParseRefreshRate(NativeRefreshRate.Text);

                int customWidth = int.Parse(CustomWidth.Text, CultureInfo.InvariantCulture);
                int customHeight = int.Parse(CustomHeight.Text, CultureInfo.InvariantCulture);

                double modifiedRefresh = nativeRefresh + 0.001;
                Logger.Log($"Native resolution: {nativeWidth}x{nativeHeight} @ {modifiedRefresh:F3} Hz");
                Logger.Log($"Custom resolution: {customWidth}x{customHeight} @ {modifiedRefresh:F3} Hz");

                if (!EnsureNativeModeForPresetCreation(nativeWidth, nativeHeight, nativeRefresh, out string nativeModeError))
                {
                    throw new Exception(nativeModeError);
                }

                if (!await Task.Run(() => NvidiaAPI.Initialize()))
                {
                    throw new Exception("Failed to initialize NVIDIA API");
                }

                uint modifiedRefreshX1K = ToRefreshX1K(modifiedRefresh);
                var presetsToReplace = new List<NvidiaAPI.CustomDisplayMode>
                {
                    new(nativeWidth, nativeHeight, modifiedRefreshX1K),
                    new(customWidth, customHeight, modifiedRefreshX1K)
                };
                await Task.Run(() => NvidiaAPI.DeleteSavedCustomDisplays(presetsToReplace));

                bool nativeSaved = await TestAndSaveResolutionAsync(
                    nativeWidth,
                    nativeHeight,
                    nativeWidth,
                    nativeHeight,
                    modifiedRefresh,
                    "native",
                    revertAfterSave: false,
                    confirmationTitle: "Confirm 1/2",
                    confirmationPrompt: $"Testing native preset {nativeWidth}x{nativeHeight} @ {modifiedRefresh:F3} Hz.\nPress Y to save or N to cancel.\nAuto-revert in {{0}}s.");
                if (!nativeSaved)
                {
                    throw new Exception("Native resolution was reverted and not saved.");
                }

                bool customSaved = await TestAndSaveResolutionAsync(
                    customWidth,
                    customHeight,
                    nativeWidth,
                    nativeHeight,
                    modifiedRefresh,
                    "custom",
                    revertAfterSave: true,
                    confirmationTitle: "Confirm 2/2",
                    confirmationPrompt: $"Testing custom preset {customWidth}x{customHeight} @ {modifiedRefresh:F3} Hz.\nPress Y to save or N to cancel.\nAuto-revert in {{0}}s.");
                if (!customSaved)
                {
                    throw new Exception("Custom resolution was reverted and not saved.");
                }

                Logger.Log("All resolutions saved successfully.");
                ApplyButton.Style = (Style)FindResource("SuccessButton");
                ApplyButton.Content = "Presets saved";

                var resetTimer = new System.Windows.Threading.DispatcherTimer();
                resetTimer.Interval = TimeSpan.FromMilliseconds(2000);
                resetTimer.Tick += (s, args) =>
                {
                    ApplyButton.Style = (Style)FindResource("ShadcnButton");
                    ApplyButton.Content = "Apply";
                    ApplyButton.IsEnabled = true;
                    resetTimer.Stop();
                };
                resetTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in ApplyButton_Click", ex);
                UnregisterEmergencyHotkey();
                NvidiaAPI.RevertCurrentTrial();

                bool wasUserCancelled =
                    ex.Message.Contains("reverted and not saved", StringComparison.OrdinalIgnoreCase);
                ShowSettingsStatus(ex.Message);
                ApplyButton.Style = (Style)FindResource("ShadcnButton");
                ApplyButton.Content = "Apply";
                ApplyButton.IsEnabled = true;

                var resetTimer = new System.Windows.Threading.DispatcherTimer();
                resetTimer.Interval = TimeSpan.FromMilliseconds(wasUserCancelled ? 1200 : 2200);
                resetTimer.Tick += (s, args) =>
                {
                    ApplyButton.Style = (Style)FindResource("ShadcnButton");
                    ApplyButton.Content = "Apply";
                    ApplyButton.IsEnabled = true;
                    resetTimer.Stop();
                };
                resetTimer.Start();
            }
            finally
            {
                if (trialConfirmation == null && ApplyButton.Style != (Style)FindResource("SuccessButton"))
                {
                    ApplyButton.IsEnabled = true;
                }

                Logger.Log("=== Apply button process completed ===");
            }
        }

        private async Task<bool> TestAndSaveResolutionAsync(
            int sourceWidth,
            int sourceHeight,
            int outputWidth,
            int outputHeight,
            double refreshRate,
            string label,
            bool revertAfterSave,
            string confirmationTitle,
            string confirmationPrompt)
        {
            bool trialStarted = await Task.Run(() => NvidiaAPI.BeginCustomResolutionTrial(
                sourceWidth,
                sourceHeight,
                outputWidth,
                outputHeight,
                refreshRate));
            if (!trialStarted)
            {
                throw new Exception(NvidiaAPI.LastErrorMessage.Length > 0
                    ? NvidiaAPI.LastErrorMessage
                    : $"Failed to test {label} preset {sourceWidth}x{sourceHeight}");
            }

            TrialConfirmationResult confirmationResult = await WaitForVisibleConfirmationAsync(
                sourceWidth,
                sourceHeight,
                refreshRate,
                label,
                15,
                confirmationTitle,
                confirmationPrompt);
            if (confirmationResult != TrialConfirmationResult.Confirmed)
            {
                if (confirmationResult == TrialConfirmationResult.Timeout)
                {
                    await Task.Run(NvidiaAPI.RevertCurrentTrial);
                    Logger.Log($"{label} resolution trial timed out and was reverted.");
                }
                else
                {
                    Logger.Log($"{label} resolution trial was cancelled with N and reverted.");
                }

                return false;
            }

            ApplyButton.Content = "Saving preset...";
            ApplyButton.IsEnabled = false;

            bool saved = await Task.Run(() => NvidiaAPI.SaveCurrentTrial(revertAfterSave));
            if (!saved)
            {
                await Task.Run(NvidiaAPI.RevertCurrentTrial);
                throw new Exception(NvidiaAPI.LastErrorMessage.Length > 0
                    ? NvidiaAPI.LastErrorMessage
                    : $"Failed to save {label} preset {sourceWidth}x{sourceHeight}");
            }

            Logger.Log($"{label} resolution saved after visible confirmation.");
            RememberCreatedPreset(sourceWidth, sourceHeight, ToRefreshX1K(refreshRate));
            return true;
        }

        private async Task<TrialConfirmationResult> WaitForVisibleConfirmationAsync(
            int width,
            int height,
            double refreshRate,
            string label,
            int seconds,
            string title,
            string promptTemplate)
        {
            trialConfirmation = new TaskCompletionSource<TrialConfirmationResult>();
            trialSecondsRemaining = seconds;
            hideStatusTimer?.Stop();
            trialCountdownTimer?.Stop();
            RegisterEmergencyHotkey();

            ApplyButton.Style = (Style)FindResource("ShadcnButton");
            ApplyButton.IsEnabled = true;
            ShowTrialOverlay(
                title,
                string.Format(CultureInfo.InvariantCulture, promptTemplate, trialSecondsRemaining));
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            trialCountdownTimer = new System.Windows.Threading.DispatcherTimer();
            trialCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            trialCountdownTimer.Tick += (s, e) =>
            {
                trialSecondsRemaining--;
                if (trialSecondsRemaining <= 0)
                {
                    trialCountdownTimer.Stop();
                    trialConfirmation?.TrySetResult(TrialConfirmationResult.Timeout);
                    return;
                }

                TrialOverlayBody.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    promptTemplate,
                    trialSecondsRemaining);
            };
            trialCountdownTimer.Start();

            TrialConfirmationResult result = await trialConfirmation.Task;

            trialCountdownTimer?.Stop();
            trialCountdownTimer = null;
            trialConfirmation = null;
            UnregisterEmergencyHotkey();
            HideTrialOverlay();
            ApplyButton.IsEnabled = true;

            return result;
        }

        private void RegisterEmergencyHotkey()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                Logger.Error("Failed to register emergency hotkey: window handle is not ready.");
                return;
            }

            UnregisterEmergencyHotkey();
            if (RegisterHotKey(handle, EmergencyHotkeyId, ModNoRepeat, VkN))
            {
                Logger.Log("Reject hotkey registered: N");
            }
            else
            {
                Logger.Error($"Failed to register reject hotkey N. Win32 error: {Marshal.GetLastWin32Error()}");
            }

            if (RegisterHotKey(handle, ConfirmHotkeyId, ModNoRepeat, VkY))
            {
                Logger.Log("Confirm hotkey registered: Y");
            }
            else
            {
                Logger.Error($"Failed to register confirm hotkey Y. Win32 error: {Marshal.GetLastWin32Error()}");
            }
        }

        private void UnregisterEmergencyHotkey()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                UnregisterHotKey(handle, EmergencyHotkeyId);
                UnregisterHotKey(handle, ConfirmHotkeyId);
            }
        }

        private void RegisterRestoreNativeHotkey()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                Logger.Error("Failed to register Shift+O hotkey: window handle is not ready.");
                return;
            }

            if (RegisterHotKey(handle, RestoreNativeHotkeyId, ModShift | ModNoRepeat, VkO))
            {
                Logger.Log("Restore native hotkey registered: Shift+O");
            }
            else
            {
                Logger.Error($"Failed to register restore native hotkey Shift+O. Win32 error: {Marshal.GetLastWin32Error()}");
            }
        }

        private void UnregisterRestoreNativeHotkey()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                UnregisterHotKey(handle, RestoreNativeHotkeyId);
            }
        }

        private void RestoreNativeModeFromHotkey()
        {
            SystemSounds.Asterisk.Play();

            if (!int.TryParse(NativeWidth.Text, out int nativeWidth) ||
                !int.TryParse(NativeHeight.Text, out int nativeHeight) ||
                !TryParseRefreshRate(NativeRefreshRate.Text, out double nativeRefresh) ||
                nativeWidth <= 0 ||
                nativeHeight <= 0 ||
                nativeRefresh <= 0)
            {
                Logger.Error("Shift+O restore failed: invalid native fields.");
                return;
            }

            uint requestedRefresh = (uint)Math.Round(nativeRefresh);
            uint refresh = ResolveAvailableRefresh(nativeWidth, nativeHeight, requestedRefresh);
            if (refresh == 0)
            {
                Logger.Error($"Shift+O restore failed: no available mode for {nativeWidth}x{nativeHeight}.");
                ShowSettingsStatus("Restore mode unavailable");
                return;
            }

            if (refresh != requestedRefresh)
            {
                NativeRefreshRate.Text = refresh.ToString(CultureInfo.InvariantCulture);
                Logger.Log($"Shift+O requested {requestedRefresh} Hz, but only {refresh} Hz is available for native mode.");
            }

            if (TryBeginDisplayModeTrial(nativeWidth, nativeHeight, refresh, out string error))
            {
                resetDisplayModeTrialActive = false;
                Logger.Log($"Shift+O restored native mode {nativeWidth}x{nativeHeight} @ {refresh} Hz.");
                ShowSettingsStatus($"Restored {nativeWidth}x{nativeHeight} @ {refresh} Hz");
            }
            else
            {
                Logger.Error($"Shift+O restore failed: {error}");
                ShowSettingsStatus("Restore failed");
            }
        }

        private void ConfirmVisibleTrial(string reason)
        {
            Logger.Log(reason);
            trialCountdownTimer?.Stop();
            trialCountdownTimer = null;
            UnregisterEmergencyHotkey();
            HideTrialOverlay();
            trialConfirmation?.TrySetResult(TrialConfirmationResult.Confirmed);
        }

        private void EmergencyRevertTrial(string reason)
        {
            Logger.Log(reason);
            trialCountdownTimer?.Stop();
            trialCountdownTimer = null;
            UnregisterEmergencyHotkey();
            bool wasResetTrial = resetDisplayModeTrialActive;

            if (wasResetTrial)
            {
                RestoreResetBackupMode();
            }
            else
            {
                NvidiaAPI.RevertCurrentTrial();
            }

            trialConfirmation?.TrySetResult(TrialConfirmationResult.Cancelled);
            HideTrialOverlay();
            ApplyButton.Content = "Apply";
            ApplyButton.IsEnabled = true;
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (trialConfirmation != null)
            {
                return;
            }

            Logger.Log("=== Reset button clicked ===");

            if (!int.TryParse(NativeWidth.Text, out int nativeWidth) || nativeWidth <= 0 ||
                !int.TryParse(NativeHeight.Text, out int nativeHeight) || nativeHeight <= 0 ||
                !TryParseRefreshRate(NativeRefreshRate.Text, out double nativeRefresh) || nativeRefresh <= 0)
            {
                ShowError("Invalid native resolution or refresh rate");
                return;
            }

            SettingsButton.IsEnabled = false;
            ApplyButton.IsEnabled = false;
            ApplyButton.Content = "Resetting...";

            try
            {
                var targets = LoadRememberedPresets().ToList();
                uint modifiedRefreshX1K = ToRefreshX1K(nativeRefresh + 0.001);
                targets.Add(new NvidiaAPI.CustomDisplayMode(nativeWidth, nativeHeight, modifiedRefreshX1K));

                if (int.TryParse(CustomWidth.Text, out int customWidth) &&
                    int.TryParse(CustomHeight.Text, out int customHeight) &&
                    customWidth > 0 &&
                    customHeight > 0)
                {
                    targets.Add(new NvidiaAPI.CustomDisplayMode(customWidth, customHeight, modifiedRefreshX1K));
                }

                if (!TryBeginNativeResetTrial(nativeWidth, nativeHeight, nativeRefresh, out string resetError))
                {
                    throw new Exception(resetError);
                }

                TrialConfirmationResult confirmation = await WaitForResetConfirmationAsync(nativeWidth, nativeHeight, nativeRefresh, 15);
                if (confirmation != TrialConfirmationResult.Confirmed)
                {
                    if (confirmation == TrialConfirmationResult.Timeout)
                    {
                        RestoreResetBackupMode();
                        Logger.Log("Reset display mode trial timed out and was reverted.");
                    }
                    else
                    {
                        Logger.Log("Reset display mode trial was cancelled with N and reverted.");
                    }

                    throw new Exception("Reset was reverted and presets were not deleted.");
                }

                resetDisplayModeTrialActive = false;
                int deleted = await Task.Run(() => NvidiaAPI.DeleteSavedCustomDisplays(targets));
                if (deleted < 0)
                {
                    throw new Exception(NvidiaAPI.LastErrorMessage.Length > 0
                        ? NvidiaAPI.LastErrorMessage
                        : "Failed to delete saved custom presets.");
                }

                ClearRememberedPresets();
                ApplyButton.Content = $"Reset complete ({deleted})";
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in ResetButton_Click", ex);
                ApplyButton.Style = (Style)FindResource("ShadcnButton");
                ApplyButton.Content = "Apply";
                ShowSettingsStatus($"Reset error: {ex.Message}");
            }
            finally
            {
                resetDisplayModeTrialActive = false;
                SettingsButton.IsEnabled = true;
                ApplyButton.IsEnabled = true;
                var resetTimer = new System.Windows.Threading.DispatcherTimer();
                resetTimer.Interval = TimeSpan.FromMilliseconds(2000);
                resetTimer.Tick += (s, args) =>
                {
                    ApplyButton.Style = (Style)FindResource("ShadcnButton");
                    ApplyButton.Content = "Apply";
                    ApplyButton.IsEnabled = true;
                    resetTimer.Stop();
                };
                resetTimer.Start();
                Logger.Log("=== Reset button process completed ===");
            }
        }

        private bool TryBeginNativeResetTrial(int width, int height, double refreshRate, out string error)
        {
            error = string.Empty;
            resetBackupMode = CreateDevMode();
            if (!EnumDisplaySettings(SelectedDisplayDeviceName, EnumCurrentSettings, ref resetBackupMode))
            {
                error = "Failed to read current display mode.";
                Logger.Error(error);
                return false;
            }

            var resetMode = resetBackupMode;
            resetMode.dmPelsWidth = (uint)width;
            resetMode.dmPelsHeight = (uint)height;
            resetMode.dmDisplayFrequency = (uint)Math.Round(refreshRate);
            resetMode.dmFields |= DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

            int testResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref resetMode, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (testResult != DispChangeSuccessful)
            {
                error = $"Windows rejected native reset mode {width}x{height} @ {refreshRate:F0} Hz. Code: {testResult}.";
                Logger.Error(error);
                return false;
            }

            int applyResult = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref resetMode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            if (applyResult != DispChangeSuccessful)
            {
                error = $"Failed to apply native reset mode {width}x{height} @ {refreshRate:F0} Hz. Code: {applyResult}.";
                Logger.Error(error);
                return false;
            }

            resetDisplayModeTrialActive = true;
            Logger.Log($"Native reset trial is active: {width}x{height} @ {refreshRate:F0} Hz.");
            return true;
        }

        private async Task<TrialConfirmationResult> WaitForResetConfirmationAsync(int width, int height, double refreshRate, int seconds, string actionName = "reset")
        {
            trialConfirmation = new TaskCompletionSource<TrialConfirmationResult>();
            trialSecondsRemaining = seconds;
            hideStatusTimer?.Stop();
            trialCountdownTimer?.Stop();
            RegisterEmergencyHotkey();

            ApplyButton.Style = (Style)FindResource("ShadcnButton");
            ApplyButton.IsEnabled = true;
            ShowTrialOverlay(
                "Confirm",
                $"{actionName} trial: {width}x{height} @ {refreshRate:F0} Hz.\nPress Y to confirm or N to cancel.\nAuto-revert in {trialSecondsRemaining}s.");

            trialCountdownTimer = new System.Windows.Threading.DispatcherTimer();
            trialCountdownTimer.Interval = TimeSpan.FromSeconds(1);
            trialCountdownTimer.Tick += (s, e) =>
            {
                trialSecondsRemaining--;
                if (trialSecondsRemaining <= 0)
                {
                    trialCountdownTimer.Stop();
                    trialConfirmation?.TrySetResult(TrialConfirmationResult.Timeout);
                    return;
                }

                TrialOverlayBody.Text = $"{actionName} trial: {width}x{height} @ {refreshRate:F0} Hz.\nPress Y to confirm or N to cancel.\nAuto-revert in {trialSecondsRemaining}s.";
            };
            trialCountdownTimer.Start();

            TrialConfirmationResult result = await trialConfirmation.Task;

            trialCountdownTimer?.Stop();
            trialCountdownTimer = null;
            trialConfirmation = null;
            UnregisterEmergencyHotkey();
            HideTrialOverlay();
            ApplyButton.IsEnabled = true;

            return result;
        }

        private void RestoreResetBackupMode()
        {
            if (!resetDisplayModeTrialActive)
            {
                return;
            }

            int result = ChangeDisplaySettingsEx(SelectedDisplayDeviceName, ref resetBackupMode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            Logger.Log(result == DispChangeSuccessful
                ? $"Restored previous display mode {resetBackupMode.dmPelsWidth}x{resetBackupMode.dmPelsHeight} @ {resetBackupMode.dmDisplayFrequency} Hz."
                : $"Failed to restore previous display mode. ChangeDisplaySettingsEx code: {result}.");
            resetDisplayModeTrialActive = false;
        }

        private static DevMode CreateDevMode()
        {
            return new DevMode
            {
                dmDeviceName = new string('\0', 32),
                dmFormName = new string('\0', 32),
                dmSize = (ushort)Marshal.SizeOf<DevMode>()
            };
        }

        private static DisplayDevice CreateDisplayDevice()
        {
            return new DisplayDevice
            {
                cb = (uint)Marshal.SizeOf<DisplayDevice>(),
                DeviceName = new string('\0', 32),
                DeviceString = new string('\0', 128),
                DeviceID = new string('\0', 128),
                DeviceKey = new string('\0', 128)
            };
        }

        private string? SelectedDisplayDeviceName => selectedMonitor?.DeviceName;

        private static uint ToRefreshX1K(double refreshRate)
        {
            return (uint)Math.Round(refreshRate * 1000.0);
        }

        private static string PresetHistoryPath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickAltTab",
                "created-presets.txt");

        private static void RememberCreatedPreset(int width, int height, uint refreshX1K)
        {
            try
            {
                var presets = LoadRememberedPresets().ToHashSet();
                presets.Add(new NvidiaAPI.CustomDisplayMode(width, height, refreshX1K));

                string path = PresetHistoryPath;
                string? directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllLines(path, presets.Select(mode => $"{mode.Width},{mode.Height},{mode.RefreshX1K}"));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to remember created preset.", ex);
            }
        }

        private static IEnumerable<NvidiaAPI.CustomDisplayMode> LoadRememberedPresets()
        {
            string path = PresetHistoryPath;
            if (!System.IO.File.Exists(path))
            {
                return [];
            }

            var result = new List<NvidiaAPI.CustomDisplayMode>();
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string[] parts = line.Split(',');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) &&
                    uint.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint refreshX1K))
                {
                    result.Add(new NvidiaAPI.CustomDisplayMode(width, height, refreshX1K));
                }
            }

            return result;
        }

        private static void ClearRememberedPresets()
        {
            try
            {
                string path = PresetHistoryPath;
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to clear remembered presets.", ex);
            }
        }

        private static void RemoveRememberedPreset(int width, int height, uint refreshX1K)
        {
            try
            {
                var presets = LoadRememberedPresets()
                    .Where(mode => mode.Width != width ||
                                   mode.Height != height ||
                                   Math.Abs((long)mode.RefreshX1K - refreshX1K) > 1)
                    .ToHashSet();

                string path = PresetHistoryPath;
                if (presets.Count == 0)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }

                    return;
                }

                string? directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllLines(path, presets.Select(mode => $"{mode.Width},{mode.Height},{mode.RefreshX1K}"));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to remove remembered preset.", ex);
            }
        }

        private bool ValidateInputs()
        {
            if (!int.TryParse(NativeWidth.Text, out int nativeWidth) || nativeWidth <= 0)
            {
                ShowError("Invalid native width");
                return false;
            }

            if (!int.TryParse(NativeHeight.Text, out int nativeHeight) || nativeHeight <= 0)
            {
                ShowError("Invalid native height");
                return false;
            }

            if (!TryParseRefreshRate(NativeRefreshRate.Text, out double refresh) || refresh <= 0)
            {
                ShowError("Invalid refresh rate");
                return false;
            }

            if (!int.TryParse(CustomWidth.Text, out int customWidth) || customWidth <= 0)
            {
                ShowError("Invalid custom width");
                return false;
            }

            if (!int.TryParse(CustomHeight.Text, out int customHeight) || customHeight <= 0)
            {
                ShowError("Invalid custom height");
                return false;
            }

            return true;
        }

        private static double ParseRefreshRate(string value)
        {
            if (TryParseRefreshRate(value, out double refresh))
            {
                return refresh;
            }

            throw new FormatException("Invalid refresh rate");
        }

        private static bool TryParseRefreshRate(string value, out double refresh)
        {
            return double.TryParse(
                value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out refresh);
        }

        private void ShowError(string message)
        {
            Logger.Error(message);
            StatusBorder.Visibility = Visibility.Collapsed;
        }

        private void HideStatusAfterDelay(int delayMilliseconds)
        {
            hideStatusTimer?.Stop();
            hideStatusTimer = new System.Windows.Threading.DispatcherTimer();
            hideStatusTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
            hideStatusTimer.Tick += (s, e) =>
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                hideStatusTimer?.Stop();
            };
            hideStatusTimer.Start();
        }
    }
}
