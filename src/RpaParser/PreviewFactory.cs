namespace RpaParser
{
    /// <summary>
    /// Builds previews from raw bytes.
    ///
    /// Kept apart from <see cref="Archive"/> so the archive stays a description of what is
    /// stored: producing a preview needs a decompiler for compiled scripts, which is a
    /// property of the machine rather than of the archive.
    /// </summary>
    public sealed class PreviewFactory(Decompiler decompiler)
    {
        public Decompiler Decompiler { get; } = decompiler;

        public PreviewFactory(DecompilerOptions options) : this(new Decompiler(options))
        {
        }

        /// <summary>
        /// Presents <paramref name="data"/> according to the format claiming
        /// <paramref name="fileName"/>.
        /// </summary>
        public PreviewResult Create(string fileName, byte[] data) =>
            ContentFormat.Detect(fileName).CreatePreview(data, Decompiler);

        public PreviewResult Create(Archive archive, string fileName) =>
            archive.Index.ContainsKey(fileName)
                ? Create(fileName, archive.Read(fileName))
                : new PreviewResult(ContentFormat.Unknown, null);

        /// <summary>The bytes as stored, tagged with the format that claims them.</summary>
        public PreviewResult CreateRaw(Archive archive, string fileName)
        {
            if (!archive.Index.ContainsKey(fileName))
            {
                return new PreviewResult(ContentFormat.Unknown, null);
            }

            var data = archive.Read(fileName);
            return new PreviewResult(Create(fileName, data).Format, data);
        }
    }
}
