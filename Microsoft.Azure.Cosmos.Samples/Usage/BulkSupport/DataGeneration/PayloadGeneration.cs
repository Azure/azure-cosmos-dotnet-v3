namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.IO;
    using Newtonsoft.Json;
    using System.Linq;

    /// <summary>
    /// Payload Generator
    /// </summary>
    internal sealed class PayloadGenerator
    {
        private Random m_random;
        private string[] m_keys;
        private int m_numCharsPerStringValue;

        private string jsonDocument;

        String[] devicesArch = new String[] { "x64", "x86", "arm" };
        String[] os = new String[] { "Windows8", "Windows8.1" };
        int[] level = new int[] { 1, 2 };
        int[] offset = new int[] { 7200, 3600 };
        String[] datacenter = new String[] { "East US", "West US", "East Europe", "West Europe" };
        int[] instance = new int[] { 1, 2 };

        String[] app = new String[] { "BingWeather", "BingNews", "BingTravel", "Bing Sports" };
        String[] culture = new string[] { "en-GB", "fr-FR" , "sr-Latn-RS", "de-DE", "pt-PT", "pl-pl", "nl-NL", "tr-tr",
            "ru-ru", "it-IT", "nl-BE", "es-ES"};

        String[] city = new String[] { "monheim am rhein", "Belgrade", "Monterrey", "Buenos Aires", "Rome", "Florence" };
        String[] country = new String[] { "Germany", "Serbia", "Mexico", "Argentina", "Italy" };

        String[] state = new String[] { "suspend", "start" };

        //BingTemplate m_template;

        public PayloadGenerator(string[] keys, int numCharsPerStringValue)
        {
            m_random = new Random();
            m_keys = keys;
            m_numCharsPerStringValue = numCharsPerStringValue;

            jsonDocument = @"
            {
                ""partitionKey"": ""lol"",
              ""Device"": {
                                ""Architecture"": ""(Type:String, Values:x64$x86$arm)"",
                ""OS"": ""(Type:String, Values:Windows/8$Windows/8.1)""
              },
              ""EventId"": ""(Type:Guid,IncludedPath:true)"",
              ""N"": 1,
              ""SessionId"": ""(Type:Guid,Uniqueness:10)"",
              ""Level"": 1,
              ""Timestamp"": {
                                ""Offset"": 1,
                ""Time"": ""(Type:DateTime, DateType:Ticks)""
              },
              ""Ingestion"": {
                                ""Datacenter"": ""(Type:String, Values:East US$West US$East Europe$West Europe)"",
                ""Environment"": ""PROD"",
                ""IP"": ""127.0.0.1"",
                ""Instance"": 1,
                ""Role"": ""Collector"",
                ""Time"": 1,
                ""TimeCorrection"": 6199,
                ""Version"": ""1.0.52528.0""
              },
              ""Source"": {
                                ""App"": ""(Type:String, Values:BingWeather$BingNews$BingTravel$BingSports)"",
                ""Culture"": ""(Type:String, Values:en-GB$fr-FR$sr-Latn-RS$de-DE$pt-PT$pl-pl$nl-NL$tr-tr$ru-ru$it-IT$nl-BE$es-ES)"",
                ""DeploymentId"": ""(Type:Guid)"",
                ""Group"": ""OSD/Appex"",
                ""Market"": ""(Type:String, Values:en-GB$fr-FR$sr-Latn-RS$de-DE$pt-PT$pl-pl$nl-NL$tr-tr$ru-ru$it-IT$nl-BE$es-ES)"",
                ""Publisher"": ""Microsoft"",
                ""Version"": ""1.5.1.245""
              },
              ""User"": {
                                ""CEIP"": ""os"",
                ""UserId"": ""(Type:Guid)""
              },
              ""Location"": {
                                ""City"": ""(Type:String, Values:monheim am rhein$Belgrade$Monterrey$Buenos Aires$Rome$Florence)"",
                ""Country"": ""(Type:String, Values:Germany$Serbia$Mexico$Argentina$Italy)"",
                ""Lat"": 1.11143436789,
                ""Lon"": 2.22454647637,
                ""Source"": ""server""
              },
              ""Type"": ""Session"",
              ""Session"": {
                                ""Count"": 1,
                ""Duration"": 1,
                ""State"": ""(Type:String, Values:suspend$start)""
              },
              ""id"": ""(Type:Guid)""
            }";

            //m_template = JsonConvert.DeserializeObject<BingTemplate>(jsonDocument);
        }

        /// <summary>
        /// Generate payload.
        /// </summary>
        /// <returns></returns>
        public string GeneratePayload(string partitionKey)
        {
            BingTemplate m_template = new BingTemplate();
            m_template.Partitionkey = partitionKey;

            m_template.Device = new Device();
            m_template.Device.Architecture = devicesArch[m_random.Next(0, devicesArch.Length)];
            m_template.Device.Os = os[m_random.Next(0, os.Length)];

            m_template.EventId = Guid.NewGuid().ToString();
            m_template.N = m_random.Next(1, 1500);
            m_template.SessionId = Guid.NewGuid().ToString();
            m_template.Level = level[m_random.Next(0, level.Length)];

            m_template.Timestamp = new Timestamp();
            m_template.Timestamp.Offset = offset[m_random.Next(0, offset.Length)];
            m_template.Timestamp.Time = DateTime.Now.AddDays(m_random.Next(10000)).ToString();

            m_template.Ingestion = new Ingestion();
            m_template.Ingestion.Datacenter = datacenter[m_random.Next(0, datacenter.Length)];
            m_template.Ingestion.Environment = "PROD";
            m_template.Ingestion.Ip = "127.0.0.1";
            m_template.Ingestion.Instance = instance[m_random.Next(0, instance.Length)];
            m_template.Ingestion.Role = "Collector";
            m_template.Ingestion.Time = m_random.Next();
            m_template.Ingestion.TimeCorrection = 6199;
            m_template.Ingestion.Version = "1.0.52528.0";

            int randArrayLength = m_random.Next(0, 12);
            m_template.Source = new Source[randArrayLength];

            for (int i = 0; i < randArrayLength; i++)
            {
                m_template.Source[i] = new Source();

                m_template.Source[i].App = app[m_random.Next(0, app.Length)];
                m_template.Source[i].Culture = culture[m_random.Next(0, culture.Length)];

                int localLenght = m_random.Next(0, culture.Length);
                string[] localResult = new string[localLenght];
                Array.Copy(culture, culture.Length - localLenght, localResult, 0, localLenght);

                m_template.Source[i].CultureList = localResult;

                m_template.Source[i].DeploymentId = Guid.NewGuid().ToString();
                m_template.Source[i].Group = "OSD/Appex";
                m_template.Source[i].Market = culture[m_random.Next(0, culture.Length)];
                m_template.Source[i].Publisher = "Microsoft";
                m_template.Source[i].Version = "1.5.1.245";
            }

            m_template.User = new User();
            m_template.User.Ceip = "os";
            m_template.User.UserId = Guid.NewGuid().ToString();

            m_template.Location = new Location();
            m_template.Location.City = city[m_random.Next(0, city.Length)];
            m_template.Location.Country = country[m_random.Next(0, country.Length)];

            double lat = m_random.NextDouble() * (90 - 0) + 0;
            double longitude = m_random.NextDouble() * (180 - 0) + 0;

            m_template.Location.Lat = lat;
            m_template.Location.Lon = longitude;
            m_template.Location.Source = "Server";

            m_template.Type = "Session";

            m_template.Session = new Session();
            m_template.Session.Count = m_random.Next(0, 20);
            m_template.Session.Duration = m_random.Next(10, 3600);
            m_template.Session.State = state[m_random.Next(0, state.Length)];

            m_template.Id = Guid.NewGuid().ToString();

            string json = JsonConvert.SerializeObject(m_template).ToString();

            if(String.IsNullOrEmpty(m_template.Partitionkey.ToString()))
            {
                Console.WriteLine("Using null or empty partitionId");
            }

            return json;
        }

        /// <summary>
        /// Generate a large string value.
        /// </summary>
        /// <returns></returns>
        private string GenerateLargeString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < m_numCharsPerStringValue; i++)
            {
                // All printable characters
                int randomCharValue = m_random.Next(0x20, 0x7F);
                // Special handeling for double quote (0x2c) and back slash (0x5c) - switch to the next character in the sequence
                if (randomCharValue == 0x22 || randomCharValue == 0x5c) ++randomCharValue;

                char ch = Convert.ToChar(randomCharValue);
                stringBuilder.Append(ch);
            }

            return stringBuilder.ToString();
        }

    }
}