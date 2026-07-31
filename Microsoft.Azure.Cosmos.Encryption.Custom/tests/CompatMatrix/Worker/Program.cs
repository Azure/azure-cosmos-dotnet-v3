//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CompatMatrix
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using CustomEncryptionKeyWrapMetadata = Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionKeyWrapMetadata;

    public static class Program
    {
#if COMPAT_CURRENT
        private const string WorkerRole = "current";
#else
        private const string WorkerRole = "released";
#endif

        private const string ActivitySourceName = "Microsoft.Azure.Cosmos.Encryption.Custom";
        private const string StreamPropertyName = "encryption-json-processor";
        private const string PartitionKeyValue = "compat-matrix";
        private const string MdeFamily = "MDE";
        private const string AeadFamily = "AEAD";
        private const string NewtonsoftProcessor = "Newtonsoft";
        private const string StreamProcessor = "Stream";
        private const string MdeAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;
#pragma warning disable CS0618
        private static readonly string AeadAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618

        private const string EscapedPropertyName = "esc\"name\\x";
        private const string EscapedPropertyPath = "/" + EscapedPropertyName;
        private const string PlainEscapedValue = "p_q=\" p_b=\\ p_nl=\n p_u=\u00e9 end";
        private const string EncryptedEscapedValue = "q=\" b=\\ nl=\n tab=\t u=\u00e9 ctl=\u0001 end";
        private const string EncryptedAstralValue = "😀𐍈🜨 日本語 العربية \uD83D\uDE00 Z\u0301";
        private const string EscapedPropertyValue = "named-secret";
        private const long EncryptedLongValue = 9007199254740993L;
        private const double EncryptedIntegralDoubleValue = 5.0;
        private const double EncryptedNormalDoubleValue = 1234.5;

        private static readonly string[] EncryptedPropertyNames =
        {
            "Sensitive",
            "EncEscaped",
            "EncAstral",
            EscapedPropertyName,
            "EncObj",
            "EncArr",
            "EncLong",
            "EncIntegralDouble",
            "EncNormalDouble",
        };

        private static readonly string[] EncryptedPaths =
        {
            "/Sensitive",
            "/EncEscaped",
            "/EncAstral",
            EscapedPropertyPath,
            "/EncObj",
            "/EncArr",
            "/EncLong",
            "/EncIntegralDouble",
            "/EncNormalDouble",
        };

        public static async Task<int> Main(string[] args)
        {
            Dictionary<string, string> arguments = ParseArguments(args);
            string action = arguments.GetValueOrDefault("action", "identity");

            try
            {
                int failures = action switch
                {
                    "identity" => EmitIdentity(),
                    "write" => await WriteAsync(arguments),
                    "read" => await ReadAsync(arguments),
                    "tamper" => await TamperAsync(arguments),
                    _ => throw new InvalidOperationException($"Unknown worker action: {action}"),
                };

                Emit(new WorkerRecord
                {
                    Kind = "completion",
                    Role = WorkerRole,
                    Status = failures == 0 ? "pass" : "fail",
                    Detail = $"action={action};failures={failures}",
                });
                return failures == 0 ? 0 : 1;
            }
            catch (Exception exception)
            {
                Emit(new WorkerRecord
                {
                    Kind = "error",
                    Role = WorkerRole,
                    Status = "fail",
                    Detail = Describe(exception),
                });
                Emit(new WorkerRecord
                {
                    Kind = "completion",
                    Role = WorkerRole,
                    Status = "fail",
                    Detail = $"action={action};unhandled",
                });
                return 1;
            }
        }

        private static int EmitIdentity()
        {
            Assembly assembly = typeof(EncryptionContainerExtensions).Assembly;
            string assemblyPath = assembly.Location;
            string informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "<missing>";

            Emit(new WorkerRecord
            {
                Kind = "identity",
                Role = WorkerRole,
                PackageVersion = informationalVersion.Split('+')[0],
                InformationalVersion = informationalVersion,
                AssemblyVersion = assembly.GetName().Version?.ToString(),
                AssemblyMvid = assembly.ManifestModule.ModuleVersionId.ToString("D"),
                AssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
                AssemblyPath = assemblyPath,
                CosmosVersion = typeof(CosmosClient).Assembly.GetName().Version?.ToString(),
                MdeVersion = typeof(EncryptionKeyStoreProvider).Assembly.GetName().Version?.ToString(),
            });
            return 0;
        }

        private static async Task<int> WriteAsync(IReadOnlyDictionary<string, string> arguments)
        {
            WorkerSettings settings = WorkerSettings.Create(arguments);
            using CosmosClient client = CreateClient(settings);
            Database database = await client.CreateDatabaseIfNotExistsAsync(settings.Database);
            Container keyContainer = await CreateContainerAsync(database, GetKeyContainerId(WorkerRole), "/id");
            CosmosDataEncryptionKeyProvider provider = await CreateProviderAsync(database, keyContainer.Id);

            await provider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                GetDekId(WorkerRole, MdeFamily),
                MdeAlgorithm,
                new CustomEncryptionKeyWrapMetadata("compat-matrix", GetMasterKeyId(WorkerRole)));
            await provider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                GetDekId(WorkerRole, AeadFamily),
                AeadAlgorithm,
                new CustomEncryptionKeyWrapMetadata("compat-matrix", GetMasterKeyId(WorkerRole)));

            int failures = 0;
            foreach (WriteScenario scenario in GetWriteScenarios())
            {
                string scenarioId = $"write:{WorkerRole}:{scenario.Family}:{scenario.Processor}";
                try
                {
                    Container plain = await CreateContainerAsync(
                        database,
                        GetItemContainerId(WorkerRole, scenario.Family),
                        "/PK");
                    Container encrypted = plain.WithEncryptor(new MatrixEncryptor(provider));
                    string documentId = GetDocumentId(WorkerRole, scenario.Family, scenario.Processor);
                    Doc document = BuildDocument(documentId);
                    List<string> scopes = await CaptureScopesAsync(async () =>
                    {
                        await encrypted.UpsertItemAsync(
                            document,
                            new PartitionKey(PartitionKeyValue),
                            CreateEncryptionOptions(WorkerRole, scenario.Family, scenario.Processor));

                        Doc selfRead = (await encrypted.ReadItemAsync<Doc>(
                            documentId,
                            new PartitionKey(PartitionKeyValue),
                            WithProcessor(new ItemRequestOptions(), scenario.Processor))).Resource;
                        EnsureDocumentMatches(selfRead, documentId);
                    });

                    EnsureRawEncrypted(await ReadRawAsync(plain, documentId), scenario.Family);
                    EnsureProcessorScopes(
                        scopes,
                        scenario.Family,
                        scenario.Processor,
                        expectScope: true,
                        allowNewtonsoftFallback: false);
                    EmitObservation(scenarioId, "pass", "write, raw encryption, and self-read succeeded", scenario.Processor, scopes);
                }
                catch (Exception exception)
                {
                    failures++;
                    EmitObservation(scenarioId, "fail", Describe(exception), scenario.Processor, null);
                }
            }

            return failures;
        }

        private static async Task<int> ReadAsync(IReadOnlyDictionary<string, string> arguments)
        {
            WorkerSettings settings = WorkerSettings.Create(arguments);
            string writer = GetRequired(arguments, "writer");
            if (writer != "released" && writer != "current")
            {
                throw new InvalidOperationException($"Unknown writer role: {writer}");
            }

            using CosmosClient client = CreateClient(settings);
            Database database = client.GetDatabase(settings.Database);
            Container keyContainer = database.GetContainer(GetKeyContainerId(writer));
            CosmosDataEncryptionKeyProvider provider = await CreateProviderAsync(database, keyContainer.Id);

            int failures = 0;
            foreach (ReadScenario scenario in GetReadScenarios(writer))
            {
                foreach (string path in GetReadPaths(scenario.ReadProcessor))
                {
                    string scenarioId =
                        $"read:{writer}->{WorkerRole}:{scenario.Family}:{scenario.WriteProcessor}->{scenario.ReadProcessor}:{path}";
                    try
                    {
                        Container plain = database.GetContainer(GetItemContainerId(writer, scenario.Family));
                        Container encrypted = plain.WithEncryptor(new MatrixEncryptor(provider));
                        string documentId = GetDocumentId(writer, scenario.Family, scenario.WriteProcessor);
                        EnsureRawEncrypted(await ReadRawAsync(plain, documentId), scenario.Family);

                        Doc document = null;
                        List<string> scopes = await CaptureScopesAsync(async () =>
                        {
                            document = await ReadDocumentAsync(encrypted, documentId, path, scenario.ReadProcessor);
                            await EnsureDecryptedJsonFidelityAsync(
                                encrypted,
                                documentId,
                                path,
                                scenario.ReadProcessor);
                        });

                        EnsureDocumentMatches(document, documentId);
                        EnsureProcessorScopes(
                            scopes,
                            scenario.Family,
                            scenario.ReadProcessor,
                            expectScope: true,
                            allowNewtonsoftFallback:
                                scenario.ReadProcessor == StreamProcessor &&
                                path == "query");
                        EmitObservation(scenarioId, "pass", "peer document decrypted exactly", scenario.ReadProcessor, scopes);
                    }
                    catch (Exception exception)
                    {
                        failures++;
                        EmitObservation(scenarioId, "fail", Describe(exception), scenario.ReadProcessor, null);
                    }
                }
            }

            return failures;
        }

        private static IEnumerable<string> GetReadPaths(string processor)
        {
            _ = processor;
            yield return "point";
            yield return "query";
            yield return "feed";
        }

        private static async Task<int> TamperAsync(IReadOnlyDictionary<string, string> arguments)
        {
            WorkerSettings settings = WorkerSettings.Create(arguments);
            using CosmosClient client = CreateClient(settings);
            Database database = await client.CreateDatabaseIfNotExistsAsync(settings.Database);
            Container plain = await CreateContainerAsync(database, "items-tamper", "/PK");
            string documentId = $"tamper-{WorkerRole}";
            await plain.UpsertItemAsync(BuildDocument(documentId), new PartitionKey(PartitionKeyValue));

            string scenarioId = "guard:plaintext-rejected";
            JObject raw = await ReadRawAsync(plain, documentId);
            try
            {
                EnsureRawEncrypted(raw, MdeFamily);
                EmitObservation(scenarioId, "fail", "plaintext document passed the raw encryption oracle", null, null);
                return 1;
            }
            catch (CompatibilityOracleException)
            {
                EmitObservation(scenarioId, "pass", "plaintext document was rejected", null, null);
                return 0;
            }
        }

        private static IEnumerable<WriteScenario> GetWriteScenarios()
        {
            yield return new WriteScenario(MdeFamily, NewtonsoftProcessor);
#if COMPAT_CURRENT
            yield return new WriteScenario(MdeFamily, StreamProcessor);
#endif
            yield return new WriteScenario(AeadFamily, NewtonsoftProcessor);
        }

        private static IEnumerable<ReadScenario> GetReadScenarios(string writer)
        {
            if (writer == "released")
            {
                yield return new ReadScenario(MdeFamily, NewtonsoftProcessor, NewtonsoftProcessor);
#if COMPAT_CURRENT
                yield return new ReadScenario(MdeFamily, NewtonsoftProcessor, StreamProcessor);
#endif
                yield return new ReadScenario(AeadFamily, NewtonsoftProcessor, NewtonsoftProcessor);
                yield break;
            }

            yield return new ReadScenario(MdeFamily, NewtonsoftProcessor, NewtonsoftProcessor);
            yield return new ReadScenario(MdeFamily, StreamProcessor, NewtonsoftProcessor);
            yield return new ReadScenario(AeadFamily, NewtonsoftProcessor, NewtonsoftProcessor);
        }

        private static CosmosClient CreateClient(WorkerSettings settings)
        {
            return new CosmosClient(
                settings.Endpoint,
                settings.Key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    HttpClientFactory = () => new HttpClient(
                        new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                        }),
                });
        }

        private static async Task<Container> CreateContainerAsync(
            Database database,
            string containerId,
            string partitionKeyPath)
        {
            return (await database.CreateContainerIfNotExistsAsync(containerId, partitionKeyPath, 400)).Container;
        }

        private static async Task<CosmosDataEncryptionKeyProvider> CreateProviderAsync(
            Database database,
            string keyContainerId)
        {
            CosmosDataEncryptionKeyProvider provider = new(
                new MatrixKeyWrapProvider(),
                new MatrixKeyStoreProvider());
            await provider.InitializeAsync(database, keyContainerId);
            return provider;
        }

        private static EncryptionItemRequestOptions CreateEncryptionOptions(
            string writer,
            string family,
            string processor)
        {
            EncryptionItemRequestOptions options = new()
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = GetDekId(writer, family),
                    EncryptionAlgorithm = family == MdeFamily ? MdeAlgorithm : AeadAlgorithm,
                    PathsToEncrypt = new List<string>(EncryptedPaths),
                },
            };
            return WithProcessor(options, processor);
        }

        private static T WithProcessor<T>(T requestOptions, string processor)
            where T : RequestOptions
        {
            requestOptions.Properties = new Dictionary<string, object>
            {
                [StreamPropertyName] = processor,
            };
            return requestOptions;
        }

        private static async Task<Doc> ReadDocumentAsync(
            Container encrypted,
            string documentId,
            string path,
            string processor)
        {
            if (path == "point")
            {
                return (await encrypted.ReadItemAsync<Doc>(
                    documentId,
                    new PartitionKey(PartitionKeyValue),
                    WithProcessor(new ItemRequestOptions(), processor))).Resource;
            }

            QueryDefinition query = path == "query"
                ? new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", documentId)
                : null;
            QueryRequestOptions requestOptions = WithProcessor(
                new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(PartitionKeyValue),
                    MaxItemCount = 1,
                },
                processor);
            using FeedIterator<Doc> iterator = encrypted.GetItemQueryIterator<Doc>(
                queryDefinition: query,
                continuationToken: null,
                requestOptions: requestOptions);
            while (iterator.HasMoreResults)
            {
                foreach (Doc document in await iterator.ReadNextAsync())
                {
                    if (document.id == documentId)
                    {
                        return document;
                    }
                }
            }

            return null;
        }

        private static async Task EnsureDecryptedJsonFidelityAsync(
            Container encrypted,
            string documentId,
            string path,
            string processor)
        {
            if (path == "point")
            {
                using ResponseMessage response = await encrypted.ReadItemStreamAsync(
                    documentId,
                    new PartitionKey(PartitionKeyValue),
                    WithProcessor(new ItemRequestOptions(), processor));
                response.EnsureSuccessStatusCode();
                using JsonDocument payload = await JsonDocument.ParseAsync(response.Content);
                EnsureDecryptedJsonFidelity(payload.RootElement, documentId);
                return;
            }

            QueryDefinition query = path == "query"
                ? new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", documentId)
                : null;
            QueryRequestOptions requestOptions = WithProcessor(
                new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(PartitionKeyValue),
                    MaxItemCount = 1,
                },
                processor);
            using FeedIterator iterator = encrypted.GetItemQueryStreamIterator(
                queryDefinition: query,
                continuationToken: null,
                requestOptions: requestOptions);
            while (iterator.HasMoreResults)
            {
                using ResponseMessage response = await iterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();
                using JsonDocument payload = await JsonDocument.ParseAsync(response.Content);
                if (!payload.RootElement.TryGetProperty("Documents", out JsonElement documents) ||
                    documents.ValueKind != JsonValueKind.Array)
                {
                    throw new CompatibilityOracleException("Decrypted feed response did not contain a Documents array.");
                }

                foreach (JsonElement document in documents.EnumerateArray())
                {
                    if (document.TryGetProperty("id", out JsonElement id) &&
                        string.Equals(id.GetString(), documentId, StringComparison.Ordinal))
                    {
                        EnsureDecryptedJsonFidelity(document, documentId);
                        return;
                    }
                }
            }

            throw new CompatibilityOracleException($"Decrypted stream response did not contain document {documentId}.");
        }

        private static void EnsureDecryptedJsonFidelity(JsonElement document, string documentId)
        {
            if (!document.TryGetProperty("id", out JsonElement id) ||
                !string.Equals(id.GetString(), documentId, StringComparison.Ordinal) ||
                !document.TryGetProperty("PK", out JsonElement partitionKey) ||
                !string.Equals(partitionKey.GetString(), PartitionKeyValue, StringComparison.Ordinal))
            {
                throw new CompatibilityOracleException("Decrypted JSON identity fields did not match the expected document.");
            }

            string expectedLong = EncryptedLongValue.ToString(CultureInfo.InvariantCulture);
            if (!document.TryGetProperty("EncLong", out JsonElement longValue) ||
                longValue.ValueKind != JsonValueKind.Number ||
                !string.Equals(longValue.GetRawText(), expectedLong, StringComparison.Ordinal))
            {
                throw new CompatibilityOracleException(
                    $"Decrypted EncLong did not preserve its exact JSON integer representation: {GetRawTextOrMissing(longValue)}");
            }

            if (!document.TryGetProperty("EncIntegralDouble", out JsonElement integralDouble) ||
                integralDouble.ValueKind != JsonValueKind.Number ||
                !string.Equals(integralDouble.GetRawText(), "5.0", StringComparison.Ordinal))
            {
                throw new CompatibilityOracleException(
                    $"Decrypted EncIntegralDouble did not preserve its exact JSON double representation: {GetRawTextOrMissing(integralDouble)}");
            }

            if (!document.TryGetProperty("EncNormalDouble", out JsonElement normalDouble) ||
                normalDouble.ValueKind != JsonValueKind.Number ||
                !string.Equals(normalDouble.GetRawText(), "1234.5", StringComparison.Ordinal))
            {
                throw new CompatibilityOracleException(
                    $"Decrypted EncNormalDouble did not preserve its exact JSON double representation: {GetRawTextOrMissing(normalDouble)}");
            }
        }

        private static string GetRawTextOrMissing(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.Undefined ? "<missing>" : value.GetRawText();
        }

        private static async Task<JObject> ReadRawAsync(Container plain, string documentId)
        {
            try
            {
                return (await plain.ReadItemAsync<JObject>(
                    documentId,
                    new PartitionKey(PartitionKeyValue))).Resource;
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Raw document was not found: {documentId}", exception);
            }
        }

        private static void EnsureRawEncrypted(JObject raw, string family)
        {
            if (raw?["_ei"] is not JObject encryptionInfo)
            {
                throw new CompatibilityOracleException("Encrypted document does not contain _ei metadata.");
            }

            int expectedFormatVersion = family == MdeFamily ? 3 : 2;
            int actualFormatVersion = encryptionInfo.Value<int?>("_ef") ?? -1;
            if (actualFormatVersion != expectedFormatVersion)
            {
                throw new CompatibilityOracleException(
                    $"Encrypted document format is v{actualFormatVersion}, expected v{expectedFormatVersion}.");
            }

            if (family == MdeFamily)
            {
                foreach (string propertyName in EncryptedPropertyNames)
                {
                    JToken token = raw[propertyName];
                    if (token == null || token.Type != JTokenType.String)
                    {
                        throw new CompatibilityOracleException(
                            $"MDE property {propertyName} was not stored as opaque ciphertext.");
                    }
                }

                if (encryptionInfo["_ep"] is not JArray encryptedPathArray)
                {
                    throw new CompatibilityOracleException("MDE document does not contain _ei._ep.");
                }

                foreach (string encryptedPath in EncryptedPaths)
                {
                    if (!encryptedPathArray.Any(token => token.Value<string>() == encryptedPath))
                    {
                        throw new CompatibilityOracleException($"MDE metadata omitted encrypted path {encryptedPath}.");
                    }
                }

                if (encryptedPathArray.Any(token =>
                    token.Type == JTokenType.Null ||
                    string.IsNullOrWhiteSpace(token.Value<string>())))
                {
                    throw new CompatibilityOracleException("MDE metadata contains a null or empty encrypted path.");
                }
            }
            else
            {
                foreach (string propertyName in EncryptedPropertyNames)
                {
                    if (raw[propertyName] != null && raw[propertyName].Type != JTokenType.Null)
                    {
                        throw new CompatibilityOracleException($"AEAD property {propertyName} remained in plaintext.");
                    }
                }

                if (string.IsNullOrWhiteSpace(encryptionInfo.Value<string>("_ed")))
                {
                    throw new CompatibilityOracleException("AEAD document does not contain _ei._ed ciphertext.");
                }
            }
        }

        private static void EnsureDocumentMatches(Doc actual, string documentId)
        {
            if (actual == null)
            {
                throw new InvalidOperationException($"Document was not found: {documentId}");
            }

            string actualSignature = GetSignature(actual);
            string expectedSignature = GetSignature(BuildDocument(documentId));
            if (!string.Equals(actualSignature, expectedSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Decrypted document mismatch. Actual={Show(actualSignature)} Expected={Show(expectedSignature)}");
            }
        }

        private static async Task<List<string>> CaptureScopesAsync(Func<Task> action)
        {
            List<string> scopes = new();
            using ActivityListener listener = new()
            {
                ShouldListenTo = source => string.Equals(source.Name, ActivitySourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (!string.IsNullOrWhiteSpace(activity?.OperationName))
                    {
                        lock (scopes)
                        {
                            scopes.Add(activity.OperationName);
                        }
                    }
                },
            };

            ActivitySource.AddActivityListener(listener);
            await action();
            lock (scopes)
            {
                return scopes.ToList();
            }
        }

        private static void EnsureProcessorScopes(
            IReadOnlyCollection<string> scopes,
            string family,
            string processor,
            bool expectScope,
            bool allowNewtonsoftFallback)
        {
#if COMPAT_CURRENT
            if (family != MdeFamily || !expectScope)
            {
                return;
            }

            string expectedEncrypt = "EncryptionProcessor.Encrypt.Mde." + processor;
            string expectedDecrypt = "EncryptionProcessor.Decrypt.Mde." + processor;
            string oppositeProcessor = processor == StreamProcessor ? NewtonsoftProcessor : StreamProcessor;
            string oppositeEncrypt = "EncryptionProcessor.Encrypt.Mde." + oppositeProcessor;
            string oppositeDecrypt = "EncryptionProcessor.Decrypt.Mde." + oppositeProcessor;

            if (!scopes.Contains(expectedEncrypt, StringComparer.Ordinal) &&
                !scopes.Contains(expectedDecrypt, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Requested {processor} processor was not observed. Scopes=[{string.Join(", ", scopes)}]");
            }

            bool oppositeEncryptObserved = scopes.Contains(oppositeEncrypt, StringComparer.Ordinal);
            bool oppositeDecryptObserved = scopes.Contains(oppositeDecrypt, StringComparer.Ordinal);
            if (oppositeEncryptObserved ||
                (oppositeDecryptObserved &&
                 !(allowNewtonsoftFallback && processor == StreamProcessor)))
            {
                throw new InvalidOperationException(
                    $"Unexpected {oppositeProcessor} processor was observed. Scopes=[{string.Join(", ", scopes)}]");
            }
#else
            _ = scopes;
            _ = family;
            _ = processor;
            _ = expectScope;
            _ = allowNewtonsoftFallback;
#endif
        }

        private static Doc BuildDocument(string documentId)
        {
            return new Doc
            {
                id = documentId,
                PK = PartitionKeyValue,
                NonSensitive = "plain",
                Sensitive = $"secret::{documentId}",
                PlainEscaped = PlainEscapedValue,
                EncEscaped = EncryptedEscapedValue,
                EncAstral = EncryptedAstralValue,
                EscapedPropertyValue = EscapedPropertyValue,
                EncObj = new JObject { ["a"] = JValue.CreateNull(), ["b"] = 1 },
                EncArr = new JArray { 1, JValue.CreateNull(), 2 },
                EncLong = EncryptedLongValue,
                EncIntegralDouble = EncryptedIntegralDoubleValue,
                EncNormalDouble = EncryptedNormalDoubleValue,
            };
        }

        private static string GetSignature(Doc document)
        {
            if (document == null)
            {
                return "<null-document>";
            }

            string objectSignature = document.EncObj == null
                ? "<null-object>"
                : $"{{a={GetTokenSignature(document.EncObj["a"])},b={GetTokenSignature(document.EncObj["b"])}}}";
            string arraySignature = document.EncArr == null
                ? "<null-array>"
                : "[" + string.Join(",", document.EncArr.Select(GetTokenSignature)) + "]";
            return string.Join(
                "\u001F",
                new[]
                {
                    document.id ?? "<null>",
                    document.PK ?? "<null>",
                    document.Sensitive ?? "<null>",
                    document.NonSensitive ?? "<null>",
                    document.PlainEscaped ?? "<null>",
                    document.EncEscaped ?? "<null>",
                    document.EncAstral ?? "<null>",
                    document.EscapedPropertyValue ?? "<null>",
                    document.EncLong.ToString(CultureInfo.InvariantCulture),
                    document.EncIntegralDouble.ToString("R", CultureInfo.InvariantCulture),
                    document.EncNormalDouble.ToString("R", CultureInfo.InvariantCulture),
                    objectSignature,
                    arraySignature,
                });
        }

        private static string GetTokenSignature(JToken token)
        {
            return token == null
                ? "<missing>"
                : token.Type == JTokenType.Null
                    ? "null"
                    : token.ToString(Formatting.None);
        }

        private static string GetKeyContainerId(string writer)
        {
            return $"keys-{writer}";
        }

        private static string GetItemContainerId(string writer, string family)
        {
            return $"items-{writer}-{family.ToLowerInvariant()}";
        }

        private static string GetDekId(string writer, string family)
        {
            return $"{writer}-{family.ToLowerInvariant()}-dek";
        }

        private static string GetMasterKeyId(string writer)
        {
            return $"https://compat.matrix/{writer}";
        }

        private static string GetDocumentId(string writer, string family, string processor)
        {
            return $"{writer}-{family.ToLowerInvariant()}-{processor.ToLowerInvariant()}";
        }

        private static Dictionary<string, string> ParseArguments(IEnumerable<string> args)
        {
            Dictionary<string, string> parsed = new(StringComparer.OrdinalIgnoreCase);
            foreach (string argument in args)
            {
                int separator = argument.IndexOf('=');
                if (argument.StartsWith("--", StringComparison.Ordinal) && separator > 2)
                {
                    parsed[argument.Substring(2, separator - 2)] = argument[(separator + 1)..];
                }
            }

            return parsed;
        }

        private static string GetRequired(IReadOnlyDictionary<string, string> arguments, string name)
        {
            if (!arguments.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required argument --{name}=...");
            }

            return value;
        }

        private static void EmitObservation(
            string scenarioId,
            string status,
            string detail,
            string processor,
            IReadOnlyList<string> scopes)
        {
            Emit(new WorkerRecord
            {
                Kind = "observation",
                Role = WorkerRole,
                ScenarioId = scenarioId,
                Status = status,
                Detail = detail,
                Processor = processor,
                ObservedScopes = scopes,
            });
        }

        private static void Emit(WorkerRecord record)
        {
            Console.Out.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
        }

        private static string Describe(Exception exception)
        {
            return $"{exception.GetType().Name}: {Show(exception.Message)}";
        }

        private static string Show(string value)
        {
            return (value ?? "<null>")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\u0001", "\\u0001");
        }

        private sealed class WorkerSettings
        {
            public string Endpoint { get; private set; }

            public string Key { get; private set; }

            public string Database { get; private set; }

            public static WorkerSettings Create(IReadOnlyDictionary<string, string> arguments)
            {
                return new WorkerSettings
                {
                    Endpoint = GetRequired(arguments, "endpoint"),
                    Key = GetRequired(arguments, "key"),
                    Database = GetRequired(arguments, "database"),
                };
            }
        }

        private sealed class WorkerRecord
        {
            public string Kind { get; set; }

            public string Role { get; set; }

            public string ScenarioId { get; set; }

            public string Status { get; set; }

            public string Detail { get; set; }

            public string PackageVersion { get; set; }

            public string InformationalVersion { get; set; }

            public string AssemblyVersion { get; set; }

            public string AssemblyMvid { get; set; }

            public string AssemblySha256 { get; set; }

            public string AssemblyPath { get; set; }

            public string CosmosVersion { get; set; }

            public string MdeVersion { get; set; }

            public string Processor { get; set; }

            public IReadOnlyList<string> ObservedScopes { get; set; }
        }

        private sealed class CompatibilityOracleException : InvalidOperationException
        {
            public CompatibilityOracleException(string message)
                : base(message)
            {
            }
        }

        private sealed class WriteScenario
        {
            public WriteScenario(string family, string processor)
            {
                this.Family = family;
                this.Processor = processor;
            }

            public string Family { get; }

            public string Processor { get; }
        }

        private sealed class ReadScenario
        {
            public ReadScenario(string family, string writeProcessor, string readProcessor)
            {
                this.Family = family;
                this.WriteProcessor = writeProcessor;
                this.ReadProcessor = readProcessor;
            }

            public string Family { get; }

            public string WriteProcessor { get; }

            public string ReadProcessor { get; }
        }

        private sealed class Doc
        {
            public string id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive { get; set; }

            public string PlainEscaped { get; set; }

            public string EncEscaped { get; set; }

            public string EncAstral { get; set; }

            [JsonProperty(EscapedPropertyName)]
            public string EscapedPropertyValue { get; set; }

            public JObject EncObj { get; set; }

            public JArray EncArr { get; set; }

            public long EncLong { get; set; }

            public double EncIntegralDouble { get; set; }

            public double EncNormalDouble { get; set; }
        }
    }
}
