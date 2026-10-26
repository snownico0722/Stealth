using Stealth.Model;
using Stealth.Properties;
using Stealth.ViewModel;
using System;
using System.Globalization;
using System.Windows;

namespace Stealth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private OverlayService _overlayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplyLanguage(Settings.Default.UiLanguage, false);
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Closed += MainWindow_Closed;
            mainWindow.Show();

            SetOverlayEnabled(Settings.Default.OverlayEnabled);
        }

        public static string CurrentLanguage { get; private set; } = "en-US";

        public static string Localize(string key)
        {
            return Current.TryFindResource(key) as string ?? key;
        }

        public static void ApplyLanguage(string requestedLanguage, bool savePreference = true)
        {
            string language = requestedLanguage == "zh-CN" || requestedLanguage == "en-US"
                ? requestedLanguage
                : (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US");
            // Resolve before changing resources, avoiding an empty UI on failure.
            var replacement = new ResourceDictionary
            {
                Source = new Uri("/Stealth;component/Languages/Strings." + language + ".xaml", UriKind.Relative)
            };
            var merged = Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source?.OriginalString;
                if (source != null && source.Contains("/Strings.") &&
                    source.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    merged.RemoveAt(i);
            }
            merged.Add(replacement);
            CurrentLanguage = language;
            if (savePreference)
            {
                Settings.Default.UiLanguage = language;
                Settings.Default.Save();
            }
        }

        public void SetOverlayEnabled(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_overlayService == null)
                {
                    _overlayService = new OverlayService();
                    _overlayService.OpacityApplied += OverlayService_OpacityApplied;
                    _overlayService.Start();
                }

                return;
            }

            DisposeOverlayService();
        }

        private void OverlayService_OpacityApplied(IntPtr handle, byte opacity)
        {
            ((ViewModelLocator)Resources["Locator"]).Main.UpdateOpacityFromOverlay(handle, opacity);
        }

        private void DisposeOverlayService()
        {
            if (_overlayService == null)
            {
                return;
            }

            _overlayService.OpacityApplied -= OverlayService_OpacityApplied;
            _overlayService.Dispose();
            _overlayService = null;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            DisposeOverlayService();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DisposeOverlayService();
            base.OnExit(e);
        }
    }
}
