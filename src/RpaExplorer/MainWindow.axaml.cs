using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using RpaParser;

namespace RpaExplorer
{
    public partial class MainWindow : Window
    {
        private Archive _archive;

        // Where the external decompiler lives is a machine setting, so it sits alongside
        // the archive rather than inside it.
        private readonly DecompilerOptions _decompilerOptions = new();
        private PreviewFactory Previews => new(_decompilerOptions);

        private bool _archiveChanged;
        private bool _archiveLoaded;
        private bool _cancelAdd;
        private bool _forceClose;
        private volatile bool _operationEnabled = true;

        private SortedDictionary<string, ArchiveEntry> _fileListBackup = new();
        private readonly Dictionary<string, long> _indexPathSize = new();
        private FileNode _root;
        private int _searchStartIndex;

        // LibVLC media playback (https://code.videolan.org/mfkl/libvlcsharp-samples)
        private LibVLC _libVlc;
        private MediaPlayer _mediaPlayer;
        private MemoryStream _memoryStreamVlc;
        private StreamMediaInput _streamMediaInputVlc;
        private Media _mediaVlc;
        private string _mediaUnavailableReason = string.Empty;

        private readonly string _settingsPath;
        private static Settings _settings;

        public MainWindow()
        {
            InitializeComponent();

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPA Explorer");
            Directory.CreateDirectory(appDataDir);
            _settingsPath = Path.Combine(appDataDir, "settings.ini");
            _settings = new Settings(_settingsPath);

            foreach (var lang in _settings.LangList)
            {
                LanguageCombo.Items.Add(lang.Name);
            }
            LanguageCombo.SelectedItem = _settings.GetLang().Name;

            LoadTexts();

            // LibVLC initiation
            try
            {
                if (VlcSetup.Initialize())
                {
                    _libVlc = new LibVLC(VlcSetup.PlayerOptions());
                    _mediaPlayer = new MediaPlayer(_libVlc) { Volume = 50 };
                    _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
                    _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
                }
                else
                {
                    _mediaUnavailableReason = VlcSetup.UnavailableReason;
                }
            }
            catch (Exception ex)
            {
                // Media playback unavailable; previews of other types still work and the
                // media tab reports the actual reason when the user opens a media file.
                _mediaUnavailableReason = ex.Message + Environment.NewLine + Environment.NewLine +
                                          VlcSetup.InstallHint;
                _libVlc = null;
                _mediaPlayer = null;
            }
            SetMediaTimeLabel(0, 0);

            WireEvents();

            AssociateMenuItem.IsVisible = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // Attach the media player to the native VideoView only after the window is shown,
            // and open any archive passed on the command line. Doing the native attach in the
            // constructor can race with native view creation on macOS.
            Opened += OnOpened;
        }

        private bool _opened;

        private async void OnOpened(object sender, EventArgs e)
        {
            if (_opened)
            {
                return;
            }
            _opened = true;

            if (_mediaPlayer != null)
            {
                try
                {
                    VideoView.MediaPlayer = _mediaPlayer;
                }
                catch
                {
                    // ignored - media stays unavailable
                }
            }

            var args = Program.StartupArgs;
            if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
            {
                await LoadArchive(args[0]);
            }
        }

        private void WireEvents()
        {
            CreateButton.Click += async (_, _) => await CreateNewArchive();
            LoadButton.Click += async (_, _) => await LoadArchive(string.Empty);
            ExportButton.Click += async (_, _) => await ExportChecked();
            CancelButton.Click += (_, _) => _operationEnabled = false;
            RemoveButton.Click += (_, _) => RemoveChecked();
            SaveButton.Click += async (_, _) => await SaveArchive();
            SearchButton.Click += (_, _) => DoSearch();
            PlayPauseButton.Click += (_, _) => PlayPauseMedia();

            Tree.SelectionChanged += (_, _) => { PreviewSelectedItem(); GenerateArchiveInfo(); };
            LanguageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
            VolumeSlider.ValueChanged += (_, _) =>
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = (int) VolumeSlider.Value;
                }
            };

            AssociateMenuItem.Click += async (_, _) =>
                await MessageBox.ShowInfo(this,
                    "File association is only available on Windows.", GetText("Options"));
            DownloadUnrpycMenuItem.Click += async (_, _) => await DownloadUnrpyc(true);
            UnrpycMenuItem.Click += async (_, _) => await DefineUnrpycLocation();
            PythonMenuItem.Click += async (_, _) => await DefinePythonLocation();
            AboutMenu.Click += (_, _) => new AboutWindow().ShowDialog(this);

            Tree.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
            Tree.AddHandler(DragDrop.DropEvent, OnTreeDrop);
            Tabs.AddHandler(DragDrop.DragOverEvent, OnTabsDragOver);
            Tabs.AddHandler(DragDrop.DropEvent, OnTabsDrop);

            Closing += OnClosing;
        }

        // ----- Localization -----

        internal static string GetText(string name)
        {
            return Strings.Get(_settings?.GetLang().Abbrev ?? "EN", name);
        }

        private void LoadTexts()
        {
            Title = GetText("Explorer_title");
            LanguageLabel.Text = GetText("Language");
            LoadButton.Content = GetText("Load_file");
            ExportButton.Content = GetText("Export_checked");
            StatusText.Text = GetText("Ready");
            CancelButton.Content = GetText("Cancel_operation");
            TabNone.Header = GetText("None");
            UsageLabel.Text = _archiveLoaded ? GetText("Usage_instructions_loaded") : GetText("Usage_instructions_new");
            TabImage.Header = GetText("Image");
            TabText.Header = GetText("Text");
            TabMedia.Header = GetText("Media");
            PlayPauseButton.Content = GetText("Pause");
            CreateButton.Content = GetText("Create_new_archive");
            FileListLabel.Text = GetText("File_list");
            RemoveButton.Content = GetText("Remove_checked");
            SaveButton.Content = GetText("Save_archive");
            SearchButton.Content = GetText("Search_next");
            SearchLabel.Text = GetText("Search");
            OptionsMenu.Header = GetText("Options");
            AssociateMenuItem.Header = GetText("File_association");
            UnrpycMenuItem.Header = GetText("Locate_unrpyc");
            PythonMenuItem.Header = GetText("Locate_python");
            AboutMenu.Header = GetText("About");

            GenerateArchiveInfo();
        }

        // Ensures LibVLC is usable, retrying detection in case VLC was installed after
        // start-up. When it is still missing the user is prompted with a way to get it.
        private async Task<bool> EnsureMediaAvailable()
        {
            if (_mediaPlayer != null)
            {
                return true;
            }

            try
            {
                if (VlcSetup.Initialize())
                {
                    _libVlc = new LibVLC(VlcSetup.PlayerOptions());
                    _mediaPlayer = new MediaPlayer(_libVlc) { Volume = (int) VolumeSlider.Value };
                    _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
                    _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
                    _mediaUnavailableReason = string.Empty;
                    return true;
                }

                _mediaUnavailableReason = VlcSetup.UnavailableReason;
            }
            catch (Exception ex)
            {
                _mediaUnavailableReason = ex.Message;
                _libVlc = null;
                _mediaPlayer = null;
            }

            await PromptInstallVlc();
            return false;
        }

        private async Task PromptInstallVlc()
        {
            var openPage = await MessageBox.ShowYesNo(this,
                GetText(Platform.HasHomebrew ? "Vlc_required_prompt_brew" : "Vlc_required_prompt"),
                GetText("Vlc_required"));

            if (openPage)
            {
                Platform.OpenUrl(VlcSetup.DownloadUrl);
            }
        }

        // Fetches unrpyc from GitHub and selects it. Returns true when unrpyc is available
        // afterwards. When confirm is false the caller has already asked the user.
        private async Task<bool> DownloadUnrpyc(bool confirm)
        {
            if (confirm)
            {
                var proceed = await MessageBox.ShowYesNo(this,
                    string.Format(GetText("Download_unrpyc_prompt"),
                        UnrpycInstaller.Version,
                        UnrpycInstaller.DownloadUrl,
                        UnrpycInstaller.ToolsDirectory),
                    GetText("Download_unrpyc"));

                if (!proceed)
                {
                    return false;
                }
            }

            var previousStatus = StatusText.Text;
            try
            {
                Progress<string> progress = new(message => StatusText.Text = message);
                StatusText.Text = GetText("Downloading_unrpyc");

                var script = await UnrpycInstaller.EnsureAsync(progress);

                _settings.SetUnrpyc(script);
                if (_archive != null)
                {
                    _decompilerOptions.UnrpycPath = script;
                }

                StatusText.Text = GetText("Ready");
                await MessageBox.ShowInfo(this,
                    string.Format(GetText("Unrpyc_ready"),
                        UnrpycInstaller.Version, script, UnrpycInstaller.MinimumPython),
                    GetText("Download_unrpyc"));

                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = previousStatus;
                await MessageBox.ShowError(this,
                    string.Format(GetText("Unrpyc_download_failed"), ex.Message),
                    GetText("Download_unrpyc"));
                return false;
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageCombo.SelectedItem is string lang)
            {
                _settings.SetLang(lang);
                LoadTexts();
            }
        }

        // ----- Tree building -----

        private void GenerateTreeView()
        {
            // Preserve currently-expanded folders across the rebuild.
            HashSet<string> expanded = [];
            if (_root != null)
            {
                foreach (var node in _root.All())
                {
                    if (node.IsExpanded)
                    {
                        expanded.Add(node.FullPath);
                    }
                }
            }

            _indexPathSize.Clear();
            _indexPathSize[string.Empty] = 0;

            _root = new FileNode { Name = "/", FullPath = string.Empty, IsFolder = true, InArchive = true };
            Dictionary<string, FileNode> nodeByPath = new() { [string.Empty] = _root };

            foreach (var kvp in _archive.Index)
            {
                var parts = kvp.Key.Split('/');
                var build = string.Empty;
                var parent = _root;

                for (var i = 0; i < parts.Length; i++)
                {
                    var isFile = i == parts.Length - 1;
                    build = build == string.Empty ? parts[i] : build + "/" + parts[i];

                    if (!nodeByPath.TryGetValue(build, out var node))
                    {
                        node = new FileNode
                        {
                            Name = parts[i],
                            FullPath = build,
                            IsFolder = !isFile,
                            Parent = parent,
                            InArchive = isFile ? kvp.Value.InArchive : true
                        };
                        parent.Children.Add(node);
                        nodeByPath[build] = node;
                    }

                    parent = node;
                }
            }

            // Folder size accumulation (matches the original behaviour).
            foreach (var kvp in _archive.Index)
            {
                var length = kvp.Value.Length;
                var parts = kvp.Key.Split('/');
                var build = string.Empty;

                for (var i = 0; i < parts.Length; i++)
                {
                    build = build == string.Empty ? parts[i] : build + "/" + parts[i];
                    if (i < parts.Length - 1)
                    {
                        if (!_indexPathSize.ContainsKey(build))
                        {
                            _indexPathSize[build] = 0;
                        }
                        _indexPathSize[build] += length;
                    }
                }
                _indexPathSize[string.Empty] += length;
            }

            // Mark unsaved (new) entries green and propagate to ancestors.
            foreach (var node in _root.All())
            {
                if (!node.IsFolder && !node.InArchive)
                {
                    MarkChanged(node);
                }
            }

            Tree.ItemsSource = new ObservableCollection<FileNode> { _root };

            foreach (var node in _root.All())
            {
                if (expanded.Contains(node.FullPath))
                {
                    node.IsExpanded = true;
                }
            }
            _root.IsExpanded = true;

            _archiveLoaded = true;

            GenerateArchiveInfo();
        }

        private static void MarkChanged(FileNode node)
        {
            node.IsChanged = true;
            var parent = node.Parent;
            while (parent != null)
            {
                parent.IsChanged = true;
                parent = parent.Parent;
            }
        }

        // ----- Archive info panel -----

        private void GenerateArchiveInfo()
        {
            var info = string.Empty;

            if (_archiveLoaded && _archive != null)
            {
                var selectedPath = (Tree.SelectedItem as FileNode)?.FullPath ?? string.Empty;

                long selectedSize = -1;
                var unsavedCount = 0;
                foreach (var kvp in _archive.Index)
                {
                    if (!kvp.Value.InArchive)
                    {
                        unsavedCount++;
                    }
                    if (selectedPath == kvp.Key)
                    {
                        selectedSize = kvp.Value.Length;
                    }
                }

                if (_indexPathSize.ContainsKey(selectedPath))
                {
                    selectedSize = _indexPathSize[selectedPath];
                }

                if (_archive.Format != null)
                {
                    info += GetText("Archive_version") + _archive.Format + Environment.NewLine;
                    info += GetText("Archive_file_location") + _archive.ArchiveInfo.FullName + Environment.NewLine;
                    info += GetText("Archive_file_size") + FormatSize(_archive.ArchiveInfo.Length) + Environment.NewLine;
                    if (_archive.IndexInfo != null)
                    {
                        info += GetText("Index_file_location") + _archive.IndexInfo.FullName + Environment.NewLine;
                        info += GetText("Index_file_size") + FormatSize(_archive.IndexInfo.Length) + Environment.NewLine;
                    }
                }

                info += GetText("Files_count") + _archive.Index.Count + Environment.NewLine;
                info += GetText("Unsaved_files_count") + unsavedCount + Environment.NewLine;

                if (selectedSize != -1)
                {
                    if (selectedPath == string.Empty)
                    {
                        selectedPath = "/";
                    }
                    info += GetText("Selected_file_path") + selectedPath + Environment.NewLine;
                    info += GetText("Selected_file_size") + FormatSize(selectedSize) + Environment.NewLine;
                }
            }

            ArchiveInfo.Text = info.Trim();
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return (unit == 0 ? size.ToString("0") : size.ToString("0.##")) + " " + units[unit];
        }

        // ----- Preview -----

        private void ResetPreviewFields()
        {
            PreviewImage.Source = null;
            PreviewText.Text = string.Empty;
            AudioArt.IsVisible = false;
            StopMedia();
            SetMediaTimeLabel(0, 0);
        }

        private void StopMedia()
        {
            try
            {
                if (_mediaPlayer is { IsPlaying: true })
                {
                    _mediaPlayer.Stop();
                }
            }
            catch
            {
                // ignored
            }

            _mediaVlc?.Dispose();
            _mediaVlc = null;
            _streamMediaInputVlc?.Dispose();
            _streamMediaInputVlc = null;
            _memoryStreamVlc?.Dispose();
            _memoryStreamVlc = null;
        }

        private async void PreviewSelectedItem()
        {
            ResetPreviewFields();

            if (Tree.SelectedItem is not FileNode selected)
            {
                return;
            }

            var path = selected.FullPath;
            if (selected.IsFolder || _archive == null || !_archive.Index.ContainsKey(path))
            {
                Tabs.SelectedItem = TabNone;
                UsageLabel.Text = GetText("Preview_is_not_supported");
                return;
            }

            var unsupported = true;
            string failureMessage = null;
            try
            {
                PreviewResult data = null;
                try
                {
                    data = Previews.Create(_archive, path);
                }
                catch (Exception ex)
                {
                    if (ContentFormat.Detect(path) is CompiledScriptContent
                        && ex.Message.StartsWith(Decompiler.InfoBanner))
                    {
                        // unrpyc simply has not been obtained yet: offer to fetch it and
                        // retry, rather than making the user go and install it by hand.
                        var retried = false;
                        if (string.IsNullOrEmpty(_decompilerOptions.UnrpycPath)
                            && await DownloadUnrpyc(true))
                        {
                            try
                            {
                                data = Previews.Create(_archive, path);
                                retried = true;
                            }
                            catch (Exception retryEx)
                            {
                                data = new PreviewResult(ContentFormat.Text,
                                    string.Format(GetText("Preview_failed_reason_hint"), retryEx.Message));
                                retried = true;
                            }
                        }

                        if (!retried)
                        {
                            data = new PreviewResult(ContentFormat.Text,
                                string.Format(GetText("Preview_failed_reason_hint"), ex.Message));
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                if (data.Format is ImageContent)
                {
                    PreviewImage.Source = Images.DecodeToBitmap(data.AsBytes());
                    Tabs.SelectedItem = TabImage;
                    unsupported = false;
                }
                else if (data.Format is TextContent)
                {
                    _searchStartIndex = 0;
                    PreviewText.Text = data.AsText();
                    Tabs.SelectedItem = TabText;
                    unsupported = false;
                }
                else if (data.Format is AudioContent or VideoContent)
                {
                    // Prompts the user to install VLC when it is missing; the placeholder tab
                    // then explains what to do, so no second error dialog is raised here.
                    if (await EnsureMediaAvailable())
                    {
                        _memoryStreamVlc = new MemoryStream(data.AsBytes());
                        _streamMediaInputVlc = new StreamMediaInput(_memoryStreamVlc);
                        _mediaVlc = new Media(_libVlc, _streamMediaInputVlc);
                        SetMediaTimeLabel(_mediaVlc.Duration, 0);
                        AudioArt.IsVisible = data.Format is AudioContent;
                        PlayPauseButton.Content = GetText("Pause");

                        // The media tab must be visible *before* playback starts: a TabControl
                        // does not realise the content of an unselected tab, so the VideoView's
                        // native surface would not exist yet and libvlc would fail with
                        // "No drawable-nsobject found / video output creation failed".
                        Tabs.SelectedItem = TabMedia;
                        await WaitForVideoSurfaceAsync();
                        _mediaPlayer.Play(_mediaVlc);
                        unsupported = false;
                    }
                    else
                    {
                        failureMessage = GetText("Vlc_not_installed_hint");
                    }
                }
            }
            catch (Exception ex)
            {
                failureMessage = ex.Message;
                await MessageBox.ShowError(this,
                    string.Format(GetText("Preview_failed_reason"), ex.Message), GetText("Preview_failed"));
            }

            if (unsupported)
            {
                Tabs.SelectedItem = TabNone;
                UsageLabel.Text = failureMessage ?? GetText("Preview_is_not_supported");
            }
        }

        // Gives the TabControl a chance to realise the media tab (and with it the native
        // video surface) and re-attaches the player to the freshly created surface. The
        // VideoView is destroyed and recreated whenever the tab is switched away and back,
        // so the association has to be refreshed before every playback.
        private async Task WaitForVideoSurfaceAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            VideoView.MediaPlayer = null;
            VideoView.MediaPlayer = _mediaPlayer;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        }

        // ----- Media controls -----

        private void PlayPauseMedia()
        {
            if (_mediaPlayer == null)
            {
                return;
            }

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                PlayPauseButton.Content = GetText("Play");
            }
            else
            {
                _mediaPlayer.Play();
                PlayPauseButton.Content = GetText("Pause");
            }
        }

        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            // MediaPlayer.Length is the value libvlc refines while decoding; Media.Duration
            // stays at -1 for stream-fed media and never updates the total time.
            SetMediaTimeLabel(_mediaPlayer?.Length ?? 0, e.Time);
        }

        // Fires once libvlc has worked out the length, which for stream-fed media happens
        // after playback has already started.
        private void MediaPlayer_LengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            SetMediaTimeLabel(e.Length, _mediaPlayer?.Time ?? 0);
        }

        private void SetMediaTimeLabel(long totalMs, long currentMs)
        {
            const string timeFormat = @"hh\:mm\:ss\.f";
            const string unknown = "--:--:--.-";

            var current = TimeSpan.FromMilliseconds(currentMs < 0 ? 0 : currentMs);
            string text;

            if (totalMs <= 0)
            {
                // Length is not known yet (or at all). Media fed through StreamMediaInput
                // reports a duration of -1 until libvlc has demuxed enough to work it out,
                // and some formats never report one; show the elapsed time rather than a
                // bogus total that never updates.
                text = current.ToString(timeFormat) + " / " + unknown;
            }
            else
            {
                var total = TimeSpan.FromMilliseconds(totalMs);
                var remainingMs = totalMs - currentMs;
                var remaining = TimeSpan.FromMilliseconds(remainingMs < 0 ? 0 : remainingMs);
                text = current.ToString(timeFormat) + " / " + total.ToString(timeFormat) +
                       " (-" + remaining.ToString(timeFormat) + ")";
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                MediaTimeLabel.Text = text;
            }
            else
            {
                Dispatcher.UIThread.Post(() => MediaTimeLabel.Text = text);
            }
        }

        // ----- Search -----

        private void DoSearch()
        {
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                Search(PreviewText, SearchBox.Text.Trim());
            }
        }

        private void Search(TextBox tb, string pattern)
        {
            tb.Focus();
            var text = tb.Text ?? string.Empty;
            var start = Math.Min(_searchStartIndex, text.Length);

            var index = text.IndexOf(pattern, start, StringComparison.Ordinal);
            if (index == -1)
            {
                _searchStartIndex = 0;
                index = text.IndexOf(pattern, 0, StringComparison.Ordinal);
            }

            if (index != -1)
            {
                tb.SelectionStart = index;
                tb.SelectionEnd = index + pattern.Length;
                tb.CaretIndex = index + pattern.Length;
                _searchStartIndex = index + pattern.Length;
            }
            else
            {
                tb.SelectionStart = tb.SelectionEnd = 0;
            }
        }

        // ----- Checked-item helpers -----

        private List<string> CheckedFiles()
        {
            List<string> list = [];
            if (_root == null)
            {
                return list;
            }
            foreach (var node in _root.All())
            {
                if (node.IsChecked == true && !node.IsFolder && _archive.Index.ContainsKey(node.FullPath))
                {
                    list.Add(node.FullPath);
                }
            }
            return list;
        }

        private void RemoveChecked()
        {
            var changed = false;
            foreach (var node in _root.All())
            {
                if (node.IsChecked == true && !node.IsFolder && _archive.Index.ContainsKey(node.FullPath))
                {
                    _archive.Index.Remove(node.FullPath);
                    changed = true;
                }
            }

            if (changed)
            {
                _archiveChanged = true;
                GenerateTreeView();
            }
        }

        // ----- Load / Create / Save / Export -----

        private async Task CreateNewArchive()
        {
            if (await CheckIfChanged(GetText("Archive_modified_new")))
            {
                return;
            }

            _archive = Archive.Create();
            GenerateTreeView();

            Tabs.SelectedItem = TabNone;
            UsageLabel.Text = GetText("Usage_instructions_loaded");
            ResetPreviewFields();
            Tree.SelectedItem = null;

            ExportButton.IsEnabled = true;
            RemoveButton.IsEnabled = true;
            SaveButton.IsEnabled = true;

            StatusText.Text = GetText("Ready");
            _archiveChanged = true;
        }

        private async Task LoadArchive(string openFile, bool ignoreChanges = false)
        {
            if (!ignoreChanges && await CheckIfChanged(GetText("Archive_modified_load")))
            {
                return;
            }

            string chosen;
            if (string.IsNullOrEmpty(openFile))
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = GetText("Load_RenPy_Archive"),
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(GetText("RPA_RPI_files")) { Patterns = ["*.rpa", "*.rpi"] }
                    ],
                    SuggestedStartLocation = await StartLocation(_settings.GetArchive())
                });

                if (files.Count == 0)
                {
                    return;
                }
                chosen = files[0].TryGetLocalPath();
            }
            else
            {
                if (openFile.EndsWith(".rpa") || openFile.EndsWith(".rpi"))
                {
                    chosen = openFile;
                }
                else
                {
                    await MessageBox.ShowError(this,
                        string.Format(GetText("Load_failed_reason"), GetText("Not_valid_archive_file")),
                        GetText("Load_failed"));
                    StatusText.Text = GetText("Ready");
                    return;
                }
            }

            if (string.IsNullOrEmpty(chosen))
            {
                return;
            }

            _settings.SetArchive(chosen);
            StatusText.Text = GetText("Loading_file") + chosen;

            try
            {
                // An explicitly configured interpreter always wins. Auto-detection is
                // deliberately not written back to the settings file: persisting a guess
                // makes it sticky and stops improved detection from ever taking effect.
                if (!string.IsNullOrEmpty(_settings.GetPython()))
                {
                    _decompilerOptions.PythonPath = _settings.GetPython();
                }

                if (!string.IsNullOrEmpty(_settings.GetUnrpyc()))
                {
                    _decompilerOptions.UnrpycPath = _settings.GetUnrpyc();
                }
                else
                {
                    // Silently reuse a previous download instead of prompting again.
                    var downloaded = UnrpycInstaller.FindExisting();
                    if (downloaded != null)
                    {
                        _decompilerOptions.UnrpycPath = downloaded;
                    }
                }

                _archive = Archive.Load(chosen);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowError(this,
                    string.Format(GetText("Load_failed_reason"), ex.Message), GetText("Load_failed"));
                StatusText.Text = GetText("Ready");
                return;
            }

            GenerateTreeView();

            Tabs.SelectedItem = TabNone;
            UsageLabel.Text = GetText("Usage_instructions_loaded");
            ResetPreviewFields();
            Tree.SelectedItem = null;

            ExportButton.IsEnabled = true;
            RemoveButton.IsEnabled = true;
            SaveButton.IsEnabled = true;

            StatusText.Text = GetText("Ready");
            _archiveChanged = false;
        }

        private async Task SaveArchive()
        {
            if (_archive.Index.Count == 0)
            {
                await MessageBox.ShowInfo(this, GetText("Empty_archive_save"), GetText("Empty_archive"));
                return;
            }

            StatusText.Text = GetText("Saving_archive");

            _archive.OptionsConfirmed = false;
            var options = new ArchiveSaveWindow(_archive);
            await options.ShowDialog<bool>(this);

            if (!_archive.OptionsConfirmed)
            {
                StatusText.Text = GetText("Ready");
                return;
            }

            var save = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = GetText("Save_RenPy_Archive"),
                DefaultExtension = "rpa",
                FileTypeChoices =
                [
                    new FilePickerFileType(GetText("RPA_RPI_files")) { Patterns = ["*.rpa", "*.rpi"] }
                ],
                SuggestedStartLocation = await StartLocation(_archive.ArchiveInfo?.DirectoryName)
            });

            if (save == null)
            {
                StatusText.Text = GetText("Ready");
                return;
            }

            var target = save.TryGetLocalPath();
            if (string.IsNullOrEmpty(target))
            {
                StatusText.Text = GetText("Ready");
                return;
            }

            try
            {
                var saveName = _archive.Save(target);
                await LoadArchive(saveName, true);
            }
            catch (Exception ex)
            {
                // Saving does not touch the loaded archive and Archive.Load only assigns on
                // success, so the archive in hand is still the one that was open.
                await MessageBox.ShowError(this,
                    string.Format(GetText("Save_failed_reason"), ex.Message), GetText("Save_failed"));
            }

            GenerateTreeView();
            StatusText.Text = GetText("Ready");
        }

        private async Task ExportChecked()
        {
            var exportFilesList = CheckedFiles();
            if (exportFilesList.Count == 0)
            {
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = GetText("Export_checked"),
                AllowMultiple = false,
                SuggestedStartLocation = await StartLocation(_archive.ArchiveInfo?.DirectoryName)
            });

            if (folders.Count == 0)
            {
                return;
            }

            var dest = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(dest))
            {
                return;
            }

            LoadButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            Progress.Value = 0;
            ProgressLabel.Text = string.Empty;
            _operationEnabled = true;

            await Task.Run(() => ExportFiles(exportFilesList, dest));
        }

        private void ExportFiles(List<string> exportFilesList, string destination)
        {
            var counter = 0;
            var jobSize = exportFilesList.Count;

            foreach (var file in exportFilesList)
            {
                counter++;
                var pctProcessed = (int) Math.Ceiling((double) counter / jobSize * 100);
                var current = counter;
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressLabel.Text = $"{current} / {jobSize}";
                    Progress.Value = pctProcessed;
                    StatusText.Text = GetText("Exporting_file") + file;
                });

                _archive.Extract(file, destination);

                if (!_operationEnabled)
                {
                    break;
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                LoadButton.IsEnabled = true;
                ExportButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                Progress.Value = 0;
                ProgressLabel.Text = string.Empty;
                StatusText.Text = GetText("Ready");
            });

            _operationEnabled = true;
        }

        // ----- Adding files (drag & drop) -----

        private void AddFilesToArchive(string[] pathList)
        {
            _fileListBackup.Clear();
            _fileListBackup = _archive.CopyIndex(_archive.Index);
            _cancelAdd = false;

            foreach (var path in pathList)
            {
                var originalPath = path;
                if (Directory.Exists(path))
                {
                    originalPath = new DirectoryInfo(path).Parent?.FullName;
                }
                if (File.Exists(path))
                {
                    originalPath = new FileInfo(path).DirectoryName;
                }
                AddPathToIndex(path, originalPath);
            }

            if (!_cancelAdd)
            {
                _archive.Index = _archive.CopyIndex(_fileListBackup);
            }

            _fileListBackup.Clear();

            GenerateTreeView();
        }

        private void AddPathToIndex(string path, string originalPath)
        {
            _archiveChanged = true;

            if (Directory.Exists(path))
            {
                foreach (var pathFile in Directory.GetFiles(path))
                {
                    AddPathToIndex(pathFile, originalPath);
                }
                foreach (var pathDir in Directory.GetDirectories(path))
                {
                    AddPathToIndex(pathDir, originalPath);
                }
            }

            if (File.Exists(path) && !_cancelAdd)
            {
                var index = ArchiveEntry.FromFilename(path, originalPath);

                if (_fileListBackup.ContainsKey(index.TreePath))
                {
                    _fileListBackup.Remove(index.TreePath);
                }
                _fileListBackup.Add(index.TreePath, index);
            }
        }

        private void OnTreeDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) && _archiveLoaded
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnTreeDrop(object sender, DragEventArgs e)
        {
            if (!_archiveLoaded)
            {
                return;
            }

            var paths = e.Data.GetFiles()?
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (paths is { Length: > 0 })
            {
                AddFilesToArchive(paths);
            }
        }

        private void OnTabsDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void OnTabsDrop(object sender, DragEventArgs e)
        {
            var first = e.Data.GetFiles()?
                .Select(f => f.TryGetLocalPath())
                .FirstOrDefault(p => !string.IsNullOrEmpty(p));

            if (!string.IsNullOrEmpty(first))
            {
                await LoadArchive(first);
            }
        }

        // ----- Options menu -----

        private async Task DefineUnrpycLocation()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = GetText("Locate_unrpyc_script"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GetText("UNRPYC_script")) { Patterns = ["unrpyc.py", "*.py"] }
                ],
                SuggestedStartLocation = await StartLocation(_settings.GetUnrpyc())
            });

            if (files.Count == 0)
            {
                return;
            }

            var chosen = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(chosen))
            {
                _settings.SetUnrpyc(chosen);
                if (_archive != null)
                {
                    _decompilerOptions.UnrpycPath = chosen;
                }
            }
        }

        private async Task DefinePythonLocation()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = GetText("Locate_Python_Interpreter"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GetText("Python_interpreter")) { Patterns = ["python*", "*"] }
                ],
                SuggestedStartLocation = await StartLocation(_settings.GetPython())
            });

            if (files.Count == 0)
            {
                return;
            }

            var chosen = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(chosen))
            {
                _settings.SetPython(chosen);
                if (_archive != null)
                {
                    _decompilerOptions.PythonPath = chosen;
                }
            }
        }

        // ----- Helpers -----

        private async Task<IStorageFolder> StartLocation(string pathHint)
        {
            try
            {
                if (string.IsNullOrEmpty(pathHint))
                {
                    return null;
                }
                var dir = Directory.Exists(pathHint) ? pathHint : Path.GetDirectoryName(pathHint);
                if (string.IsNullOrEmpty(dir))
                {
                    return null;
                }
                return await StorageProvider.TryGetFolderFromPathAsync(dir);
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> CheckIfChanged(string message)
        {
            if (!string.IsNullOrEmpty(message) && _archiveChanged)
            {
                var yes = await MessageBox.ShowYesNo(this, message, GetText("Archive_modified"));
                return !yes;
            }

            return _archiveChanged;
        }

        // Release libvlc explicitly. Its worker threads are native and would otherwise be
        // torn down non-deterministically at process exit.
        protected override void OnClosed(EventArgs e)
        {
            StopMedia();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
                _mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                try
                {
                    VideoView.MediaPlayer = null;
                }
                catch
                {
                    // ignored
                }
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _libVlc?.Dispose();
            _libVlc = null;

            base.OnClosed(e);
        }

        private async void OnClosing(object sender, WindowClosingEventArgs e)
        {
            if (_forceClose || !_archiveChanged)
            {
                return;
            }

            e.Cancel = true;
            var abort = await CheckIfChanged(GetText("Archive_modified_close"));
            if (!abort)
            {
                _forceClose = true;
                Close();
            }
        }
    }
}
