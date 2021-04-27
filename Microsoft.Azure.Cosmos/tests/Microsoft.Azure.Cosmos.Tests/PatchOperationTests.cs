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
            try
            {
                PatchOperation.Add(null, "1");
                Assert.Fail();
            }
            catch(ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }

            try
            {
                PatchOperation.Remove(null);
                Assert.Fail();
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }
        }

        [TestMethod]
        public void ConstructPatchOperationTest()
        {
            PatchOperation operation = PatchOperation.Add(path, "string");
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, "string");

            DateTime current = DateTime.UtcNow;
            operation = PatchOperation.Add(path, current);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, current);

            dynamic complexObject = new { a = "complex", b = 12.34, c = true };
            operation = PatchOperation.Add(path,  complexObject);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, complexObject);

            operation = PatchOperation.Remove(path);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Remove, "value not required");

            int[] arrayObject = { 1, 2, 3 };
            operation = PatchOperation.Replace(path, arrayObject);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Replace, arrayObject);

            Guid guid = new Guid();
            operation = PatchOperation.Set(path, guid);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Set, guid);
        }

        private static void ValidateOperations<T>(PatchOperation patchOperation, PatchOperationType operationType, T value)
        {
            Assert.AreEqual(operationType, patchOperation.OperationType);
            Assert.AreEqual(path, patchOperation.Path);

            if (!operationType.Equals(PatchOperationType.Remove))
            {
                string expected;
                CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
                using (Stream stream = cosmosSerializer.ToStream(value))
                {
                    using (StreamReader streamReader = new StreamReader(stream))
                    {
                        expected = streamReader.ReadToEnd();
                    }
                }

                Assert.IsTrue(patchOperation.TrySerializeValueParameter(new CustomSerializer(), out Stream valueParam));

                string actual;
                using (valueParam)
                {
                    using (StreamReader streamReader = new StreamReader(valueParam))
                    {
                        actual = streamReader.ReadToEnd();
                    }
                }

                Assert.AreEqual(expected, actual);
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
