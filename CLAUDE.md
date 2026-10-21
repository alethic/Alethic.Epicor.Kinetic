# Alethic.Epicor.Kinetic

.NET client library for the Epicor Kinetic REST v2 API. Epicor Kinetic is a commercial product; there is no Epicor
source in this repository, only this client. The repository is named for the API, not for the client, because it may
come to hold more than the client; the client is `Alethic.Epicor.Kinetic.Client` within it.

## Layout

- `src/Alethic.Epicor.Kinetic.Client` - the library. Targets `netstandard2.0`, `net472` and `net8.0`.
- `tests/Alethic.Epicor.Kinetic.Client.Tests` - xUnit tests. Target `net10.0` and `net48` so the net472 build is
  exercised on .NET Framework.
- `samples/Alethic.Epicor.Kinetic.Sample` - command-line explorer for a live instance. Targets `net10.0`.
- `src/dist-nuget`, `src/dist-tests` - NoTargets projects that pack the library and publish each test project per
  target framework. `Alethic.Epicor.Kinetic.dist.msbuildproj` drives them and writes to `dist/`.
- `Directory.Build.props` - shared language settings, company and version defaults, embedded PDB and sources, and the
  `IKVM.Core.MSBuild` package that provides `PackageProjectReference` and `PublishProjectReference` to the dist projects.
- `.github/workflows/Alethic.Epicor.Kinetic.yml` - GitHub Actions: build via the dist project, matrix tests across platforms
  and target frameworks from the published test payload, then the release job that publishes the packages.
- `GitVersion.yml` plus `.config/dotnet-tools.json` - versioning. `main` builds are `pre` prereleases, `develop` are
  `dev`, tags are releases. The pipeline passes GitVersion output into MSBuild version properties.
- `Alethic.Epicor.Kinetic.slnx` - solution. `global.json` pins the .NET 10 SDK and the NoTargets MSBuild SDK.

## Commands

```bash
dotnet build Alethic.Epicor.Kinetic.slnx
```

```bash
dotnet test Alethic.Epicor.Kinetic.slnx
```

Full distribution build, as the pipeline runs it. Output lands in `dist/nuget` and `dist/tests`:

```bash
dotnet msbuild /m /p:Configuration=Release Alethic.Epicor.Kinetic.dist.msbuildproj
```

```bash
dotnet run --project samples/Alethic.Epicor.Kinetic.Sample -- function ExampleFunctions CalculateCharge '{}'
```

## Architecture

Small wrapping classes composed through DI. `AddKineticClient` registers everything.

1. `KineticHttpHandler` is the bottom layer. A `DelegatingHandler` that adds the API key, Basic or bearer credentials,
   the JSON Accept header and the optional CallSettings header, and logs each request. Any HttpClient with this handler is
   an authenticated Kinetic client. The named client `KineticServiceCollectionExtensions.HttpClientName` is that.
2. Generic clients on top of it, one per Kinetic call pattern. Each takes `HttpClient`, `IOptions<KineticOptions>` and
   `ILogger<T>`:
   - `IKineticFunctionClient` - Epicor Functions, `POST api/v2/efx/{company}/{library}/{function}`.
   - `IKineticServiceClient` - any business object method, `POST api/v2/odata/{company}/{service}/{method}`, plus a raw
     `SendAsync` escape hatch for any relative path.
   - `IKineticBaqClient` - BAQ reads and updatable BAQ writes on `api/v2/odata/{company}/BaqSvc/{baqId}/Data`.
   - `IKineticODataContextFactory` - Microsoft.OData.Client `DataServiceContext` per service root, routed through the
     same authenticated pipeline via `OnMessageCreating`.
3. Typed contracts for specific services, functions or BAQs are layered on these generic clients later. Generated OData
   containers plug in through `IKineticODataContextFactory.Configure`.

## Conventions

- Namespaces, projects, directories and the solution are prefixed `Alethic.Epicor.Kinetic.`.
- One type per file, file named after the type. Generic arity uses the StyleCop form: `BoResponse{TReturn}.cs`.
- A single blank line after a type's opening brace and before its closing brace.
- File-scoped namespaces. Explicit `using` directives; `ImplicitUsings` is off. System namespaces sort first.
- Omit `private`; it is the default. Fields are `_camelCase`, static ones included. Constants are PascalCase.
- Member order inside a type: nested types first, then static members, then instance members.
- No braces around single-line `if` bodies. Braces for anything multi-line.
- Every method has an XML doc. `<summary>` open and close tags on their own lines. Say what a caller needs; no history
  or design rationale.
- Lines, including comments and XML docs, wrap at 140 columns.
- csproj files use four-space indentation. The `.slnx` is tool-managed; leave its formatting alone.
- `.editorconfig` encodes what tooling can enforce. Keep it in sync with this list.

## Target framework rules

The library must compile for `netstandard2.0` and `net472`, so inside `src/`:

- No `ArgumentNullException.ThrowIfNull` or `ArgumentException.ThrowIfNullOrWhiteSpace`; use `Internal/Guard`.
- No `HttpMethod.Patch`; use `KineticHttp.Patch`. No `Stopwatch.GetElapsedTime`, `string.EndsWith(char)`,
  `ReadAsStringAsync(CancellationToken)`, `await using`, or `KeyValuePair` deconstruction.
- `Polyfill` (source-only, `PrivateAssets="all"`) supplies `IsExternalInit` and friends. Records and `init` are fine.
- `System.Text.Json` is referenced explicitly for netstandard2.0 and net472 only.
- Prefer `#if NET8_0_OR_GREATER` over dropping a feature when a modern API materially helps.

## Kinetic API facts

- Base URL shape: `https://<host>/<instance>/`. Tenant example in `appsettings.json`:
  `https://example.epicorsaas.com/prod/`, company `100`.
  The real host, company and credentials belong in user secrets, never in `appsettings.json`.
- REST v2 requires an `x-api-key` header plus user credentials. Basic auth works for native Epicor users. SSO users
  (Epicor IdP, Entra ID) need a bearer token; `KineticOptions.AccessTokenProvider` supplies it.
- Errors are JSON `{ HttpStatus, ReasonPhrase, ErrorMessage, ErrorType, ErrorDetails[], CorrelationId }`, or XML with
  the same element names when the Accept header was not JSON. `KineticApiException` parses both.
- JSON casing is mixed: method parameters are camelCase (`custNum`, `ds`), tableset columns are PascalCase (`CustNum`,
  `RowMod`). `KineticJson.Default` therefore preserves declared names and reads case-insensitively.
- Business object responses are `{ returnObj, parameters }`. `BoResponse<TReturn>` models that.
- Functions take their inputs as a JSON object body and return their outputs as a JSON object. An empty object body is
  required when there are no inputs.
- Each service is its own OData root with its own `$metadata`; there is no combined document.
- OpenAPI 3.0.1 documents, one per service, behind Basic auth plus the API key:
  `api/swagger/v2/odata/{Service}.json` for entity sets and `api/swagger/v2/methods/{Service}.json` for methods.
  The v2 help index is `api/help/v2/index`. `GET api/v2/efx/{company}` lists function libraries. The function
  documents under `api/swagger/v2/efx/` come back with no paths, so function signatures are learned by calling them.
- Functions return HTTP 200 even when they fail; the body carries the function's own status and message fields.
- `UpdateExt` returns the `Ice.BOUpdErrorTableset` as `returnObj` and the updated tableset under `parameters`.
- The sample's `get` command fetches any authenticated path, so documents can be pulled without a browser:

```bash
dotnet run --project samples/Alethic.Epicor.Kinetic.Sample -- get api/swagger/v2/methods/Erp.BO.CustomerSvc.json --out customer.json
```

## Build and release rules

- Every build of `main` or `develop` pushes to GitHub Packages. A tag additionally creates the GitHub release and
  pushes to nuget.org, which cannot be undone - so a tag is the release.
- Add a new test project by adding `TestTarget` items to `src/dist-tests/dist-tests.csproj` and, if its target
  frameworks differ, matrix entries to the workflow. Add a new package by adding a `PackageProjectReference` to
  `src/dist-nuget/dist-nuget.csproj`.
- Version properties come from GitVersion in CI; local builds fall back to `0.0.0-dev` from `Directory.Build.props`.
  `main` is labelled `pre`, `develop` is labelled `dev`.

## Dependencies

- `Microsoft.OData.Client` 7.x is the OData client. 8.x ships only a net8.0 build, so it cannot be used while .NET
  Framework is supported. Do not use `Simple.OData.Client`; it is unmaintained.
- The OData transport is `Internal/KineticODataRequestMessage`, plugged in through `OnMessageCreating`. 7.x has no
  `IHttpClientFactory` hook.
- `Microsoft.Extensions.Http` and `Microsoft.Extensions.Options.ConfigurationExtensions` for DI, options and logging.

## Authorship

All work is authored by the repository owner. Do not mention an AI assistant anywhere: not in code, comments, commit
messages, pull requests, or documentation. No co-author trailers, no generated-by lines.

## Secrets

Never commit credentials. The sample reads `Kinetic:Username`, `Kinetic:Password` and `Kinetic:ApiKey` from user secrets
or environment variables. Do not paste secrets into chat or source; ask for them to be set with `dotnet user-secrets`.
