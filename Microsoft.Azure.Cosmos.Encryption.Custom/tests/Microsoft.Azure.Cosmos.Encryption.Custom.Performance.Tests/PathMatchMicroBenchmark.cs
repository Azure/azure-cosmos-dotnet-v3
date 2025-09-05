using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, iterationCount: 8)]
    public class PathMatchMicroBenchmark
    {
        private byte[]? json;
        private string[]? encryptedPaths;
        private HashSet<string>? encryptedFullPaths;
        private byte[][]? topLevelNameUtf8;
        private string[]? topLevelFullPaths;

        [Params(50, 200)]
        public int TopLevelProperties { get; set; }

        [Params(1, 10)]
        public int EncryptedTopLevelCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.json = GenerateDocument(this.TopLevelProperties);
            this.encryptedPaths = Enumerable.Range(1, this.EncryptedTopLevelCount).Select(i => $"/P{i}").ToArray();
            this.encryptedFullPaths = new HashSet<string>(this.encryptedPaths);
            this.topLevelFullPaths = this.encryptedPaths.ToArray();
            this.topLevelNameUtf8 = this.encryptedPaths
                .Select(p => Encoding.UTF8.GetBytes(p.Substring(1))) // strip leading
                .ToArray();
        }

        [Benchmark(Description = "Legacy: concat + HashSet.Contains")] 
        public int Legacy_ConcatAndContains()
        {
            int matches = 0;
            var reader = new System.Text.Json.Utf8JsonReader(this.json!.AsSpan(), new System.Text.Json.JsonReaderOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
            int depth = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case System.Text.Json.JsonTokenType.StartObject:
                    case System.Text.Json.JsonTokenType.StartArray:
                        depth++;
                        break;
                    case System.Text.Json.JsonTokenType.EndObject:
                    case System.Text.Json.JsonTokenType.EndArray:
                        depth--;
                        break;
                    case System.Text.Json.JsonTokenType.PropertyName when depth == 1:
                    {
                        string name = reader.GetString()!;
                        string full = "/" + name; // alloc each hit
                        if (this.encryptedFullPaths!.Contains(full))
                        {
                            matches++;
                        }
                        break;
                    }
                }
            }
            return matches;
        }

        [Benchmark(Description = "Optimized: ValueTextEquals + canonical path reuse")] 
        public int Optimized_ValueTextEquals_ScanUtf8()
        {
            int matches = 0;
            var reader = new System.Text.Json.Utf8JsonReader(this.json!.AsSpan(), new System.Text.Json.JsonReaderOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
            int depth = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case System.Text.Json.JsonTokenType.StartObject:
                    case System.Text.Json.JsonTokenType.StartArray:
                        depth++;
                        break;
                    case System.Text.Json.JsonTokenType.EndObject:
                    case System.Text.Json.JsonTokenType.EndArray:
                        depth--;
                        break;
                    case System.Text.Json.JsonTokenType.PropertyName when depth == 1:
                    {
                        // Linear scan of encrypted top-level names (small N)
                        for (int i = 0; i < this.topLevelNameUtf8!.Length; i++)
                        {
                            if (reader.ValueTextEquals(this.topLevelNameUtf8[i]))
                            {
                                string canonical = this.topLevelFullPaths![i]; // reuse
                                // No HashSet lookup, we already matched
                                matches++;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            return matches;
        }

        private static byte[] GenerateDocument(int props)
        {
            StringBuilder sb = new();
            sb.Append('{');
            for (int i = 1; i <= props; i++)
            {
                if (i > 1) sb.Append(',');
                sb.Append('\"').Append("P").Append(i).Append('\"').Append(':');
                if ((i % 3) == 0)
                {
                    sb.Append(i); // number
                }
                else
                {
                    sb.Append('\"').Append("value").Append(i).Append('\"'); // short string
                }
            }
            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
