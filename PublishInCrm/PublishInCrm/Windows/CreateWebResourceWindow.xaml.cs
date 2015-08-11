using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xrm.Sdk;

namespace CemYabansu.PublishInCrm.Windows
{
    public partial class CreateWebResourceWindow
    {
        public WebResource CreatedWebResource { get; set; }

        public CreateWebResourceWindow(string fileName)
        {
            InitializeComponent();

            NameTextBox.Dispatcher.Invoke(new Action(() => NameTextBox.Text = fileName));
            DisplayNameTextBox.Dispatcher.Invoke(new Action(() => DisplayNameTextBox.Text = fileName));

            if (Path.GetExtension(fileName) == ".js")
            {
                TypeComboBox.Dispatcher.Invoke(new Action(() => TypeComboBox.SelectedIndex = 2));
            }
            else if (Path.GetExtension(fileName) == ".css")
            {
                TypeComboBox.Dispatcher.Invoke(new Action(() => TypeComboBox.SelectedIndex = 1));
            }
            else if (Path.GetExtension(fileName) == ".html" || Path.GetExtension(fileName) == ".htm")
            {
                TypeComboBox.Dispatcher.Invoke(new Action(() => TypeComboBox.SelectedIndex = 0));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CreatedWebResource = null;
            Close();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            CreatedWebResource = new WebResource()
            {
                Name = NameTextBox.Text,
                DisplayName = DisplayNameTextBox.Text,
                Description = DescriptionTextBox.Text,
                WebResourceType = new OptionSetValue((int.Parse(((ComboBoxItem)TypeComboBox.SelectedItem).Tag.ToString())))
            };
            Close();
        }
    }
}
