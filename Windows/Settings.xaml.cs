using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
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
            destinationTextBox.Text = Properties.Settings.Default.DestDirectory;
            structureComboBox.SelectedIndex = Properties.Settings.Default.DestStructure;
            deleteCheckBox.IsChecked = Properties.Settings.Default.DeleteAfterIngest;

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location);
            versionText.Text = string.Format("V{0}.{1}.{2}",
                versionInfo.ProductMajorPart, versionInfo.ProductMinorPart, versionInfo.ProductBuildPart);
        }

        private void DestinationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateFields();
        }

        private void SelectDestButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new();

            if (dialog.ShowDialog() == true)
                destinationTextBox.Text = dialog.FolderName;
        }

        private void StructureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateFields();
        }

        private void DeleteCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ValidateFields();
        }

        /// <summary>
        /// Enables or disables the save button depending on whether the field values have changed and are valid.
        /// </summary>
        private void ValidateFields()
        {
            if ((destinationTextBox.Text.Length > 0 &&
                destinationTextBox.Text != Properties.Settings.Default.DestDirectory) ||
                structureComboBox.SelectedIndex != Properties.Settings.Default.DestStructure ||
                deleteCheckBox.IsChecked != Properties.Settings.Default.DeleteAfterIngest)
            {
                saveButton.IsEnabled = true;
            }
            else saveButton.IsEnabled = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DestDirectory = destinationTextBox.Text;
            Properties.Settings.Default.DestStructure = structureComboBox.SelectedIndex;
            Properties.Settings.Default.DeleteAfterIngest = (bool)deleteCheckBox.IsChecked!;
            Properties.Settings.Default.Save();

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
