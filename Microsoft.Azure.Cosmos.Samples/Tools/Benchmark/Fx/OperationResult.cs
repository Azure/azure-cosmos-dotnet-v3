namespace CosmosBenchmark
{
    using System;
    using Microsoft.Azure.Cosmos;

    internal struct OperationResult
    {
        public string DatabseName { get; set; }
        public string ContainerName { get; set; }
        public double RuCharges { get; set; }
        public Func<string> LazyDiagnostics { get; set; }
        public CosmosDiagnostics CosmosDiagnostics { get; set; }
    }
}
