// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Testing.xunit;
using Xunit;

namespace Microsoft.AspNet.TestHost
{
    public class TestClientTests
    {
        private readonly TestServer _server;

        public TestClientTests()
        {
            _server = new TestServer(new WebApplicationBuilder().Configure(app => app.Run(ctx => Task.FromResult(0))));
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task GetAsyncWorks()
        {
            // Arrange
            var expected = "GET Response";
            RequestDelegate appDelegate = ctx =>
                ctx.Response.WriteAsync(expected);
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            // Act
            var actual = await client.GetStringAsync("http://localhost:12345");

            // Assert
            Assert.Equal(expected, actual);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task NoTrailingSlash_NoPathBase()
        {
            // Arrange
            var expected = "GET Response";
            RequestDelegate appDelegate = ctx =>
            {
                Assert.Equal("", ctx.Request.PathBase.Value);
                Assert.Equal("/", ctx.Request.Path.Value);
                return ctx.Response.WriteAsync(expected);
            };
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            // Act
            var actual = await client.GetStringAsync("http://localhost:12345");

            // Assert
            Assert.Equal(expected, actual);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task SingleTrailingSlash_NoPathBase()
        {
            // Arrange
            var expected = "GET Response";
            RequestDelegate appDelegate = ctx =>
            {
                Assert.Equal("", ctx.Request.PathBase.Value);
                Assert.Equal("/", ctx.Request.Path.Value);
                return ctx.Response.WriteAsync(expected);
            };
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            // Act
            var actual = await client.GetStringAsync("http://localhost:12345/");

            // Assert
            Assert.Equal(expected, actual);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task PutAsyncWorks()
        {
            // Arrange
            RequestDelegate appDelegate = ctx =>
                ctx.Response.WriteAsync(new StreamReader(ctx.Request.Body).ReadToEnd() + " PUT Response");
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            // Act
            var content = new StringContent("Hello world");
            var response = await client.PutAsync("http://localhost:12345", content);

            // Assert
            Assert.Equal("Hello world PUT Response", await response.Content.ReadAsStringAsync());
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task PostAsyncWorks()
        {
            // Arrange
            RequestDelegate appDelegate = async ctx =>
                await ctx.Response.WriteAsync(new StreamReader(ctx.Request.Body).ReadToEnd() + " POST Response");
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            // Act
            var content = new StringContent("Hello world");
            var response = await client.PostAsync("http://localhost:12345", content);

            // Assert
            Assert.Equal("Hello world POST Response", await response.Content.ReadAsStringAsync());
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task WebSocketWorks()
        {
            // Arrange
            RequestDelegate appDelegate = async ctx =>
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var websocket = await ctx.WebSockets.AcceptWebSocketAsync();
                    var receiveArray = new byte[1024];
                    while (true)
                    {
                        var receiveResult = await websocket.ReceiveAsync(new System.ArraySegment<byte>(receiveArray), CancellationToken.None);
                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", CancellationToken.None);
                            break;
                        }
                        else
                        {
                            var sendBuffer = new System.ArraySegment<byte>(receiveArray, 0, receiveResult.Count);
                            await websocket.SendAsync(sendBuffer, receiveResult.MessageType, receiveResult.EndOfMessage, CancellationToken.None);
                        }
                    }
                }
            };
            var builder = new WebApplicationBuilder().Configure(app =>
            {
                app.Run(appDelegate);
            });
            var server = new TestServer(builder);

            // Act
            var client = server.CreateWebSocketClient();
            var clientSocket = await client.ConnectAsync(new System.Uri("http://localhost"), CancellationToken.None);
            var hello = Encoding.UTF8.GetBytes("hello");
            await clientSocket.SendAsync(new System.ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
            var world = Encoding.UTF8.GetBytes("world!");
            await clientSocket.SendAsync(new System.ArraySegment<byte>(world), WebSocketMessageType.Binary, true, CancellationToken.None);
            await clientSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", CancellationToken.None);

            // Assert
            Assert.Equal(WebSocketState.CloseSent, clientSocket.State);

            var buffer = new byte[1024];
            var result = await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(hello.Length, result.Count);
            Assert.True(hello.SequenceEqual(buffer.Take(hello.Length)));
            Assert.Equal(WebSocketMessageType.Text, result.MessageType);

            result = await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(world.Length, result.Count);
            Assert.True(world.SequenceEqual(buffer.Take(world.Length)));
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);

            result = await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(WebSocketMessageType.Close, result.MessageType);
            Assert.Equal(WebSocketState.Closed, clientSocket.State);

            clientSocket.Dispose();
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task WebSocketDisposalThrowsOnPeer()
        {
            // Arrange
            RequestDelegate appDelegate = async ctx =>
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var websocket = await ctx.WebSockets.AcceptWebSocketAsync();
                    websocket.Dispose();
                }
            };
            var builder = new WebApplicationBuilder().Configure(app =>
            {
                app.Run(appDelegate);
            });
            var server = new TestServer(builder);

            // Act
            var client = server.CreateWebSocketClient();
            var clientSocket = await client.ConnectAsync(new System.Uri("http://localhost"), CancellationToken.None);
            var buffer = new byte[1024];
            await Assert.ThrowsAsync<IOException>(async () => await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), CancellationToken.None));

            clientSocket.Dispose();
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task WebSocketTinyReceiveGeneratesEndOfMessage()
        {
            // Arrange
            RequestDelegate appDelegate = async ctx =>
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var websocket = await ctx.WebSockets.AcceptWebSocketAsync();
                    var receiveArray = new byte[1024];
                    while (true)
                    {
                        var receiveResult = await websocket.ReceiveAsync(new System.ArraySegment<byte>(receiveArray), CancellationToken.None);
                        var sendBuffer = new System.ArraySegment<byte>(receiveArray, 0, receiveResult.Count);
                        await websocket.SendAsync(sendBuffer, receiveResult.MessageType, receiveResult.EndOfMessage, CancellationToken.None);
                    }
                }
            };
            var builder = new WebApplicationBuilder().Configure(app =>
            {
                app.Run(appDelegate);
            });
            var server = new TestServer(builder);

            // Act
            var client = server.CreateWebSocketClient();
            var clientSocket = await client.ConnectAsync(new System.Uri("http://localhost"), CancellationToken.None);
            var hello = Encoding.UTF8.GetBytes("hello");
            await clientSocket.SendAsync(new System.ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);

            // Assert
            var buffer = new byte[1];
            for (var i = 0; i < hello.Length; i++)
            {
                bool last = i == (hello.Length - 1);
                var result = await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), CancellationToken.None);
                Assert.Equal(buffer.Length, result.Count);
                Assert.Equal(buffer[0], hello[i]);
                Assert.Equal(last, result.EndOfMessage);
            }

            clientSocket.Dispose();
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task ClientDisposalAbortsRequest()
        {
            // Arrange
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            RequestDelegate appDelegate = async ctx =>
            {
                // Write Headers
                await ctx.Response.Body.FlushAsync();

                var sem = new SemaphoreSlim(0);
                try
                {
                    await sem.WaitAsync(ctx.RequestAborted);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            };

            // Act
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:12345");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            // Abort Request
            response.Dispose();

            // Assert
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await tcs.Task);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Hangs randomly (issue #507)")]
        public async Task ClientCancellationAbortsRequest()
        {
            // Arrange
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            RequestDelegate appDelegate = async ctx =>
            {
                var sem = new SemaphoreSlim(0);
                try
                {
                    await sem.WaitAsync(ctx.RequestAborted);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            };

            // Act
            var builder = new WebApplicationBuilder().Configure(app => app.Run(appDelegate));
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(500);
            var response = await client.GetAsync("http://localhost:12345", cts.Token);

            // Assert
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await tcs.Task);
        }
    }
}
