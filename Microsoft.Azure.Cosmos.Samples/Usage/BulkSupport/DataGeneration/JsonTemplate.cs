using Newtonsoft.Json;

public partial class BingTemplate
{
    [JsonProperty("partitionKey")]
    public string Partitionkey { get; set; }

    [JsonProperty("Device")]
    public Device Device { get; set; }

    [JsonProperty("EventId")]
    public string EventId { get; set; }

    [JsonProperty("N")]
    public int N { get; set; }

    [JsonProperty("SessionId")]
    public string SessionId { get; set; }

    [JsonProperty("Level")]
    public int Level { get; set; }

    [JsonProperty("Timestamp")]
    public Timestamp Timestamp { get; set; }

    [JsonProperty("Ingestion")]
    public Ingestion Ingestion { get; set; }

    [JsonProperty("Source")]
    public Source Source { get; set; }

    [JsonProperty("User")]
    public User User { get; set; }

    [JsonProperty("Location")]
    public Location Location { get; set; }

    [JsonProperty("Type")]
    public string Type { get; set; }

    [JsonProperty("Session")]
    public Session Session { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }
}

public partial class Device
{
    [JsonProperty("Architecture")]
    public string Architecture { get; set; }

    [JsonProperty("OS")]
    public string Os { get; set; }
}

public partial class Ingestion
{
    [JsonProperty("Datacenter")]
    public string Datacenter { get; set; }

    [JsonProperty("Environment")]
    public string Environment { get; set; }

    [JsonProperty("IP")]
    public string Ip { get; set; }

    [JsonProperty("Instance")]
    public int Instance { get; set; }

    [JsonProperty("Role")]
    public string Role { get; set; }

    [JsonProperty("Time")]
    public int Time { get; set; }

    [JsonProperty("TimeCorrection")]
    public long TimeCorrection { get; set; }

    [JsonProperty("Version")]
    public string Version { get; set; }
}

public partial class Location
{
    [JsonProperty("City")]
    public string City { get; set; }

    [JsonProperty("Country")]
    public string Country { get; set; }

    [JsonProperty("Lat")]
    public double Lat { get; set; }

    [JsonProperty("Lon")]
    public double Lon { get; set; }

    [JsonProperty("Source")]
    public string Source { get; set; }
}

public partial class Session
{
    [JsonProperty("Count")]
    public int Count { get; set; }

    [JsonProperty("Duration")]
    public int Duration { get; set; }

    [JsonProperty("State")]
    public string State { get; set; }
}

public partial class Source
{
    [JsonProperty("App")]
    public string App { get; set; }

    [JsonProperty("Culture")]
    public string Culture { get; set; }

    [JsonProperty("DeploymentId")]
    public string DeploymentId { get; set; }

    [JsonProperty("Group")]
    public string Group { get; set; }

    [JsonProperty("Market")]
    public string Market { get; set; }

    [JsonProperty("Publisher")]
    public string Publisher { get; set; }

    [JsonProperty("Version")]
    public string Version { get; set; }
}

public partial class Timestamp
{
    [JsonProperty("Offset")]
    public int Offset { get; set; }

    [JsonProperty("Time")]
    public string Time { get; set; }
}

public partial class User
{
    [JsonProperty("CEIP")]
    public string Ceip { get; set; }

    [JsonProperty("UserId")]
    public string UserId { get; set; }
}