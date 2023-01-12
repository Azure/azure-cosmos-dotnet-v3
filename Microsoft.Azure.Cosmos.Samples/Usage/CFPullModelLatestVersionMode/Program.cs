namespace CFPullModelLatestVersionMode
{
    using System.Threading.Tasks;
    using Cosmos.Samples.CFPullModelLatestVersionMode;

    class Program
    {
        static async Task Main()
        {
            ChangeFeedDemo changeFeedDemo = new ChangeFeedDemo();
            await changeFeedDemo.GetOrCreateContainer();
            await changeFeedDemo.CreateLatestVersionChangeFeedIterator();
            await changeFeedDemo.IngestData();
            await changeFeedDemo.ReadLatestVersionChangeFeed();
        }
    }
}
