// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Trace.Tests.Unit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
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
        public void TestMaxLength()
        {
            TraceAndValidate(
                () => EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Critical,
                    0,
                    new string('a', EtwTraceListener.MaxEtwEventLength * 5)),
                data => Assert.AreEqual(new string('a', EtwTraceListener.MaxEtwEventLength), data.FormattedMessage));
        }

        [TestMethod]
        [Owner("soham")]
        [TestCategory("Integration")]
        public void TestMaxLengthFormat()
        {
            TraceAndValidate(
                () => EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Critical,
                    0,
                    "{0},{1}",
                    new string('b', EtwTraceListener.MaxEtwEventLength * 2),
                    new string('a', EtwTraceListener.MaxEtwEventLength * 2)),
                data => Assert.AreEqual(new string('b', EtwTraceListener.MaxEtwEventLength), data.FormattedMessage));
        }

        [TestMethod]
        [Owner("soham")]
        public void TestFormat()
        {
            StackFrame stackFrame = new StackFrame();
            string expectedResult = string.Format(
                "I'm formatted {0} {1} {2} {3} {4}",
                1,
                1.15f,
                SourceLevels.Critical,
                "Hi formatted I'm dad",
                stackFrame);

            TraceAndValidate(
                () => EtwTraceListenerTests.Source.TraceEvent(
                    TraceEventType.Error,
                    0,
                    "I'm formatted {0} {1} {2} {3} {4}",
                    1,
                    1.15f,
                    SourceLevels.Critical,
                    "Hi formatted I'm dad",
                    stackFrame),
                data =>
                {
                    // Remove \0.
                    string eventContents = Encoding.Unicode.GetString(data.EventData(), 0, data.EventDataLength - sizeof(char));
                    Assert.AreEqual(TraceEventLevel.Error, data.Level);
                    Assert.AreEqual(expectedResult, eventContents);
                });
        }

        private static void TraceAndValidate(Action trace, Action<TraceEvent> validate)
        {
            string sessionName = $"etwTestSession{Guid.NewGuid():N}";
            string traceFile = Path.Combine(Path.GetTempPath(), $"{sessionName}.etl");

            try
            {
                using (TraceEventSession session = new TraceEventSession(sessionName, traceFile))
                {
                    session.EnableProvider(EtwTraceListenerTests.EventWriteStringGuid);

                    trace();
                    Assert.AreEqual(0u, EtwTraceListenerTests.Listener.LastReturnCode);

                    // Stopping flushes the capture before it is read, without a real-time delivery deadline.
                    session.Stop();
                }

                int eventCount = 0;
                using (ETWTraceEventSource source = new ETWTraceEventSource(traceFile))
                {
                    source.AllEvents += data =>
                    {
                        if (data.ProviderGuid == EtwTraceListenerTests.EventWriteStringGuid)
                        {
                            validate(data);
                            eventCount++;
                        }
                    };

                    source.Process();
                }

                Assert.AreEqual(1, eventCount, "Expected exactly one trace from the test provider.");
            }
            finally
            {
                File.Delete(traceFile);
            }
        }
    }
}