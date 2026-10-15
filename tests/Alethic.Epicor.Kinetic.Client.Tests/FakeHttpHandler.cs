using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.Epicor.Kinetic.Client.Tests;

/// <summary>
/// Records every request and answers with whatever <see cref="Respond"/> returns.
/// </summary>
sealed class FakeHttpHandler : HttpMessageHandler
{

    /// <summary>
    /// Builds a JSON response with the given status code.
    /// </summary>
    public static HttpResponseMessage Json(int status, string json)
    {
        return Text(status, json, "application/json");
    }

    /// <summary>
    /// Builds a text response with the given status code and media type.
    /// </summary>
    public static HttpResponseMessage Text(int status, string body, string mediaType)
    {
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };
    }

    /// <summary>
    /// Requests seen so far, in order.
    /// </summary>
    public List<CapturedRequest> Requests { get; } = new();

    /// <summary>
    /// Produces the response for a request. Defaults to an empty JSON object with status 200.
    /// </summary>
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(200, "{}");

    /// <summary>
    /// How long to wait, honouring cancellation, before answering. Defaults to no wait.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Captures the request, including its body, waits for <see cref="Delay"/>, then delegates to <see cref="Respond"/>.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var contentType = request.Content?.Headers.ContentType?.ToString();
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, headers, body, contentType));
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken);

        return Respond(request);
    }

}
