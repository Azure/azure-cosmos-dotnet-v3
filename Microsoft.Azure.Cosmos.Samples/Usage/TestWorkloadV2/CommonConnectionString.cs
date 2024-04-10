namespace TestWorkloadV2
{
    internal class CommonConnectionString
    {
        public CommonConnectionString(string connectionString)
        {
            this.WithCredential = connectionString;
        }

        public static implicit operator CommonConnectionString(string withCredential)
        {
            return new CommonConnectionString(withCredential);
        }

        internal string ForLogging { private get; set; }

        public string WithCredential { get; }

        public string GetForLogging()
        {
            return this.ForLogging;
        }
    }
}
