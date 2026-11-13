//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Friends.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestClass]
    public sealed class ValueStopwatchTests
    {
        [TestMethod]
        [Owner("kmontrose")]
        public void StartStop()
        {
            // in a valid but unstarted state
            ValueStopwatch stopwatch = default;
            Assert.IsFalse(stopwatch.IsRunning);
            Assert.AreEqual(0, stopwatch.ElapsedTicks);

            // start the stopwatch
            stopwatch.Start();
            Assert.IsTrue(stopwatch.IsRunning);

            // some number of ticks must have happened
            Thread.Sleep(10);
            long firstTicks = stopwatch.ElapsedTicks;
            Assert.IsTrue(firstTicks > 0);

            // and now more ticks must have happened
            Thread.Sleep(10);
            long secondTicks = stopwatch.ElapsedTicks;
            Assert.IsTrue(secondTicks > firstTicks);

            // we stop, but the elapsed time remains > 0
            stopwatch.Stop();
            Assert.IsFalse(stopwatch.IsRunning);
            long thirdTicks = stopwatch.ElapsedTicks;
            Assert.IsTrue(thirdTicks > 0);

            // we've remained stopped, so elapsed time is unchanged
            Thread.Sleep(10);
            long fourthTicks = stopwatch.ElapsedTicks;
            Assert.AreEqual(thirdTicks, fourthTicks);

            // we start again, time starts passing again
            stopwatch.Start();
            Assert.IsTrue(stopwatch.IsRunning);
            Thread.Sleep(10);
            long fifthTicks = stopwatch.ElapsedTicks;
            Assert.IsTrue(fifthTicks > fourthTicks);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void StartNew()
        {
            // is returned started
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            Assert.IsTrue(stopwatch.IsRunning);

            // and we get valid date out of it
            Thread.Sleep(10);
            stopwatch.Stop();
            Assert.IsFalse(stopwatch.IsRunning);
            Assert.AreNotEqual(0, stopwatch.ElapsedTicks);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void Reset()
        {
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();

            Thread.Sleep(10);
            Assert.AreNotEqual(0, stopwatch.ElapsedTicks);

            // reset should clear everything
            stopwatch.Reset();
            Assert.IsFalse(stopwatch.IsRunning);
            Assert.AreEqual(0, stopwatch.ElapsedTicks);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void Restart()
        {
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();

            Thread.Sleep(10);
            long afterLongSleep = stopwatch.ElapsedTicks;
            Assert.AreNotEqual(0, afterLongSleep);

            // reset should clear elapsed time, but we remain running
            stopwatch.Restart();
            Assert.IsTrue(stopwatch.IsRunning);

            // we've reset the duration, but it's still advancing
            Thread.Sleep(0);
            long afterReset = stopwatch.ElapsedTicks;
            Assert.AreNotEqual(0, afterReset);
            Assert.IsTrue(afterReset < afterLongSleep);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void GetTimestamp()
        {
            // pass through is trivial, but breaking this would be bad... so trivial test
            long fromStopwatch = Stopwatch.GetTimestamp();
            long fromValueStopwatch = ValueStopwatch.GetTimestamp();

            Assert.IsTrue(fromValueStopwatch >= fromStopwatch);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void Frequency()
        {
            // pass through is trivial, but breaking this would be bad... so trivial test
            Assert.AreEqual(Stopwatch.Frequency, ValueStopwatch.Frequency);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void IsHighResolution()
        {
            // pass through is trivial, but breaking this would be bad... so trivial test
            Assert.AreEqual(Stopwatch.IsHighResolution, ValueStopwatch.IsHighResolution);
        }

        [TestMethod]
        [Owner("kmontrose")]
        public void Elapsed()
        {
            Stopwatch bclStopwatch = Stopwatch.StartNew();
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            Thread.Sleep(10);
            stopwatch.Stop();
            bclStopwatch.Stop();

            long asTicks = stopwatch.ElapsedTicks;
            long asMS = stopwatch.ElapsedMilliseconds;
            TimeSpan asTimeSpan = stopwatch.Elapsed;

            // Sleep(10) is imprecise, this checks that we got something realistic by
            // comparing to the BCL's Stopwatch
            //
            // we can't directly compare ElapsedXXX properties because there's no
            // guarantee the StartNew()s and Stop()s don't have arbitrary time between
            // them but we can use the guaranteed ordering to check for realistic
            // outcomes
            Assert.IsTrue(asMS > 0 && asMS <= bclStopwatch.ElapsedMilliseconds);

            // should always be within on millisecond (after rouding)
            double msDelta = Math.Abs(asTimeSpan.TotalMilliseconds - (double)asMS);
            Assert.IsTrue(msDelta <= 1);

            // likewise, but using ticks instead of milliseconds as the starting point
            double stopwatchTicksToMS = ((double)asTicks) / (ValueStopwatch.Frequency / 1_000);
            double ticksDeltaMs = Math.Abs(asTimeSpan.TotalMilliseconds - stopwatchTicksToMS);
            Assert.IsTrue(ticksDeltaMs <= 1);
        }
    }
}
