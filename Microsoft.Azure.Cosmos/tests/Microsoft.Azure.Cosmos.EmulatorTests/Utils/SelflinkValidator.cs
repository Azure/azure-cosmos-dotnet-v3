//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class SelflinkValidator
    {
        internal static void ValidateDbSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/"));
        }

        internal static void ValidateContainerSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/colls/(.*)/"));
        }

        internal static void ValidateUdfSelfLink(string selfLink)
        {
            Assert.IsTrue(Regex.IsMatch(selfLink, "dbs/(.*)/colls/(.*)/udfs/(.*)/"));
        }

        internal static void ValidateSprocSelfLink(string selfLink)
        {
            Assert.IsTrue(Regex.IsMatch(selfLink, "dbs/(.*)/colls/(.*)/sprocs/(.*)/"));
        }

        internal static void ValidateTriggerSelfLink(string selfLink)
        {
            Assert.IsTrue(Regex.IsMatch(selfLink, "dbs/(.*)/colls/(.*)/triggers/(.*)/"));
        }

        internal static void ValidateUserSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/users/(.*)/"));
        }

        internal static void ValidatePermissionSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/users/(.*)/permissions/(.*)/"));
        }

        internal static void ValidateTroughputSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "offers/(.*)/"));
        }
    }
}
