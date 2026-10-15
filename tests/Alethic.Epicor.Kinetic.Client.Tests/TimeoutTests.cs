using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Client;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class TimeoutTests
{

    /// <summary>
    /// Every call that takes call settings, by name. Each runs one call against the given provider with the given
    /// settings.
    /// </summary>
    public static IEnumerable<object[]> Calls()
    {
        yield return Call("function", (p, s) => Functions(p).InvokeAsync("ExampleFunctions", "Ping", null, s));
        yield return Call("service method", (p, s) => Services(p).CallAsync("Erp.BO.CustomerSvc", "GetByID", null, s));
        yield return Call("entity set", (p, s) => Services(p).QueryAsync<Customer>("Erp.BO.CustomerSvc", "Customers", null, s));
        yield return Call("raw send", (p, s) => Services(p).SendAsync<object>(HttpMethod.Get, "api/v2/efx/100", null, s));
        yield return Call("BAQ query", (p, s) => Baqs(p).QueryAsync<Customer>("zCustomer01", null, s));
        yield return Call("BAQ add", (p, s) => Baqs(p).AddRowAsync("zCustomer01", new Customer(), s));
        yield return Call("BAQ update", (p, s) => Baqs(p).UpdateRowAsync("zCustomer01", new Customer(), s));
        yield return Call("OData service", (p, s) => ODataContextFactoryTests.QueryAcme(Factory(p).ForService("Erp.BO.CustomerSvc", s)));
        yield return Call("OData BAQ", (p, s) => ODataContextFactoryTests.QueryAcme(Factory(p).ForBaq("zCustomer01", s)));
        yield return Call("OData configured", (p, s) => ODataContextFactoryTests.QueryAcme(Factory(p).Configure(CustomerContext(), s)));
    }

    /// <summary>
    /// Pairs a call with its name for the theory data.
    /// </summary>
    static object[] Call(string name, Func<IServiceProvider, KineticCallSettings, Task> call)
    {
        return new object[] { name, call };
    }

    /// <summary>
    /// Returns whether the exception or one of its inner exceptions is a timeout. Microsoft.OData.Client wraps transport
    /// failures in an <see cref="InvalidOperationException"/>, so an OData timeout sits one level further in.
    /// </summary>
    static bool IsTimeout(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is TaskCanceledException { InnerException: TimeoutException })
                return true;

        return false;
    }

    /// <summary>
    /// Resolves the function client.
    /// </summary>
    static IKineticFunctionClient Functions(IServiceProvider provider)
    {
        return provider.GetRequiredService<IKineticFunctionClient>();
    }

    /// <summary>
    /// Resolves the service client.
    /// </summary>
    static IKineticServiceClient Services(IServiceProvider provider)
    {
        return provider.GetRequiredService<IKineticServiceClient>();
    }

    /// <summary>
    /// Resolves the BAQ client.
    /// </summary>
    static IKineticBaqClient Baqs(IServiceProvider provider)
    {
        return provider.GetRequiredService<IKineticBaqClient>();
    }

    /// <summary>
    /// Resolves the OData context factory.
    /// </summary>
    static IKineticODataContextFactory Factory(IServiceProvider provider)
    {
        return provider.GetRequiredService<IKineticODataContextFactory>();
    }

    /// <summary>
    /// Creates a bare context on the customer service root, as a generated container would be.
    /// </summary>
    static DataServiceContext CustomerContext()
    {
        return new DataServiceContext(new Uri(ODataContextFactoryTests.ServiceRoot), ODataProtocolVersion.V4);
    }

    /// <summary>
    /// Builds a provider with the given configured timeout whose fake server answers after <paramref name="delay"/>,
    /// except for OData metadata, which it answers at once.
    /// </summary>
    static ServiceProvider Build(TimeSpan configured, TimeSpan delay)
    {
        var (provider, handler) = TestHost.Build(options => options.Timeout = configured);
        handler.Respond = ODataContextFactoryTests.FakeKineticService;
        handler.Delay = r => r.RequestUri!.AbsoluteUri.EndsWith("$metadata", StringComparison.Ordinal) ? TimeSpan.Zero : delay;
        return provider;
    }

    /// <summary>
    /// A call's own timeout shorter than the configured one cancels that call with a timeout.
    /// </summary>
    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Call_timeout_shorter_than_configured_cancels_the_call(
        string name,
        Func<IServiceProvider, KineticCallSettings, Task> call)
    {
        using var provider = Build(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        var ex = await Record.ExceptionAsync(() => call(provider, new KineticCallSettings { Timeout = TimeSpan.FromMilliseconds(50) }));

        Assert.True(IsTimeout(ex), $"{name} did not report a timeout: {ex}");
    }

    /// <summary>
    /// A call's own timeout longer than the configured one lets that call outlast the configured timeout.
    /// </summary>
    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Call_timeout_longer_than_configured_extends_the_call(
        string name,
        Func<IServiceProvider, KineticCallSettings, Task> call)
    {
        using var provider = Build(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));

        await call(provider, new KineticCallSettings { Timeout = TimeSpan.FromMinutes(1) });

        Assert.NotNull(name);
    }

    /// <summary>
    /// The configured timeout applies when the call settings give no timeout of their own.
    /// </summary>
    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Configured_timeout_applies_without_a_call_timeout(
        string name,
        Func<IServiceProvider, KineticCallSettings, Task> call)
    {
        using var provider = Build(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(30));

        var ex = await Record.ExceptionAsync(() => call(provider, new KineticCallSettings { Plant = "West" }));

        Assert.True(IsTimeout(ex), $"{name} did not report a timeout: {ex}");
    }

    /// <summary>
    /// An infinite call timeout turns the configured limit off for that call.
    /// </summary>
    [Fact]
    public async Task Infinite_call_timeout_turns_the_limit_off()
    {
        using var provider = Build(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        await client.InvokeAsync("ExampleFunctions", "Ping", null, new KineticCallSettings { Timeout = Timeout.InfiniteTimeSpan });
    }

    /// <summary>
    /// The configured timeout applies to a call made without call settings.
    /// </summary>
    [Fact]
    public async Task Configured_timeout_applies_without_call_settings()
    {
        using var provider = Build(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(30));
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => client.InvokeAsync("ExampleFunctions", "Ping"));

        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    /// <summary>
    /// Cancellation by the caller is reported as cancellation, not as a timeout.
    /// </summary>
    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_timeout()
    {
        using var provider = Build(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        var client = provider.GetRequiredService<IKineticFunctionClient>();
        var settings = new KineticCallSettings { Timeout = TimeSpan.FromMinutes(1) };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.InvokeAsync("ExampleFunctions", "Ping", null, settings, cancellation.Token));

        Assert.IsNotType<TimeoutException>(ex.InnerException);
    }

    /// <summary>
    /// A zero or negative call timeout is rejected when it is set.
    /// </summary>
    [Fact]
    public void Non_positive_call_timeout_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KineticCallSettings { Timeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new KineticCallSettings { Timeout = TimeSpan.FromSeconds(-1) });
    }

    /// <summary>
    /// A zero configured timeout fails validation when the first client is resolved.
    /// </summary>
    [Fact]
    public void Zero_configured_timeout_fails_validation()
    {
        var (provider, _) = TestHost.Build(options => options.Timeout = TimeSpan.Zero);

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IKineticFunctionClient>());

        Assert.Contains("Timeout", ex.Message);
    }

}
