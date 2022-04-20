using System.IO;

internal static class TextWriterExtension
{
    public static void WriteMetrics(this TextWriter textWriter, params double[] metrics)
    {
        foreach (double metric in metrics)
        {
            textWriter.WriteLine(metric);
        }
    }
}
