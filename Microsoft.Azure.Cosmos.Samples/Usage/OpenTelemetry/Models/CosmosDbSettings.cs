namespace OpenTelemetry.Models
{
    public class CosmosDbSettings
    {
        public string ConnectionString { get; set; }
        public bool EnableOpenTelemetry { get; set; }
        public string ConnectionMode { get; set; }
    }
}
