# Alethic.Epicor.Kinetic

A .NET client for the Epicor Kinetic REST v2 API: service methods, Epicor Functions, BAQs and OData over one
authenticated HttpClient pipeline. Works on .NET Framework 4.7.2+ and modern .NET.

```
dotnet add package Alethic.Epicor.Kinetic.Client
```

## Use

[The package README](src/Alethic.Epicor.Kinetic.Client/README.md) covers configuration and every client, and is what
nuget.org shows.

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
