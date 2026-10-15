# Alethic.Epicor.Kinetic

A thin, composable .NET client for the Epicor Kinetic REST v2 API. Works on .NET Framework 4.7.2+ and modern .NET.

```
dotnet add package Alethic.Epicor.Kinetic.Client
```

## Install and register

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

Supply `Kinetic:ApiKey`, `Kinetic:Username` and `Kinetic:Password` from user secrets, environment variables or a vault.
For SSO users set `KineticOptions.AccessTokenProvider` in code instead of a username and password.

## Use

Functions:

```csharp
var result = await functions.InvokeAsync<ChargeResult>("ExampleFunctions", "CalculateCharge", new { orderNum = 123 });
```

Business object methods:

```csharp
var response = await services.CallAsync<BoResponse<CustomerTableset>>("Erp.BO.CustomerSvc", "GetByID", new { custNum = 1 });
var customer = response!.ReturnObj!.Customer[0];
```

BAQs:

```csharp
var rows = await baq.QueryAsync<OpenOrderRow>("OpenOrders", new BaqQuery { Filter = "Customer_CustID eq 'ACME'", Top = 50 });
```

OData with Microsoft.OData.Client:

```csharp
var context = odata.ForService("Erp.BO.CustomerSvc");
var query = (DataServiceQuery<Customer>)context.CreateQuery<Customer>("Customers").Where(c => c.CustID == "ACME");
var customers = await query.ExecuteAsync();
```

Entity types need `[Key]` on their key columns. A container generated with OData Connected Service can be routed
through the same pipeline with `odata.Configure(new Container(odata.ServiceRoot("Erp.BO.CustomerSvc")))`.

Errors from Kinetic surface as `KineticApiException` with the status, `ErrorMessage`, `ErrorType`, `ErrorDetails` and
`CorrelationId` parsed from the response.

## Layers

`KineticHttpHandler` authenticates any HttpClient. The generic clients (`IKineticFunctionClient`, `IKineticServiceClient`,
`IKineticBaqClient`, `IKineticODataContextFactory`) sit on it. Typed contracts for specific services are meant to be
built on those generic clients.

## Build

`.github/workflows/Alethic.Epicor.Kinetic.yml` builds the outer dist project, runs the published tests on Windows and Linux
for net10.0 plus net48 on Windows, and publishes: every build of `main` or `develop` goes to GitHub Packages, and a
tag additionally creates the GitHub release and pushes to nuget.org. Versions come from GitVersion. The same build
runs locally with:

```bash
dotnet msbuild /m /p:Configuration=Release Alethic.Epicor.Kinetic.dist.msbuildproj
```

## Sample

```bash
dotnet run --project samples/Alethic.Epicor.Kinetic.Sample -- call Erp.BO.CustomerSvc GetByID '{"custNum":1}'
```

See `CLAUDE.md` for coding conventions.

## License

MIT. See [LICENSE](LICENSE).
