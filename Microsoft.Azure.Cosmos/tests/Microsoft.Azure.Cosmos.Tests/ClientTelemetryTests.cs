//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HdrHistogram;
    using System.Net.Http;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Telemetry;

    /// <summary>
    /// Tests for <see cref="ClientTelemetry"/>.
    /// </summary>
    [TestClass]
    public class ClientTelemetryTests
    {
        [TestMethod]
        public async Task ParseAzureVMMetadataTest()
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

            // Add a substatus code that is not part of the enum. 
            // This ensures that if the backend adds a enum the status code is not lost.
            object jsonObject = JsonConvert.DeserializeObject("{\"compute\":{\"azEnvironment\":\"AzurePublicCloud\",\"customData\":\"\",\"isHostCompatibilityLayerVm\":\"false\",\"licenseType\":\"\",\"location\":\"eastus\",\"name\":\"sourabh-testing\",\"offer\":\"UbuntuServer\",\"osProfile\":{\"adminUsername\":\"azureuser\",\"computerName\":\"sourabh-testing\"},\"osType\":\"Linux\",\"placementGroupId\":\"\",\"plan\":{\"name\":\"\",\"product\":\"\",\"publisher\":\"\"},\"platformFaultDomain\":\"0\",\"platformUpdateDomain\":\"0\",\"provider\":\"Microsoft.Compute\",\"publicKeys\":[{\"keyData\":\"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC5uCeOAm3ehmhI+2PbMoMl17Eo\r\nqfHKCycSaBJsv9qxlmBOuFheSJc1XknJleXUSsuTO016/d1PyWpevnqOZNRksWoa\r\nJvQ23sDTxcK+X2OP3QlCUeX4cMjPXqlL8z1UYzU4Bx3fFvf8fs67G3N72sxWBw5P\r\nZyuXyhBm0NCe/2NYMKgEDT4ma8XszO0ikbhoPKbMbgHAQk/ktWQHNcqYOPQKEWqp\r\nEK1R0rjS2nmtovfScP/ZGXcvOpJ1/NDBo4dh1K+OxOGM/4PSH/F448J5Zy4eAyEk\r\nscys+IpeIOTOlRUy/703SNIX0LEWlnYqbyL9c1ypcYLQqF76fKkDfzzFI/OWVlGw\r\nhj/S9uP8iMsR+fhGIbn6MAa7O4DWPWLuedSp7KDYyjY09gqNJsfuaAJN4LiC6bPy\r\nhknm0PVLK3ux7EUOt+cZrHCdIFWbdOtxiPNIl1tkv9kV5aE5Aj2gJm4MeB9uXYhS\r\nOuksboBc0wyUGrl9+XZJ1+NlZOf7IjVi86CieK8= generated-by-azure\r\n\",\"path\":\"/home/azureuser/.ssh/authorized_keys\"}],\"publisher\":\"Canonical\",\"resourceGroupName\":\"sourabh-telemetry-sdk\",\"resourceId\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/virtualMachines/sourabh-testing\",\"securityProfile\":{\"secureBootEnabled\":\"false\",\"virtualTpmEnabled\":\"false\"},\"sku\":\"18.04-LTS\",\"storageProfile\":{\"dataDisks\":[],\"imageReference\":{\"id\":\"\",\"offer\":\"UbuntuServer\",\"publisher\":\"Canonical\",\"sku\":\"18.04-LTS\",\"version\":\"latest\"},\"osDisk\":{\"caching\":\"ReadWrite\",\"createOption\":\"FromImage\",\"diffDiskSettings\":{\"option\":\"\"},\"diskSizeGB\":\"30\",\"encryptionSettings\":{\"enabled\":\"false\"},\"image\":{\"uri\":\"\"},\"managedDisk\":{\"id\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/disks/sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"storageAccountType\":\"Premium_LRS\"},\"name\":\"sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"osType\":\"Linux\",\"vhd\":{\"uri\":\"\"},\"writeAcceleratorEnabled\":\"false\"}},\"subscriptionId\":\"8fba6d4f-7c37-4d13-9063-fd58ad2b86e2\",\"tags\":\"azsecpack:nonprod;platformsettings.host_environment.service.platform_optedin_for_rootcerts:true\",\"tagsList\":[{\"name\":\"azsecpack\",\"value\":\"nonprod\"},{\"name\":\"platformsettings.host_environment.service.platform_optedin_for_rootcerts\",\"value\":\"true\"}],\"version\":\"18.04.202103250\",\"vmId\":\"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd\",\"vmScaleSetName\":\"\",\"vmSize\":\"Standard_D2s_v3\",\"zone\":\"1\"},\"network\":{\"interface\":[{\"ipv4\":{\"ipAddress\":[{\"privateIpAddress\":\"10.0.7.5\",\"publicIpAddress\":\"\"}],\"subnet\":[{\"address\":\"10.0.7.0\",\"prefix\":\"24\"}]},\"ipv6\":{\"ipAddress\":[]},\"macAddress\":\"000D3A8F8BA0\"}]}}");
            string payload = JsonConvert.SerializeObject(jsonObject);
            result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            AzureVMMetadata metadata = await ClientTelemetryOptions.ProcessResponseAsync(result);

            Assert.AreEqual("eastus", metadata.Compute.Location);
            Assert.AreEqual("18.04-LTS", metadata.Compute.SKU);
            Assert.AreEqual("AzurePublicCloud", metadata.Compute.AzEnvironment);
            Assert.AreEqual("Linux", metadata.Compute.OSType);
            Assert.AreEqual("Standard_D2s_v3", metadata.Compute.VMSize);
        }

        [TestMethod]
        public void CheckMetricsAggregationLogic()
        {
            MetricInfo metrics = new MetricInfo("metricsName", "unitName");

            LongConcurrentHistogram histogram = new LongConcurrentHistogram(1,
                   Int64.MaxValue,
                   5);

            histogram.RecordValue((long)10);
            histogram.RecordValue((long)20);
            histogram.RecordValue((long)30);
            histogram.RecordValue((long)40);

            metrics.SetAggregators(histogram);

            Assert.AreEqual(40, metrics.Max);
            Assert.AreEqual(10, metrics.Min);
            Assert.AreEqual(4, metrics.Count);
            Assert.AreEqual(25, metrics.Mean);

            Assert.AreEqual(20, metrics.Percentiles[ClientTelemetryOptions.Percentile50]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile90]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile95]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile99]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile999]);
        }

        [TestMethod]
        public void CheckMetricsAggregationLogicWithAdjustment()
        {
            MetricInfo metrics = new MetricInfo("metricsName", "unitName");
            long adjustmentFactor = 1000;

           LongConcurrentHistogram histogram = new LongConcurrentHistogram(1,
                         Int64.MaxValue,
                         5);

            histogram.RecordValue((long)(10 * adjustmentFactor));
            histogram.RecordValue((long)(20 * adjustmentFactor));
            histogram.RecordValue((long)(30 * adjustmentFactor));
            histogram.RecordValue((long)(40 * adjustmentFactor));

            metrics.SetAggregators(histogram, adjustmentFactor);

            Assert.AreEqual(40, metrics.Max);
            Assert.AreEqual(10, metrics.Min);
            Assert.AreEqual(4, metrics.Count);

            Assert.AreEqual(25, metrics.Mean);

            Assert.AreEqual(20, metrics.Percentiles[ClientTelemetryOptions.Percentile50]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile90]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile95]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile99]);
            Assert.AreEqual(40, metrics.Percentiles[ClientTelemetryOptions.Percentile999]);
        }

        [TestMethod]
        public void CheckJsonSerializerContract()
        {
            string json = JsonConvert.SerializeObject(new ClientTelemetryProperties("clientId", "", null, ConnectionMode.Direct, null), ClientTelemetryOptions.JsonSerializerSettings);
            Assert.AreEqual("{\"clientId\":\"clientId\",\"processId\":\"\",\"connectionMode\":\"Direct\",\"systemInfo\":[]}",json);
        }

    }
}
