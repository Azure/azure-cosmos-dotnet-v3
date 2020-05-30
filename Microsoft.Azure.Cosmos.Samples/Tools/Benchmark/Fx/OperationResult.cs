namespace CosmosBenchmark
{
    using System;

    internal struct OperationResult
    {
        public string DatabseName { get; set; }
        public string ContainerName { get; set; }
        public double RuCharges { get; set; }

        public Func<string> lazyDiagnostics { get; set; }
    }
}
