namespace Cosmos.Samples.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// This handler will send telemetry to Application Insights
    /// </summary>
    class LoggingHandler : CosmosRequestHandler
    {
        private readonly TelemetryClient telemetryClient;
        public LoggingHandler()
        {
            this.telemetryClient = new TelemetryClient();
        }

        public override async Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request,
            CancellationToken cancellationToken)
        {

            using (Microsoft.ApplicationInsights.Extensibility.IOperationHolder<RequestTelemetry> operation = this.telemetryClient.StartOperation<RequestTelemetry>("CosmosDBRequest"))
            {
                this.telemetryClient.TrackTrace($"{request.Method.Method} - {request.RequestUri.ToString()}");
                CosmosResponseMessage response = await base.SendAsync(request, cancellationToken);

                operation.Telemetry.ResponseCode = ((int)response.StatusCode).ToString();
                operation.Telemetry.Success = response.IsSuccessStatusCode;

                this.telemetryClient.StopOperation(operation);
                return response;
            }
        }
    }
}
