namespace MediaTranscodeEngine.Cli.Parsing;

/*
Это helper для CLI-formatting supported values.
Он преобразует runtime-owned catalogs в help и error display без знания доменной логики.
*/
internal static class CliValueFormatter
{
    public static string FormatAlternatives<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return string.Join("|", values);
    }

    public static string FormatList<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return string.Join(", ", values);
    }
}
