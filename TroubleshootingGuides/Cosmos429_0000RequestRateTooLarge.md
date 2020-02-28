## Cosmos429_0000

<table>
<tr>
  <td>TypeName</td>
  <td>Cosmos429_0000RequestRateTooLarge</td>
</tr>
<tr>
  <td>CheckId</td>
  <td>Cosmos429_0000</td>
</tr>
<tr>
  <td>Category</td>
  <td>Service</td>
</tr>
</table>

## Issue

Request rate too large' or error code 429 indicates that your requests are being throttled, because the consumed throughput (RU/s) has exceeded the provisioned throughput. The SDK will automatically retry requests based on the specified retry policy. If you get this failure often, consider increasing the throughput on the collection. Check the portal’s metrics to see if you are getting 429 errors. Review your partition key to ensure it results in an even distribution of storage and request volume.

## Solution

Use the portal or the SDK to increase the throttling.