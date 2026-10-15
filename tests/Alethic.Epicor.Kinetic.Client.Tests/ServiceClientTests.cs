using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class ServiceClientTests
{

    sealed class CustomerTableset
    {

        /// <summary>
        /// Rows of the Customer table.
        /// </summary>
        public List<CustomerRow> Customer { get; set; } = new();

    }

    sealed class CustomerRow
    {

        /// <summary>
        /// Company the customer belongs to.
        /// </summary>
        public string Company { get; set; } = string.Empty;

        /// <summary>
        /// Internal customer number.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>
        /// User-facing customer ID.
        /// </summary>
        public string CustID { get; set; } = string.Empty;

        /// <summary>
        /// Customer name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Row modification flag for Update calls.
        /// </summary>
        public string RowMod { get; set; } = string.Empty;

    }

    /// <summary>
    /// A method call posts camelCase parameters to the service URL and the return value comes back as returnObj.
    /// </summary>
    [Fact]
    public async Task Call_posts_named_parameters_and_reads_returnObj()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """
            {"returnObj":{"Customer":[{"Company":"100","CustNum":1,"CustID":"ACME","Name":"Acme Corp","RowMod":""}]},"parameters":{}}
            """);
        var client = provider.GetRequiredService<IKineticServiceClient>();

        var response = await client.CallAsync<BoResponse<CustomerTableset>>("Erp.BO.CustomerSvc", "GetByID", new { custNum = 1 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.epicorsaas.com/prod/api/v2/odata/100/Erp.BO.CustomerSvc/GetByID", request.Uri.AbsoluteUri);
        Assert.Equal("""{"custNum":1}""", request.Body);
        Assert.NotNull(response?.ReturnObj);
        var row = Assert.Single(response.ReturnObj.Customer);
        Assert.Equal("ACME", row.CustID);
    }

    /// <summary>
    /// Tableset rows keep their PascalCase column names when serialised.
    /// </summary>
    [Fact]
    public async Task Call_serialises_tableset_rows_with_declared_casing()
    {
        var (provider, handler) = TestHost.Build();
        var client = provider.GetRequiredService<IKineticServiceClient>();
        var ds = new CustomerTableset
        {
            Customer = new List<CustomerRow>
            {
                new() { Company = "100", CustNum = 1, CustID = "ACME", Name = "Acme Corp", RowMod = RowMod.Updated },
            },
        };

        await client.CallAsync("Erp.BO.CustomerSvc", "Update", new { ds });

        var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!).RootElement;
        var row = body.GetProperty("ds").GetProperty("Customer")[0];
        Assert.Equal("U", row.GetProperty("RowMod").GetString());
        Assert.Equal("ACME", row.GetProperty("CustID").GetString());
    }

    /// <summary>
    /// The escape hatch sends any relative path, including OData query options, without a body.
    /// </summary>
    [Fact]
    public async Task Send_allows_any_relative_path()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"value":[{"UserID":"svc.user"}]}""");
        var client = provider.GetRequiredService<IKineticServiceClient>();

        var result = await client.SendAsync<JsonElement>(HttpMethod.Get, "api/v2/odata/100/Ice.BO.UserFileSvc/UserFiles?$top=1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.epicorsaas.com/prod/api/v2/odata/100/Ice.BO.UserFileSvc/UserFiles?$top=1", request.Uri.AbsoluteUri);
        Assert.Null(request.Body);
        Assert.Equal("svc.user", result.GetProperty("value")[0].GetProperty("UserID").GetString());
    }

    /// <summary>
    /// Configuring a plant adds the CallSettings header.
    /// </summary>
    [Fact]
    public async Task CallSettings_header_is_sent_when_plant_is_configured()
    {
        var (provider, handler) = TestHost.Build(options => options.Plant = "MfgSys");
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.CallAsync("Erp.BO.PartSvc", "GetList", new { whereClause = "", pageSize = 1, absolutePage = 0 });

        Assert.Equal("""{"Company":"100","Plant":"MfgSys"}""", Assert.Single(handler.Requests).Header("CallSettings"));
    }

    /// <summary>
    /// A call's own plant replaces the configured one for that call only.
    /// </summary>
    [Fact]
    public async Task Call_settings_replace_the_configured_plant_for_one_call()
    {
        var (provider, handler) = TestHost.Build(options => options.Plant = "MfgSys");
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.CallAsync("Erp.BO.CustShipSvc", "GetRows", null, new KineticCallSettings { Plant = "West" });
        await client.CallAsync("Erp.BO.CustShipSvc", "GetRows");

        Assert.Equal("""{"Company":"100","Plant":"West"}""", handler.Requests[0].Header("CallSettings"));
        Assert.Equal("""{"Company":"100","Plant":"MfgSys"}""", handler.Requests[1].Header("CallSettings"));
    }

    /// <summary>
    /// A call's own plant is sent when no plant is configured, and values the call leaves null keep the configured ones.
    /// </summary>
    [Fact]
    public async Task Call_settings_add_to_the_configured_values()
    {
        var (provider, handler) = TestHost.Build(options => options.Language = "en");
        var client = provider.GetRequiredService<IKineticServiceClient>();

        var settings = new KineticCallSettings { Plant = "West" };
        await client.CallAsync<JsonElement>("Erp.BO.CustShipSvc", "GetRows", new { pageSize = 0 }, settings);

        Assert.Equal("""{"Company":"100","Plant":"West","Language":"en"}""", Assert.Single(handler.Requests).Header("CallSettings"));
    }

    /// <summary>
    /// The escape hatch carries a call's own settings too, for OData queries in another plant.
    /// </summary>
    [Fact]
    public async Task Send_applies_call_settings()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"value":[]}""");
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.SendAsync<JsonElement>(
            HttpMethod.Get,
            "api/v2/odata/100/Erp.BO.CustShipSvc/ShipDtls?$filter=OrderNum eq 1",
            null,
            new KineticCallSettings { Plant = "West" });

        Assert.Equal("""{"Company":"100","Plant":"West"}""", Assert.Single(handler.Requests).Header("CallSettings"));
    }

    /// <summary>
    /// Empty call settings leave the request as the configuration makes it.
    /// </summary>
    [Fact]
    public async Task Empty_call_settings_send_no_header_when_nothing_is_configured()
    {
        var (provider, handler) = TestHost.Build();
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.CallAsync("Erp.BO.PartSvc", "GetList", null, new KineticCallSettings());

        Assert.Null(Assert.Single(handler.Requests).Header("CallSettings"));
    }

    /// <summary>
    /// Without plant, language or culture there is no CallSettings header at all.
    /// </summary>
    [Fact]
    public async Task CallSettings_header_is_omitted_by_default()
    {
        var (provider, handler) = TestHost.Build();
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.CallAsync("Erp.BO.PartSvc", "GetList");

        Assert.Null(Assert.Single(handler.Requests).Header("CallSettings"));
    }

}
