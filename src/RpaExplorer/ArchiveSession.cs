#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using RpaParser;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaExplorer
{
    /// <summary>Reported once per file while exporting, so the window can show progress.</summary>
    public readonly record struct ExportProgress(int Done, int Total, string TreePath);

    /// <summary>
    /// The archive the user has open and everything that follows from it: whether it has
    /// unsaved changes, where the external decompiler lives, and the work behind each of the
    /// window's buttons.
    ///
    /// Nothing here touches Avalonia. The window owns the widgets and the media player; this
    /// owns the state and the work, so what a button does can be read - and tested - on its
    /// own rather than through the event handler that happens to call it.
    /// </summary>
    public sealed class ArchiveSession(Settings settings)
    {
        public Archive? Archive { get; private set; }

        /// <summary>
        /// Where Python and unrpyc live. A property of the machine rather than of any one
        /// archive, so it outlives the archive currently open.
        /// </summary>
        public DecompilerOptions DecompilerOptions { get; } = new();

        public bool IsOpen => Archive != null;

        public bool HasUnsavedChanges { get; private set; }

        public bool Contains(string treePath) => Archive?.Index.ContainsKey(treePath) == true;

        public void CreateNew()
        {
            Archive = new Archive();

            // Nothing has been written yet, so an empty new archive is already unsaved.
            HasUnsavedChanges = true;
        }

        /// <summary>
        /// Opens an archive. A failed read throws without disturbing the archive already in
        /// hand, so the user does not lose their work to a mistyped path.
        /// </summary>
        public void Open(string path)
        {
            ApplyConfiguredTools();

            Archive = new Archive(path);
            HasUnsavedChanges = false;
        }

        /// <summary>Writes the archive and returns where it landed, which may gain an extension.</summary>
        public string Save(string target) => Require().Save(target);

        public bool Remove(IEnumerable<string> treePaths)
        {
            var index = Require().Index;
            var removed = treePaths.Count(path => index.Remove(path));

            HasUnsavedChanges |= removed > 0;

            return removed > 0;
        }

        /// <summary>
        /// Stages files and directories to be written on the next save. Directory contents
        /// keep their paths relative to the directory that was dropped, so dropping "images"
        /// adds "images/scenes/room.png" rather than the whole path from the drive root.
        /// </summary>
        public void AddFiles(IEnumerable<string> paths)
        {
            var archive = Require();

            // Built to one side and swapped in, so a failure part way through leaves the
            // index the user is looking at untouched.
            var staged = archive.Index.Copy();

            foreach (var path in paths)
            {
                Stage(staged, path, RootOf(path));
            }

            archive.Index = staged;
            HasUnsavedChanges = true;
        }

        public void Export(IReadOnlyList<string> treePaths, string destination,
            IProgress<ExportProgress>? progress = null, CancellationToken cancellation = default)
        {
            var archive = Require();

            for (var i = 0; i < treePaths.Count; i++)
            {
                progress?.Report(new ExportProgress(i + 1, treePaths.Count, treePaths[i]));

                archive.Export(treePaths[i], destination);

                // Checked after the write so a cancelled export leaves whole files behind.
                if (cancellation.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        public PreviewResult Preview(string treePath) => Previews().Create(Require(), treePath);

        public PreviewResult PreviewRaw(string treePath) => Previews().CreateRaw(Require(), treePath);

        /// <summary>
        /// Total bytes stored under each folder, keyed by archive path, with the empty path
        /// standing for the whole archive. The index records a length per file, but the tree
        /// shows a size for folders too.
        /// </summary>
        public IReadOnlyDictionary<string, long> FolderSizes()
        {
            Dictionary<string, long> sizes = new() { [string.Empty] = 0 };

            if (Archive == null)
            {
                return sizes;
            }

            foreach (var entry in Archive.Index)
            {
                var parts = entry.Key.Split('/');
                var folder = string.Empty;

                for (var i = 0; i < parts.Length - 1; i++)
                {
                    folder = folder == string.Empty ? parts[i] : folder + "/" + parts[i];
                    sizes[folder] = sizes.GetValueOrDefault(folder) + entry.Value.Length;
                }

                sizes[string.Empty] += entry.Value.Length;
            }

            return sizes;
        }

        public void UsePython(string path)
        {
            settings.SetPython(path);
            DecompilerOptions.PythonPath = path;
        }

        public void UseUnrpyc(string path)
        {
            settings.SetUnrpyc(path);
            DecompilerOptions.UnrpycPath = path;
        }

        /// <summary>
        /// An explicitly configured interpreter always wins. Auto-detection is deliberately
        /// not written back to the settings file: persisting a guess makes it sticky and
        /// stops improved detection from ever taking effect.
        /// </summary>
        private void ApplyConfiguredTools()
        {
            if (!string.IsNullOrEmpty(settings.GetPython()))
            {
                DecompilerOptions.PythonPath = settings.GetPython();
            }

            if (!string.IsNullOrEmpty(settings.GetUnrpyc()))
            {
                DecompilerOptions.UnrpycPath = settings.GetUnrpyc();
            }
            else if (UnrpycInstaller.FindExisting() is { } downloaded)
            {
                // Silently reuse a previous download instead of prompting again.
                DecompilerOptions.UnrpycPath = downloaded;
            }
        }

        private PreviewFactory Previews() => new(DecompilerOptions);

        private Archive Require() =>
            Archive ?? throw new InvalidOperationException("No archive is open.");

        /// <summary>
        /// The directory a dropped path's archive paths are taken relative to. Null for a
        /// path with no parent, which leaves the whole path as the name inside the archive.
        /// </summary>
        private static string? RootOf(string path) =>
            Directory.Exists(path) ? new DirectoryInfo(path).Parent?.FullName
            : File.Exists(path) ? new FileInfo(path).DirectoryName
            : path;

        private static void Stage(ArchiveIndex index, string path, string? rootPath)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    Stage(index, file, rootPath);
                }

                foreach (var directory in Directory.GetDirectories(path))
                {
                    Stage(index, directory, rootPath);
                }
            }

            if (File.Exists(path))
            {
                var entry = ArchiveEntry.FromFilename(path, rootPath);
                index[entry.TreePath] = entry;
            }
        }
    }
}
