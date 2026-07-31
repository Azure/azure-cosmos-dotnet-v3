//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompatibilityMatrixInfrastructureTests
    {
        [TestMethod]
        public void ParseRecord_RejectsMalformedJson()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixProtocol.ParseRecord("not-json"));
        }

        [TestMethod]
        public void ValidateResults_RejectsDuplicateScenario()
        {
            IReadOnlyCollection<string> expected = new[] { "released-write-mde-newtonsoft" };
            CompatibilityMatrixRecord[] actual =
            {
                Pass("released-write-mde-newtonsoft"),
                Pass("released-write-mde-newtonsoft"),
            };

            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixResultOracle.Validate(expected, actual));
        }

        [TestMethod]
        public void ValidateResults_RejectsMissingScenario()
        {
            IReadOnlyCollection<string> expected = new[]
            {
                "released-write-mde-newtonsoft",
                "released-write-aead-newtonsoft",
            };

            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixResultOracle.Validate(expected, new[] { Pass("released-write-mde-newtonsoft") }));
        }

        [TestMethod]
        public void ValidateResults_RejectsUnexpectedScenario()
        {
            IReadOnlyCollection<string> expected = new[] { "released-write-mde-newtonsoft" };
            CompatibilityMatrixRecord[] actual =
            {
                Pass("released-write-mde-newtonsoft"),
                Pass("unexpected"),
            };

            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixResultOracle.Validate(expected, actual));
        }

        [TestMethod]
        public void ValidateResults_RejectsNonPassingScenario()
        {
            IReadOnlyCollection<string> expected = new[] { "released-write-mde-newtonsoft" };
            CompatibilityMatrixRecord actual = Pass("released-write-mde-newtonsoft");
            actual.Status = "fail";
            actual.Detail = "ciphertext was plaintext";

            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixResultOracle.Validate(expected, new[] { actual }));
        }

        [TestMethod]
        public void ValidateIdentity_RequiresExactReleasedVersionAndDistinctBinaries()
        {
            CompatibilityMatrixRecord released = Identity("released", "1.0.0-preview07", "released-hash");
            CompatibilityMatrixRecord current = Identity("current", "1.0.0-preview09", "current-hash");

            CompatibilityMatrixIdentityValidator.Validate(released, current, current.AssemblySha256);

            released.PackageVersion = "1.0.0-preview08";
            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixIdentityValidator.Validate(released, current, current.AssemblySha256));

            released.PackageVersion = "1.0.0-preview07";
            current.AssemblySha256 = released.AssemblySha256;
            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixIdentityValidator.Validate(released, current, "current-hash"));

            current.AssemblySha256 = "current-hash";
            Assert.ThrowsException<InvalidOperationException>(
                () => CompatibilityMatrixIdentityValidator.Validate(released, current, "stale-current-hash"));
        }

        [TestMethod]
        public void ParseWorkerManifest_RequiresOneReleasedAndOneCurrentWorker()
        {
            string[] lines =
            {
                @"Q:\repo\CompatMatrix.Released.csproj|Q:\repo\bin\Debug\net8.0\CompatMatrix.Released.dll",
                @"Q:\repo\CompatMatrix.Current.csproj|Q:\repo\bin\Debug\net8.0\CompatMatrix.Current.dll",
            };

            IReadOnlyDictionary<string, string> workers = CompatibilityMatrixWorkerManifest.Parse(lines);

            Assert.AreEqual(@"Q:\repo\bin\Debug\net8.0\CompatMatrix.Released.dll", workers["released"]);
            Assert.AreEqual(@"Q:\repo\bin\Debug\net8.0\CompatMatrix.Current.dll", workers["current"]);
        }

        private static CompatibilityMatrixRecord Pass(string scenarioId)
        {
            return new CompatibilityMatrixRecord
            {
                Kind = "observation",
                ScenarioId = scenarioId,
                Status = "pass",
            };
        }

        private static CompatibilityMatrixRecord Identity(string role, string version, string sha256)
        {
            return new CompatibilityMatrixRecord
            {
                Kind = "identity",
                Role = role,
                PackageVersion = version,
                AssemblySha256 = sha256,
                AssemblyMvid = role + "-mvid",
                AssemblyPath = role + ".dll",
            };
        }
    }
}
#endif
