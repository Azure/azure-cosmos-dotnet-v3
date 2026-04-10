namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    using System.Text;
    using Newtonsoft.Json;

    public partial class EncryptionBenchmark
    {
        internal class TestDoc
        {
            public static HashSet<string> PathsToEncrypt { get; } = new HashSet<string>{ "/SensitiveStr", "/SensitiveInt", "/SensitiveDict" };

            [JsonProperty("id")]
            public string Id { get; set; } = default!;

            public string NonSensitive { get; set; } = default!;

            public string SensitiveStr { get; set; } = default!;

            public int SensitiveInt { get; set; } = default!;

            public Dictionary<string, string> SensitiveDict { get; set; } = default!;

            public TestDoc()
            {
            }

            public static TestDoc Create(int approximateSize = -1)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    SensitiveStr = Guid.NewGuid().ToString(),
                    SensitiveInt = new Random().Next(),
                    SensitiveDict = GenerateBigDictionary(approximateSize),
                };
            }

            private static Dictionary<string, string> GenerateBigDictionary(int approximateSize)
            {
                const int stringSize = 100;
                int items = Math.Max(1, approximateSize / stringSize);

                return Enumerable.Range(1, items).ToDictionary(x => x.ToString(), y => GenerateRandomString(stringSize));
            }

            private static string GenerateRandomString(int size)
            {
                Random rnd = new ();
                const string characters = "abcdefghijklmnopqrstuvwxyz0123456789";

                StringBuilder sb = new();
                for (int i = 0; i < size; i++)
                {
                    sb.Append(characters[rnd.Next(0, characters.Length)]);
                }
                return sb.ToString();
            }
        }

    }
}