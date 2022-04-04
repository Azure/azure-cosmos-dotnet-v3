//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class VmMetadataApiHandlerTest
    {
        [TestInitialize]
        public void Intialize()
        {
            VmMetadataApiHandler.Clear();
        }

        [TestMethod]
        public async Task GetVmIdAsMachineIdTest()
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
            CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            VmMetadataApiHandler.Initialize(cosmoshttpClient);

            await Task.Delay(2000);
            Assert.AreEqual("vmId:d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd", VmMetadataApiHandler.GetMachineId());
        }

        [TestMethod]
        public async Task GetHashedMachineNameAsMachineIdTest()
        {
            string expectedMachineId = "hashedMachineName:" + VmMetadataApiHandler.ComputeHash(Environment.MachineName);

            static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken) { throw new Exception("error while making API call"); };

            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            VmMetadataApiHandler.Initialize(cosmoshttpClient);

            await Task.Delay(2000);
            Assert.AreEqual(expectedMachineId, VmMetadataApiHandler.GetMachineId());
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
    }
}
