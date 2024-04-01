namespace TestWorkloadV2
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Threading;
    using System;

    internal class DataSource
    {
        public readonly string PartitionKeyValuePrefix;
        public readonly string[] PartitionKeyStrings;

        private readonly List<string> additionalProperties = new List<string>();
        private readonly int partitionKeyCount;
        private string padding;

        private int itemIndex;

        public delegate string PaddingCreator(DataSource dataSource);

        public DataSource(CommonConfiguration configuration)
        {
            this.PartitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");
            this.partitionKeyCount = Math.Min(configuration.PartitionKeyCount, configuration.TotalRequestCount);
            this.PartitionKeyStrings = this.GetPartitionKeys(this.partitionKeyCount);
            // Setup properties - reduce some for standard properties like PK and Id we are adding
            //for (int i = 0; i < configuration.ItemPropertyCount - 10; i++)
            //{
            //    this.additionalProperties.Add(i.ToString());
            //}
            this.padding = string.Empty;
        }

        // Ugly as the caller has to remember to do this, but anyway looks optional
        public void InitializePadding(string padding)
        {
            this.padding = padding;
            this.itemIndex = 0;
        }

        public (MyDocument, int) GetNextItem()
        {
            int incremented = Interlocked.Increment(ref this.itemIndex);
            int currentPKIndex = incremented % this.partitionKeyCount;
            string partitionKey = this.PartitionKeyStrings[currentPKIndex];
            string id = Guid.NewGuid().ToString();

            return (new MyDocument()
            {
                Id = id,
                PK = partitionKey,
                //Arr = this.additionalProperties,
                Other = this.padding
            }, currentPKIndex);
        }

        private string[] GetPartitionKeys(int partitionKeyCount)
        {
            string[] partitionKeys = new string[partitionKeyCount];
            int partitionKeySuffixLength = (partitionKeyCount - 1).ToString().Length;
            string partitionKeySuffixFormatSpecifier = "D" + partitionKeySuffixLength;

            for (int i = 0; i < partitionKeyCount; i++)
            {
                partitionKeys[i] = this.PartitionKeyValuePrefix + i.ToString(partitionKeySuffixFormatSpecifier);
            }

            return partitionKeys;
        }
    }

}
