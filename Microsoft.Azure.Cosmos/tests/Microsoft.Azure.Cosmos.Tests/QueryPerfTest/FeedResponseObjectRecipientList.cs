using Newtonsoft.Json;

internal sealed class RecipientList
{
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "city")]
    public string City { get; set; }

    [JsonProperty(PropertyName = "postalcode")]
    public string PostalCode { get; set; }

    [JsonProperty(PropertyName = "region")]
    public string Region { get; set; }

    [JsonProperty(PropertyName = "guid")]
    public string GUID { get; set; }

    [JsonProperty(PropertyName = "quantity")]
    public int Quantity { get; set; }
}