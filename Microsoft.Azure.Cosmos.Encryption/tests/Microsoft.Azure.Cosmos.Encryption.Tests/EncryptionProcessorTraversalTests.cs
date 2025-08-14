//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorTraversalTests
    {

        private static JToken MakeNestedNullGraph()
        {
            return JToken.Parse("{ \"a\": null, \"b\": [ null, null, { \"c\": null } ], \"d\": { \"e\": [ null ] } }");
        }

        [TestMethod]
        public async Task EncryptJTokenAsync_Traverses_Nested_ObjectArray_WithNullLeaves_NoCrypto()
        {
            JToken token = MakeNestedNullGraph();
            // encryptionSettingForProperty: null is okay because null leaves short-circuit before usage
            await EncryptionProcessor.EncryptJTokenAsync(token, encryptionSettingForProperty: null, shouldEscape: false, cancellationToken: CancellationToken.None);

            // Remains structurally the same (all null leaves)
            Assert.IsTrue(JToken.DeepEquals(token, MakeNestedNullGraph()));
        }

        [TestMethod]
        public async Task DecryptJTokenAsync_Traverses_Nested_ObjectArray_WithNullLeaves_NoCrypto()
        {
            JToken token = MakeNestedNullGraph();
            // isEscaped = true to exercise that branch (as if property == "id")
            await EncryptionProcessor.DecryptJTokenAsync(token, encryptionSettingForProperty: null, isEscaped: true, cancellationToken: CancellationToken.None);

            Assert.IsTrue(JToken.DeepEquals(token, MakeNestedNullGraph()));
        }

        [TestMethod]
        public async Task EncryptJTokenAsync_ShouldEscapeTrue_SubtreeTraversal_NoCrypto_WithStringIdPresent()
        {
            // Document has a string id, but we traverse only the 'sub' subtree which has null leaves.
            JObject doc = JObject.Parse("{ \"id\": \"abc\", \"sub\": { \"a\": null, \"b\": [ null, { \"c\": null } ] } }");

            // Take the subtree token to avoid touching the top-level id string.
            JToken subtree = doc["sub"]!;

            // shouldEscape=true to simulate id semantics; encryptionSettingForProperty is not needed for null leaves.
            await EncryptionProcessor.EncryptJTokenAsync(subtree, encryptionSettingForProperty: null, shouldEscape: true, cancellationToken: CancellationToken.None);

            // Structure remains unchanged and no exceptions thrown (no crypto invoked).
            Assert.IsTrue(JToken.DeepEquals(doc, JObject.Parse("{ \"id\": \"abc\", \"sub\": { \"a\": null, \"b\": [ null, { \"c\": null } ] } }")));
        }
    }
}
