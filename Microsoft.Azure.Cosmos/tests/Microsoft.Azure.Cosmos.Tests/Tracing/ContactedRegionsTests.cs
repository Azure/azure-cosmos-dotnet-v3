namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ContactedRegionsTests
    {
        [TestMethod]
        public void ContactedRegionsWithNameTest()
        {
            CosmosDiagnostics diagnostics = new CosmosTraceDiagnostics(this.CreateTestTraceTree());
            IReadOnlyList<(string, Uri)> regionsContacted = diagnostics.GetContactedRegions();
            Assert.IsNotNull(regionsContacted);
            Assert.AreEqual(regionsContacted.Count, 4);
        }

        private ITrace CreateTestTraceTree()
        {
            ITrace trace;
            using (trace = Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, TraceLevel.Info))
            {
                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    using (ITrace secondLevel = firstLevel.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                    {
                        using (ITrace thirdLevel = secondLevel.StartChild("Third level Node", TraceComponent.Unknown, TraceLevel.Info))
                        {
                            thirdLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralUS));
                        }
                    }

                    using (ITrace secondLevel = trace.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                    {
                        secondLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralIndia, Regions.EastUS2));
                    }
                }

                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    firstLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.FranceCentral));
                }
            }

            return trace;
        }

        private TraceDatum GetDatumObject(string regionName1, string regionName2 = null)
        {
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Trace.GetRootTrace(nameof(ContactedRegionsTests)));
            Uri uri1 = new Uri("http://someUri1.com");
            datum.RegionsContacted.Add((regionName1, uri1));
            if (regionName2 != null)
            {
                Uri uri2 = new Uri("http://someUri2.com");
                datum.RegionsContacted.Add((regionName2, uri2));
            }

            return datum;
        }

        [TestMethod]
        public void ContactedRegionsWithNameForClientTelemetryTest()
        {
            CosmosDiagnostics diagnostics = new CosmosTraceDiagnostics(this.CreateTestTraceTree());

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(diagnostics.GetContactedRegions());
            Assert.IsNotNull(regionsContacted);
            Assert.AreEqual("Central US,Central India,East US 2,France Central", regionsContacted);

        }

        [TestMethod]
        public void ContactedRegionWithNameForClientTelemetryTest()
        {
            Trace trace;
            using (trace = Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, TraceLevel.Info))
            {
                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    firstLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.FranceCentral));
                }
            }

            CosmosDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(diagnostics.GetContactedRegions());
            Assert.IsNotNull(regionsContacted);
            Assert.AreEqual("France Central", regionsContacted);
        }

    }
}