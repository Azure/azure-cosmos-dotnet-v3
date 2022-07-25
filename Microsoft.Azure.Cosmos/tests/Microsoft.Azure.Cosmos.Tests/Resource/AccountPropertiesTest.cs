//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource
{
    using System;
    using System.Collections.ObjectModel;
    using Documents;
    using Newtonsoft.Json;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AccountPropertiesTest
    {
        /// <summary>
        /// Resetting AccountProperties like this should not be allowed and is not encouraged/recommended.
        /// This test exist to make sure we are resetting AccountNameWithCloudInformation variable with Id.
        /// Since Account Properties id is already internal type and it is possible that internal package might be misusing it and we don't want to break them.
        /// </summary>
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

        /// <summary>
        /// Resetting AccountProperties like this should not be allowed and is not encouraged/recommended.
        /// This test exist to make sure we are resetting AccountNameWithCloudInformation variable with Id.
        /// Since Account Properties id is already internal type and it is possible that internal package might be misusing it and we don't want to break them.
        /// </summary>
        [TestMethod]
        public void GetAccountIdWithCloudNameWithJsonSerialization()
        {
            string json = "{'id' :'testId'}";
            AccountProperties cosmosAccountSettings = JsonConvert.DeserializeObject<AccountProperties>(json);

            Assert.IsNotNull(cosmosAccountSettings, "AccountProperties- is deserialized to null");
            Assert.AreEqual("testId(NonAzureVM)", cosmosAccountSettings.AccountNameWithCloudInformation);

            cosmosAccountSettings.Id = "newTestId";

            Assert.AreEqual("newTestId(NonAzureVM)", cosmosAccountSettings.AccountNameWithCloudInformation);
        }

    }
}
