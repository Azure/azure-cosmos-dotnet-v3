//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PatchOperationTests
    {
        private const string path = "/random";

        [TestMethod]
        public void ThrowsOnNullArguement()
        {
            PatchSpecification patchSpecification = new PatchSpecification();

            try
            {
                patchSpecification.Add(null, "1");
                Assert.Fail();
            }
            catch(ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }

            try
            {
                patchSpecification.Remove(null);
                Assert.Fail();
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }
        }

        [TestMethod]
        public void ConstructPatchSpecificationTest()
        {
            int index = 0;
            PatchSpecification patchSpecification = new PatchSpecification();
            Assert.IsNotNull(patchSpecification.Operations);
            Assert.AreEqual(0, patchSpecification.Operations.Count);

            patchSpecification.Add(path, "string");
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Add, "string");

            DateTime current = DateTime.UtcNow;
            patchSpecification.Add(path, current);
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Add, current);

            dynamic complexObject = new { a = "complex", b = 12.34, c = true };
            patchSpecification.Add(path,  complexObject);
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Add, complexObject);

            patchSpecification.Remove(path);
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Remove, "value not required");

            int[] arrayObject = { 1, 2, 3 };
            patchSpecification.Replace(path, arrayObject);
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Replace, arrayObject);

            Guid guid = new Guid();
            patchSpecification.Set(path, guid);
            PatchOperationTests.ValidateOperations(patchSpecification, index++, PatchOperationType.Set, guid);
        }

        private static void ValidateOperations<T>(PatchSpecification patch, int index, PatchOperationType operationType, T value)
        {
            Assert.AreEqual(index + 1, patch.Operations.Count);
            Assert.AreEqual(operationType, patch.Operations[index].OperationType);
            Assert.AreEqual(path, patch.Operations[index].Path);

            if (!operationType.Equals(PatchOperationType.Remove))
            {
                string expected;
                CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
                Stream stream = cosmosSerializer.ToStream(value);
                using (StreamReader streamReader = new StreamReader(stream))
                {
                     expected = streamReader.ReadToEnd();
                }

                Assert.AreEqual(expected, patch.Operations[index].SerializeValueParameter(new CustomSerializer()));
            }
        }

        private class CustomSerializer : CosmosSerializer
        {
            private CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();

            public override T FromStream<T>(Stream stream)
            {
                return this.cosmosSerializer.FromStream<T>(stream);
            }

            public override Stream ToStream<T>(T input)
            {
                return this.cosmosSerializer.ToStream<T>(input);
            }
        }
    }
}
