//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public class DirectConfigurationContractTests
    {
        [TestMethod]
        [Owner("kirankk")]
        public void RestVersionSettable()
        {
            string originalVersion = HttpConstants.Versions.CurrentVersion;
            try
            {
                HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
            }
            finally
            {
                HttpConstants.Versions.CurrentVersion = originalVersion;
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void ReqeustOptionsAutoScaleSettingsTest()
        {
            IEnumerable<string> propertiesWithScaleInName = typeof(RequestOptions)
                .GetProperties()
                .Select(e => e.Name)
                .Where(e => e.IndexOf("Scale", StringComparison.OrdinalIgnoreCase) > 0);

            Assert.AreEqual(0, propertiesWithScaleInName.Count(), string.Join(",", propertiesWithScaleInName.ToArray()));
        }

        [TestMethod]
        [Owner("kirankk")]
        public void AppConfigDisbaleSkipInteropTest()
        {
            string disableSkipInteropConfig = "DisableSkipInterop";
            string allowGatewayToParseQueriesConfig = "AllowGatewayToParseQueries";

            string preTestDisableSkipInteropConfig = System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig];
            string preAllowGatewayToParseQueriesConfig = System.Configuration.ConfigurationManager.AppSettings[allowGatewayToParseQueriesConfig];

            lock (ServiceInteropWrapper.AssembliesExist)
            {
                try
                {
                    // All V2 variations should return True.
                    System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig] = bool.TrueString;
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig] = bool.FalseString;
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig] = "invalid";
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig] = "1";
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    // Only V3 variations should work as configured.
                    System.Configuration.ConfigurationManager.AppSettings[allowGatewayToParseQueriesConfig] = bool.TrueString;
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    System.Configuration.ConfigurationManager.AppSettings[allowGatewayToParseQueriesConfig] = bool.FalseString;
                    Assert.IsFalse(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());

                    System.Configuration.ConfigurationManager.AppSettings[allowGatewayToParseQueriesConfig] = "invalid";
                    Assert.IsTrue(ServiceInteropWrapper.IsGatewayAllowedToParseQueries());
                }
                finally
                {
                    System.Configuration.ConfigurationManager.AppSettings[disableSkipInteropConfig] = preTestDisableSkipInteropConfig;
                    System.Configuration.ConfigurationManager.AppSettings[allowGatewayToParseQueriesConfig] = preAllowGatewayToParseQueriesConfig;
                }
            }
        }
    }
}