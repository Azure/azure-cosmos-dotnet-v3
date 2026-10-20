// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Trace.Tests.Unit
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Session;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="EtwTraceListener" />.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("WindowsIntegration")]
    public class EtwTraceListenerTests
    {
        private static readonly Guid EventWriteStringGuid = Guid.NewGuid();

        private static EtwTraceListener Listener;

        private static readonly TraceSource Source = new TraceSource("Your name here");

        [ClassInitialize]
        public static void CreateTraceSource(TestContext tc)
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("ETW tests require Windows.");
            }

            if (TraceEventSession.IsElevated() != true)
            {
                Assert.Inconclusive("ETW tests require an elevated Windows test process.");
            }

            EtwTraceListenerTests.Listener = new EtwTraceListener(
                EtwTraceListenerTests.EventWriteStringGuid,
                "TestEtwEventWriteString");

            // Remove default listener.
            EtwTraceListenerTests.Source.Listeners.Clear();

            SourceSwitch sourceSwitch = new SourceSwitch("ClientSwitch", "Verbose");
            sourceSwitch.Level = SourceLevels.All;
            EtwTraceListenerTests.Source.Switch = sourceSwitch;

            EtwTraceListenerTests.Source.Listeners.Add(EtwTraceListenerTests.Listener);
        }

        [ClassCleanup]
        public static void CloseTraceSource()
        {
            EtwTraceListenerTests.Source.Close();
            EtwTraceListenerTests.Listener?.Dispose();
        }

        [TestMethod]
        [Owner("soham")]
        public async Task TestMaxLengthAsync()
        {
            using (TraceEventSession session = new TraceEventSession($"etwTestSession{Guid.NewGuid()}"))
            {
                string eventData = null;
                session.Source.AllEvents += EtwHandler;

                session.EnableProvider(EtwTraceListenerTests.EventWriteStringGuid);

                void EtwHandler(TraceEvent data)
                {
                    Console.WriteLine(data);

                    eventData = data.FormattedMessage;

                    session.Stop();
                }

                EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Critical,
                    0,
                    new string('a', EtwTraceListener.MaxEtwEventLength * 5));

                Assert.AreEqual(0u, EtwTraceListenerTests.Listener.LastReturnCode);

                using (CancellationTokenSource cts = new CancellationTokenSource(5000))
                {
                    cts.Token.Register(() => session.Stop());

                    await Task.Run(() => session.Source.Process(), cts.Token);

                    Assert.IsFalse(cts.IsCancellationRequested, "Expected traces were not received before timeout.");
                }

                Assert.AreEqual(new string('a', EtwTraceListener.MaxEtwEventLength), eventData);
            }
        }

        [TestMethod]
        [Owner("soham")]
        [TestCategory("Integration")] // Bug 3692802: Need to move test(TestMaxLengthFormatAsync()) to Integration to fix managed gated pipeline
        public async Task TestMaxLengthFormatAsync()
        {
            using (TraceEventSession session = new TraceEventSession($"etwTestSession{Guid.NewGuid()}"))
            {
                string eventData = null;
                session.Source.AllEvents += EtwHandler;

                session.EnableProvider(EtwTraceListenerTests.EventWriteStringGuid);

                void EtwHandler(TraceEvent data)
                {
                    Console.WriteLine(data);

                    eventData = data.FormattedMessage;

                    session.Stop();
                }

                EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Critical,
                    0,
                    "{0},{1}",
                    new string('b', EtwTraceListener.MaxEtwEventLength * 2),
                    new string('a', EtwTraceListener.MaxEtwEventLength * 2));

                Assert.AreEqual(0u, EtwTraceListenerTests.Listener.LastReturnCode);

                using (CancellationTokenSource cts = new CancellationTokenSource(5000))
                {
                    cts.Token.Register(() => session.Stop());

                    await Task.Run(() => session.Source.Process(), cts.Token);

                    Assert.IsFalse(cts.IsCancellationRequested, "Expected traces were not received before timeout.");
                }

                Assert.AreEqual(new string('b', EtwTraceListener.MaxEtwEventLength), eventData);
            }
        }

        [TestMethod]
        [Owner("soham")]
        public async Task TestFormatAsync()
        {
            using (TraceEventSession session = new TraceEventSession($"etwTestSession{Guid.NewGuid()}"))
            {
                string eventContents = null;
                session.Source.AllEvents += EtwHandler;

                session.EnableProvider(EtwTraceListenerTests.EventWriteStringGuid);

                void EtwHandler(TraceEvent data)
                {
                    // Remove \0.
                    eventContents = Encoding.Unicode.GetString(data.EventData(), 0, data.EventDataLength - sizeof(char));
                    Assert.AreEqual(data.Level, TraceEventLevel.Error);

                    session.Stop();
                }

                StackFrame stackFrame = new StackFrame();

                EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Error,
                    0,
                    "I'm formatted {0} {1} {2} {3} {4}",
                    1,
                    1.15f,
                    SourceLevels.Critical,
                    "Hi formatted I'm dad",
                    stackFrame);

                Assert.AreEqual(0u, EtwTraceListenerTests.Listener.LastReturnCode);

                using (CancellationTokenSource cts = new CancellationTokenSource(5000))
                {
                    cts.Token.Register(() => session.Stop());

                    await Task.Run(() => session.Source.Process(), cts.Token);

                    Assert.IsFalse(cts.IsCancellationRequested, "Expected traces were not received before timeout.");
                }

                string expectedResult = string.Format(
                    "I'm formatted {0} {1} {2} {3} {4}",
                    1,
                    1.15f,
                    SourceLevels.Critical,
                    "Hi formatted I'm dad",
                    stackFrame);

                Assert.AreEqual(expectedResult, eventContents);
            }
        }
    }
}