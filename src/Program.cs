using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

namespace ManagementAPI
{
    // In order to use the Azure Resource Manager API you need to prepare the target subscription. This is discussed
    // in detail here:
    // http://msdn.microsoft.com/en-us/library/azure/dn790557.aspx

    class Program
    {
        // You can obtain this information from the Azure management portal. The instructions in the link above 
        // include details for this as well.
        private const string TenantId = "<your tenant id>";
        private const string ClientId = "<your client id>";
        private const string SubscriptionId = "<your subscription id>";

        // This is the return URL you configure during AD client application setup. For this type of apps (non-web apps)
        // you can set this to something like http://localhost/testapp. The important thing is that the URL here and the
        // URL in AD configuration match.
        private static readonly Uri RedirectUrl = new Uri("<your redirect url");

        private static readonly Random _random = new Random();

        private static string _authorizationToken = null;

        static void Main(string[] args)
        {
            HttpResponseMessage response;

            // Get some general subscription information, not Azure Search specific
            response = ExecuteArmRequest(HttpMethod.Get, "?api-version=2014-04-01-Preview");
            DumpResponse("Subscription data", response);

            // Register the Azure Search resource provider with the subscription. In the Azure Resource Manager model, you need
            // to register a resource provider in a subscription before you can use it. 
            // You only need to do this once per subscription/per resource provider.
            // More details here:
            // http://msdn.microsoft.com/en-us/library/azure/dn790548.aspx
            response = ExecuteArmRequest(HttpMethod.Post, "providers/Microsoft.Search/register?api-version=2014-04-01");
            DumpResponse("Azure Search resource provider registration", response);

            // List all search services in the subscription that are in a specific resource group. In this case 
            // we use "Default-Web-WestUS", change this to whatever resource group you might have in your environment or
            // list all resource groups first to see what's available. How to list resource groups is detailed here:
            // http://msdn.microsoft.com/en-us/library/azure/dn790529.aspx
            response = ExecuteArmRequest(HttpMethod.Get, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices?api-version=2014-07-31-Preview");
            DumpResponse("Azure Search services", response);

            // Create a new free search service called "sample#" (# is a random number, to make it less likely to have collisions)
            // NOTE: if you already have a free service in this subscription this operation will fail
            string name = "sample" + _random.Next(0, 1000000).ToString();
            response = ExecuteArmRequest(HttpMethod.Put,
                                         "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "?api-version=2014-07-31-Preview",
                                         new
                                         {
                                             type = "Microsoft.Search/searchServices",
                                             location = "West US",
											 sku = new { name = "standard" }, // use "standard" for standard services
                                             properties = new
                                             {
                                                 partitionCount = 1,
                                                 replicaCount = 1
                                             }
                                         });
										 									 
            DumpResponse("Create new Azure Search service", response);

            // Retrieve service definition
            response = ExecuteArmRequest(HttpMethod.Get, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "?api-version=2014-07-31-Preview");
            DumpResponse("Azure Search service definition", response);

            // Retrieve service admin API keys
            response = ExecuteArmRequest(HttpMethod.Post, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "/listAdminKeys?api-version=2014-07-31-Preview");
            DumpResponse("Azure Search service admin keys", response);

            // Re-generate secondary admin API key
            // (use /primary to regenerate the primary admin API key)
            response = ExecuteArmRequest(HttpMethod.Post, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "/regenerateAdminKey/secondary?api-version=2014-07-31-Preview");
            DumpResponse("Re-generated Azure Search secondary admin key", response);

            // Create a new query API key
            response = ExecuteArmRequest(HttpMethod.Post, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "/createQueryKey/myQueryKey?api-version=2014-07-31-Preview");
            DumpResponse("Created new Azure Search service query key", response);

            // Retrieve query API keys
            response = ExecuteArmRequest(HttpMethod.Get, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "/listQueryKeys?api-version=2014-07-31-Preview");
            DumpResponse("List Azure Search service query keys", response);

            // Delete a query API key (pick the first one)
            string key = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().Result).value[0].key;
            response = ExecuteArmRequest(HttpMethod.Delete, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "/deleteQueryKey/" + key + "?api-version=2014-07-31-Preview");
            DumpResponse("Delete Azure Search service query key", response);

            // Change service replica/partition count
            // NOTE: this will fail unless you change the service creation code above to make it a "standard" service and wait until it's provisioned
            response = ExecuteArmRequest(HttpMethod.Put,
                                         "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "?api-version=2014-07-31-Preview",
                                         new
                                         {
                                             type = "Microsoft.Search/searchServices",
                                             location = "West US",
                                             properties = new
                                             {
                                                 sku = new { name = "standard" },
                                                 partitionCount = 1,
                                                 replicaCount = 2
                                             }
                                         });
            DumpResponse("Dynamically scale Azure Search service", response);

            // Delete search service
            response = ExecuteArmRequest(HttpMethod.Delete, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "?api-version=2014-07-31-Preview");
            DumpResponse("Delete Azure Search service", response);
        }

        private static void ProvisionAndWait()
        {
            // This method shows an example of a long-running operation that will need polling to determine if it's done. This same
            // pattern applies both to provisioning of standard services and to scale-up/-down operations. 
            // Note that provisioning of free services doesn't need this since it typically completes instantaneously.

            HttpResponseMessage response;

            // Create a new "standard" search service called "sample#" (# is a random number, to make it less likely to have collisions)
            // NOTE: standard services have a cost that will be charged to the Azure account you're using to run this sample
            string name = "sample" + _random.Next(0, 1000000).ToString();
            response = ExecuteArmRequest(HttpMethod.Put,
                                         "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + name + "?api-version=2014-07-31-Preview",
                                         new
                                         {
                                             type = "Microsoft.Search/searchServices",
                                             location = "West US",
                                             properties = new
                                             {
                                                 sku = new { name = "standard" }, // use "standard" for standard services
                                                 partitionCount = 1,
                                                 replicaCount = 1
                                             }
                                         });
            DumpResponse("Create new Azure Search service", response);

            Console.WriteLine("Waiting for provisioning operation to complete");
            WaitForProvisioningOperation(name);
            Console.WriteLine("Provisioning operation completed");
        }

        private static void WaitForProvisioningOperation(string serviceName)
        {
            string state = null;

            do
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
                HttpResponseMessage response = ExecuteArmRequest(HttpMethod.Get, "resourcegroups/Default-Web-WestUS/providers/Microsoft.Search/searchServices/" + serviceName + "?api-version=2014-07-31-Preview");
                state = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().Result).properties.provisioningState;
                Console.WriteLine("Service state: {0}", state);
            } while (state == "provisioning");

            if (state != "succeeded")
            {
                throw new Exception("Provisioning operation failed. Service state: " + state);
            }
        }

        private static void DumpResponse(string title, HttpResponseMessage response)
        {
            Console.WriteLine(title);
            Console.WriteLine("Request: {0} {1}", response.RequestMessage.Method, response.RequestMessage.RequestUri);
            Console.WriteLine("Status: {0}", response.StatusCode);
            Console.WriteLine();

            if (response.Content != null)
            {
                // Round-trip this through a JSON serializer to get good formatting
                string json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result), Formatting.Indented);
                Console.WriteLine(json);
            }

            Console.WriteLine();
            Console.WriteLine("----------------------------------------------------");
        }

        private static HttpResponseMessage ExecuteArmRequest(HttpMethod httpMethod, string relativeUrl, object requestBody = null)
        {
            Uri baseUrl = new Uri("https://management.azure.com/subscriptions/" + SubscriptionId + "/");

            // We assume this application runs for a short period of time and the obtained token won't expire. Refer
            // to documentation for how to detect and refresh expired tokens
            if (_authorizationToken == null)
            {
                _authorizationToken = GetAuthorizationHeader();
            }

            HttpClient client = new HttpClient(); // If you'll make many requests you'll want to reuse this instance

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, new Uri(baseUrl, relativeUrl));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorizationToken);

            if (requestBody != null)
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = client.SendAsync(request).Result;

            return response;
        }

        // This method taken from: http://msdn.microsoft.com/en-us/library/azure/dn790557.aspx
        private static string GetAuthorizationHeader()
        {
            AuthenticationResult result = null;

            var context = new AuthenticationContext("https://login.windows.net/" + TenantId);

            var thread = new Thread(() =>
            {
                result = context.AcquireToken(
                  "https://management.core.windows.net/",
                  ClientId,
                  RedirectUrl,
                  PromptBehavior.Always);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = "AquireTokenThread";
            thread.Start();
            thread.Join();

            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }

            string token = result.AccessToken;
            return token;
        }
    }
}
