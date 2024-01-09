using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace DcimIngester.Windows
{
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxDestDir.Text = Properties.Settings.Default.DestDirectory;
            ComboBoxDestStruc.SelectedIndex = Properties.Settings.Default.DestStructure;
            CheckBoxDelete.IsChecked = Properties.Settings.Default.DeleteAfterIngest;
        }

        private void TextBoxDestDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateFields();
        }

        private void ButtonBrowseDest_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();

            if (dialog.ShowDialog() == true)
                TextBoxDestDir.Text = dialog.FolderName;
        }

        private void ComboBoxDestStruc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateFields();
        }

        private void CheckBoxDelete_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ValidateFields();
        }

        /// <summary>
        /// Enables or disables the save button depending on whether the field values have changed and are valid.
        /// </summary>
        private void ValidateFields()
        {
            if ((TextBoxDestDir.Text.Length > 0 &&
                TextBoxDestDir.Text != Properties.Settings.Default.DestDirectory) ||
                ComboBoxDestStruc.SelectedIndex != Properties.Settings.Default.DestStructure ||
                CheckBoxDelete.IsChecked != Properties.Settings.Default.DeleteAfterIngest)
            {
                ButtonSave.IsEnabled = true;
            }
            else ButtonSave.IsEnabled = false;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DestDirectory = TextBoxDestDir.Text;
            Properties.Settings.Default.DestStructure = ComboBoxDestStruc.SelectedIndex;
            Properties.Settings.Default.DeleteAfterIngest = (bool)CheckBoxDelete.IsChecked!;
            Properties.Settings.Default.Save();

            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
