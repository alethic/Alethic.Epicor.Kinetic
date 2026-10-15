using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Alethic.Epicor.Kinetic.Client.Tests;

public class TimeoutTests
{

    /// <summary>
    /// A call's own timeout shorter than the configured one cancels that call with a timeout.
    /// </summary>
    [Fact]
    public async Task Call_timeout_shorter_than_configured_cancels_the_call()
    {
        var (provider, handler) = TestHost.Build(options => options.Timeout = TimeSpan.FromMinutes(1));
        handler.Delay = TimeSpan.FromSeconds(30);
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.InvokeAsync("ExampleFunctions", "Ping", null, new KineticCallSettings { Timeout = TimeSpan.FromMilliseconds(50) }));

        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    /// <summary>
    /// A call's own timeout longer than the configured one lets that call outlast the configured timeout.
    /// </summary>
    [Fact]
    public async Task Call_timeout_longer_than_configured_extends_the_call()
    {
        var (provider, handler) = TestHost.Build(options => options.Timeout = TimeSpan.FromMilliseconds(50));
        handler.Delay = TimeSpan.FromMilliseconds(300);
        var client = provider.GetRequiredService<IKineticServiceClient>();

        await client.CallAsync("Erp.BO.CustomerSvc", "GetByID", new { custNum = 1 }, new KineticCallSettings { Timeout = TimeSpan.FromMinutes(1) });

        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// An infinite call timeout turns the configured limit off for that call.
    /// </summary>
    [Fact]
    public async Task Infinite_call_timeout_turns_the_limit_off()
    {
        var (provider, handler) = TestHost.Build(options => options.Timeout = TimeSpan.FromMilliseconds(50));
        handler.Delay = TimeSpan.FromMilliseconds(300);
        var client = provider.GetRequiredService<IKineticFunctionClient>();

        await client.InvokeAsync("ExampleFunctions", "Ping", null, new KineticCallSettings { Timeout = Timeout.InfiniteTimeSpan });

        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// The configured timeout applies when the call gives none, including when other call settings are given.
    /// </summary>
    [Fact]
    public async Task Configured_timeout_applies_without_a_call_timeout()
    {
        var (provider, handler) = TestHost.Build(options => options.Timeout = TimeSpan.FromMilliseconds(50));
        handler.Delay = TimeSpan.FromSeconds(30);
        var client = provider.GetRequiredService<IKineticBaqClient>();

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.QueryAsync<Customer>("zCustomer01", null, new KineticCallSettings { Plant = "West" }));

        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    /// <summary>
    /// Cancellation by the caller is reported as cancellation, not as a timeout.
    /// </summary>
    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_timeout()
    {
        var (provider, handler) = TestHost.Build();
        handler.Delay = TimeSpan.FromSeconds(30);
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
