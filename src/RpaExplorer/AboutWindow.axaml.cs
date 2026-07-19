using System.Reflection;
using Avalonia.Controls;

namespace RpaExplorer
{
    public partial class AboutWindow : Window
    {
        private static readonly string[] TranslatorsList = ["-"];

        // Credited from the commit history of both repositories.
        private static readonly string[] ContributorsList = ["jensbrak"];

        // The informational version is the git tag MinVer derived, so it keeps any
        // pre-release suffix that the four-part assembly version would drop.
        private readonly string _appVersion = InformationalVersion();

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

        private static string InformationalVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            // MinVer appends "+<commit sha>", which is noise in a dialog.
            return informational?.Split('+')[0]
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }
}
