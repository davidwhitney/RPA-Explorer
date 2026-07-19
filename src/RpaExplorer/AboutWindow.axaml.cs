using System.Reflection;
using Avalonia.Controls;

namespace RpaExplorer
{
    public partial class AboutWindow : Window
    {
        private static readonly string[] TranslatorsList = ["-"];
        private static readonly string[] ContributorsList = ["-"];

        private readonly string _appVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        private readonly string _appCreator = "Martin Suchy";
        private readonly string _appRepository = "https://github.com/UniverseDevel/RPA-Explorer";

        public AboutWindow()
        {
            InitializeComponent();

            Title = MainWindow.GetText("About");
            AboutText.Text = string.Format(
                MainWindow.GetText("About_text"),
                _appVersion,
                _appCreator,
                _appRepository,
                string.Join(", ", TranslatorsList),
                string.Join(", ", ContributorsList));

            RepoButton.Click += (_, _) => Platform.OpenUrl(_appRepository);
            CloseButton.Click += (_, _) => Close();
        }
    }
}
