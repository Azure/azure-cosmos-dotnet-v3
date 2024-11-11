//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Util;

    [TestClass]
    public class VmMetadataApiHandlerTest
    {
        [TestInitialize]
        public void Initialize()
        {
            FieldInfo isInitializedField = typeof(VmMetadataApiHandler).GetField("isInitialized",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            isInitializedField.SetValue(null, false);

            FieldInfo azMetadataField = typeof(VmMetadataApiHandler).GetField("azMetadata",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            azMetadataField.SetValue(null, null);
        }

        [TestMethod]
        [DataRow("true", DisplayName = "When COSMOS_DISABLE_IMDS_ACCESS is set as true, VM ID should not be fetched")]
        [DataRow("false", DisplayName = "When COSMOS_DISABLE_IMDS_ACCESS is set as false, VM ID should be fetched")]
        [DataRow(null, DisplayName = "When COSMOS_DISABLE_IMDS_ACCESS is NOT set, VM ID should be fetched")]
        public async Task GetVmIdAsMachineIdTest(string isVmMetadataAccessDisabled)
        {
            if (isVmMetadataAccessDisabled != null)
            {
                Environment.SetEnvironmentVariable("COSMOS_DISABLE_IMDS_ACCESS", isVmMetadataAccessDisabled);
            }

            try
            {
                static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    object jsonObject = JsonConvert.DeserializeObject("{\"compute\":{\"azEnvironment\":\"AzurePublicCloud\",\"customData\":\"\",\"isHostCompatibilityLayerVm\":\"false\",\"licenseType\":\"\",\"location\":\"eastus\",\"name\":\"sourabh-testing\",\"offer\":\"UbuntuServer\",\"osProfile\":{\"adminUsername\":\"azureuser\",\"computerName\":\"sourabh-testing\"},\"osType\":\"Linux\",\"placementGroupId\":\"\",\"plan\":{\"name\":\"\",\"product\":\"\",\"publisher\":\"\"},\"platformFaultDomain\":\"0\",\"platformUpdateDomain\":\"0\",\"provider\":\"Microsoft.Compute\",\"publicKeys\":[{\"keyData\":\"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC5uCeOAm3ehmhI+2PbMoMl17Eo\r\nqfHKCycSaBJsv9qxlmBOuFheSJc1XknJleXUSsuTO016/d1PyWpevnqOZNRksWoa\r\nJvQ23sDTxcK+X2OP3QlCUeX4cMjPXqlL8z1UYzU4Bx3fFvf8fs67G3N72sxWBw5P\r\nZyuXyhBm0NCe/2NYMKgEDT4ma8XszO0ikbhoPKbMbgHAQk/ktWQHNcqYOPQKEWqp\r\nEK1R0rjS2nmtovfScP/ZGXcvOpJ1/NDBo4dh1K+OxOGM/4PSH/F448J5Zy4eAyEk\r\nscys+IpeIOTOlRUy/703SNIX0LEWlnYqbyL9c1ypcYLQqF76fKkDfzzFI/OWVlGw\r\nhj/S9uP8iMsR+fhGIbn6MAa7O4DWPWLuedSp7KDYyjY09gqNJsfuaAJN4LiC6bPy\r\nhknm0PVLK3ux7EUOt+cZrHCdIFWbdOtxiPNIl1tkv9kV5aE5Aj2gJm4MeB9uXYhS\r\nOuksboBc0wyUGrl9+XZJ1+NlZOf7IjVi86CieK8= generated-by-azure\r\n\",\"path\":\"/home/azureuser/.ssh/authorized_keys\"}],\"publisher\":\"Canonical\",\"resourceGroupName\":\"sourabh-telemetry-sdk\",\"resourceId\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/virtualMachines/sourabh-testing\",\"securityProfile\":{\"secureBootEnabled\":\"false\",\"virtualTpmEnabled\":\"false\"},\"sku\":\"18.04-LTS\",\"storageProfile\":{\"dataDisks\":[],\"imageReference\":{\"id\":\"\",\"offer\":\"UbuntuServer\",\"publisher\":\"Canonical\",\"sku\":\"18.04-LTS\",\"version\":\"latest\"},\"osDisk\":{\"caching\":\"ReadWrite\",\"createOption\":\"FromImage\",\"diffDiskSettings\":{\"option\":\"\"},\"diskSizeGB\":\"30\",\"encryptionSettings\":{\"enabled\":\"false\"},\"image\":{\"uri\":\"\"},\"managedDisk\":{\"id\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/disks/sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"storageAccountType\":\"Premium_LRS\"},\"name\":\"sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"osType\":\"Linux\",\"vhd\":{\"uri\":\"\"},\"writeAcceleratorEnabled\":\"false\"}},\"subscriptionId\":\"8fba6d4f-7c37-4d13-9063-fd58ad2b86e2\",\"tags\":\"azsecpack:nonprod;platformsettings.host_environment.service.platform_optedin_for_rootcerts:true\",\"tagsList\":[{\"name\":\"azsecpack\",\"value\":\"nonprod\"},{\"name\":\"platformsettings.host_environment.service.platform_optedin_for_rootcerts\",\"value\":\"true\"}],\"version\":\"18.04.202103250\",\"vmId\":\"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd\",\"vmScaleSetName\":\"\",\"vmSize\":\"Standard_D2s_v3\",\"zone\":\"1\"},\"network\":{\"interface\":[{\"ipv4\":{\"ipAddress\":[{\"privateIpAddress\":\"10.0.7.5\",\"publicIpAddress\":\"\"}],\"subnet\":[{\"address\":\"10.0.7.0\",\"prefix\":\"24\"}]},\"ipv6\":{\"ipAddress\":[]},\"macAddress\":\"000D3A8F8BA0\"}]}}");
                    string payload = JsonConvert.SerializeObject(jsonObject);
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

                VmMetadataApiHandler.TryInitialize(cosmosHttpClient);

                await Task.Delay(2000);

                if (isVmMetadataAccessDisabled != null &&
                    Boolean.TryParse(isVmMetadataAccessDisabled, out bool isVmMetadataAccessDisabledBool) &&
                    isVmMetadataAccessDisabledBool)
                {
                    Assert.AreNotEqual($"{VmMetadataApiHandler.VmIdPrefix}{"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd"}", VmMetadataApiHandler.GetMachineId());
                    Assert.IsNull(VmMetadataApiHandler.GetMachineRegion(), VmMetadataApiHandler.GetMachineRegion());
                }
                else
                {
                    Assert.AreEqual($"{VmMetadataApiHandler.VmIdPrefix}{"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd"}", VmMetadataApiHandler.GetMachineId());
                    Assert.AreEqual(VmMetadataApiHandler.GetMachineRegion(), "eastus");
                }

            }
            finally
            {
                Environment.SetEnvironmentVariable("COSMOS_DISABLE_IMDS_ACCESS", null);
            }
        }

        [TestMethod]
        public async Task GetVMMachineIdWithNullComputeTest()
        {
            static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                object jsonObject = JsonConvert.DeserializeObject("{\"compute\":null}");
                string payload = JsonConvert.SerializeObject(jsonObject);
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }

            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            VmMetadataApiHandler.TryInitialize(cosmosHttpClient);

            await Task.Delay(2000);
            Assert.IsNull(VmMetadataApiHandler.GetMachineInfo());
            Assert.IsNotNull(VmMetadataApiHandler.GetMachineId());
            Assert.IsNull(VmMetadataApiHandler.GetMachineRegion());
        }

        [TestMethod]
        public async Task GetHashedMachineNameAsMachineIdTest()
        {
            string expectedMachineId = VmMetadataApiHandler.HashedMachineNamePrefix + HashingExtension.ComputeHash(Environment.MachineName);

            static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken) { throw new Exception("error while making API call"); };

            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            VmMetadataApiHandler.TryInitialize(cosmosHttpClient);

            await Task.Delay(2000);
            Assert.AreEqual(expectedMachineId, VmMetadataApiHandler.GetMachineId());
        }


        [TestMethod]
        public void ComputeHashTest()
        {
            string hashedValue = HashingExtension.ComputeHash("abc");
            Assert.AreEqual("bf1678ba-018f-eacf-4141-40de5dae2223", hashedValue);
        }


        [TestMethod]
        public async Task ParseAzureVMMetadataTest()
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

            // Add a substatus code that is not part of the enum. 
            // This ensures that if the backend adds a enum the status code is not lost.
            object jsonObject = JsonConvert.DeserializeObject("{\"compute\":{\"azEnvironment\":\"AzurePublicCloud\",\"customData\":\"\",\"isHostCompatibilityLayerVm\":\"false\",\"licenseType\":\"\",\"location\":\"eastus\",\"name\":\"sourabh-testing\",\"offer\":\"UbuntuServer\",\"osProfile\":{\"adminUsername\":\"azureuser\",\"computerName\":\"sourabh-testing\"},\"osType\":\"Linux\",\"placementGroupId\":\"\",\"plan\":{\"name\":\"\",\"product\":\"\",\"publisher\":\"\"},\"platformFaultDomain\":\"0\",\"platformUpdateDomain\":\"0\",\"provider\":\"Microsoft.Compute\",\"publicKeys\":[{\"keyData\":\"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC5uCeOAm3ehmhI+2PbMoMl17Eo\r\nqfHKCycSaBJsv9qxlmBOuFheSJc1XknJleXUSsuTO016/d1PyWpevnqOZNRksWoa\r\nJvQ23sDTxcK+X2OP3QlCUeX4cMjPXqlL8z1UYzU4Bx3fFvf8fs67G3N72sxWBw5P\r\nZyuXyhBm0NCe/2NYMKgEDT4ma8XszO0ikbhoPKbMbgHAQk/ktWQHNcqYOPQKEWqp\r\nEK1R0rjS2nmtovfScP/ZGXcvOpJ1/NDBo4dh1K+OxOGM/4PSH/F448J5Zy4eAyEk\r\nscys+IpeIOTOlRUy/703SNIX0LEWlnYqbyL9c1ypcYLQqF76fKkDfzzFI/OWVlGw\r\nhj/S9uP8iMsR+fhGIbn6MAa7O4DWPWLuedSp7KDYyjY09gqNJsfuaAJN4LiC6bPy\r\nhknm0PVLK3ux7EUOt+cZrHCdIFWbdOtxiPNIl1tkv9kV5aE5Aj2gJm4MeB9uXYhS\r\nOuksboBc0wyUGrl9+XZJ1+NlZOf7IjVi86CieK8= generated-by-azure\r\n\",\"path\":\"/home/azureuser/.ssh/authorized_keys\"}],\"publisher\":\"Canonical\",\"resourceGroupName\":\"sourabh-telemetry-sdk\",\"resourceId\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/virtualMachines/sourabh-testing\",\"securityProfile\":{\"secureBootEnabled\":\"false\",\"virtualTpmEnabled\":\"false\"},\"sku\":\"18.04-LTS\",\"storageProfile\":{\"dataDisks\":[],\"imageReference\":{\"id\":\"\",\"offer\":\"UbuntuServer\",\"publisher\":\"Canonical\",\"sku\":\"18.04-LTS\",\"version\":\"latest\"},\"osDisk\":{\"caching\":\"ReadWrite\",\"createOption\":\"FromImage\",\"diffDiskSettings\":{\"option\":\"\"},\"diskSizeGB\":\"30\",\"encryptionSettings\":{\"enabled\":\"false\"},\"image\":{\"uri\":\"\"},\"managedDisk\":{\"id\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/disks/sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"storageAccountType\":\"Premium_LRS\"},\"name\":\"sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"osType\":\"Linux\",\"vhd\":{\"uri\":\"\"},\"writeAcceleratorEnabled\":\"false\"}},\"subscriptionId\":\"8fba6d4f-7c37-4d13-9063-fd58ad2b86e2\",\"tags\":\"azsecpack:nonprod;platformsettings.host_environment.service.platform_optedin_for_rootcerts:true\",\"tagsList\":[{\"name\":\"azsecpack\",\"value\":\"nonprod\"},{\"name\":\"platformsettings.host_environment.service.platform_optedin_for_rootcerts\",\"value\":\"true\"}],\"version\":\"18.04.202103250\",\"vmId\":\"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd\",\"vmScaleSetName\":\"\",\"vmSize\":\"Standard_D2s_v3\",\"zone\":\"1\"},\"network\":{\"interface\":[{\"ipv4\":{\"ipAddress\":[{\"privateIpAddress\":\"10.0.7.5\",\"publicIpAddress\":\"\"}],\"subnet\":[{\"address\":\"10.0.7.0\",\"prefix\":\"24\"}]},\"ipv6\":{\"ipAddress\":[]},\"macAddress\":\"000D3A8F8BA0\"}]}}");
            string payload = JsonConvert.SerializeObject(jsonObject);
            result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            AzureVMMetadata metadata = await VmMetadataApiHandler.ProcessResponseAsync(result);

            Assert.AreEqual("eastus", metadata.Compute.Location);
            Assert.AreEqual("18.04-LTS", metadata.Compute.SKU);
            Assert.AreEqual("AzurePublicCloud", metadata.Compute.AzEnvironment);
            Assert.AreEqual("Linux", metadata.Compute.OSType);
            Assert.AreEqual("Standard_D2s_v3", metadata.Compute.VMSize);
            Assert.AreEqual($"{VmMetadataApiHandler.VmIdPrefix}{"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd"}", metadata.Compute.VMId);
        }

        [TestMethod]
        public void CatchMetadataApiCallExceptionTest()
        {
            static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken) { throw new Exception("error while making API call"); };

            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmosHttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            string expectedMsg = "Azure Environment metadata information not available.";
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            void TraceHandler(string message)
            {
                if (message.Contains(expectedMsg))
                {
                    manualResetEvent.Set();
                }
            }

            DefaultTrace.TraceSource.Listeners.Add(new TestTraceListener { Callback = TraceHandler });
            DefaultTrace.InitEventListener();

            VmMetadataApiHandler.TryInitialize(cosmosHttpClient);

            int timeout = 30000;
            Assert.IsTrue(manualResetEvent.WaitOne(timeout));
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc;

            public MockMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request, cancellationToken);
            }
        }
        private class TestTraceListener : TraceListener
        {
            public Action<string> Callback { get; set; }
            public override bool IsThreadSafe => true;
            public override void Write(string message)
            {
                this.Callback(message);
            }

            public override void WriteLine(string message)
            {
                this.Callback(message);
            }
        }
    }
}