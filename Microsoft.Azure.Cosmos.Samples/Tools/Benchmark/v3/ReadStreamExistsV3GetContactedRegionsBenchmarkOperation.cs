namespace CosmosBenchmark.v3
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal class ReadStreamExistsV3GetContactedRegionsBenchmarkOperation : ReadNotExistsV3BenchmarkOperation
    {
        public ReadStreamExistsV3GetContactedRegionsBenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson) : base(cosmosClient, dbName, containerName, partitionKeyPath, sampleJson)
        {
        }

        public override async Task<OperationResult> ExecuteOnceAsync()
        {
            using (ResponseMessage itemResponse = await this.container.ReadItemStreamAsync(
                        this.nextExecutionItemId,
                        new PartitionKey(this.nextExecutionItemPartitionKey)))
            {
                if (itemResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"ReadItem failed wth {itemResponse.StatusCode}");
                }

                string contactedRegions = ReadStreamExistsV3GetContactedRegionsBenchmarkOperation.GetContactedRegions(itemResponse.Diagnostics);;
                if (string.IsNullOrWhiteSpace(contactedRegions))
                {
                    throw new Exception($"ReadItem succesful but no regions contacted {itemResponse.Diagnostics}");
                }

                return new OperationResult()
                {
                    DatabseName = this.databsaeName,
                    ContainerName = this.containerName,
                    RuCharges = itemResponse.Headers.RequestCharge,
                    CosmosDiagnostics = itemResponse.Diagnostics,
                    LazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                };
            }
        }

        /// <summary>
        /// Copied from ClientTelemetryHelper.GetContactedRegions 
        /// Remove once package is refreshed 
        /// </summary>
        internal static string GetContactedRegions(CosmosDiagnostics cosmosDiagnostics)
        {
            IReadOnlyList<(string regionName, Uri uri)> regionList = cosmosDiagnostics.GetContactedRegions();

            if (regionList.Count == 1)
            {
                return regionList[0].regionName;
            }

            StringBuilder regionsContacted = new StringBuilder();
            foreach ((string name, _) in regionList)
            {
                if (regionsContacted.Length > 0)
                {
                    regionsContacted.Append(",");

                }

                regionsContacted.Append(name);
            }

            return regionsContacted.ToString();
        }
    }
}
