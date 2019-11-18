//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TryCatchTests
    {
        [TestMethod]
        public void TestStackTrace()
        {
            TryCatch<object> tryCatch = this.SomeNestedMethod();
            throw tryCatch.Exception;
        }

        private TryCatch<object> SomeNestedMethod()
        {
            TryCatch<object> tryCatch;
            try
            {
                this.SomeNestedMethod2();
                tryCatch = TryCatch<object>.FromResult(null);
            }
            catch (Exception ex)
            {
                tryCatch = TryCatch<object>.FromException(ex);
            }

            return tryCatch;
        }

        private void SomeNestedMethod2()
        {
            throw new Exception();
        }
    }
}
