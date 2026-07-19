namespace RpaParser.Decompilation;

/// <summary>Where to find the external tools a compiled script preview needs.</summary>
public sealed class DecompilerOptions
{
    /// <summary>Python interpreter. Defaults to whatever the machine offers.</summary>
    public string PythonPath { get; set; } = PythonLocator.Detected;

    /// <summary>Path to unrpyc.py. Empty until the user supplies or downloads one.</summary>
    public string UnrpycPath { get; set; } = string.Empty;
}