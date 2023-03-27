// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.AI.OpenAI.HttpSchema;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.Text;
using static Microsoft.SemanticKernel.HttpStatusCodeExtension;

namespace Microsoft.SemanticKernel.AI.OpenAI.Clients;

/// <summary>
/// An abstract OpenAI Client.
/// </summary>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "OpenAI users use strings")]
public abstract class OpenAIClientAbstract : IDisposable
{
    /// <summary>
    /// Logger
    /// </summary>
    protected ILogger Log { get; } = NullLogger.Instance;

    /// <summary>
    /// HTTP client
    /// </summary>
    protected HttpClient HTTPClient { get; }

    private readonly HttpClientHandler _httpClientHandler;
    private readonly IDelegatingHandlerFactory _handlerFactory;
    private readonly DelegatingHandler _retryHandler;

    internal OpenAIClientAbstract(ILogger? log = null, IDelegatingHandlerFactory? handlerFactory = null)
    {
        this.Log = log ?? this.Log;
        this._handlerFactory = handlerFactory ?? new DefaultHttpRetryHandlerFactory();

        this._httpClientHandler = new() { CheckCertificateRevocationList = true };
        this._retryHandler = this._handlerFactory.Create(this.Log);
        this._retryHandler.InnerHandler = this._httpClientHandler;

        this.HTTPClient = new HttpClient(this._retryHandler);
        this.HTTPClient.DefaultRequestHeaders.Add("User-Agent", HTTPUseragent);
    }

    /// <summary>
    /// Asynchronously sends a completion request for the prompt
    /// </summary>
    /// <param name="url">URL for the completion request API</param>
    /// <param name="requestBody">Prompt to complete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The text completion</returns>
    /// <exception cref="AIException">AIException thrown during the request.</exception>
    protected async Task<string> ExecuteTextCompleteRequestAsync(
        string url,
        string requestBody,
        CancellationToken cancellationToken)
    {
        try
        {
            this.Log.LogTrace("Sending text completion request to {0}: {1}", url, requestBody);

            var result = await this.ExecutePostRequestAsync<TextCompletionResponse>(url, requestBody, cancellationToken);
            if (result.Completions.Count < 1)
            {
                throw new AIException(
                    AIException.ErrorCodes.InvalidResponseContent,
                    "Completions not found");
            }

            return result.Completions.First().Text;
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// Asynchronously sends a chat completion request for the given history
    /// </summary>
    /// <param name="url">URL for the chat request API</param>
    /// <param name="requestBody">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next chat message</returns>
    /// <exception cref="AIException">AIException thrown during the request.</exception>
    protected async Task<string> ExecuteChatCompleteRequestAsync(
        string url,
        string requestBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this.Log.LogTrace("Sending chat completion request to {0}: {1}", url, requestBody);

            var result = await this.ExecutePostRequestAsync<ChatCompletionResponse>(url, requestBody, cancellationToken);
            if (result.Completions.Count < 1)
            {
                throw new AIException(
                    AIException.ErrorCodes.InvalidResponseContent,
                    "Chat message not found");
            }

            return result.Completions.First().Message.Content;
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// Asynchronously sends a text embedding request for the text.
    /// </summary>
    /// <param name="url">URL for the chat request API</param>
    /// <param name="requestBody">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of text embeddings</returns>
    /// <exception cref="AIException">AIException thrown during the request.</exception>
    protected async Task<IList<Embedding<float>>> ExecuteTextEmbeddingRequestAsync(
        string url,
        string requestBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await this.ExecutePostRequestAsync<TextEmbeddingResponse>(url, requestBody, cancellationToken);
            if (result.Embeddings.Count < 1)
            {
                throw new AIException(
                    AIException.ErrorCodes.InvalidResponseContent,
                    "Embeddings not found");
            }

            return result.Embeddings.Select(e => new Embedding<float>(e.Values.ToArray())).ToList();
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// Run the HTTP request to generate a list of images
    /// </summary>
    /// <param name="url">URL for the chat request API</param>
    /// <param name="requestBody">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of image URLs</returns>
    /// <exception cref="AIException">AIException thrown during the request.</exception>
    protected async Task<IList<string>> ExecuteImageUrlGenerationRequestAsync(
        string url,
        string requestBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await this.ExecutePostRequestAsync<ImageUrlGenerationResponse>(url, requestBody, cancellationToken);
            return result.Images.Select(x => x.Url).ToList();
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// Run the HTTP request to generate a list of images
    /// </summary>
    /// <param name="url">URL for the chat request API</param>
    /// <param name="requestBody">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of images serialized in base64</returns>
    /// <exception cref="AIException">AIException thrown during the request.</exception>
    protected async Task<IList<string>> ExecuteImageBase64GenerationRequestAsync(
        string url,
        string requestBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await this.ExecutePostRequestAsync<ImageUrlGenerationResponse>(url, requestBody, cancellationToken);
            return result.Images.Select(x => x.AsBase64).ToList();
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// Explicit finalizer called by IDisposable
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        // Request CL runtime not to call the finalizer - reduce cost of GC
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Overridable finalizer for concrete classes
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.HTTPClient.Dispose();
            this._httpClientHandler.Dispose();
            this._retryHandler.Dispose();
        }
    }

    #region private ================================================================================

    // HTTP user agent sent to remote endpoints
    private const string HTTPUseragent = "Microsoft Semantic Kernel";

    private async Task<T> ExecutePostRequestAsync<T>(string url, string requestBody, CancellationToken cancellationToken = default)
    {
        string responseJson;

        try
        {
            using HttpContent content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await this.HTTPClient.PostAsync(url, content, cancellationToken);

            if (response == null)
            {
                throw new AIException(AIException.ErrorCodes.NoResponse, "Empty response");
            }

            this.Log.LogTrace("HTTP response: {0} {1}", (int)response.StatusCode, response.StatusCode.ToString("G"));

            responseJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                int statusCode = (int)response.StatusCode;
                switch (statusCode)
                {
                    case (int)(int)HttpStatusCode.BadRequest:
                    case (int)HttpStatusCode.MethodNotAllowed:
                    case (int)HttpStatusCode.NotFound:
                    case (int)HttpStatusCode.NotAcceptable:
                    case (int)HttpStatusCode.Conflict:
                    case (int)HttpStatusCode.Gone:
                    case (int)HttpStatusCode.LengthRequired:
                    case (int)HttpStatusCode.PreconditionFailed:
                    case (int)HttpStatusCode.RequestEntityTooLarge:
                    case (int)HttpStatusCode.RequestUriTooLong:
                    case (int)HttpStatusCode.UnsupportedMediaType:
                    case (int)HttpStatusCode.RequestedRangeNotSatisfiable:
                    case (int)HttpStatusCode.ExpectationFailed:
                    case (int)ExtendedHttpStatusCode.MisdirectedRequest:
                    case (int)ExtendedHttpStatusCode.UnprocessableEntity:
                    case (int)ExtendedHttpStatusCode.Locked:
                    case (int)ExtendedHttpStatusCode.FailedDependency:
                    case (int)HttpStatusCode.UpgradeRequired:
                    case (int)ExtendedHttpStatusCode.PreconditionRequired:
                    case (int)ExtendedHttpStatusCode.RequestHeaderFieldsTooLarge:
                    case (int)HttpStatusCode.HttpVersionNotSupported:
                        throw new AIException(
                            AIException.ErrorCodes.InvalidRequest,
                            $"The request is not valid, HTTP status: {response.StatusCode:G}");

                    case (int)HttpStatusCode.Unauthorized:
                    case (int)HttpStatusCode.Forbidden:
                    case (int)HttpStatusCode.ProxyAuthenticationRequired:
                    case (int)ExtendedHttpStatusCode.UnavailableForLegalReasons:
                    case (int)ExtendedHttpStatusCode.NetworkAuthenticationRequired:
                        throw new AIException(
                            AIException.ErrorCodes.AccessDenied,
                            $"The request is not authorized, HTTP status: {response.StatusCode:G}");

                    case (int)HttpStatusCode.RequestTimeout:
                        throw new AIException(
                            AIException.ErrorCodes.RequestTimeout,
                            $"The request timed out, HTTP status: {response.StatusCode:G}");

                    case (int)ExtendedHttpStatusCode.TooManyRequests:
                        throw new AIException(
                            AIException.ErrorCodes.Throttling,
                            $"Too many requests, HTTP status: {response.StatusCode:G}");

                    case (int)HttpStatusCode.InternalServerError:
                    case (int)HttpStatusCode.NotImplemented:
                    case (int)HttpStatusCode.BadGateway:
                    case (int)HttpStatusCode.ServiceUnavailable:
                    case (int)HttpStatusCode.GatewayTimeout:
                    case (int)ExtendedHttpStatusCode.InsufficientStorage:
                        throw new AIException(
                            AIException.ErrorCodes.ServiceError,
                            $"The service failed to process the request, HTTP status: {response.StatusCode:G}");

                    default:
                        throw new AIException(
                            AIException.ErrorCodes.UnknownError,
                            $"Unexpected HTTP response, status: {response.StatusCode:G}");
                }
            }
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }

        try
        {
            var result = Json.Deserialize<T>(responseJson);
            if (result != null) { return result; }

            throw new AIException(
                AIException.ErrorCodes.InvalidResponseContent,
                "Response JSON parse error");
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <summary>
    /// C# finalizer
    /// </summary>
    ~OpenAIClientAbstract()
    {
        this.Dispose(false);
    }

    #endregion
}
