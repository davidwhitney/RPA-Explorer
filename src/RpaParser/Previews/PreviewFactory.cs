using RpaParser.Content;
using RpaParser.Decompilation;

namespace RpaParser.Previews
{
    public sealed class PreviewFactory(Decompiler decompiler)
    {
        private Decompiler Decompiler { get; } = decompiler;

        public PreviewFactory(DecompilerOptions options) : this(new Decompiler(options))
        {
        }

        private PreviewResult Create(string fileName, byte[] data) =>
            ContentFormat.Detect(fileName).CreatePreview(data, Decompiler);

        public PreviewResult Create(Archive archive, string fileName) =>
            archive.Index.ContainsKey(fileName)
                ? Create(fileName, archive.Read(fileName))
                : PreviewResult.Missing;

        public PreviewResult CreateRaw(Archive archive, string fileName)
        {
            if (!archive.Index.ContainsKey(fileName))
            {
                return PreviewResult.Missing;
            }

            var data = archive.Read(fileName);
            return new PreviewResult(Create(fileName, data).Format, data);
        }
    }
}
