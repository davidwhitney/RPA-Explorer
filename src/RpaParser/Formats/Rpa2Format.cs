namespace RpaParser.Formats;

public sealed class Rpa2Format : ArchiveFormat
{
    public override double Version => 2;
    public override string DisplayName => "RPA 2.0";
    protected override string Magic => "RPA-2.0 ";
}