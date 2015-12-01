---
services: search
platforms: dotnet
author: liamca
---

# Azure Search .NET Management API Demo

The Azure Search Service Management API provides programmatic access to much of the functionality available through the Azure Search extension within the Azure Preview Management Portal. The management API is a REST API. Its purpose is to allow administrators to automate common tasks typically performed from the Azure Portal UI.

Examples of programmable operations include:
- Create or delete Azure search services.
- Alter the scale of an Azure Search service in response to changes in query scale or storage requirements. Increasing or decreasing replicas or partitions affects billing and will change the number of Search Units allocated to a service.
- Create or change api-keys. This allows administrators to automate regular changes to administrative keys to the Azure Search service.

See Get started with Azure Search REST API http://msdn.microsoft.com/library/azure/dn798935.aspx for a walkthrough of sample code that demonstrates the API.

To contact me, please visit my blog http://blog.liamcavanagh.com/ or reach out to me at Twitter http://twitter.com/liamca/
