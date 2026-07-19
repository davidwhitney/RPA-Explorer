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
    public readonly record struct ExportProgress(int Done, int Total, string TreePath);

    public sealed class ArchiveSession(Settings settings)
    {
        public Archive? Archive { get; private set; }
        public DecompilerOptions DecompilerOptions { get; } = new();
        public bool IsOpen => Archive != null;
        public bool HasUnsavedChanges { get; private set; }

        public bool Contains(string treePath) => Archive?.Index.ContainsKey(treePath) == true;

        public void CreateNew()
        {
            Archive = new Archive();
            HasUnsavedChanges = true;
        }

        public void Open(string path)
        {
            ApplyConfiguredTools();

            Archive = new Archive(path);
            HasUnsavedChanges = false;
        }

        public string Save(string target) => Require().Save(target);

        public bool Remove(IEnumerable<string> treePaths)
        {
            var index = Require().Index;
            var removed = treePaths.Count(index.Remove);

            HasUnsavedChanges |= removed > 0;

            return removed > 0;
        }

        public void AddFiles(IEnumerable<string> paths)
        {
            var archive = Require();
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
