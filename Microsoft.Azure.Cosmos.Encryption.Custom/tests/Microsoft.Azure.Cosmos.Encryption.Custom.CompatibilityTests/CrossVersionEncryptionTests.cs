namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide;
    using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Tests cross-version encryption/decryption compatibility.
    /// The core purpose: verify that data encrypted with version A can be decrypted with version B, and vice versa.
    /// </summary>
    [Trait("Category", "Compatibility")]
    public class CrossVersionEncryptionTests : CompatibilityTestBase
    {
        public CrossVersionEncryptionTests(ITestOutputHelper output) : base(output) { }

        /// <summary>
        /// Generates all version pairs to test (A, B) where A encrypts and B decrypts.
        /// This ensures we test all combinations of cross-version compatibility.
        /// </summary>
        public static IEnumerable<object[]> GetVersionPairs()
        {
            string[] versions = VersionMatrix.GetTestVersions();

            // Generate all pairs (including same version)
            foreach (string encryptVersion in versions)
            {
                foreach (string decryptVersion in versions)
                {
                    yield return new object[] { encryptVersion, decryptVersion };
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetVersionPairs))]
        public void CanEncryptWithVersionA_AndDecryptWithVersionB(string encryptVersion, string decryptVersion)
        {
            this.LogInfo($"Testing: Encrypt with {encryptVersion} → Decrypt with {decryptVersion}");

            // Arrange
            byte[] testData = this.CreateTestPayload();
            byte[] encryptedData;
            byte[] decryptedData;

            try
            {
                // Act: Encrypt with version A
                using (VersionLoader encryptLoader = VersionLoader.Load(encryptVersion))
                {
                    encryptedData = this.EncryptDataWithVersion(encryptLoader, testData);
                    encryptedData.Should().NotBeNull($"Encryption with {encryptVersion} should produce data");
                    encryptedData.Length.Should().BeGreaterThan(0);
                    this.LogInfo($"  ✓ Encrypted with {encryptVersion}: {encryptedData.Length} bytes");
                }

                // Act: Decrypt with version B
                using (VersionLoader decryptLoader = VersionLoader.Load(decryptVersion))
                {
                    decryptedData = this.DecryptDataWithVersion(decryptLoader, encryptedData);
                    decryptedData.Should().NotBeNull($"Decryption with {decryptVersion} should produce data");
                    this.LogInfo($"  ✓ Decrypted with {decryptVersion}: {decryptedData.Length} bytes");
                }

                // Assert: Data should round-trip correctly
                decryptedData.Should().BeEquivalentTo(testData,
                    $"Data encrypted with {encryptVersion} must decrypt correctly with {decryptVersion}");

                this.LogInfo($"✓ SUCCESS: {encryptVersion} → {decryptVersion} compatibility verified");
            }
            catch (Exception ex)
            {
                this.LogError($"✗ FAILED: {encryptVersion} → {decryptVersion}");
                this.LogError($"  Error: {ex.Message}");
                throw;
            }
        }

        [Theory]
        [MemberData(nameof(GetVersionPairs))]
        public void CanEncryptAndDecryptDeterministic_AcrossVersions(string encryptVersion, string decryptVersion)
        {
            this.LogInfo($"Testing Deterministic: Encrypt with {encryptVersion} → Decrypt with {decryptVersion}");

            // Arrange
            byte[] testData = this.CreateTestPayload();
            byte[] encryptedData1;
            byte[] encryptedData2;
            byte[] decryptedData;

            try
            {
                // Act: Encrypt same data twice with version A using deterministic encryption
                using (VersionLoader encryptLoader = VersionLoader.Load(encryptVersion))
                {
                    encryptedData1 = this.EncryptDataWithVersion(encryptLoader, testData, isDeterministic: true);
                    encryptedData2 = this.EncryptDataWithVersion(encryptLoader, testData, isDeterministic: true);
                    
                    encryptedData1.Should().NotBeNull();
                    encryptedData2.Should().NotBeNull();
                    
                    // Deterministic encryption should produce identical ciphertext
                    encryptedData1.Should().BeEquivalentTo(encryptedData2,
                        $"Deterministic encryption with {encryptVersion} should produce identical output");
                    
                    this.LogInfo($"  ✓ Deterministic encryption with {encryptVersion}: {encryptedData1.Length} bytes");
                }

                // Act: Decrypt with version B
                using (VersionLoader decryptLoader = VersionLoader.Load(decryptVersion))
                {
                    decryptedData = this.DecryptDataWithVersion(decryptLoader, encryptedData1, isDeterministic: true);
                    decryptedData.Should().NotBeNull();
                    this.LogInfo($"  ✓ Decrypted with {decryptVersion}: {decryptedData.Length} bytes");
                }

                // Assert
                decryptedData.Should().BeEquivalentTo(testData,
                    $"Deterministic data encrypted with {encryptVersion} must decrypt correctly with {decryptVersion}");

                this.LogInfo($"✓ SUCCESS: Deterministic {encryptVersion} → {decryptVersion} compatibility verified");
            }
            catch (NotSupportedException notSupported)
            {
                this.LogInfo($"Skipping deterministic scenario for {encryptVersion} → {decryptVersion}: {notSupported.Message}");
                return;
            }
            catch (Exception ex)
            {
                this.LogError($"✗ FAILED: Deterministic {encryptVersion} → {decryptVersion}");
                this.LogError($"  Error: {ex.Message}");
                throw;
            }
        }

        [Theory]
        [MemberData(nameof(GetVersionPairs))]
        public void CanEncryptAndDecryptRandomized_AcrossVersions(string encryptVersion, string decryptVersion)
        {
            this.LogInfo($"Testing Randomized: Encrypt with {encryptVersion} → Decrypt with {decryptVersion}");

            // Arrange
            byte[] testData = this.CreateTestPayload();
            byte[] encryptedData1;
            byte[] encryptedData2;
            byte[] decryptedData;

            try
            {
                // Act: Encrypt same data twice with version A using randomized encryption
                using (VersionLoader encryptLoader = VersionLoader.Load(encryptVersion))
                {
                    encryptedData1 = this.EncryptDataWithVersion(encryptLoader, testData, isDeterministic: false);
                    encryptedData2 = this.EncryptDataWithVersion(encryptLoader, testData, isDeterministic: false);
                    
                    encryptedData1.Should().NotBeNull();
                    encryptedData2.Should().NotBeNull();
                    
                    // Randomized encryption should produce different ciphertext
                    encryptedData1.Should().NotBeEquivalentTo(encryptedData2,
                        $"Randomized encryption with {encryptVersion} should produce different output each time");
                    
                    this.LogInfo($"  ✓ Randomized encryption with {encryptVersion}: {encryptedData1.Length} bytes");
                }

                // Act: Decrypt with version B
                using (VersionLoader decryptLoader = VersionLoader.Load(decryptVersion))
                {
                    decryptedData = this.DecryptDataWithVersion(decryptLoader, encryptedData1, isDeterministic: false);
                    decryptedData.Should().NotBeNull();
                    this.LogInfo($"  ✓ Decrypted with {decryptVersion}: {decryptedData.Length} bytes");
                }

                // Assert
                decryptedData.Should().BeEquivalentTo(testData,
                    $"Randomized data encrypted with {encryptVersion} must decrypt correctly with {decryptVersion}");

                this.LogInfo($"✓ SUCCESS: Randomized {encryptVersion} → {decryptVersion} compatibility verified");
            }
            catch (Exception ex)
            {
                this.LogError($"✗ FAILED: Randomized {encryptVersion} → {decryptVersion}");
                this.LogError($"  Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates test data payload for encryption/decryption tests.
        /// </summary>
        private byte[] CreateTestPayload()
        {
            string testString = "This is test data for cross-version encryption compatibility validation. " +
                               "It includes special characters: !@#$%^&*()_+-=[]{}|;':\",./<>? " +
                               "And Unicode: 你好世界 مرحبا العالم Привет мир";
            return Encoding.UTF8.GetBytes(testString);
        }

        /// <summary>
        /// Encrypts data using a specific version's encryption API.
        /// Uses reflection to invoke the encryption functionality from the loaded assembly.
        /// </summary>
        private byte[] EncryptDataWithVersion(VersionLoader loader, byte[] plaintext, bool isDeterministic = false)
        {
            string algorithm = this.ResolveEncryptionAlgorithm(loader, isDeterministic);
            object dataEncryptionKey = this.CreateDataEncryptionKey(loader, algorithm, isDeterministic);

            MethodInfo encryptMethod = dataEncryptionKey
                .GetType()
                .GetMethod("EncryptData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(byte[]) }, null)
                ?? throw new InvalidOperationException($"The loaded version '{loader.Version}' does not expose a public EncryptData(byte[]) method on DataEncryptionKey.");

            try
            {
                return (byte[])encryptMethod.Invoke(dataEncryptionKey, new object[] { plaintext });
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new InvalidOperationException($"Failed to encrypt with version {loader.Version}: {tie.InnerException.Message}", tie.InnerException);
            }
        }

        /// <summary>
        /// Decrypts data using a specific version's decryption API.
        /// Uses reflection to invoke the decryption functionality from the loaded assembly.
        /// </summary>
        private byte[] DecryptDataWithVersion(VersionLoader loader, byte[] ciphertext, bool isDeterministic = false)
        {
            string algorithm = this.ResolveEncryptionAlgorithm(loader, isDeterministic);
            object dataEncryptionKey = this.CreateDataEncryptionKey(loader, algorithm, isDeterministic);

            MethodInfo decryptMethod = dataEncryptionKey
                .GetType()
                .GetMethod("DecryptData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(byte[]) }, null)
                ?? throw new InvalidOperationException($"The loaded version '{loader.Version}' does not expose a public DecryptData(byte[]) method on DataEncryptionKey.");

            try
            {
                return (byte[])decryptMethod.Invoke(dataEncryptionKey, new object[] { ciphertext });
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new InvalidOperationException($"Failed to decrypt with version {loader.Version}: {tie.InnerException.Message}", tie.InnerException);
            }
        }

        private string ResolveEncryptionAlgorithm(VersionLoader loader, bool isDeterministic)
        {
            Type algorithmType = loader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionAlgorithm")
                ?? throw new InvalidOperationException($"Could not find CosmosEncryptionAlgorithm type in version {loader.Version}");

            string[] preferredFieldNames = isDeterministic
                ? new[]
                {
                    "MdeAeadAes256CbcHmac256Deterministic",
                    "AEAes256CbcHmacSha256Deterministic",
                }
                : new[]
                {
                    "MdeAeadAes256CbcHmac256Randomized",
                    "AEAes256CbcHmacSha256Randomized",
                };

            foreach (string fieldName in preferredFieldNames)
            {
                FieldInfo field = algorithmType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    string value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            string keyword = isDeterministic ? "Deterministic" : "Randomized";
            FieldInfo[] algorithmFields = algorithmType.GetFields(BindingFlags.Public | BindingFlags.Static);
            FieldInfo fallbackField = algorithmFields
                .FirstOrDefault(f => f.FieldType == typeof(string) && f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (fallbackField != null)
            {
                string fallbackValue = fallbackField.GetValue(null) as string;
                if (!string.IsNullOrEmpty(fallbackValue))
                {
                    return fallbackValue;
                }
            }

            if (isDeterministic)
            {
                throw new NotSupportedException($"Deterministic encryption is not exposed via the public API in version {loader.Version}.");
            }

            throw new InvalidOperationException($"Version {loader.Version} does not expose a supported encryption algorithm via the public API.");
        }

        private object CreateDataEncryptionKey(VersionLoader loader, string algorithm, bool isDeterministic)
        {
            Type dekType = loader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKey")
                ?? throw new InvalidOperationException($"Could not find DataEncryptionKey type in version {loader.Version}");

            MethodInfo[] factoryMethods = dekType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => string.Equals(m.Name, "Create", StringComparison.Ordinal))
                .ToArray();

            if (factoryMethods.Length == 0)
            {
                throw new InvalidOperationException($"Version {loader.Version} does not expose a public DataEncryptionKey.Create factory method.");
            }

            byte[] rawKey = CreateDeterministicKeyMaterial();
            IEnumerable<string> algorithmCandidates = this.GetAlgorithmCandidates(loader, algorithm);
            IEnumerable<MethodInfo> orderedMethods = factoryMethods.OrderByDescending(m => m.GetParameters().Length);

            foreach (string candidateAlgorithm in algorithmCandidates)
            {
                foreach (MethodInfo method in orderedMethods)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < 2)
                    {
                        continue;
                    }

                    object[] args = new object[parameters.Length];
                    args[0] = rawKey;
                    args[1] = candidateAlgorithm;

                    if (parameters.Length >= 3)
                    {
                        if (!this.TryPopulateEncryptionModeArgument(parameters[2].ParameterType, isDeterministic, out object modeArgument))
                        {
                            continue;
                        }

                        args[2] = modeArgument;
                    }
                    else if (isDeterministic)
                    {
                        // Method does not allow specifying deterministic mode.
                        continue;
                    }

                    try
                    {
                        return method.Invoke(null, args);
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException is ArgumentException)
                    {
                        // Try next overload or algorithm candidate.
                        continue;
                    }
                }
            }

            if (isDeterministic)
            {
                throw new NotSupportedException($"Version {loader.Version} does not expose a public deterministic encryption path via DataEncryptionKey.Create.");
            }

            throw new InvalidOperationException($"Unable to create a data encryption key using the public API for version {loader.Version}.");
        }

        private IEnumerable<string> GetAlgorithmCandidates(VersionLoader loader, string preferredAlgorithm)
        {
            HashSet<string> yieldedAlgorithms = new HashSet<string>(StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(preferredAlgorithm) && yieldedAlgorithms.Add(preferredAlgorithm))
            {
                yield return preferredAlgorithm;
            }

            string legacyAlgorithm = this.TryGetAlgorithmValue(loader, "AEAes256CbcHmacSha256Randomized");
            if (!string.IsNullOrEmpty(legacyAlgorithm) && yieldedAlgorithms.Add(legacyAlgorithm))
            {
                yield return legacyAlgorithm;
            }
        }

        private string TryGetAlgorithmValue(VersionLoader loader, string fieldName)
        {
            Type algorithmType = loader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionAlgorithm");
            if (algorithmType == null)
            {
                return null;
            }

            FieldInfo field = algorithmType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            return field?.GetValue(null) as string;
        }

        private bool TryPopulateEncryptionModeArgument(Type parameterType, bool isDeterministic, out object value)
        {
            if (parameterType == typeof(bool))
            {
                value = isDeterministic;
                return true;
            }

            if (parameterType.IsEnum)
            {
                string enumName = isDeterministic ? "Deterministic" : "Randomized";
                if (Enum.GetNames(parameterType).Any(name => string.Equals(name, enumName, StringComparison.OrdinalIgnoreCase)))
                {
                    value = Enum.Parse(parameterType, enumName, ignoreCase: true);
                    return true;
                }
            }

            if (parameterType == typeof(string))
            {
                value = isDeterministic ? "Deterministic" : "Randomized";
                return true;
            }

            value = null;
            return false;
        }

        private static byte[] CreateDeterministicKeyMaterial()
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(i + 1);
            }

            return key;
        }
    }
}
