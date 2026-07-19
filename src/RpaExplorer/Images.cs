using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp.Formats.Png;

namespace RpaExplorer
{
    // Cross-platform image helpers. Replaces the old System.Drawing + native WebP wrapper.
    // ImageSharp decodes every format the app previews (jpeg, png, bmp, tiff, webp, gif, ...)
    // and we re-encode to PNG so Avalonia's Bitmap can display it uniformly.
    public static class Images
    {
        private const string AssetBase = "avares://RpaExplorer/Assets/";

        public static Bitmap LoadAsset(string fileName)
        {
            using var stream = AssetLoader.Open(new Uri(AssetBase + fileName));
            return new Bitmap(stream);
        }

        private static Bitmap _folder;
        private static Bitmap _file;
        private static Bitmap _fileChanged;

        public static Bitmap FolderIcon => _folder ??= LoadAsset("folder.png");
        public static Bitmap FileIcon => _file ??= LoadAsset("file.png");
        public static Bitmap FileChangedIcon => _fileChanged ??= LoadAsset("fileChanged.png");

        public static Bitmap DecodeToBitmap(byte[] data)
        {
            using var image = SixLabors.ImageSharp.Image.Load(data);
            using var output = new MemoryStream();
            image.Save(output, new PngEncoder());
            output.Position = 0;
            return new Bitmap(output);
        }
    }
}
