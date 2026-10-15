using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class BaqClientTests
{

    sealed class BaqRow
    {

        /// <summary>
        /// Customer ID column as the BAQ names it.
        /// </summary>
        public string Customer_CustID { get; set; } = string.Empty;

        /// <summary>
        /// Order total column as the BAQ names it.
        /// </summary>
        public decimal OrderHed_TotalCharges { get; set; }

    }

    /// <summary>
    /// OData options and BAQ parameters both land in the query string of the Data URL.
    /// </summary>
    [Fact]
    public async Task Query_builds_data_url_with_odata_options_and_baq_parameters()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """
            {"@odata.context":"https://x/$metadata#Data","value":[{"Customer_CustID":"ACME","OrderHed_TotalCharges":"1250.75"}]}
            """);
        var client = provider.GetRequiredService<IKineticBaqClient>();

        var rows = await client.QueryAsync<BaqRow>("OpenOrders", new BaqQuery
        {
            Filter = "Customer_CustID eq 'ACME'",
            Top = 5,
            Parameters = new Dictionary<string, string> { ["StartDate"] = "2026-01-01" },
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://example.epicorsaas.com/prod/api/v2/odata/100/BaqSvc/OpenOrders/Data" +
            "?$filter=Customer_CustID%20eq%20%27ACME%27&$top=5&StartDate=2026-01-01",
            request.Uri.AbsoluteUri);
        Assert.Null(request.Body);
        var row = Assert.Single(rows);
        Assert.Equal("ACME", row.Customer_CustID);
        Assert.Equal(1250.75m, row.OrderHed_TotalCharges);
    }

    /// <summary>
    /// A BAQ with no options hits the bare Data URL, and no rows yields an empty list rather than null.
    /// </summary>
    [Fact]
    public async Task Query_without_options_hits_bare_data_url_and_returns_empty_list_for_no_rows()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"value":[]}""");
        var client = provider.GetRequiredService<IKineticBaqClient>();

        var rows = await client.QueryAsync<BaqRow>("OpenOrders");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://example.epicorsaas.com/prod/api/v2/odata/100/BaqSvc/OpenOrders/Data", request.Uri.AbsoluteUri);
        Assert.Empty(rows);
    }

    /// <summary>
    /// Updating through an updatable BAQ patches the Data endpoint with the row as JSON.
    /// </summary>
    [Fact]
    public async Task UpdateRow_patches_the_data_endpoint()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"Customer_CustID":"ACME","OrderHed_TotalCharges":99}""");
        var client = provider.GetRequiredService<IKineticBaqClient>();

        var stored = await client.UpdateRowAsync("OpenOrders", new BaqRow { Customer_CustID = "ACME", OrderHed_TotalCharges = 99 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new HttpMethod("PATCH"), request.Method);
        Assert.EndsWith("/BaqSvc/OpenOrders/Data", request.Uri.AbsoluteUri);
        Assert.Equal("""{"Customer_CustID":"ACME","OrderHed_TotalCharges":99}""", request.Body);
        Assert.Equal(99m, stored?.OrderHed_TotalCharges);
    }

    /// <summary>
    /// Queries and writes carry a call's own settings; values the call leaves null keep the configured ones.
    /// </summary>
    [Fact]
    public async Task Call_settings_apply_to_queries_and_writes()
    {
        var (provider, handler) = TestHost.Build(options => options.Language = "en");
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"value":[]}""");
        var client = provider.GetRequiredService<IKineticBaqClient>();
        var west = new KineticCallSettings { Plant = "West" };

        await client.QueryAsync<BaqRow>("OpenOrders", null, west);
        await client.AddRowAsync("OpenOrders", new BaqRow { Customer_CustID = "ACME" }, west);
        await client.UpdateRowAsync("OpenOrders", new BaqRow { Customer_CustID = "ACME" }, west);
        await client.QueryAsync<BaqRow>("OpenOrders");

        const string West = """{"Company":"100","Plant":"West","Language":"en"}""";
        Assert.All(handler.Requests.Take(3), r => Assert.Equal(West, r.Header("CallSettings")));
        Assert.Equal("""{"Company":"100","Language":"en"}""", handler.Requests[3].Header("CallSettings"));
    }

}
