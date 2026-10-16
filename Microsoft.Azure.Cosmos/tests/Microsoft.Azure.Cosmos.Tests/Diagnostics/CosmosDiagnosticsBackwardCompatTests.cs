//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Backward-compatibility tests for the Hedging Detection API additions to
    /// <see cref="CosmosDiagnostics"/>. Covers AC9: legacy subclasses that predate
    /// the new virtual methods must continue to work without throwing.
    /// </summary>
    [TestClass]
    public class CosmosDiagnosticsBackwardCompatTests
    {
        private sealed class LegacyCosmosDiagnostics : CosmosDiagnostics
        {
            // Intentionally does NOT override any of the new virtual methods, to
            // simulate a customer subclass written before they existed.
            public override string ToString() => "legacy";
            public override TimeSpan GetClientElapsedTime() => TimeSpan.Zero;
            public override IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions() =>
                Array.Empty<(string, Uri)>();
        }

        [TestMethod]
        public void HedgingStarted_DefaultsToFalse_OnLegacySubclass()
        {
            CosmosDiagnostics d = new LegacyCosmosDiagnostics();
            Assert.IsFalse(d.HedgingStarted());
        }

        [TestMethod]
        public void GetRequestedRegions_DefaultsToEmpty_OnLegacySubclass()
        {
            CosmosDiagnostics d = new LegacyCosmosDiagnostics();
            IReadOnlyList<RequestedRegion> regions = d.GetRequestedRegions();
            Assert.IsNotNull(regions);
            Assert.AreEqual(0, regions.Count);
        }

        [TestMethod]
        public void GetRespondedRegions_DefaultsToEmpty_OnLegacySubclass()
        {
            CosmosDiagnostics d = new LegacyCosmosDiagnostics();
            IReadOnlyList<string> regions = d.GetRespondedRegions();
            Assert.IsNotNull(regions);
            Assert.AreEqual(0, regions.Count);
        }
    }
}
