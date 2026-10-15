using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Client;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class ODataContextFactoryTests
{

    internal const string ServiceRoot = "https://example.epicorsaas.com/prod/api/v2/odata/100/Erp.BO.CustomerSvc/";

    const string CustomerMetadata = """
        <?xml version="1.0" encoding="utf-8"?>
        <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
          <edmx:DataServices>
            <Schema Namespace="Erp.BO" xmlns="http://docs.oasis-open.org/odata/ns/edm">
              <EntityType Name="Customer">
                <Key>
                  <PropertyRef Name="Company" />
                  <PropertyRef Name="CustNum" />
                </Key>
                <Property Name="Company" Type="Edm.String" Nullable="false" />
                <Property Name="CustNum" Type="Edm.Int32" Nullable="false" />
                <Property Name="CustID" Type="Edm.String" />
                <Property Name="Name" Type="Edm.String" />
              </EntityType>
              <EntityContainer Name="Container">
                <EntitySet Name="Customers" EntityType="Erp.BO.Customer" />
              </EntityContainer>
            </Schema>
          </edmx:DataServices>
        </edmx:Edmx>
        """;

    const string CustomerRows = """
        {"@odata.context":"https://example.epicorsaas.com/prod/api/v2/odata/100/Erp.BO.CustomerSvc/$metadata#Customers",
         "value":[{"Company":"100","CustNum":1,"CustID":"ACME","Name":"Acme Corp"}]}
        """;

    internal static bool IsMetadata(CapturedRequest request)
    {
        return request.Uri.AbsoluteUri.EndsWith("$metadata", StringComparison.Ordinal);
    }

    internal static HttpResponseMessage FakeKineticService(HttpRequestMessage request)
    {
        if (request.RequestUri!.AbsoluteUri.EndsWith("$metadata", StringComparison.Ordinal))
            return FakeHttpHandler.Text(200, CustomerMetadata, "application/xml");

        var response = FakeHttpHandler.Json(200, CustomerRows);
        response.Headers.Add("OData-Version", "4.0");
        return response;
    }

    internal static Task<System.Collections.Generic.IEnumerable<Customer>> QueryAcme(DataServiceContext context)
    {
        var query = (DataServiceQuery<Customer>)context.CreateQuery<Customer>("Customers").Where(c => c.CustID == "ACME").Take(1);
        return query.ExecuteAsync();
    }

    /// <summary>
    /// A LINQ query on a service context is sent through the authenticated pipeline and materialised into entities.
    /// </summary>
    [Fact]
    public async Task ForService_queries_through_the_authenticated_pipeline()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = FakeKineticService;
        var factory = provider.GetRequiredService<IKineticODataContextFactory>();

        var customers = (await QueryAcme(factory.ForService("Erp.BO.CustomerSvc"))).ToList();

        var customer = Assert.Single(customers);
        Assert.Equal(1, customer.CustNum);
        Assert.Equal("Acme Corp", customer.Name);

        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(TestHost.ApiKey, request.Header("x-api-key"));
            Assert.Equal(TestHost.ExpectedBasicAuth, request.Header("Authorization"));
        });

        var metadata = Assert.Single(handler.Requests, IsMetadata);
        Assert.Equal(ServiceRoot + "$metadata", metadata.Uri.AbsoluteUri);

        var query = handler.Requests.Last();
        Assert.StartsWith(ServiceRoot + "Customers?", query.Uri.AbsoluteUri);
        var queryString = Uri.UnescapeDataString(query.Uri.Query);
        Assert.Contains("$filter=CustID eq 'ACME'", queryString);
        Assert.Contains("$top=1", queryString);
    }

    /// <summary>
    /// A context made with call settings sends its queries with them; the shared metadata download does not carry them.
    /// </summary>
    [Fact]
    public async Task ForService_with_call_settings_sends_them_on_each_query()
    {
        var (provider, handler) = TestHost.Build(options => options.Plant = "MfgSys");
        handler.Respond = FakeKineticService;
        var factory = provider.GetRequiredService<IKineticODataContextFactory>();

        await QueryAcme(factory.ForService("Erp.BO.CustomerSvc", new KineticCallSettings { Plant = "West" }));
        await QueryAcme(factory.ForService("Erp.BO.CustomerSvc"));

        Assert.Equal("""{"Company":"100","Plant":"MfgSys"}""", Assert.Single(handler.Requests, IsMetadata).Header("CallSettings"));
        var queries = handler.Requests.Where(r => !IsMetadata(r)).ToList();
        Assert.Equal("""{"Company":"100","Plant":"West"}""", queries[0].Header("CallSettings"));
        Assert.Equal("""{"Company":"100","Plant":"MfgSys"}""", queries[1].Header("CallSettings"));
    }

    /// <summary>
    /// The service model is downloaded once per service root and reused by later contexts.
    /// </summary>
    [Fact]
    public async Task Metadata_is_fetched_once_per_service_root()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = FakeKineticService;
        var factory = provider.GetRequiredService<IKineticODataContextFactory>();

        await QueryAcme(factory.ForService("Erp.BO.CustomerSvc"));
        await QueryAcme(factory.ForService("Erp.BO.CustomerSvc"));

        Assert.Equal(1, handler.Requests.Count(IsMetadata));
        Assert.Equal(2, handler.Requests.Count(r => !IsMetadata(r)));
    }

    /// <summary>
    /// The service root is the v2 OData path for the service, ending with a slash.
    /// </summary>
    [Fact]
    public void ServiceRoot_is_the_v2_odata_path_for_the_service()
    {
        var (provider, _) = TestHost.Build();
        var factory = provider.GetRequiredService<IKineticODataContextFactory>();

        Assert.Equal(new Uri(ServiceRoot), factory.ServiceRoot("Erp.BO.CustomerSvc"));
        Assert.Equal(new Uri(ServiceRoot), factory.ForService("Erp.BO.CustomerSvc").BaseUri);
    }

    /// <summary>
    /// A BAQ context is rooted at the BaqSvc path for that query.
    /// </summary>
    [Fact]
    public void ForBaq_roots_the_context_at_the_baq_service()
    {
        var (provider, _) = TestHost.Build();
        var factory = provider.GetRequiredService<IKineticODataContextFactory>();

        var context = factory.ForBaq("OpenOrders");

        Assert.Equal(new Uri("https://example.epicorsaas.com/prod/api/v2/odata/100/BaqSvc/OpenOrders/"), context.BaseUri);
    }

}
