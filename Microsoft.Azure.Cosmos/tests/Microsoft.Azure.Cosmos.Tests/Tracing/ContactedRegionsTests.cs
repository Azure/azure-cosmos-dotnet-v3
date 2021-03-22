namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ContactedRegionsTests
    {
        [TestMethod]
        public void ContactedRegionsTest()
        {
            CosmosDiagnostics diagnostics = new CosmosTraceDiagnostics(this.CreateTestTraceTree());
            IReadOnlyList<(string, Uri)> regionsContacted = diagnostics.GetContactedRegions();
            Assert.IsNotNull(regionsContacted);
            Assert.AreEqual(regionsContacted.Count, 4);
        }

        private ITrace CreateTestTraceTree()
        {
            ITrace trace;
            using (trace  = Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, TraceLevel.Info))
            {
                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    using (ITrace secondLevel = trace.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                    {
                        using (ITrace thirdLevel = trace.StartChild("Third level Node", TraceComponent.Unknown, TraceLevel.Info))
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
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);
            Uri uri1 = new Uri("http://someUri1.com");
            datum.RegionsContactedWithName.Add((regionName1, uri1));
            if (regionName2 != null)
            {
                Uri uri2 = new Uri("http://someUri2.com");
                datum.RegionsContactedWithName.Add((regionName2, uri2));
            }

            return datum;
        }
    }
}
