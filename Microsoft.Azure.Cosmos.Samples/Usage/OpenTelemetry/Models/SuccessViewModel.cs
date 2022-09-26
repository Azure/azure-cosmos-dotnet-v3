namespace OpenTelemetry.Models
{
    public class SuccessViewModel
    {
        public string PointOpsMessage { get; set; } = "Not Triggered Yet";
        public string QueryOpsMessage { get; set; } = "Not Triggered Yet";
        public string StreamOpsMessage { get; set; } = "Not Triggered Yet";
        public string BulkOpsMessage { get; set; } = "Not Triggered Yet";
        public string CrossQueryOpsMessage { get; set; } = "Not Triggered Yet";

    }
}
