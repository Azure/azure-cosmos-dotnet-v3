//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for <see cref="TransportRequestStats"/> class.
    /// </summary>
    [TestClass]
    public class TransportRequestStatsTests
    {
        [TestMethod]
        [Owner("askagarw")]
        public void RequestTimelineTest()
        {
            TransportRequestStats requestStats = new TransportRequestStats();
            Assert.AreEqual(requestStats.CurrentStage, TransportRequestStats.RequestStage.Created);

            requestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);
            Assert.AreEqual(requestStats.CurrentStage, TransportRequestStats.RequestStage.ChannelAcquisitionStarted);

            requestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);
            requestStats.RecordState(TransportRequestStats.RequestStage.Sent);
            requestStats.RecordState(TransportRequestStats.RequestStage.Received);
            requestStats.RecordState(TransportRequestStats.RequestStage.Completed);

            Assert.AreEqual(requestStats.CurrentStage, TransportRequestStats.RequestStage.Completed);

            requestStats.RequestBodySizeInBytes = 42;
            requestStats.RequestSizeInBytes = 4200;
            requestStats.ResponseMetadataSizeInBytes = 9000;
            requestStats.ResponseBodySizeInBytes = 9999;

            TransportRequestStatsTests.ValidateRequestStatList(
                requestStats: requestStats,
                expectedEventCount: 6,
                minimumDuration: TimeSpan.Zero);

            // FAILED STATE TEST
            requestStats = new TransportRequestStats();
            requestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);
            requestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);
            requestStats.RecordState(TransportRequestStats.RequestStage.Failed);

            Assert.AreEqual(requestStats.CurrentStage, TransportRequestStats.RequestStage.Failed);
            TransportRequestStatsTests.ValidateRequestStatList(
                requestStats: requestStats,
                expectedEventCount: 4,
                minimumDuration: TimeSpan.Zero);
        }

        [TestMethod]
        [Owner("askagarw")]
        public void TimelineDurationTest()
        {
            List<TransportRequestStats.RequestStage> stages = new List<TransportRequestStats.RequestStage>()
            {
                TransportRequestStats.RequestStage.ChannelAcquisitionStarted,
                TransportRequestStats.RequestStage.Pipelined,
                TransportRequestStats.RequestStage.Sent,
                TransportRequestStats.RequestStage.Received,
                TransportRequestStats.RequestStage.Completed
            };

            TransportRequestStats transportRequestStats = new TransportRequestStats();

            foreach (TransportRequestStats.RequestStage stage in stages)
            {
                Thread.Sleep(1);
                transportRequestStats.RecordState(stage);
            }

            TransportRequestStatsTests.ValidateRequestStatList(
                requestStats: transportRequestStats,
                expectedEventCount: 6,
                minimumDuration: TimeSpan.FromMilliseconds(1));

            // Negative Duration Test
            transportRequestStats = new TransportRequestStats();
            Thread.Sleep(1);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.ChannelAcquisitionStarted);
            Thread.Sleep(1);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Pipelined);
            Thread.Sleep(1);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Received); // Receive before Sent
            Thread.Sleep(1);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);
            Thread.Sleep(1);
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Completed);

            TransportRequestStatsTests.ValidateRequestStatList(
                requestStats: transportRequestStats,
                expectedEventCount: 6,
                minimumDuration: null);
        }

        private static void ValidateRequestStatList(
            TransportRequestStats requestStats,
            int expectedEventCount,
            TimeSpan? minimumDuration = null)
        {
            string jsonEvents = requestStats.ToString();
            JObject jObject = JObject.Parse(jsonEvents);

            if (requestStats.ResponseBodySizeInBytes.HasValue)
            {
                Assert.AreEqual(jObject["responseBodySizeInBytes"].ToObject<long>(), requestStats.ResponseBodySizeInBytes.Value);
            }

            if (requestStats.ResponseMetadataSizeInBytes.HasValue)
            {
                Assert.AreEqual(jObject["responseMetadataSizeInBytes"].ToObject<long>(), requestStats.ResponseMetadataSizeInBytes.Value);
            }

            if (requestStats.RequestSizeInBytes.HasValue)
            {
                Assert.AreEqual(jObject["requestSizeInBytes"].ToObject<long>(), requestStats.RequestSizeInBytes.Value);
            }

            if (requestStats.RequestBodySizeInBytes.HasValue)
            {
                Assert.AreEqual(jObject["requestBodySizeInBytes"].ToObject<long>(), requestStats.RequestBodySizeInBytes.Value);
            }

            TransportRequestStatsFromJson transportRequestStats = JsonConvert.DeserializeObject<TransportRequestStatsFromJson>(jsonEvents);
            Assert.AreEqual(expectedEventCount, transportRequestStats.RequestTimeline.Count);

            string previousEventName = null;
            DateTime? previousEventDateTime = null;
            foreach (TransportRequestStatsEvent evt in transportRequestStats.RequestTimeline)
            {
                string currentEventName = evt.EventName;
                Assert.IsNotNull(currentEventName);

                // We deserialize this information to a JSON.NET JRaw instance in order to 
                // avoid JSON.NET's DateTime deserialization behavior, which by default will
                // round values to the per-second level. In our cases, all tests typically
                // run within 1000 ms, so rounding off in this way does not help us
                // validate that events were ordered properly, by startTimeUtc value.
                string currentEventStartTime = evt.StartTimeUtcRaw.ToString().Trim('"');
                Assert.IsNotNull(currentEventStartTime);
                DateTime currentDateTime = DateTime.Parse(currentEventStartTime);
                if (minimumDuration.HasValue && previousEventDateTime.HasValue)
                {
                    Assert.IsTrue(
                        currentDateTime >= previousEventDateTime.Value,
                        $"'{currentEventName}' event start time observed ({currentDateTime}) precedes " +
                        $"last observed '{previousEventName}' event start time ({previousEventDateTime}).");
                }
                previousEventName = currentEventName;
                previousEventDateTime = currentDateTime;

                double durationInMs = evt.DurationInMs;
                if (minimumDuration.HasValue && currentEventName.ToString() != "Completed")
                {
                    Assert.IsTrue(durationInMs >= 0, $"Event:{currentEventName}, Duration: {durationInMs}");
                    Assert.IsTrue(durationInMs >= minimumDuration.Value.TotalMilliseconds, $"{durationInMs} >= {minimumDuration}");
                }
            }
        }

        private sealed class TransportRequestStatsFromJson
        {
            [JsonProperty("requestTimeline")]
            public List<TransportRequestStatsEvent> RequestTimeline { get; set; }
        }

        private sealed class TransportRequestStatsEvent
        {
            [JsonProperty("event")]
            public string EventName { get; set; }

            [JsonProperty("startTimeUtc")]
            public JRaw StartTimeUtcRaw { get; set; }

            [JsonProperty("durationInMs")]
            public double DurationInMs { get; set; }
        }
    }
}