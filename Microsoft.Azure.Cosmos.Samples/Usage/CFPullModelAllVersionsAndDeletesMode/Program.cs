namespace CFPullModelAllVersionsAndDeletesMode;

using System.Threading.Tasks;
using Cosmos.Samples.ChangeFeedPullModel.CFPullModelAllVersionsAndDeletesMode;

class Program
{
    static async Task Main()
    {
        ChangeFeedDemo changeFeedDemo = new ChangeFeedDemo();
        await changeFeedDemo.GetOrCreateContainer();
        await changeFeedDemo.CreateAllVersionsAndDeletesChangeFeedIterator();
        await changeFeedDemo.IngestData();
        await changeFeedDemo.DeleteData();
        await changeFeedDemo.ReadAllVersionsAndDeletesChangeFeed();
    }
}
