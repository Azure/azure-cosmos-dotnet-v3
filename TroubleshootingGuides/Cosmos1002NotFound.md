## Cosmos1002

<table>
<tr>
  <td>TypeName</td>
  <td>Cosmos1002NotFound</td>
</tr>
<tr>
  <td>CheckId</td>
  <td>Cosmos1002</td>
</tr>
<tr>
  <td>Category</td>
  <td>Service</td>
</tr>
</table>

## Description

This status code represents that the resource no longer exists. 

## Known issues

The document does exists, but still returns a 404. 

### Cause 1: Race condition 
There is multiple SDK client instances and the read happened before the write.

### Solution
1. For session consistency the create item will return a session token that can be passed between SDK instances to guarantee that the read request is reading from a replica with that change.
2. Change the consistency level to a stronger level

### Cause 2: Invalid chacters in id field
For this scenario use query to get the item and replace/escape the invalid characters.
