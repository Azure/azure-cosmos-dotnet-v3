//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PatchOperationTTests
    {
        private const string path = "/random";

        [TestMethod]
        public void CastPatchOperationTest()
        {
            PatchOperation operation = PatchOperation.Add(path, "test_string");
            PatchOperation<string> addStringOp = AssertCanCast<string>(operation);
            Assert.AreEqual("test_string", addStringOp?.Value);

            DateTime dateTime = new DateTime(2022, 8, 1);
            operation = PatchOperation.Add(path, dateTime);
            PatchOperation<DateTime> addDateTimeOp = AssertCanCast<DateTime>(operation);
            Assert.AreEqual(dateTime, addDateTimeOp?.Value);

            var complexObject = new { a = "complex", b = 12.34, c = true };
            operation = PatchOperation.Add(path, complexObject);
            dynamic addDynamicOp = operation;
            Assert.AreEqual("complex", addDynamicOp.Value.a);
            Assert.AreEqual(12.34, addDynamicOp.Value.b);
            Assert.AreEqual(true, addDynamicOp.Value.c);

            int[] arrayObject = { 1, 2, 3 };
            operation = PatchOperation.Replace(path, arrayObject);
            PatchOperation<int[]> replaceOp = AssertCanCast<int[]>(operation);
            Assert.AreEqual(arrayObject, replaceOp.Value);

            Guid guid = new Guid();
            operation = PatchOperation.Set(path, guid);
            PatchOperation<Guid> setGuidOp = AssertCanCast<Guid>(operation);
            Assert.AreEqual(guid, setGuidOp.Value);

            operation = PatchOperation.Set<object>(path, null);
            PatchOperation<object> setObjectOp = AssertCanCast<object>(operation);
            Assert.AreEqual(null, setObjectOp.Value);

            operation = PatchOperation.Increment(path, 7.0);
            PatchOperation<double> incrementDoubleOp = AssertCanCast<double>(operation);
            Assert.AreEqual(7.0, incrementDoubleOp.Value);

            operation = PatchOperation.Increment(path, 40);
            PatchOperation<long> incrementIntOp = AssertCanCast<long>(operation);
            Assert.AreEqual(40, incrementIntOp.Value);
        }

        private static PatchOperation<TPatchData> AssertCanCast<TPatchData>(PatchOperation operation)
        {
            PatchOperation<TPatchData> castedOp = operation as PatchOperation<TPatchData>;
            Assert.IsNotNull(castedOp, $"{nameof(PatchOperation)} should be castable to {nameof(PatchOperation<TPatchData>)}");

            return castedOp;
        }
    }
}