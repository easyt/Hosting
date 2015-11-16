// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Internal;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Internal;

namespace Microsoft.AspNet.TestHost
{
    public class WebSocketClient
    {
        private readonly Func<object, Task> _next;
        private readonly IHttpApplication _application;
        private readonly PathString _pathBase;

        internal WebSocketClient(Func<object, Task> next, PathString pathBase, IHttpApplication application)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            _next = next;
            _application = application;

            // PathString.StartsWithSegments that we use below requires the base path to not end in a slash.
            if (pathBase.HasValue && pathBase.Value.EndsWith("/"))
            {
                pathBase = new PathString(pathBase.Value.Substring(0, pathBase.Value.Length - 1));
            }
            _pathBase = pathBase;

            SubProtocols = new List<string>();
        }

        public IList<string> SubProtocols
        {
            get;
            private set;
        }

        public Action<HttpRequest> ConfigureRequest
        {
            get;
            set;
        }

        public async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            var state = new RequestState(uri, _pathBase, cancellationToken, _application);

            if (ConfigureRequest != null)
            {
                ConfigureRequest(state.HostingApplicationContext.HttpContext.Request);
            }

            // Async offload, don't let the test code block the caller.
            var offload = Task.Factory.StartNew(async () =>
            {
                try
                {
                    await _next(state.HostingApplicationContext);
                    state.ServerCleanup();
                    state.PipelineComplete();
                }
                catch (Exception ex)
                {
                    state.ServerCleanup(ex);
                    state.PipelineFailed(ex);
                }
                finally
                {
                    state.Dispose();
                }
            });

            return await state.WebSocketTask;
        }

        private class RequestState : IDisposable, IHttpWebSocketFeature
        {
            private readonly IHttpApplication _application;
            private TaskCompletionSource<WebSocket> _clientWebSocketTcs;
            private WebSocket _serverWebSocket;

            public HostingApplicationContext HostingApplicationContext { get; private set; }
            public Task<WebSocket> WebSocketTask { get { return _clientWebSocketTcs.Task; } }

            public RequestState(Uri uri, PathString pathBase, CancellationToken cancellationToken, IHttpApplication application)
            {
                _clientWebSocketTcs = new TaskCompletionSource<WebSocket>();
                _application = application;

                // HttpContext
                HostingApplicationContext = (HostingApplicationContext)_application.CreateContext(new FeatureCollection());
                var httpContext = HostingApplicationContext.HttpContext;

                // Request
                httpContext.Features.Set<IHttpRequestFeature>(new RequestFeature());
                var request = httpContext.Request;
                request.Protocol = "HTTP/1.1";
                var scheme = uri.Scheme;
                scheme = (scheme == "ws") ? "http" : scheme;
                scheme = (scheme == "wss") ? "https" : scheme;
                request.Scheme = scheme;
                request.Method = "GET";
                var fullPath = PathString.FromUriComponent(uri);
                PathString remainder;
                if (fullPath.StartsWithSegments(pathBase, out remainder))
                {
                    request.PathBase = pathBase;
                    request.Path = remainder;
                }
                else
                {
                    request.PathBase = PathString.Empty;
                    request.Path = fullPath;
                }
                request.QueryString = QueryString.FromUriComponent(uri);
                request.Headers.Add("Connection", new string[] { "Upgrade" });
                request.Headers.Add("Upgrade", new string[] { "websocket" });
                request.Headers.Add("Sec-WebSocket-Version", new string[] { "13" });
                request.Headers.Add("Sec-WebSocket-Key", new string[] { CreateRequestKey() });
                request.Body = Stream.Null;

                // Response
                httpContext.Features.Set<IHttpResponseFeature>(new ResponseFeature());
                var response = httpContext.Response;
                response.Body = Stream.Null;
                response.StatusCode = 200;

                // WebSocket
                httpContext.Features.Set<IHttpWebSocketFeature>(this);
            }

            public void PipelineComplete()
            {
                PipelineFailed(new InvalidOperationException("Incomplete handshake, status code: " + HostingApplicationContext.HttpContext.Response.StatusCode));
            }

            public void PipelineFailed(Exception ex)
            {
                _clientWebSocketTcs.TrySetException(new InvalidOperationException("The websocket was not accepted.", ex));
            }

            public void Dispose()
            {
                if (_serverWebSocket != null)
                {
                    _serverWebSocket.Dispose();
                }
            }

            internal void ServerCleanup()
            {
                ServerCleanup(exception: null);
            }

            internal void ServerCleanup(Exception exception)
            {
                if (HostingApplicationContext != null)
                {
                    _application.DisposeContext(HostingApplicationContext, exception);
                }
            }

            private string CreateRequestKey()
            {
                byte[] data = new byte[16];
                var rng = RandomNumberGenerator.Create();
                rng.GetBytes(data);
                return Convert.ToBase64String(data);
            }

            bool IHttpWebSocketFeature.IsWebSocketRequest
            {
                get
                {
                    return true;
                }
            }

            Task<WebSocket> IHttpWebSocketFeature.AcceptAsync(WebSocketAcceptContext context)
            {
                HostingApplicationContext.HttpContext.Response.StatusCode = 101; // Switching Protocols

                var websockets = TestWebSocket.CreatePair(context.SubProtocol);
                _clientWebSocketTcs.SetResult(websockets.Item1);
                _serverWebSocket = websockets.Item2;
                return Task.FromResult<WebSocket>(_serverWebSocket);
            }
        }
    }
}