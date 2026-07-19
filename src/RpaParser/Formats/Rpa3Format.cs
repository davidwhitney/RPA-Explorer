using System.Collections.Generic;

namespace RpaParser.Formats;

public sealed class Rpa3Format : ArchiveFormat
{
    public override double Version => 3;
    public override string DisplayName => "RPA 3.0";
    protected override string Magic => "RPA-3.0 ";

    protected override int KeyFieldIndex => 2;

    protected override IEnumerable<long> FieldsAfterOffset(long obfuscationKey) => [obfuscationKey];
}