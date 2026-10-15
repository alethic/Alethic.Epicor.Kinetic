using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class FunctionClientTests
{

    /// <summary>
    /// Response shape of the sample function.
    /// </summary>
    /// <param name="Total">Charge total.</param>
    /// <param name="Applied">Whether the charge was applied.</param>
    sealed record CalcResult(decimal Total, bool Applied);

    /// <summary>
    /// A function call posts to the efx URL with the API key, Basic credentials and the request as JSON.
    /// </summary>
    [Fact]
    public async Task Invoke_posts_to_efx_url_with_auth_headers_and_body()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(200, """{"total": 12.5, "applied": true}""");
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        var result = await client.InvokeAsync<CalcResult>("ExampleFunctions", "CalculateCharge", new { orderNum = 123, orderLine = 1 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.epicorsaas.com/prod/api/v2/efx/100/ExampleFunctions/CalculateCharge", request.Uri.AbsoluteUri);
        Assert.Equal(TestHost.ApiKey, request.Header("x-api-key"));
        Assert.Equal(TestHost.ExpectedBasicAuth, request.Header("Authorization"));
        Assert.Equal("application/json", request.Header("Accept"));
        Assert.StartsWith("application/json", request.ContentType);
        Assert.Equal("""{"orderNum":123,"orderLine":1}""", request.Body);
        Assert.NotNull(result);
        Assert.Equal(12.5m, result.Total);
        Assert.True(result.Applied);
    }

    /// <summary>
    /// A function with no inputs still gets an empty JSON object body.
    /// </summary>
    [Fact]
    public async Task Invoke_without_request_sends_empty_object()
    {
        var (provider, handler) = TestHost.Build();
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        await client.InvokeAsync("ExampleFunctions", "Ping");

        Assert.Equal("{}", Assert.Single(handler.Requests).Body);
    }

    /// <summary>
    /// A configured token provider replaces Basic credentials with a bearer token.
    /// </summary>
    [Fact]
    public async Task Invoke_uses_bearer_token_when_provider_configured()
    {
        var (provider, handler) = TestHost.Build(options =>
        {
            options.Username = string.Empty;
            options.Password = string.Empty;
            options.AccessTokenProvider = _ => new ValueTask<string>("idp-token");
        });
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        await client.InvokeAsync("ExampleFunctions", "Ping");

        Assert.Equal("Bearer idp-token", Assert.Single(handler.Requests).Header("Authorization"));
    }

    /// <summary>
    /// A JSON error payload from Kinetic is surfaced on the exception, including row-level details.
    /// </summary>
    [Fact]
    public async Task Invoke_maps_json_error_payload_to_exception()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Json(400, """
            {
              "HttpStatus": 400,
              "ReasonPhrase": "REST API Exception",
              "ErrorMessage": "Order 123 not found.",
              "ErrorType": "Ice.Common.BusinessObjectException",
              "ErrorDetails": [ { "Message": "Order 123 not found.", "Type": "Error", "Table": "OrderHed", "Field": "OrderNum" } ],
              "CorrelationId": "abc-123"
            }
            """);
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        var ex = await Assert.ThrowsAsync<KineticApiException>(
            () => client.InvokeAsync("ExampleFunctions", "CalculateCharge", new { orderNum = 123 }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Order 123 not found.", ex.ErrorMessage);
        Assert.Equal("Ice.Common.BusinessObjectException", ex.ErrorType);
        Assert.Equal("abc-123", ex.CorrelationId);
        var detail = Assert.Single(ex.ErrorDetails);
        Assert.Equal("OrderHed", detail.Table);
        Assert.Equal("OrderNum", detail.Field);
        Assert.Contains("400", ex.Message);
        Assert.Contains("CalculateCharge", ex.Message);
    }

    /// <summary>
    /// An XML error payload, as returned for authentication failures, is parsed the same way.
    /// </summary>
    [Fact]
    public async Task Invoke_maps_xml_error_payload_to_exception()
    {
        var (provider, handler) = TestHost.Build();
        handler.Respond = _ => FakeHttpHandler.Text(401, """
            <ApiExceptionResponse xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <HttpStatus>Unauthorized</HttpStatus>
              <ErrorMessage>Invalid username or password.</ErrorMessage>
              <ErrorType>System.UnauthorizedAccessException</ErrorType>
              <CorrelationId>65673cd6</CorrelationId>
            </ApiExceptionResponse>
            """, "application/xml");
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        var ex = await Assert.ThrowsAsync<KineticApiException>(() => client.InvokeAsync("ExampleFunctions", "Ping"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("Invalid username or password.", ex.ErrorMessage);
        Assert.Equal("System.UnauthorizedAccessException", ex.ErrorType);
        Assert.Equal("65673cd6", ex.CorrelationId);
    }

    /// <summary>
    /// A call's own plant is sent for that invocation only; values it leaves null keep the configured ones.
    /// </summary>
    [Fact]
    public async Task Invoke_applies_call_settings_to_one_call()
    {
        var (provider, handler) = TestHost.Build(options => options.Plant = "MfgSys");
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        await client.InvokeAsync("ExampleFunctions", "CalculateCharge", new { orderNum = 1 }, new KineticCallSettings { Plant = "West" });
        await client.InvokeAsync("ExampleFunctions", "CalculateCharge");

        Assert.Equal("""{"Company":"100","Plant":"West"}""", handler.Requests[0].Header("CallSettings"));
        Assert.Equal("""{"Company":"100","Plant":"MfgSys"}""", handler.Requests[1].Header("CallSettings"));
    }

}
