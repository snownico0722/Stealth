using System;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Stealth.ViewModel;

namespace Stealth
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            UpdateLanguageMenu();
            Closed += (s, e) => ((ViewModelLocator)Application.Current.Resources["Locator"]).Cleanup();
            WeakReferenceMessenger.Default.Register<ShowAboutViewMessage>(this, (recipient, msg) => ((MainWindow)recipient).NotificationMessageReceived(msg));
        }

        private void UpdateLanguageMenu()
        {
            EnglishLanguageMenu.IsChecked = App.CurrentLanguage == "en-US";
            ChineseLanguageMenu.IsChecked = App.CurrentLanguage == "zh-CN";
        }
        private void EnglishLanguageMenu_Click(object sender, RoutedEventArgs e)
        {
            App.ApplyLanguage("en-US");
            UpdateLanguageMenu();
        }
        private void ChineseLanguageMenu_Click(object sender, RoutedEventArgs e)
        {
            App.ApplyLanguage("zh-CN");
            UpdateLanguageMenu();
        }

        private void NotificationMessageReceived(ShowAboutViewMessage msg)
        {
            if (msg.Value == "ShowAboutView")
            {
                new AboutView().ShowDialog();
            }
        }
    }
}