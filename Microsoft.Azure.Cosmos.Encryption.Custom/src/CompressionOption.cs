namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class CompressionOption
    {
        public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.None;

        public List<string> PathsToCompress { get; set; } = new List<string>();
    }

    public enum CompressionAlgorithm
    {
        None,
        Gzip,
        Brotli
    }
}