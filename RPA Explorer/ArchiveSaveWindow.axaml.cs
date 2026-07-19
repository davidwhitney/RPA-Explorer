using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RPA_Parser;

namespace RPA_Explorer
{
    public partial class ArchiveSaveWindow : Window
    {
        private readonly RpaParser _rpaParser;

        // Parameterless ctor for the XAML designer/loader.
        public ArchiveSaveWindow()
        {
            InitializeComponent();
        }

        public ArchiveSaveWindow(RpaParser rpaParser) : this()
        {
            _rpaParser = rpaParser;

            LoadTexts();

            VersionCombo.Items.Add(RpaParser.Version.RPA_3_2);
            VersionCombo.Items.Add(RpaParser.Version.RPA_3);
            VersionCombo.Items.Add(RpaParser.Version.RPA_2);
            VersionCombo.Items.Add(RpaParser.Version.RPA_1);

            VersionCombo.SelectedItem =
                _rpaParser.CheckVersion(_rpaParser.ArchiveVersion, RpaParser.Version.Unknown)
                    ? RpaParser.Version.RPA_3
                    : _rpaParser.ArchiveVersion;

            PaddingBox.Text = _rpaParser.Padding.ToString();
            KeyBox.Text = _rpaParser.ObfuscationKey.ToString();

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
                _rpaParser.ArchiveVersion = _rpaParser.CheckSupportedVersion((double) VersionCombo.SelectedItem);
                _rpaParser.Padding = Convert.ToInt32(PaddingBox.Text);
                _rpaParser.ObfuscationKey = Convert.ToInt64(KeyBox.Text);
                _rpaParser.OptionsConfirmed = true;
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

            switch ((double) VersionCombo.SelectedItem)
            {
                case RpaParser.Version.RPA_1:
                    PaddingBox.IsEnabled = false;
                    KeyBox.IsEnabled = false;
                    PaddingBox.Text = "0";
                    KeyBox.Text = "0";
                    break;
                case RpaParser.Version.RPA_2:
                    PaddingBox.IsEnabled = true;
                    KeyBox.IsEnabled = false;
                    PaddingBox.Text = _rpaParser.Padding.ToString();
                    KeyBox.Text = "0";
                    break;
                default:
                    PaddingBox.IsEnabled = true;
                    KeyBox.IsEnabled = true;
                    PaddingBox.Text = _rpaParser.Padding.ToString();
                    KeyBox.Text = _rpaParser.ObfuscationKey.ToString();
                    break;
            }
        }
    }
}
