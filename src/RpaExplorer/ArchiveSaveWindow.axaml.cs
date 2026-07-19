using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RpaParser;

namespace RpaExplorer
{
    public partial class ArchiveSaveWindow : Window
    {
        private readonly Archive _archive;

        // Parameterless ctor for the XAML designer/loader.
        public ArchiveSaveWindow()
        {
            InitializeComponent();
        }

        public ArchiveSaveWindow(Archive archive) : this()
        {
            _archive = archive;

            LoadTexts();

            // The dialog offers the formats themselves, so there is no parallel list of
            // version numbers to keep in step.
            foreach (var format in ArchiveFormat.All)
            {
                VersionCombo.Items.Add(format);
            }

            VersionCombo.SelectedItem = _archive.Format ?? ArchiveFormat.Rpa3;

            PaddingBox.Text = _archive.Padding.ToString();
            KeyBox.Text = _archive.ObfuscationKey.ToString();

            VersionCombo.SelectionChanged += VersionCombo_SelectionChanged;
            ContinueButton.Click += ContinueButton_Click;
            CancelButton.Click += (_, _) => Close(false);
        }

        private void LoadTexts()
        {
            Title = MainWindow.GetText("Archive_save_title");
            VersionLabel.Text = MainWindow.GetText("Archive_save_version");
            PaddingLabel.Text = MainWindow.GetText("Archive_save_padding");
            KeyLabel.Text = MainWindow.GetText("Archive_save_obfuscationkey");
            ContinueButton.Content = MainWindow.GetText("Archive_save_continue");
            CancelButton.Content = MainWindow.GetText("Archive_save_cancel");
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _archive.Format = (ArchiveFormat) VersionCombo.SelectedItem;
                _archive.Padding = Convert.ToInt32(PaddingBox.Text);
                _archive.ObfuscationKey = Convert.ToInt64(KeyBox.Text);
                _archive.OptionsConfirmed = true;
                Close(true);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowError(this, ex.Message, MainWindow.GetText("Invalid_values"));
            }
        }

        private void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionCombo.SelectedItem == null)
            {
                return;
            }

            // Offer each option only where the chosen format actually supports it, rather
            // than enumerating the versions that happen to.
            var format = (ArchiveFormat) VersionCombo.SelectedItem;

            PaddingBox.IsEnabled = format.SupportsPadding;
            KeyBox.IsEnabled = format.UsesObfuscation;

            PaddingBox.Text = format.SupportsPadding ? _archive.Padding.ToString() : "0";
            KeyBox.Text = format.UsesObfuscation ? _archive.ObfuscationKey.ToString() : "0";
        }
    }
}
