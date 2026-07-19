using System.Collections.Generic;

namespace RpaParser.Formats;

public sealed class Rpa32Format : ArchiveFormat
{
    public override double Version => 3.2;
    public override string DisplayName => "RPA 3.2";
    protected override string Magic => "RPA-3.2 ";

    protected override int KeyFieldIndex => 3;

    protected override IEnumerable<long> FieldsAfterOffset(long obfuscationKey) => [0, obfuscationKey];
}