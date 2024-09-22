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
    class LoggingHandler : RequestHandler
    {
        private readonly TelemetryClient telemetryClient;
        public LoggingHandler()
        {
#pragma warning disable CS0618 
            this.telemetryClient = new TelemetryClient();
#pragma warning restore CS0618 
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {

            using (Microsoft.ApplicationInsights.Extensibility.IOperationHolder<RequestTelemetry> operation = this.telemetryClient.StartOperation<RequestTelemetry>("CosmosDBRequest"))
            {
                this.telemetryClient.TrackTrace($"{request.Method.Method} - {request.RequestUri}");
                ResponseMessage response = await base.SendAsync(request, cancellationToken);

                operation.Telemetry.ResponseCode = ((int)response.StatusCode).ToString();
                operation.Telemetry.Success = response.IsSuccessStatusCode;

                this.telemetryClient.StopOperation(operation);
                return response;
            }
        }
    }
}
