//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource
{
    using System;
    using System.Collections.ObjectModel;
    using Documents;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AccountPropertiesTest
    {
        [TestMethod]
        public void GetAccountIdWithCloudName()
        {
            AccountProperties cosmosAccountSettings = new AccountProperties
            {
                Id = "testId",
            };

            Assert.AreEqual("testId(NonAzureVM)", cosmosAccountSettings.AccountNameWithCloudInformation);

            cosmosAccountSettings.Id = "newTestId";

            Assert.AreEqual("newTestId(NonAzureVM)", cosmosAccountSettings.AccountNameWithCloudInformation);
        }

    }
}
