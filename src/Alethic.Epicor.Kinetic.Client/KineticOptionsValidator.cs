using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Reports incomplete <see cref="KineticOptions"/> when they are first resolved.
/// </summary>
sealed class KineticOptionsValidator : IValidateOptions<KineticOptions>
{

    /// <summary>
    /// Checks that the base address, company, API key and credentials are all present and that the timeout is usable.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, KineticOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            failures.Add("Kinetic:BaseAddress must be an absolute http(s) URL such as https://host/instance/.");

        if (string.IsNullOrWhiteSpace(options.Company))
            failures.Add("Kinetic:Company is required.");

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            failures.Add("Kinetic:ApiKey is required. REST v2 rejects calls without an API key.");

        if (options.AccessTokenProvider is null &&
            (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrEmpty(options.Password)))
            failures.Add("Kinetic:Username and Kinetic:Password are required unless AccessTokenProvider is set.");

        if (!KineticHttp.IsValidTimeout(options.Timeout))
            failures.Add("Kinetic:Timeout must be positive and at most Int32.MaxValue ms, or infinite.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

}
