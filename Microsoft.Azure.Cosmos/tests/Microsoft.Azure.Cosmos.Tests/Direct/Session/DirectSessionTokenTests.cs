//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Client.Test
{
    using System;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public sealed class DirectSessionTokenTests
    {
        private FieldInfo falseProgressMergeDisabledField;
        private bool originalFalseProgressMergeDisabled;

        [TestInitialize]
        public void TestInitialize()
        {
            this.falseProgressMergeDisabledField = typeof(VectorSessionToken)
                .GetField("isFalseProgressMergeDisabled", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(this.falseProgressMergeDisabledField);
            this.originalFalseProgressMergeDisabled = (bool)this.falseProgressMergeDisabledField.GetValue(null);
            this.falseProgressMergeDisabledField.SetValue(null, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.falseProgressMergeDisabledField?.SetValue(null, this.originalFalseProgressMergeDisabled);
        }


        [TestMethod]
        [Owner("dikshibahl")]
        public void ValidateSessionTokenParsingFromSimpleSessionTokenString()
        {
            string simpleSessionToken = "500";
            Assert.IsTrue(SimpleSessionToken.TryCreate(simpleSessionToken, out _));
            Assert.IsFalse(VectorSessionToken.TryCreate(simpleSessionToken, out _));
        }


        [TestMethod]
        [Owner("amudumba")]
        public void ValidateDuringGlobalLsnMismatchOfSameVCVersion()
        {
            //Session token 1 with VC Version #1 and Region 1 at GLSN 100.
            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=5#3=30", out ISessionToken sessionToken1));
            //Session token 2 with VC Version #1 and Region 1 at GLSN .
            Assert.IsTrue(VectorSessionToken.TryCreate("1#99#1=20#2=5#3=30", out ISessionToken sessionToken2));
            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsFalse(sessionToken1.IsValid(sessionToken2));
            Assert.IsTrue(sessionToken2.IsValid(sessionToken1));

        }

        [TestMethod]
        [Owner("amudumba")]
        public void ValidateSessionTokenMergeDuringFalseProgress()
        {

            //Session token 1 with VC Version #1 and Region 1 at GLSN 105. Region 1 fails while only GLSN 0-100 only propogate to Region 2.
            Assert.IsTrue(VectorSessionToken.TryCreate("1#105#1=20#2=5#3=30", out ISessionToken sessionToken1));

            //PPAF failover to region 2 happens and Backend is now at VC Version #2 and GLSN 100.
            Assert.IsTrue(VectorSessionToken.TryCreate("2#100#4=10#2=5#3=30", out ISessionToken sessionToken2));

            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsTrue(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(VectorSessionToken.TryCreate("2#100#2=5#3=30#4=10", out ISessionToken sessionTokenMerged));

            //User presents VC#1 GLSN 100 i.e. False Progress. SDK merges backend VC#2 and incoming VC#1 while taking VC#2 GLSN as final.
            Assert.IsTrue(sessionTokenMerged.Equals(sessionToken1.Merge(sessionToken2)));
        }

        [TestMethod]
        [Owner("amudumba")]
        public void ValidateSessionTokenMergeDuringFalseProgressButFeatureDisabled()
        {

            this.falseProgressMergeDisabledField.SetValue(null, true);


            //Session token 1 with VC Version #1 and Region 1 at GLSN 105. Region 1 fails while only GLSN 0-100 only propogate to Region 2.
            Assert.IsTrue(VectorSessionToken.TryCreate("1#105#1=20#2=5#3=30", out ISessionToken sessionToken1));

            //PPAF failover to region 2 happens and Backend is now at VC Version #2 and GLSN 100.
            Assert.IsTrue(VectorSessionToken.TryCreate("2#100#4=10#2=5#3=30", out ISessionToken sessionToken2));

            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsFalse(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(VectorSessionToken.TryCreate("2#105#2=5#3=30#4=10", out ISessionToken sessionTokenMerged));

            //User presents VC#1 GLSN 100 i.e. False Progress. SDK merges backend VC#2 and incoming VC#1 while taking VC#2 GLSN as final.
            Assert.IsTrue(sessionTokenMerged.Equals(sessionToken1.Merge(sessionToken2)));
        }
    }
}