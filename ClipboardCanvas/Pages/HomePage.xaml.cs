﻿using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using ClipboardCanvas.ViewModels.Pages;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ClipboardCanvas.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePageViewModel ViewModel
        {
            get => (HomePageViewModel)DataContext;
            set => DataContext = value;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            CollectionsWidget.ViewModel.Dispose();
            this.ViewModel.Dispose();

            base.OnNavigatingFrom(e);
        }

        public HomePage()
        {
            this.InitializeComponent();

            this.ViewModel = new HomePageViewModel();
        }

        private async void TimelineWidget_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await this.ViewModel.LoadWidgets();
        }
    }
}
