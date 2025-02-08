namespace TestWorkloadV2
{
    using System.Threading;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class DataSource
    {
        private readonly List<string> arrayValue = new List<string>();
        private readonly int partitionKeyCount;
        private string padding;
        private long itemId;
     
        public readonly string PartitionKeyValuePrefix;
        public readonly string[] PartitionKeyStrings;

        public long InitialItemId { get; private set; }

        public long ItemId => this.itemId;

        public const string IdFormatSpecifier = "D10";
        public const long WorkerIdMultiplier = 10000000000;


        public static async Task<DataSource> CreateAsync(CommonConfiguration configuration, 
            Func<DataSource, Task<string>> paddingGenerator, 
            Func<Task<long>> initialItemIdFinder)
        {
            DataSource dataSource = new DataSource(configuration);
            dataSource.padding = paddingGenerator != null ? await paddingGenerator(dataSource) : null;
            dataSource.InitialItemId = initialItemIdFinder != null ? await initialItemIdFinder() : 0;
            dataSource.itemId = dataSource.InitialItemId;
            return dataSource;
        }


        public string GetId(long itemId)
        {
            return itemId.ToString(IdFormatSpecifier);
        }

        /// <summary>
        /// Get's next item to insert
        /// </summary>
        /// <returns>Next document and partition key index</returns>
        public (MyDocument, int) GetNextItemToInsert()
        {
            long currentIndex = Interlocked.Add(ref this.itemId, 0);
            int currentPKIndex = (int)(currentIndex % this.partitionKeyCount);
            string partitionKey = this.PartitionKeyStrings[currentPKIndex];
            string id = this.ItemId.ToString(IdFormatSpecifier); // should match GetId()
            Interlocked.Increment(ref this.itemId);

            return (new MyDocument()
            {
                Id = id,
                PK = partitionKey,
                Arr = this.arrayValue,
                Other = this.padding
            }, currentPKIndex);
        }

        private DataSource(CommonConfiguration configuration)
        {
            this.PartitionKeyValuePrefix = DateTime.UtcNow.ToString("yyyyMMddHHmmss-");
            this.partitionKeyCount = configuration.PartitionKeyCount;
            if (configuration.TotalRequestCount.HasValue)
            {
                this.partitionKeyCount = Math.Min(this.partitionKeyCount, configuration.TotalRequestCount.Value);
            }

            this.PartitionKeyStrings = this.GetPartitionKeys(this.partitionKeyCount);

            for (int i = 0; i < configuration.ItemArrayCount; i++)
            {
                this.arrayValue.Add(i.ToString());
            }

            this.padding = string.Empty;
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
