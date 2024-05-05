namespace TestWorkloadV2
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Threading;
    using System;

    internal class DataSource
    {
        // private readonly List<string> additionalProperties = new List<string>();
        private readonly int partitionKeyCount;
        private string padding;
        private int itemId;
        private readonly int workerCount;
        private readonly int workerIndex;

        public readonly string PartitionKeyValuePrefix;
        public readonly string[] PartitionKeyStrings;

        public int InitialItemId { get; private set; }

        public int ItemId => this.itemId;

        private const string idFormatSpecifier = "D9";

        public DataSource(CommonConfiguration configuration)
        {
            this.PartitionKeyValuePrefix = DateTime.UtcNow.ToString("MMddHHmm-");
            this.partitionKeyCount = configuration.PartitionKeyCount;
            if(configuration.TotalRequestCount.HasValue)
            {
                this.partitionKeyCount = Math.Min(this.partitionKeyCount, configuration.TotalRequestCount.Value);
            }

            this.PartitionKeyStrings = this.GetPartitionKeys(this.partitionKeyCount);
            // Setup properties - reduce some for standard properties like PK and Id we are adding
            //for (int i = 0; i < configuration.ItemPropertyCount - 10; i++)
            //{
            //    this.additionalProperties.Add(i.ToString());
            //}
            this.padding = string.Empty;

            this.workerCount = configuration.WorkerCount ?? 1;
            this.workerIndex = configuration.WorkerIndex ?? 0;
        }

        // Ugly as the caller has to remember to do this, but anyway looks optional
        public void InitializePaddingAndInitialItemId(string padding, int? itemIndex = null)
        {
            this.padding = padding;
            this.InitialItemId = itemIndex ?? 0;

            int remainder = this.InitialItemId % this.workerCount;

            this.itemId = this.InitialItemId + this.workerIndex - remainder;

            // ensure we only go to higher values than what we had earlier
            if (this.workerIndex < remainder)
            {
                this.itemId += this.workerCount;
            }
        }

        public string GetId(int itemId)
        {
            return itemId.ToString(idFormatSpecifier);
        }

        /// <summary>
        /// Get's next item to insert
        /// </summary>
        /// <returns>Next document and partition key index</returns>
        public (MyDocument, int) GetNextItemToInsert()
        {
            int currentIndex = Interlocked.Add(ref this.itemId, 0);
            int currentPKIndex = currentIndex % this.partitionKeyCount;
            string partitionKey = this.PartitionKeyStrings[currentPKIndex];
            string id = this.ItemId.ToString(idFormatSpecifier); // should match GetId()
            Interlocked.Add(ref this.itemId, this.workerCount);

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
