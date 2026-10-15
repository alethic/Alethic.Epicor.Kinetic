# Alethic.Epicor.Kinetic.Client

A .NET client for the Epicor Kinetic REST v2 API. Call service methods, Epicor Functions and BAQs, or query services
with OData, through one authenticated `HttpClient` pipeline registered with dependency injection.

- **One registration.** `AddKineticClient` wires up every client, with options bound from configuration and checked at
  startup.
- **Authentication built in.** Every request carries the API key and either Basic credentials or a bearer token.
- **The whole API.** Service methods, Epicor Functions, BAQ reads and updatable BAQ writes, OData through
  Microsoft.OData.Client, and a raw escape hatch for any other endpoint.
- **Per-call plant.** Name a different plant, language or format culture for one call without a second client.
- **Readable errors.** Kinetic's error payloads, JSON or XML, become a `KineticApiException` with the message, type,
  details and correlation id.
- **Runs everywhere.** Targets .NET Standard 2.0 and .NET 8, so it works on .NET Framework 4.7.2 and later as well as
  current .NET.

## Install

```bash
dotnet add package Alethic.Epicor.Kinetic.Client
```

## Configure

Register the clients and bind their options from a configuration section:

```csharp
services.AddKineticClient(configuration.GetSection(KineticOptions.SectionName));
```

```json
{
  "Kinetic": {
    "BaseAddress": "https://example.epicorsaas.com/prod/",
    "Company": "100",
    "Plant": "MfgSys"
  }
}
```

Keep `Kinetic:ApiKey`, `Kinetic:Username` and `Kinetic:Password` out of `appsettings.json`. Supply them from user
secrets, environment variables or a vault.

Options can also be set in code:

```csharp
services.AddKineticClient(options =>
{
    options.BaseAddress = "https://example.epicorsaas.com/prod/";
    options.Company = "100";
    options.ApiKey = apiKey;
    options.Username = username;
    options.Password = password;
});
```

| Option | Required | Meaning |
|---|---|---|
| `BaseAddress` | yes | The app server instance root, such as `https://example.epicorsaas.com/prod/`. |
| `Company` | yes | The company ID. REST v2 puts it in every URL. |
| `ApiKey` | yes | An API key from API Key Maintenance. REST v2 rejects calls without one. |
| `Username`, `Password` | yes, unless a token provider is set | A Kinetic user for Basic authentication. |
| `AccessTokenProvider` | no | Supplies a bearer token instead, for users who sign in through Epicor IdP or Entra ID. |
| `Plant`, `Language`, `FormatCulture` | no | Sent in the `CallSettings` header on every call. |
| `Timeout` | no | How long a call may take. Defaults to 100 seconds. `Timeout.InfiniteTimeSpan` turns it off. |

Both registrations take an optional hook that runs on every Kinetic `HttpClient`, for example to add a resilience
handler from Microsoft.Extensions.Http.Resilience:

```csharp
services.AddKineticClient(configuration.GetSection("Kinetic"), http => http.AddStandardResilienceHandler());
```

## Use

Inject the client for the kind of call you make.

### Service methods

`IKineticServiceClient` calls any method of any service, such as the `Erp.BO` business objects' `GetByID`, `GetRows`
and `Update`: `POST api/v2/odata/{company}/{service}/{method}`. Method parameters are serialised by name, and the
response's `returnObj` and `parameters` come back in `BoResponse<T>`:

```csharp
var response = await services.CallAsync<BoResponse<CustomerTableset>>(
    "Erp.BO.CustomerSvc", "GetByID", new { custNum = 1 });

var customer = response!.ReturnObj!.Customer[0];
```

Tableset columns keep their declared casing, so rows can be sent back as they came, with `RowMod` set from the `RowMod`
constants:

```csharp
customer.Name = "Acme Corporation";
customer.RowMod = RowMod.Updated;
await services.CallAsync("Erp.BO.CustomerSvc", "Update", new { ds = response.ReturnObj });
```

`QueryAsync` reads a service's OData entity set, with `$filter`, `$select`, `$orderby`, `$top` and `$skip` in an
`ODataQuery`. It reads every page the server returns:

```csharp
var lines = await services.QueryAsync<PackLine>("Erp.BO.CustShipSvc", "ShipDtls", new ODataQuery
{
    Filter = "OrderNum eq 5001",
    Select = "PackNum,PackLine",
});
```

`SendAsync` sends any other request relative to the instance root.

### Epicor Functions

`IKineticFunctionClient` invokes a function in a library. The request object becomes the function's inputs and the
response its outputs:

```csharp
var result = await functions.InvokeAsync<ChargeResult>(
    "ExampleFunctions", "CalculateCharge", new { orderNum = 123 });
```

Functions answer HTTP 200 even when they fail, so check the status fields your function returns.

### BAQs

`IKineticBaqClient` runs a BAQ and returns its rows. OData options and BAQ parameters both go in a `BaqQuery`:

```csharp
var rows = await baq.QueryAsync<OpenOrderRow>("OpenOrders", new BaqQuery
{
    Filter = "Customer_CustID eq 'ACME'",
    OrderBy = "OrderHed_OrderDate desc",
    Top = 50,
    Parameters = new Dictionary<string, string> { ["StartDate"] = "2026-01-01" },
});
```

`QueryAsync` reads every page the server returns. Updatable BAQs take writes with `AddRowAsync` and `UpdateRowAsync`.

### OData

`IKineticODataContextFactory` creates Microsoft.OData.Client contexts that send through the same authenticated pipeline.
Each service is its own OData root, so there is one context per service:

```csharp
var context = odata.ForService("Erp.BO.CustomerSvc");
var query = (DataServiceQuery<Customer>)context.CreateQuery<Customer>("Customers")
    .Where(c => c.CustID == "ACME");

var customers = await query.ExecuteAsync();
```

Entity types need `[Key]` on their key columns. A container generated with OData Connected Service goes through the
pipeline too:

```csharp
var container = odata.Configure(new Container(odata.ServiceRoot("Erp.BO.CustomerSvc")));
```

Each service's `$metadata` is downloaded once and shared by every context for that service.

## Working in another plant

Some services return only the rows of the session's plant. `Erp.BO.CustShipSvc`, for one, serves only the
packs shipped from the plant named in `CallSettings`, and Kinetic takes a single plant per request. A plant it does not
accept is not an error: Kinetic quietly falls back to the user's default plant.

Pass `KineticCallSettings` to name a plant, language or format culture for one call. Values it leaves null keep the
configured ones:

```csharp
foreach (var plant in plants)
{
    var packs = await services.CallAsync<BoResponse<CustShipTableset>>(
        "Erp.BO.CustShipSvc", "GetRows", parameters, new KineticCallSettings { Plant = plant });
}
```

`KineticCallSettings` also takes a `Timeout` for the one call, longer or shorter than the configured one. It is not sent
to Kinetic; the client cancels the call when it elapses and throws `TaskCanceledException` wrapping a
`TimeoutException`, as `HttpClient` does:

```csharp
var result = await functions.InvokeAsync<JsonElement>(
    "ExampleFunctions", "RebuildCosts", null, new KineticCallSettings { Timeout = TimeSpan.FromMinutes(10) });
```

The registered clients enforce the timeout themselves and leave `HttpClient.Timeout` infinite. An `HttpClient` built by
hand over `KineticHttpHandler` keeps its own `Timeout` as an outer limit, so set it to `Timeout.InfiniteTimeSpan` there
too.

Function calls, BAQ calls and entity set queries take it the same way. An OData context takes it once, in `ForService`, `ForBaq` or `Configure`,
and sends it with every query it makes.

## Errors

A failed call throws `KineticApiException`, read from Kinetic's error payload whether it came back as JSON or XML:

```csharp
try
{
    await services.CallAsync("Erp.BO.CustomerSvc", "GetByID", new { custNum = 999999 });
}
catch (KineticApiException e)
{
    logger.LogWarning("{Status} {Type}: {Message} (correlation {CorrelationId})",
        e.StatusCode, e.ErrorType, e.ErrorMessage, e.CorrelationId);
}
```

`ErrorDetails` lists the row-level messages Kinetic attached, and the exception also carries the request's method and
URL. Every request is logged at Debug level with its status and duration.

## Links

- Source, issues and releases: https://github.com/alethic/Alethic.Epicor.Kinetic
- License: MIT
