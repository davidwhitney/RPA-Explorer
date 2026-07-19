using System.Reflection;
using Avalonia.Controls;

namespace RpaExplorer
{
    public partial class AboutWindow : Window
    {
        private static readonly string[] TranslatorsList = ["-"];

        // Credited from the commit history of both repositories.
        private static readonly string[] ContributorsList = ["jensbrak"];

        private readonly string _appVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

        // The archive format handling is the original author's; this fork is the UI layer
        // and the cross-platform plumbing, so both are credited and both links are offered.
        private const string OriginalAuthor = "Martin Suchy";
        private const string OriginalRepository = "https://github.com/UniverseDevel/RPA-Explorer";
        private const string PortAuthor = "David Whitney";
        private const string PortRepository = "https://github.com/davidwhitney/RPA-Explorer";

        public AboutWindow()
        {
            InitializeComponent();

            Title = MainWindow.GetText("About");
            AboutText.Text = string.Format(
                MainWindow.GetText("About_text"),
                _appVersion,
                OriginalAuthor,
                OriginalRepository,
                PortAuthor,
                PortRepository,
                string.Join(", ", TranslatorsList),
                string.Join(", ", ContributorsList));

            RepoButton.Click += (_, _) => Platform.OpenUrl(PortRepository);
            UpstreamButton.Click += (_, _) => Platform.OpenUrl(OriginalRepository);
            CloseButton.Click += (_, _) => Close();
        }
    }
}
