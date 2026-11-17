//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Tests.Contracts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ReleasedSubclassCompatibilityTests
    {
        private const string Preview07ContractFileName = "DotNetSDKEncryptionCustomAPI.preview07.json";
        private const string Preview07FixtureAssemblyName = "Microsoft.Azure.Cosmos.Encryption.Custom.Preview07Compatibility";
        private const string Preview07ProbeTypeName = Preview07FixtureAssemblyName + ".Preview07CompatibilityProbe";

        [TestMethod]
        public async Task Preview07CompiledSubclasses_LoadAndRunAgainstCurrentAssembly()
        {
            Encryptor encryptor = (Encryptor)InvokePreview07Probe("CreateEncryptor");
            DataEncryptionKey key = (DataEncryptionKey)InvokePreview07Probe("CreateDataEncryptionKey");
            byte[] plainText = new byte[] { 1, 2, 3, 4 };

            byte[] cipherText = await encryptor.EncryptAsync(
                plainText,
                "dek",
                "algorithm",
                CancellationToken.None);
            byte[] roundTrip = await encryptor.DecryptAsync(
                cipherText,
                "dek",
                "algorithm",
                CancellationToken.None);

            CollectionAssert.AreEqual(plainText, roundTrip);

            cipherText = key.EncryptData(plainText);
            roundTrip = key.DecryptData(cipherText);

            CollectionAssert.AreEqual(plainText, roundTrip);
        }

        [TestMethod]
        public void Preview07CompiledConstructors_RunAgainstCurrentAssembly()
        {
            CosmosDataEncryptionKeyProvider defaultProvider =
                (CosmosDataEncryptionKeyProvider)InvokePreview07Probe("CreateStoreProviderWithDefault");
            CosmosDataEncryptionKeyProvider nullProvider =
                (CosmosDataEncryptionKeyProvider)InvokePreview07Probe("CreateStoreProviderWithNull");
            CosmosDataEncryptionKeyProvider timeSpanProvider =
                (CosmosDataEncryptionKeyProvider)InvokePreview07Probe("CreateStoreProviderWithTimeSpan");
            CosmosDataEncryptionKeyProvider wrapProvider =
                (CosmosDataEncryptionKeyProvider)InvokePreview07Probe("CreateWrapProvider");
            CosmosDataEncryptionKeyProvider hybridProvider =
                (CosmosDataEncryptionKeyProvider)InvokePreview07Probe("CreateHybridProvider");

            Assert.IsNotNull(defaultProvider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(nullProvider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(timeSpanProvider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(wrapProvider.EncryptionKeyWrapProvider);
            Assert.IsNotNull(hybridProvider.EncryptionKeyWrapProvider);
            Assert.IsNotNull(hybridProvider.EncryptionKeyStoreProvider);
        }

        [TestMethod]
        public void Preview07Contract_RemainsACompatibleSubset()
        {
            string releasedJson = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Contracts", Preview07ContractFileName));
            string currentJson = ContractEnforcement.GetCurrentContract(
                "Microsoft.Azure.Cosmos.Encryption.Custom");

            IReadOnlyDictionary<string, HashSet<string>> releasedTypes = FlattenContract(releasedJson);
            IReadOnlyDictionary<string, HashSet<string>> currentTypes = FlattenContract(currentJson);

            foreach (KeyValuePair<string, HashSet<string>> releasedType in releasedTypes)
            {
                Assert.IsTrue(
                    currentTypes.TryGetValue(releasedType.Key, out HashSet<string> currentMembers),
                    $"Released public type is missing or incompatible: {releasedType.Key}");

                foreach (string releasedMember in releasedType.Value)
                {
                    Assert.IsTrue(
                        currentMembers.Contains(releasedMember),
                        $"Released public member is missing or incompatible: {releasedType.Key} :: {releasedMember}");
                }
            }
        }

        [TestMethod]
        public void Preview07AbstractSurface_IsExactAndUnpublishedMembersAreAbsent()
        {
            BindingFlags publicInstanceDeclared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "DecryptAsync(Byte[], String, String, CancellationToken)",
                    "EncryptAsync(Byte[], String, String, CancellationToken)",
                },
                GetDeclaredAbstractMethodSignatures(typeof(Encryptor)));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "DecryptData(Byte[])",
                    "EncryptData(Byte[])",
                    "get_EncryptionAlgorithm()",
                    "get_RawKey()",
                },
                GetDeclaredAbstractMethodSignatures(typeof(DataEncryptionKey)));

            Assert.IsNull(typeof(Encryptor).GetMethod("GetEncryptionKeyAsync", publicInstanceDeclared));
            Assert.IsNull(typeof(CosmosEncryptor).GetMethod("GetEncryptionKeyAsync", publicInstanceDeclared));
            Assert.IsNull(typeof(DataEncryptionKey).GetMethod("GetEncryptByteCount", publicInstanceDeclared));
            Assert.IsNull(typeof(DataEncryptionKey).GetMethod("GetDecryptByteCount", publicInstanceDeclared));
            Assert.IsNull(
                typeof(DataEncryptionKey).GetMethod(
                    "EncryptData",
                    publicInstanceDeclared,
                    binder: null,
                    types: new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int) },
                    modifiers: null));
            Assert.IsNull(
                typeof(DataEncryptionKey).GetMethod(
                    "DecryptData",
                    publicInstanceDeclared,
                    binder: null,
                    types: new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int) },
                    modifiers: null));
        }

        private static object InvokePreview07Probe(string methodName)
        {
            string assemblyPath = Path.Combine(
                AppContext.BaseDirectory,
                Preview07FixtureAssemblyName + ".dll");
            Assembly fixtureAssembly = Assembly.LoadFrom(assemblyPath);
            Type probeType = fixtureAssembly.GetType(Preview07ProbeTypeName, throwOnError: true);
            MethodInfo method = probeType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method);
            return method.Invoke(null, null);
        }

        private static IReadOnlyDictionary<string, HashSet<string>> FlattenContract(string json)
        {
            Dictionary<string, HashSet<string>> types = new Dictionary<string, HashSet<string>>();
            AddTypes((JObject)JObject.Parse(json)["Subclasses"], types);
            return types;
        }

        private static void AddTypes(
            JObject typeMap,
            IDictionary<string, HashSet<string>> types)
        {
            foreach (JProperty typeProperty in typeMap.Properties())
            {
                JObject type = (JObject)typeProperty.Value;
                if (!types.TryGetValue(typeProperty.Name, out HashSet<string> members))
                {
                    members = new HashSet<string>(StringComparer.Ordinal);
                    types.Add(typeProperty.Name, members);
                }

                JObject memberMap = (JObject)type["Members"];
                foreach (JProperty memberProperty in memberMap.Properties())
                {
                    JObject metadata = (JObject)memberProperty.Value;
                    members.Add($"{metadata["Type"]}|{metadata["MethodInfo"]}");
                }

                AddTypes((JObject)type["Subclasses"], types);
                AddTypes((JObject)type["NestedTypes"], types);
            }
        }

        private static string[] GetDeclaredAbstractMethodSignatures(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.IsAbstract)
                .Select(method =>
                    $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})")
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
