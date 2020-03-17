## Cosmos503_9000

<table>
<tr>
  <td>TypeName</td>
  <td>Cosmos503_9000SNATPortExhuastion</td>
</tr>
<tr>
  <td>CheckId</td>
  <td>Cosmos503_9000</td>
</tr>
<tr>
  <td>Category</td>
  <td>Connectivity</td>
</tr>
</table>

## Issue

If your app is deployed on Azure Virtual Machines without a public IP address, by default [Azure SNAT ports](https://docs.microsoft.com/azure/load-balancer/load-balancer-outbound-connections#preallocatedports) establish connections to any endpoint outside of your VM. The number of connections allowed from the VM to the Azure Cosmos DB endpoint is limited by the [Azure SNAT configuration](https://docs.microsoft.com/azure/load-balancer/load-balancer-outbound-connections#preallocatedports).

 Azure SNAT ports are used only when your VM has a private IP address and a process from the VM tries to connect to a public IP address.

## Troubleshooting steps

There are two workarounds to avoid Azure SNAT limitation:

* Add your Azure Cosmos DB service endpoint to the subnet of your Azure Virtual Machines virtual network. For more information, see [Azure Virtual Network service endpoints](https://docs.microsoft.com/azure/virtual-network/virtual-network-service-endpoints-overview). 

    When the service endpoint is enabled, the requests are no longer sent from a public IP to Azure Cosmos DB. Instead, the virtual network and subnet identity are sent. This change might result in firewall drops if only public IPs are allowed. If you use a firewall, when you enable the service endpoint, add a subnet to the firewall by using [Virtual Network ACLs](https://docs.microsoft.com/azure/virtual-network/virtual-networks-acl).
* Assign a public IP to your Azure VM.

## Related documentation
* [Diagnose and troubleshoot issues when using Azure Cosmos DB .NET SDK](https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk)
