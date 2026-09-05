//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace LegacyPreview07PointFixtureGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using CustomDataEncryptionKey = Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKey;
    using CustomEncryptionKeyWrapMetadata = Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionKeyWrapMetadata;

    internal static class Program
    {
        private const string ExpectedPackageSha256 = "121AA0ED2A518D1F791992AC4E6A90B8E3A16A9BEDE4CB719F6156CF384398F8";
        private const string ExpectedAssemblySha256 = "064FE92B0CC610B3F6CB5E290DA3DFA231643FADB7DC974D01FFDE8D9AEBA3AC";
        private const string LegacyAlgorithm = "AEAes256CbcHmacSha256Randomized";
        private const string DekId = "released-aead-dek";
        private const string DocumentId = "released-aead-newtonsoft";
        private const string PartitionKeyValue = "compat-matrix";
        private const string MasterKeyId = "https://compat.matrix/released";
        private static readonly string[] EncryptedPaths =
        {
            "/Sensitive",
            "/EncEscaped",
            "/EncAstral",
            "/esc\"name\\x",
            "/EncObj",
            "/EncArr",
            "/EncLong",
            "/EncIntegralDouble",
            "/EncNormalDouble",
        };

        public static async Task<int> Main(string[] args)
        {
            IReadOnlyDictionary<string, string> arguments = ParseArguments(args);
            string packagePath = GetRequired(arguments, "package");
            string outputPath = GetRequired(arguments, "output");
            string endpoint = arguments.TryGetValue("endpoint", out string configuredEndpoint)
                ? configuredEndpoint
                : "https://127.0.0.1:8081/";
            string key = Environment.GetEnvironmentVariable("COSMOS_LEGACY_FIXTURE_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("COSMOS_LEGACY_FIXTURE_KEY is required.");
            }

            VerifyHash(packagePath, ExpectedPackageSha256, "package");
            VerifyHash(typeof(EncryptionContainerExtensions).Assembly.Location, ExpectedAssemblySha256, "loaded assembly");

            string databaseId = "legacy-preview07-fixture-" + Guid.NewGuid().ToString("N");
            using CosmosClient client = new(
                endpoint,
                key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    HttpClientFactory = () => new HttpClient(new EmulatorHttpClientHandler()),
                });
            Database database = await client.CreateDatabaseAsync(databaseId);
            try
            {
                Container keyContainer = await database.CreateContainerAsync("keys", "/id", 400);
                Container itemContainer = await database.CreateContainerAsync("items", "/PK", 400);
#pragma warning disable CS0618
                CosmosDataEncryptionKeyProvider provider = new(
                    new FixtureKeyWrapProvider(),
                    new FixtureKeyStoreProvider(),
                    TimeSpan.FromMinutes(5));
#pragma warning restore CS0618
                await provider.InitializeAsync(database, keyContainer.Id);
                await provider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                    DekId,
                    LegacyAlgorithm,
                    new CustomEncryptionKeyWrapMetadata("compat-matrix", MasterKeyId));

                FixtureDocument plaintext = CreatePlaintext();
                Container encryptedContainer = itemContainer.WithEncryptor(new FixtureEncryptor(provider));
                await encryptedContainer.UpsertItemAsync(
                    plaintext,
                    new PartitionKey(PartitionKeyValue),
                    new EncryptionItemRequestOptions
                    {
                        EncryptionOptions = new EncryptionOptions
                        {
                            DataEncryptionKeyId = DekId,
                            EncryptionAlgorithm = LegacyAlgorithm,
                            PathsToEncrypt = new List<string>(EncryptedPaths),
                        },
                    });

                JObject rawItem = (await itemContainer.ReadItemAsync<JObject>(
                    DocumentId,
                    new PartitionKey(PartitionKeyValue))).Resource;
                JObject rawDek = (await keyContainer.ReadItemAsync<JObject>(
                    DekId,
                    new PartitionKey(DekId))).Resource;
                RemoveServiceProperties(rawItem);
                RemoveServiceProperties(rawDek);

                FixtureDocument releasedRead = (await encryptedContainer.ReadItemAsync<FixtureDocument>(
                    DocumentId,
                    new PartitionKey(PartitionKeyValue))).Resource;
                if (!JToken.DeepEquals(JObject.FromObject(plaintext), JObject.FromObject(releasedRead)))
                {
                    throw new InvalidOperationException("Released-package self-read did not reproduce the exact plaintext.");
                }

                JObject fixture = new()
                {
                    ["dataEncryptionKey"] = rawDek,
                    ["legacyItem"] = rawItem,
                    ["plaintext"] = JObject.FromObject(plaintext),
                    ["encryptedPaths"] = new JArray(EncryptedPaths),
                };
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllText(outputPath, fixture.ToString(Formatting.Indented) + Environment.NewLine);
                Console.WriteLine($"fixture={Path.GetFullPath(outputPath)}");
                Console.WriteLine($"fixture-sha256={HashFile(outputPath)}");
                Console.WriteLine($"package-sha256={ExpectedPackageSha256}");
                Console.WriteLine($"assembly-sha256={ExpectedAssemblySha256}");
                return 0;
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        private static FixtureDocument CreatePlaintext()
        {
            return new FixtureDocument
            {
                Id = DocumentId,
                PK = PartitionKeyValue,
                NonSensitive = "plain",
                Sensitive = "secret::" + DocumentId,
                PlainEscaped = "p_q=\" p_b=\\ p_nl=\n p_u=\u00e9 end",
                EncEscaped = "q=\" b=\\ nl=\n tab=\t u=\u00e9 ctl=\u0001 end",
                EncAstral = "😀𐍈🜨 日本語 العربية 😀 Z\u0301",
                EscapedPropertyValue = "named-secret",
                EncObj = new JObject { ["a"] = JValue.CreateNull(), ["b"] = 1 },
                EncArr = new JArray { 1, JValue.CreateNull(), 2 },
                EncLong = 9007199254740993L,
                EncIntegralDouble = 5.0,
                EncNormalDouble = 1234.5,
            };
        }

        private static void RemoveServiceProperties(JObject document)
        {
            foreach (string propertyName in new[] { "_rid", "_self", "_etag", "_attachments", "_ts" })
            {
                document.Remove(propertyName);
            }
        }

        private static void VerifyHash(string path, string expected, string description)
        {
            string actual = HashFile(path);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{description} SHA-256 mismatch. Actual={actual}; Expected={expected}; Path={path}");
            }
        }

        private static string HashFile(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        private static IReadOnlyDictionary<string, string> ParseArguments(IEnumerable<string> args)
        {
            Dictionary<string, string> parsed = new(StringComparer.OrdinalIgnoreCase);
            foreach (string argument in args)
            {
                int separator = argument.IndexOf('=');
                if (argument.StartsWith("--", StringComparison.Ordinal) && separator > 2)
                {
                    parsed[argument[2..separator]] = argument[(separator + 1)..];
                }
            }

            return parsed;
        }

        private static string GetRequired(IReadOnlyDictionary<string, string> arguments, string name)
        {
            return arguments.TryGetValue(name, out string value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"Missing required argument --{name}=...");
        }

        private sealed class EmulatorHttpClientHandler : HttpClientHandler
        {
            public EmulatorHttpClientHandler()
            {
                this.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
        }

        private sealed class FixtureDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive { get; set; }

            public string PlainEscaped { get; set; }

            public string EncEscaped { get; set; }

            public string EncAstral { get; set; }

            [JsonProperty("esc\"name\\x")]
            public string EscapedPropertyValue { get; set; }

            public JObject EncObj { get; set; }

            public JArray EncArr { get; set; }

            public long EncLong { get; set; }

            public double EncIntegralDouble { get; set; }

            public double EncNormalDouble { get; set; }
        }

#pragma warning disable CS0618
        private sealed class FixtureKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
                byte[] wrappedKey,
                CustomEncryptionKeyWrapMetadata metadata,
                CancellationToken cancellationToken)
            {
                int shift = GetShift(metadata?.Value);
                return Task.FromResult(new EncryptionKeyUnwrapResult(
                    wrappedKey.Select(value => unchecked((byte)(value - shift))).ToArray(),
                    TimeSpan.FromMinutes(5)));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
                byte[] key,
                CustomEncryptionKeyWrapMetadata metadata,
                CancellationToken cancellationToken)
            {
                int shift = GetShift(metadata?.Value);
                return Task.FromResult(new EncryptionKeyWrapResult(
                    key.Select(value => unchecked((byte)(value + shift))).ToArray(),
                    metadata));
            }
        }
#pragma warning restore CS0618

        private sealed class FixtureKeyStoreProvider : EncryptionKeyStoreProvider
        {
            public override string ProviderName => "legacy-preview07-fixture";

            public override byte[] UnwrapKey(
                string encryptionKeyId,
                KeyEncryptionKeyAlgorithm algorithm,
                byte[] encryptedKey)
            {
                int shift = GetShift(encryptionKeyId);
                return encryptedKey.Select(value => unchecked((byte)(value - shift))).ToArray();
            }

            public override byte[] WrapKey(
                string encryptionKeyId,
                KeyEncryptionKeyAlgorithm algorithm,
                byte[] key)
            {
                int shift = GetShift(encryptionKeyId);
                return key.Select(value => unchecked((byte)(value + shift))).ToArray();
            }

            public override byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations)
            {
                return new[] { (byte)GetShift(encryptionKeyId) };
            }

            public override bool Verify(
                string encryptionKeyId,
                bool allowEnclaveComputations,
                byte[] signature)
            {
                return signature?.Length == 1 && signature[0] == GetShift(encryptionKeyId);
            }
        }

        private sealed class FixtureEncryptor : Encryptor
        {
            private readonly DataEncryptionKeyProvider provider;

            public FixtureEncryptor(DataEncryptionKeyProvider provider)
            {
                this.provider = provider;
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                CustomDataEncryptionKey key = await this.provider.FetchDataEncryptionKeyWithoutRawKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);
                return key.EncryptData(plainText);
            }

            public override async Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                CustomDataEncryptionKey key = await this.provider.FetchDataEncryptionKeyWithoutRawKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);
                return key.DecryptData(cipherText);
            }
        }

        private static int GetShift(string value)
        {
            return ((value?.Sum(character => (int)character) ?? 0) % 31) + 1;
        }
    }
}