namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Collections;
    using System.IO;
    using System.Text;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {

        private CosmosClient client = null;
        private Cosmos.Database database = null;

        private Container container = null;
        private ContainerProperties containerProperties = null;

        private readonly Uri secondaryRegion;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.client = TestCommon.CreateCosmosClient(true);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            this.containerProperties = new ContainerProperties("test", "/pk");
            this.container = await this.database.CreateContainerAsync(this.containerProperties);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
            this.client.Dispose();
        }


        //Test avaialvility strategy does not trigger 
        //test that availability strategy triggers 
        //test that availability strategy triggers and original region returns first 
        //fabian test case

        //Timeout Primary Region Request, Secondary Region returns 200
        private class PrimaryTimeout : IChaosInterceptor
        {
            public string GetFaultInjectionRuleId(Guid activityId)
            {
                return "";
            }

            public void OnAfterConnectionWrite(ChannelCallArguments args)
            {
                ;
            }

            public void OnBeforeConnectionWrite(ChannelCallArguments args)
            {
                ;
            }

            public void OnChannelDispose(Guid connectionCorrelationId)
            {
                ;
            }

            public void OnChannelOpen(Guid activityId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel)
            {
                ;
            }

            public bool OnRequestCall(ChannelCallArguments args, out StoreResponse faultyResponse)
            {
                faultyResponse = null;
                if (args.OperationType == OperationType.Read && args.ResourceType == ResourceType.Document)
                {
                    if (args.LocationEndpointToRouteTo.toString().Contains("east"))
                    {
                        faultyResponse = new StoreResponse()
                        {
                            Status = (int)HttpStatusCode.OK,
                            ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"_rid\":\"\",\"Documents\":[{\"id\":\"1\",\"_rid\":\"\",\"_self\":\"\",\"_ts\":0,\"_etag\":\"\",\"_attachments\":\"\",\"_ts\":\"\"}],\"_count\":1}"))
                        };
                    }
                    else
                    {
                        faultyResponse = new StoreResponse()
                        {
                            Status = (int)HttpStatusCode.RequestTimeout
                        };
                        Task.Delay(10000);

                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        //Delay Primary Region Request, Secondary Region Will Timeout
        private class PrimaryDelay : IChaosInterceptor
        {
            public string GetFaultInjectionRuleId(Guid activityId)
            {
                return "";
            }

            public void OnAfterConnectionWrite(ChannelCallArguments args)
            {
                ;
            }

            public void OnBeforeConnectionWrite(ChannelCallArguments args)
            {
                ;
            }

            public void OnChannelDispose(Guid connectionCorrelationId)
            {
                ;
            }

            public void OnChannelOpen(Guid activityId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel)
            {
                ;
            }

            public bool OnRequestCall(ChannelCallArguments args, out StoreResponse faultyResponse)
            {
                faultyResponse = null;
                if (args.OperationType == OperationType.Read && args.ResourceType == ResourceType.Document)
                {
                    if (args.LocationEndpointToRouteTo.toString().Contains("east"))
                    {
                        faultyResponse = new StoreResponse()
                        {
                            Status = (int)HttpStatusCode.RequestTimeout
                        };
                        Task.Delay(10000);
                    }
                    else
                    {

                        return false;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
