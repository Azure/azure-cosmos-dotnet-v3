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
    [TestCategory("ChangeFeed")]
    public class HealthinessMonitorTests
    {
        [TestMethod]
        public async Task AcquireLease_ShouldReportHealthy_IfNoIssues()
        {
            Mock<HealthMonitor> monitor = new Mock<HealthMonitor>();
            HealthMonitoringPartitionControllerDecorator sut = new HealthMonitoringPartitionControllerDecorator(Mock.Of<PartitionController>(), monitor.Object);
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>();
            await sut.AddOrUpdateLeaseAsync(lease);

            monitor.Verify(m => m.InspectAsync(It.Is<HealthMonitoringRecord>(r => r.Severity == HealthSeverity.Informational && r.Lease == lease && r.Operation == MonitoredOperation.AcquireLease && r.Exception == null)));
        }

        [TestMethod]
        public async Task AcquireLease_ShouldReportFailure_IfSystemIssue()
        {
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>();
            Mock<HealthMonitor> monitor = new Mock<HealthMonitor>();
            Mock<PartitionController> controller = new Mock<PartitionController>();

            Exception exception = new InvalidOperationException();
            controller
                .Setup(c => c.AddOrUpdateLeaseAsync(lease))
                .Returns(Task.FromException(exception));

            HealthMonitoringPartitionControllerDecorator sut = new HealthMonitoringPartitionControllerDecorator(controller.Object, monitor.Object);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => sut.AddOrUpdateLeaseAsync(lease));

            monitor.Verify(m => m.InspectAsync(It.Is<HealthMonitoringRecord>(r => r.Severity == HealthSeverity.Error && r.Lease == lease && r.Operation == MonitoredOperation.AcquireLease && r.Exception == exception)));
        }
    }
}
