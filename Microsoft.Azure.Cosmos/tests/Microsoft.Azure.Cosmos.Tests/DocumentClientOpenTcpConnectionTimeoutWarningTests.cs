//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Pins the behavior contracted by the follow-up to PR #5873: the
    /// negative-<see cref="CosmosClientOptions.OpenTcpConnectionTimeout"/>
    /// warning trace is emitted by <c>DocumentClient</c> at client construction
    /// (the consumer of the value), not by the
    /// <c>CosmosClientOptions.OpenTcpConnectionTimeout</c> property setter.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DocumentClientOpenTcpConnectionTimeoutWarningTests
    {
        private const string AccountEndpoint = "https://localhost:8081/";
        private const string WarningSubstring = "OpenTcpConnectionTimeout";

        private CapturingTraceListener listener;
        private SourceLevels originalLevel;

        [TestInitialize]
        public void TestInitialize()
        {
            this.listener = new CapturingTraceListener();
            DefaultTrace.TraceSource.Listeners.Add(this.listener);
            this.originalLevel = DefaultTrace.TraceSource.Switch.Level;
            DefaultTrace.TraceSource.Switch.Level = SourceLevels.Warning;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.listener != null)
            {
                DefaultTrace.TraceSource.Listeners.Remove(this.listener);
                this.listener.Dispose();
                this.listener = null;
            }

            DefaultTrace.TraceSource.Switch.Level = this.originalLevel;
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutNegativeEmitsWarningFromDocumentClient()
        {
            this.BuildClientWithOpenTcpConnectionTimeout(TimeSpan.FromSeconds(-30));

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                1,
                warnings.Count,
                "DocumentClient should emit exactly one OpenTcpConnectionTimeout warning per construction with a negative value.");
            StringAssert.Contains(warnings[0].Message, "negative");
            StringAssert.Contains(warnings[0].Message, TimeSpan.FromSeconds(-30).ToString());
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutNegativeIsTruncatedToWholeSecondsAndEmitsWarning()
        {
            CosmosClient cosmosClient = this.BuildClientWithOpenTcpConnectionTimeout(TimeSpan.FromSeconds(-5.7));

            Microsoft.Azure.Cosmos.Tracing.TraceData.RntbdConnectionConfig tcpConfig =
                cosmosClient.ClientConfigurationTraceDatum.RntbdConnectionConfig;

            Assert.AreEqual(
                -5,
                tcpConfig.ConnectionTimeout,
                "Negative OpenTcpConnectionTimeout values are truncated via (int)TotalSeconds at the transport boundary.");

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                1,
                warnings.Count,
                "Truncation and warning emission must remain coupled to the negative branch.");
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutSettingNegativeOnOptionsDoesNotEmitWarning()
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            options.OpenTcpConnectionTimeout = TimeSpan.FromMilliseconds(-1);
            options.OpenTcpConnectionTimeout = TimeSpan.FromSeconds(-30);

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                0,
                warnings.Count,
                "Assigning a negative TimeSpan to CosmosClientOptions.OpenTcpConnectionTimeout must not emit a warning; the consumer (DocumentClient) owns that trace.");
        }

        [DataTestMethod]
        [DataRow(0L)]
        [DataRow(500L)]
        [DataRow(1000L)]
        [DataRow(2500L)]
        [DataRow(7000L)]
        public void OpenTcpConnectionTimeoutNonNegativeDoesNotEmitWarningFromDocumentClient(long milliseconds)
        {
            this.BuildClientWithOpenTcpConnectionTimeout(TimeSpan.FromMilliseconds(milliseconds));

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                0,
                warnings.Count,
                $"Non-negative OpenTcpConnectionTimeout ({milliseconds} ms) must not produce the negative-value warning.");
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutWarningEmittedOncePerDocumentClientConstruction()
        {
            this.BuildClientWithOpenTcpConnectionTimeout(TimeSpan.FromSeconds(-1));
            this.BuildClientWithOpenTcpConnectionTimeout(TimeSpan.FromSeconds(-2));

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                2,
                warnings.Count,
                "Two DocumentClient constructions with negative timeouts should emit exactly two warnings (once per construction, not once per setter call).");
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutNegativeInGatewayModeDoesNotEmitWarning()
        {
            // Legacy path: a caller constructs a DocumentClient directly with a raw ConnectionPolicy
            // whose ConnectionMode is Gateway. openConnectionTimeoutInSeconds is not consumed in
            // Gateway mode, so the "fall back to RequestTimeout" message would be misleading and the
            // warning is intentionally suppressed.
            ConnectionPolicy policy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                OpenTcpConnectionTimeout = TimeSpan.FromSeconds(-30),
            };

            this.BuildClientFromConnectionPolicy(policy);

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                0,
                warnings.Count,
                "Negative OpenTcpConnectionTimeout in Gateway mode must not emit the Direct-only warning.");
        }

        [TestMethod]
        public void OpenTcpConnectionTimeoutNegativeThenOverwrittenWithNonNegativeDoesNotEmitWarning()
        {
            // Documents the timing-shift improvement over the original setter-side approach:
            // assigning a negative TimeSpan and later overwriting it with a non-negative value
            // before constructing the DocumentClient must produce zero warnings, because the
            // warning is now driven by the constructed value (not the transient setter value).
            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                OpenTcpConnectionTimeout = TimeSpan.FromSeconds(-30),
            };
            options.OpenTcpConnectionTimeout = TimeSpan.FromSeconds(5);

            ConnectionPolicy policy = options.GetConnectionPolicy(clientId: 0);
            this.BuildClientFromConnectionPolicy(policy);

            IReadOnlyList<CapturedTraceEvent> warnings = this.listener.SnapshotWarnings(WarningSubstring);
            Assert.AreEqual(
                0,
                warnings.Count,
                "Overwriting a transiently negative OpenTcpConnectionTimeout with a non-negative value before construction must not produce the negative-value warning.");
        }

        private CosmosClient BuildClientWithOpenTcpConnectionTimeout(TimeSpan openTcpConnectionTimeout)
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                OpenTcpConnectionTimeout = openTcpConnectionTimeout,
            };

            ConnectionPolicy policy = options.GetConnectionPolicy(clientId: 0);
            return this.BuildClientFromConnectionPolicy(policy);
        }

        private CosmosClient BuildClientFromConnectionPolicy(ConnectionPolicy policy)
        {
            CosmosClientBuilder builder = new CosmosClientBuilder(
                accountEndpoint: AccountEndpoint,
                authKeyOrResourceToken: MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey);

            return builder.Build(new MockDocumentClient(connectionPolicy: policy));
        }

        private sealed class CapturedTraceEvent
        {
            public CapturedTraceEvent(TraceEventType eventType, string message)
            {
                this.EventType = eventType;
                this.Message = message;
            }

            public TraceEventType EventType { get; }

            public string Message { get; }
        }

        private sealed class CapturingTraceListener : TraceListener
        {
            private readonly object syncRoot = new object();
            private readonly List<CapturedTraceEvent> events = new List<CapturedTraceEvent>();

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
            {
                this.Capture(eventType, message);
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
            {
                string message = (args == null || args.Length == 0)
                    ? format
                    : string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
                this.Capture(eventType, message);
            }

            public override void Write(string message)
            {
                // No-op: per-event capture is sufficient.
            }

            public override void WriteLine(string message)
            {
                // No-op: per-event capture is sufficient.
            }

            public IReadOnlyList<CapturedTraceEvent> SnapshotWarnings(string messageSubstring)
            {
                lock (this.syncRoot)
                {
                    List<CapturedTraceEvent> matches = new List<CapturedTraceEvent>();
                    foreach (CapturedTraceEvent captured in this.events)
                    {
                        if (captured.EventType == TraceEventType.Warning
                            && captured.Message != null
                            && captured.Message.IndexOf(messageSubstring, StringComparison.Ordinal) >= 0)
                        {
                            matches.Add(captured);
                        }
                    }

                    return matches;
                }
            }

            private void Capture(TraceEventType eventType, string message)
            {
                lock (this.syncRoot)
                {
                    this.events.Add(new CapturedTraceEvent(eventType, message));
                }
            }
        }
    }
}
