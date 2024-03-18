namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class NonStreamingOrderByQueryTests : QueryTestsBase
    {
        private static readonly IReadOnlyList<string> Documents = new List<string>
        {
            @"{'id':'00', 'vector1':[1,2,3], 'data':{'vector2':[4,5,6]}}",
            @"{'id':'01', 'vector1':[-5200988, -3549847, -5196219], 'data':{'vector2':[-9009653, 2368946, 9649936]}}",
            @"{'id':'02', 'vector1':[2046860, 8314330, 3817636], 'data':{'vector2':[767698, 9689859, 3864796]}}",
            @"{'id':'03', 'vector1':[716405, 5423419, 3267102], 'data':{'vector2':[6286049, 7075631, 2100843]}}",
            @"{'id':'04', 'vector1':[5423668, 8776884, 9512539], 'data':{'vector2':[5130289, 278343, 7714280]}}",
            @"{'id':'05', 'vector1':[3343401, 2253440, 4270121], 'data':{'vector2':[7290786, 5213099, 7303736]}}",
            @"{'id':'06', 'vector1':[2181471, 2206386, 4775082], 'data':{'vector2':[5023592, 8720325, 7834388]}}",
            @"{'id':'07', 'vector1':[3052965, 6546113, 7499664], 'data':{'vector2':[5558898, 7917973, 1553853]}}",
            @"{'id':'08', 'vector1':[5957375, 7871489, 5726228], 'data':{'vector2':[4765521, 2096224, 9365730]}}",
            @"{'id':'09', 'vector1':[3023844, 926321, 2728995], 'data':{'vector2':[2452382, 1062847, 841638]}}",
            @"{'id':'10', 'vector1':[7817913, 8298421, 9964154], 'data':{'vector2':[3852280, 9172869, 2462932]}}",
            @"{'id':'11', 'vector1':[9806732, 9781726, 7953391], 'data':{'vector2':[2202335, 3712420, 6927882]}}",
            @"{'id':'12', 'vector1':[9635916, 2667721, 4558319], 'data':{'vector2':[3344434, 9827349, 6755519]}}",
            @"{'id':'13', 'vector1':[8773251, 7479951, 2366140], 'data':{'vector2':[2798439, 5766520, 5095650]}}",
            @"{'id':'14', 'vector1':[2982933, 2096619, 1001503], 'data':{'vector2':[8041819, 9419580, 7911842]}}",
            @"{'id':'15', 'vector1':[1985873, 6799746, 3601496], 'data':{'vector2':[2070870, 7633048, 3445741]}}",
            @"{'id':'16', 'vector1':[6393425, 7238542, 8602397], 'data':{'vector2':[8226526, 5301395, 1260440]}}",
            @"{'id':'17', 'vector1':[4311677, 2721210, 674570], 'data':{'vector2':[2443983, 8779891, 9509757]}}",
            @"{'id':'18', 'vector1':[7738202, 9006567, 2127392], 'data':{'vector2':[5064153, 4250886, 3479816]}}",
            @"{'id':'19', 'vector1':[1669990, 7978017, 7272220], 'data':{'vector2':[2817428, 4373207, 7466955]}}",
            @"{'id':'20', 'vector1':[2654955, 7638475, 5283149], 'data':{'vector2':[3993174, 3684833, 4172944]}}",
            @"{'id':'21', 'vector1':[0, 1058756402, 1], 'data':{'vector2':[140178743, 0, 916612128]}}",
            @"{'id':'22', 'vector1':[1, 1, 1], 'data':{'vector2':[0, 1, 198508386]}}",
            @"{'id':'23', 'vector1':[574399655, 498084122, 1], 'data':{'vector2':[1, 0, 0]}}",
            @"{'id':'24', 'vector1':[83803331, 124424830, 1907795466], 'data':{'vector2':[1, 1958773479, 1466444387]}}",
            @"{'id':'25', 'vector1':[0, 1, 1], 'data':{'vector2':[603290561, 1, 1974057815]}}",
            @"{'id':'26', 'vector1':[1, 251737714, 0], 'data':{'vector2':[0, 2117368996, 1]}}",
            @"{'id':'27', 'vector1':[532128725, 1935723869, 1], 'data':{'vector2':[1, 1, 2121625746]}}",
            @"{'id':'28', 'vector1':[1, 1, 0], 'data':{'vector2':[951159328, 1998962744, 1]}}",
            @"{'id':'29', 'vector1':[1, 1206499655, 1], 'data':{'vector2':[1415051389, 0, 1]}}",
            @"{'id':'30', 'vector1':[1, 323176253, 1040760962], 'data':{'vector2':[579506777, 1, 1]}}",
            @"{'id':'31', 'vector1':[1, 0, 1], 'data':{'vector2':[0, 1, 214137545]}}",
            @"{'id':'32', 'vector1':[1366550321, 1, 647820708], 'data':{'vector2':[373281416, 1, 1]}}",
            @"{'id':'33', 'vector1':[579662790, 967460019, 1170389007], 'data':{'vector2':[0, 1, 1679353786]}}",
            @"{'id':'34', 'vector1':[2103386592, 1, 1518705465], 'data':{'vector2':[1906760511, 1493728715, 2034776944]}}",
            @"{'id':'35', 'vector1':[2121061622, 1628233032, 0], 'data':{'vector2':[243515575, 0, 1]}}",
            @"{'id':'36', 'vector1':[1, 1269868840, 1070590958], 'data':{'vector2':[329569317, 1, 1372625395]}}",
            @"{'id':'37', 'vector1':[722956267, 0, 1022835562], 'data':{'vector2':[2108664318, 1, 1072521669]}}",
            @"{'id':'38', 'vector1':[1, 0, 1], 'data':{'vector2':[1269715978, 1, 1]}}",
            @"{'id':'39', 'vector1':[348522617, 1, 0], 'data':{'vector2':[1, 0, 1]}}",
            @"{'id':'40', 'vector1':[1904486870, 0, 1], 'data':{'vector2':[0, 1152686664, 450272001]}}",
            @"{'id':'41', 'vector1':[180933010, 1668424101, 0], 'data':{'vector2':[0, 0, 1823888644]}}",
            @"{'id':'42', 'vector1':[1, 1215190825, 1], 'data':{'vector2':[229740431, 412909444, 130077552]}}",
            @"{'id':'43', 'vector1':[484614441, 1456507629, 876957810], 'data':{'vector2':[1, 1, 1]}}",
            @"{'id':'44', 'vector1':[1586980096, 1, 1], 'data':{'vector2':[894669502, 647945940, 849775050]}}",
            @"{'id':'45', 'vector1':[1, 591865974, 1], 'data':{'vector2':[0, 1, 1044408738]}}",
            @"{'id':'46', 'vector1':[1127881317, 1695421484, 1963690650], 'data':{'vector2':[2080003549, 2122198665, 1]}}",
            @"{'id':'47', 'vector1':[0, 1, 1], 'data':{'vector2':[1, 1, 1]}}",
            @"{'id':'48', 'vector1':[1, 1, 1], 'data':{'vector2':[1351341509, 379421678, 0]}}",
            @"{'id':'49', 'vector1':[1929377974, 1053580875, 0], 'data':{'vector2':[1, 1731458409, 2096166120]}}",
            @"{'id':'50', 'vector1':[1, 0, 0], 'data':{'vector2':[1, 968230132, 1]}}",
            @"{'id':'51', 'vector1':[0, 0, 1182225063], 'data':{'vector2':[1583656039, 1, 1211454111]}}",
            @"{'id':'52', 'vector1':[19826477, 1, 632266284], 'data':{'vector2':[137673955, 1848614763, 1519301219]}}",
            @"{'id':'53', 'vector1':[1, 340879559, 1], 'data':{'vector2':[1, 1, 0]}}",
            @"{'id':'54', 'vector1':[0, 105721632, 1], 'data':{'vector2':[1, 0, 0]}}",
            @"{'id':'55', 'vector1':[483958094, 1251616442, 1095315820], 'data':{'vector2':[0, 1374311893, 1184239052]}}",
            @"{'id':'56', 'vector1':[1, 1, 198502340], 'data':{'vector2':[0, 1, 826706936]}}",
            @"{'id':'57', 'vector1':[1081487460, 1, 2102304768], 'data':{'vector2':[1861799374, 979430480, 0]}}",
            @"{'id':'58', 'vector1':[331658884, 1849486791, 1919612861], 'data':{'vector2':[1398821917, 1986769364, 287735253]}}",
            @"{'id':'59', 'vector1':[475394240, 0, 724277374], 'data':{'vector2':[1, 1, 682153639]}}",
            @"{'id':'60', 'vector1':[210766231, 0, 1710331981], 'data':{'vector2':[1, 1, 2015838519]}}",
            @"{'id':'61', 'vector1':[1, 0, 2043643512], 'data':{'vector2':[1728010400, 150887487, 0]}}",
            @"{'id':'62', 'vector1':[1, 1493670533, 417717088], 'data':{'vector2':[2034530126, 1, 1556328695]}}",
            @"{'id':'63', 'vector1':[587051840, 1438700604, 1], 'data':{'vector2':[1, 145764883, 1094974294]}}",
            @"{'id':'64', 'vector1':[4069279, 1589085743, 1587267932], 'data':{'vector2':[418648012, 1, 0]}}",
            @"{'id':'65', 'vector1':[242093230, 0, 0], 'data':{'vector2':[1830592309, 456621624, 1777145176]}}",
            @"{'id':'66', 'vector1':[1633934233, 1894865461, 2113344435], 'data':{'vector2':[846102648, 0, 1317706635]}}",
            @"{'id':'67', 'vector1':[1833475153, 1649749112, 1192501791], 'data':{'vector2':[1175064758, 1, 1]}}",
            @"{'id':'68', 'vector1':[1, 1, 0], 'data':{'vector2':[1, 0, 1500445820]}}",
            @"{'id':'69', 'vector1':[904209622, 0, 0], 'data':{'vector2':[1, 1607559980, 2101818801]}}",
            @"{'id':'70', 'vector1':[1389017506, 0, 0], 'data':{'vector2':[1, 1, 913895312]}}",
            @"{'id':'71', 'vector1':[1, 1180757318, 287370213], 'data':{'vector2':[1243657739, 40989544, 1]}}",
            @"{'id':'72', 'vector1':[907691094, 0, 1], 'data':{'vector2':[1, 1623389213, 1429653112]}}",
            @"{'id':'73', 'vector1':[1119200640, 655532382, 1559735094], 'data':{'vector2':[1213998740, 1602369370, 0]}}",
            @"{'id':'74', 'vector1':[0, 1, 1763904700], 'data':{'vector2':[1606849834, 1, 1]}}",
            @"{'id':'75', 'vector1':[1, 2006387799, 563057760], 'data':{'vector2':[0, 1577501067, 90982989]}}",
            @"{'id':'76', 'vector1':[1, 1, 754424437], 'data':{'vector2':[1761096472, 1, 643308966]}}",
            @"{'id':'77', 'vector1':[1, 1, 1], 'data':{'vector2':[1369472037, 1, 0]}}",
            @"{'id':'78', 'vector1':[107365820, 0, 0], 'data':{'vector2':[1177591766, 1, 0]}}",
            @"{'id':'79', 'vector1':[1341483693, 0, 446199115], 'data':{'vector2':[0, 0, 1]}}",
            @"{'id':'80', 'vector1':[1, 269598572, 1], 'data':{'vector2':[154517387, 0, 1]}}",
            @"{'id':'81', 'vector1':[223374368, 1335130659, 1791365535], 'data':{'vector2':[1, 1, 0]}}",
            @"{'id':'82', 'vector1':[0, 0, 1469736808], 'data':{'vector2':[560279906, 374285593, 149886642]}}",
            @"{'id':'83', 'vector1':[1, 1899407262, 0], 'data':{'vector2':[733153959, 1421089396, 547844527]}}",
            @"{'id':'84', 'vector1':[175906476, 1, 1], 'data':{'vector2':[1, 1, 1]}"
        };

        [TestMethod]
        public async Task TestOrderByQuery()
        {
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                Documents,
                RunTestsAsync);
        }

        private static async Task RunTestsAsync(Container container, IReadOnlyList<CosmosObject> _)
        {
            IEnumerable<string> queries = new List<string>
            {
                "SELECT c.id AS Id, VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'Cosine'}) AS Distance " +
                "FROM c " +
                "ORDER BY VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'Cosine'})",

                "SELECT c.id AS Id, VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'DotProduct'}) AS Distance " +
                "FROM c " +
                "ORDER BY VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'DotProduct'})",

                "SELECT c.id AS Id, VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'Euclidean'}) AS Distance " +
                "FROM c " +
                "ORDER BY VectorDistance(c.vector1, c.vector2, true, {distanceFunction:'Euclidean'})",
            };

            foreach (string query in queries)
            {
                FeedIterator<Document> iterator = container.GetItemQueryIterator<Document>(query);

                List<Document> documents = new List<Document>();
                while (iterator.HasMoreResults)
                {
                    FeedResponse<Document> response = await iterator.ReadNextAsync();
                    Assert.IsTrue(response.StatusCode.IsSuccess());

                    documents.AddRange(response.Resource);
                }

                Assert.AreEqual(Documents.Count, documents.Count);
            }
        }

        private sealed class Document
        {
            public string Id { get; set; }
            public double Distance { get; set; }
        }
    }
}
