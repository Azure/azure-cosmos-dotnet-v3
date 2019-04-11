//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Monitoring;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class HealthinessMonitorTests
    {
        [TestMethod]
        public async Task AcquireLease_ShouldReportHealthy_IfNoIssues()
        {
            var monitor = new Mock<HealthMonitor>();
            var sut = new HealthMonitoringPartitionControllerDecorator(Mock.Of<PartitionController>(), monitor.Object);
            var lease = Mock.Of<DocumentServiceLease>();
            await sut.AddOrUpdateLeaseAsync(lease);

            monitor.Verify(m => m.InspectAsync(It.Is<HealthMonitoringRecord>(r => r.Severity == HealthSeverity.Informational && r.Lease == lease && r.Operation == MonitoredOperation.AcquireLease && r.Exception == null)));
        }

        [TestMethod]
        public async Task AcquireLease_ShouldReportFailure_IfSystemIssue()
        {
            var lease = Mock.Of<DocumentServiceLease>();
            var monitor = new Mock<HealthMonitor>();
            var controller = new Mock<PartitionController>();

            Exception exception = new InvalidOperationException();
            controller
                .Setup(c => c.AddOrUpdateLeaseAsync(lease))
                .Returns(Task.FromException(exception));

            var sut = new HealthMonitoringPartitionControllerDecorator(controller.Object, monitor.Object);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => sut.AddOrUpdateLeaseAsync(lease));

            monitor.Verify(m => m.InspectAsync(It.Is<HealthMonitoringRecord>(r => r.Severity == HealthSeverity.Error && r.Lease == lease && r.Operation == MonitoredOperation.AcquireLease && r.Exception == exception)));
        }
    }
}
