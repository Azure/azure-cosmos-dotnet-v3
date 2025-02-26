//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Timers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class TimerWheelTimerCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TimeSpanZero()
        {
            _ = new TimerWheelTimerCore(TimeSpan.Zero, Mock.Of<TimerWheel>());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullTimerWheel()
        {
            _ = new TimerWheelTimerCore(TimeSpan.FromMilliseconds(1), null);
        }

        [TestMethod]
        public void TimeoutMatchesInput()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(1);
            TimerWheelTimerCore timer = new TimerWheelTimerCore(timeout, Mock.Of<TimerWheel>());
            Assert.AreEqual(timeout, timer.Timeout);
        }

        [TestMethod]
        public void SubscribesToWheel()
        {
            Mock<TimerWheel> mockedWheel = new Mock<TimerWheel>();
            TimerWheelTimerCore timer = new TimerWheelTimerCore(TimeSpan.FromMilliseconds(10), mockedWheel.Object);
            Task task = timer.StartTimerAsync();
            Mock.Get(mockedWheel.Object)
                .Verify(w => w.SubscribeForTimeouts(It.Is<TimerWheelTimer>(t => t == timer)), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task CannotStartTwice()
        {
            Mock<TimerWheel> mockedWheel = new Mock<TimerWheel>();
            TimerWheelTimerCore timer = new TimerWheelTimerCore(TimeSpan.FromMilliseconds(10), mockedWheel.Object);
            List<Task> tasks = new List<Task>()
            {
                timer.StartTimerAsync(),
                timer.StartTimerAsync()
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public void FireTimeout()
        {
            Mock<TimerWheel> mockedWheel = new Mock<TimerWheel>();
            TimerWheelTimerCore timer = new TimerWheelTimerCore(TimeSpan.FromMilliseconds(10), mockedWheel.Object);
            Task task = timer.StartTimerAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
            timer.FireTimeout();
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public void Cancel()
        {
            Mock<TimerWheel> mockedWheel = new Mock<TimerWheel>();
            TimerWheelTimerCore timer = new TimerWheelTimerCore(TimeSpan.FromMilliseconds(10), mockedWheel.Object);
            Task task = timer.StartTimerAsync();
            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
            timer.CancelTimer();
            Assert.AreEqual(TaskStatus.Canceled, task.Status);
        }
    }
}