using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MahApps.Metro.Controls;

namespace CemYabansu.PublishInCrm.Windows
{
    public partial class OutputWindow
    {
        private enum CurrentStatus
        {
            Connection,
            GettingWebresources,
            CreatingWebresources,
            UpdatingWebresources,
            Publishing
        }

        private static string _errorImagePath = @"..\Resources\error.png";
        private static string _doneImagePath = @"..\Resources\done.png";

        public OutputWindow()
        {
            InitializeComponent();
        }

        private CurrentStatus _currentStatus;

        public void AddLineToTextBox(string text)
        {
            OutputTextBox.Dispatcher.Invoke(() => AddNewLine(text));
        }

        public void AddErrorLineToTextBox(string errorMessage)
        {
            OutputTextBox.Dispatcher.Invoke(() => AddNewLine(errorMessage));
        }

        private void AddNewLine(string text)
        {
            OutputTextBox.AppendText(text + Environment.NewLine);
        }

        public void SetConnectionLabelText(string text, bool isSucceed)
        {
            SetEnabledToUiElemet(ConnectionLabel, true);
            SetTextToLabel(ConnectionLabel, text);
            var uri = new Uri(isSucceed ? _doneImagePath : _errorImagePath, UriKind.RelativeOrAbsolute);
            SetImageSourceToImage(ConnectionImage, uri);
        }

        private void SetImageSourceToImage(Image image, Uri uri)
        {
            Dispatcher.Invoke(() => image.Source = new BitmapImage(uri));
        }

        public void StartUpdating()
        {
            _currentStatus = CurrentStatus.UpdatingWebresources;
            SetActivityToProgressRing(UpdateProgressRing, true);
            SetEnabledToUiElemet(UpdateLabel, true);
        }

        public void FinishUpdating(bool isSucceed)
        {
            SetVisiblityToUiElemet(UpdateProgressRing, Visibility.Collapsed);
            SetVisiblityToUiElemet(UpdateImage, Visibility.Visible);
            var uri = new Uri(isSucceed ? _doneImagePath : _errorImagePath, UriKind.RelativeOrAbsolute);
            SetImageSourceToImage(UpdateImage, uri);
        }

        public void StartGettingWebresources()
        {
            _currentStatus = CurrentStatus.GettingWebresources;
            SetActivityToProgressRing(GettingWebresourcesProgressRing, true);
            SetEnabledToUiElemet(GettingWebresourcesLabel, true);
        }

        public void FinishGettingWebresources(bool isSucceed)
        {
            SetVisiblityToUiElemet(GettingWebresourcesProgressRing, Visibility.Collapsed);
            SetVisiblityToUiElemet(GettingWebresourcesImage, Visibility.Visible);
            var uri = new Uri(isSucceed ? _doneImagePath : _errorImagePath, UriKind.RelativeOrAbsolute);
            SetImageSourceToImage(GettingWebresourcesImage, uri);
        }

        public void StartCreating()
        {
            _currentStatus = CurrentStatus.CreatingWebresources;
            SetActivityToProgressRing(CreateProgressRing, true);
            SetEnabledToUiElemet(CreateLabel, true);
        }

        public void FinishCreating(bool isSucceed)
        {
            SetVisiblityToUiElemet(CreateProgressRing, Visibility.Collapsed);
            SetVisiblityToUiElemet(CreateImage, Visibility.Visible);
            var uri = new Uri(isSucceed ? _doneImagePath : _errorImagePath, UriKind.RelativeOrAbsolute);
            SetImageSourceToImage(CreateImage, uri);
        }

        public void StartPublishing()
        {
            _currentStatus = CurrentStatus.Publishing;
            SetActivityToProgressRing(PublishProgressRing, true);
            SetEnabledToUiElemet(PublishLabel, true);
        }

        public void FinishPublishing(bool isSucceed, string text)
        {
            if (!string.IsNullOrEmpty(text)) SetTextToLabel(PublishLabel, text);
            SetVisiblityToUiElemet(PublishProgressRing, Visibility.Collapsed);
            SetVisiblityToUiElemet(PublishImage, Visibility.Visible);
            var uri = new Uri(isSucceed ? _doneImagePath : _errorImagePath, UriKind.RelativeOrAbsolute);
            SetImageSourceToImage(PublishImage, uri);
        }

        public void AddErrorText(string message)
        {
            SetVisiblityToUiElemet(ErrorImage, Visibility.Visible);
            SetVisiblityToUiElemet(ErrorLabel, Visibility.Visible);
            SetTextToLabel(ErrorLabel, message);
            SetErrorToCurrentProcess();
        }

        private void SetErrorToCurrentProcess()
        {
            var uri = new Uri(_errorImagePath, UriKind.RelativeOrAbsolute);
            switch (_currentStatus)
            {
                case CurrentStatus.Connection:
                    SetImageSourceToImage(ConnectionImage, uri);
                    SetVisiblityToUiElemet(ConnectionImage, Visibility.Visible);
                    break;
                case CurrentStatus.GettingWebresources:
                    SetImageSourceToImage(GettingWebresourcesImage, uri);
                    SetVisiblityToUiElemet(GettingWebresourcesImage, Visibility.Visible);
                    SetVisiblityToUiElemet(GettingWebresourcesProgressRing, Visibility.Collapsed);
                    break;
                case CurrentStatus.CreatingWebresources:
                    SetImageSourceToImage(CreateImage, uri);
                    SetVisiblityToUiElemet(CreateImage, Visibility.Visible);
                    SetVisiblityToUiElemet(CreateProgressRing, Visibility.Collapsed);
                    break;
                case CurrentStatus.UpdatingWebresources:
                    SetImageSourceToImage(UpdateImage, uri);
                    SetVisiblityToUiElemet(UpdateImage, Visibility.Visible);
                    SetVisiblityToUiElemet(UpdateProgressRing, Visibility.Collapsed);
                    break;
                case CurrentStatus.Publishing:
                    SetImageSourceToImage(PublishImage, uri);
                    SetVisiblityToUiElemet(PublishImage, Visibility.Visible);
                    SetVisiblityToUiElemet(PublishProgressRing, Visibility.Collapsed);
                    break;
            }
        }

        private void SetTextToLabel(ContentControl label, string text)
        {
            Dispatcher.Invoke(() => label.Content = text);
        }

        private void SetVisiblityToUiElemet(UIElement uiElement, Visibility visibility)
        {
            Dispatcher.Invoke(() => uiElement.Visibility = visibility);
        }

        private void SetEnabledToUiElemet(UIElement uiElement, bool isEnabled)
        {
            Dispatcher.Invoke(() => uiElement.IsEnabled = isEnabled);
        }

        private void SetActivityToProgressRing(ProgressRing progressRing, bool isActive)
        {
            Dispatcher.Invoke(() => progressRing.IsActive = isActive);
        }

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (ShowDetailsButton.IsChecked == true)
            {
                SetVisiblityToUiElemet(OutputTextBox, Visibility.Visible);
                Height += 180;
            }
            else
            {
                SetVisiblityToUiElemet(OutputTextBox, Visibility.Hidden);
                Height -= 180;
            }
        }
    }
}