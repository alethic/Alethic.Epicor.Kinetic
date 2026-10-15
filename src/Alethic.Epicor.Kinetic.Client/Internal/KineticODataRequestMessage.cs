using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData;
using Microsoft.OData.Client;

namespace Alethic.Epicor.Kinetic.Client.Internal;

/// <summary>
/// Transport for Microsoft.OData.Client that sends each request through an authenticated Kinetic <see cref="HttpClient"/>.
/// </summary>
sealed class KineticODataRequestMessage : DataServiceClientRequestMessage
{

    static Task<T> ToAsyncResult<T>(Task<T> task, AsyncCallback? callback, object? state)
    {
        var completion = new TaskCompletionSource<T>(state);
        task.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted)
                    completion.TrySetException(completed.Exception!.InnerExceptions);
                else if (completed.IsCanceled)
                    completion.TrySetException(CancellationOf(completed));
                else
                    completion.TrySetResult(completed.Result);

                callback?.Invoke(completion.Task);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return completion.Task;
    }

    /// <summary>
    /// Returns the exception a cancelled task was cancelled with, so a timeout keeps its <see cref="TimeoutException"/>.
    /// </summary>
    static Exception CancellationOf(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException e)
        {
            return e;
        }

        return new TaskCanceledException(task);
    }

    readonly HttpClient _http;
    readonly KineticCallSettings? _callSettings;
    readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    readonly MemoryStream _body = new();
    readonly CancellationTokenSource _abort = new();
    string _method;
    Uri _url;

    /// <summary>
    /// Creates a message for the request described by <paramref name="args"/> that will be sent with <paramref name="http"/>,
    /// carrying <paramref name="callSettings"/> when given.
    /// </summary>
    public KineticODataRequestMessage(DataServiceClientRequestMessageArgs args, HttpClient http, KineticCallSettings? callSettings)
        : base(args.ActualMethod)
    {
        _http = http;
        _callSettings = callSettings;
        _method = args.Method;
        _url = args.RequestUri;

        foreach (var header in args.Headers)
            _headers[header.Key] = header.Value;
    }

    /// <summary>
    /// The HTTP verb the OData client asked for. With POST tunnelling the wire method is <c>ActualMethod</c>.
    /// </summary>
    public override string Method
    {
        get => _method;
        set => _method = value;
    }

    /// <summary>
    /// Absolute request URL.
    /// </summary>
    public override Uri Url
    {
        get => _url;
        set => _url = value;
    }

    /// <summary>
    /// Ignored. Authentication comes from the HttpClient pipeline.
    /// </summary>
    [Obsolete("Credentials are supplied by the Kinetic HttpClient pipeline.")]
    public override ICredentials Credentials { get; set; } = null!;

    /// <summary>
    /// Ignored. The call settings' timeout, or the configured one, applies.
    /// </summary>
    public override int Timeout { get; set; }

    /// <summary>
    /// Ignored. The call settings' timeout, or the configured one, applies.
    /// </summary>
    public override int ReadWriteTimeout { get; set; }

    /// <summary>
    /// Ignored. HttpClient decides the transfer encoding.
    /// </summary>
    public override bool SendChunked { get; set; }

    /// <summary>
    /// Headers set so far, request and content headers together.
    /// </summary>
    public override IEnumerable<KeyValuePair<string, string>> Headers => _headers;

    /// <summary>
    /// Returns a request header value, or <c>null</c> when it has not been set.
    /// </summary>
    public override string GetHeader(string headerName)
    {
        return _headers.TryGetValue(headerName, out var value) ? value : null!;
    }

    /// <summary>
    /// Sets a request header. Content headers are applied to the body when the request is sent.
    /// </summary>
    public override void SetHeader(string headerName, string headerValue)
    {
        _headers[headerName] = headerValue;
    }

    /// <summary>
    /// Returns the stream the OData client writes the request body into.
    /// </summary>
    public override Stream GetStream()
    {
        return _body;
    }

    /// <summary>
    /// Cancels the in-flight request.
    /// </summary>
    public override void Abort()
    {
        _abort.Cancel();
    }

    /// <summary>
    /// Begins the request-stream phase. The body stream is available immediately.
    /// </summary>
    public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
    {
        return ToAsyncResult(Task.FromResult<Stream>(_body), callback, state);
    }

    /// <summary>
    /// Completes the request-stream phase started by <see cref="BeginGetRequestStream"/>.
    /// </summary>
    public override Stream EndGetRequestStream(IAsyncResult asyncResult)
    {
        return ((Task<Stream>)asyncResult).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Begins sending the request.
    /// </summary>
    public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
    {
        return ToAsyncResult(SendAsync(), callback, state);
    }

    /// <summary>
    /// Completes the send started by <see cref="BeginGetResponse"/> and returns the response.
    /// </summary>
    public override IODataResponseMessage EndGetResponse(IAsyncResult asyncResult)
    {
        return ((Task<IODataResponseMessage>)asyncResult).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sends the request synchronously and returns the response.
    /// </summary>
    public override IODataResponseMessage GetResponse()
    {
        return SendAsync().GetAwaiter().GetResult();
    }

    async Task<IODataResponseMessage> SendAsync()
    {
        using var request = new HttpRequestMessage(new HttpMethod(ActualMethod), _url);
        if (_callSettings is not null)
            KineticHttp.SetCallSettings(request, _callSettings);

        if (_body.Length > 0 || _headers.ContainsKey("Content-Type"))
            request.Content = new ByteArrayContent(_body.ToArray());

        foreach (var header in _headers)
        {
            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _abort.Token).ConfigureAwait(false);
        await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return new KineticODataResponseMessage(response, stream);
    }

}
