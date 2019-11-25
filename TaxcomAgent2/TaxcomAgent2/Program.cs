using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;
using WebSocket = vtortola.WebSockets.WebSocket;

namespace TaxcomAgent2
{
    class Program
    {
        private static readonly List<string> Estate = new List<string>()
        {
            "Господин",
            "Барин",
            "Хозяин",
            "Босс",
            "Директор",
            "Тимлидер",
            "Сударь",
            "Красавчик",
            "Бухгалтер"
        };

        private static readonly Random rng = new Random();
        private static string GenEstate => Estate[rng.Next(Estate.Count)];

        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine("TaxcomAgent2");
            /*var options = new WebSocketListenerOptions();
            options.Standards.RegisterRfc6455();
            var httpListener = new WebSocketListener(new IPEndPoint(IPAddress.Loopback, 4500), options);
            await httpListener.StartAsync();*/

            var cancellation = new CancellationTokenSource();

            var bufferSize = 1024 * 8; // 8KiB
            var bufferPoolSize = 100 * bufferSize; // 800KiB pool

            var options = new WebSocketListenerOptions
            {
                SubProtocols = new[] { "text" },
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                PingMode = PingMode.Manual,
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize)
            };
            options.Standards.RegisterRfc6455(factory =>
            {
                factory.MessageExtensions.RegisterDeflateCompression();
            });
            // configure tcp transport
            options.Transports.ConfigureTcp(tcp =>
            {
                tcp.BacklogSize = 100; // max pending connections waiting to be accepted
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });

            // adding the WSS extension
            //var certificate = new X509Certificate2(File.ReadAllBytes("<PATH-TO-CERTIFICATE>"), "<PASSWORD>");
            // options.ConnectionExtensions.RegisterSecureConnection(certificate);

            var listenEndPoints = new Uri[] {
                new Uri("ws://localhost:4500/") // will listen both IPv4 and IPv6
            };

            // starting the server
            var server = new WebSocketListener(listenEndPoints, options);

            server.StartAsync().Wait();

            Console.WriteLine("Started!");

            var acceptingTask = AcceptWebSocketsAsync(server, cancellation.Token);

            Console.WriteLine("Press any key to stop.");
            Console.ReadKey(true);

            Console.WriteLine("Server stopping.");
            cancellation.Cancel();
            server.StopAsync().Wait();
            acceptingTask.Wait();
        }

        private static async Task AcceptWebSocketsAsync(WebSocketListener server, CancellationToken cancellation)
        {
            await Task.Yield();

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var webSocket = await server.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                    if (webSocket == null)
                    {
                        if (cancellation.IsCancellationRequested || !server.IsStarted)
                            break; // stopped

                        continue; // retry
                    }

#pragma warning disable 4014
                    EchoAllIncomingMessagesAsync(webSocket, cancellation);
#pragma warning restore 4014
                }
                catch (OperationCanceledException)
                {
                    /* server is stopped */
                    break;
                }
                catch (Exception acceptError)
                {
                    Console.WriteLine("An error occurred while accepting client.", acceptError);
                }
            }

            Console.WriteLine("Server has stopped accepting new clients.");
        }

        private static async Task EchoAllIncomingMessagesAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            Console.WriteLine("Client '" + webSocket.RemoteEndpoint + "' connected.");
            var sw = new Stopwatch();
            try
            {
                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var messageText = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                        if (messageText == null)
                            break; // webSocket is disconnected

                        Console.WriteLine("Client '" + webSocket.RemoteEndpoint + "' recived: " + messageText + ".");

                        sw.Restart();

                        messageText = WebSocketHandler(messageText);

                        await webSocket.WriteStringAsync(messageText, cancellation).ConfigureAwait(false);

                        Console.WriteLine("Client '" + webSocket.RemoteEndpoint + "' sent: " + messageText + ".");

                        sw.Stop();
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception readWriteError)
                    {
                        Console.WriteLine("An error occurred while reading/writing echo message.", readWriteError);
                        await webSocket.CloseAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                webSocket.Dispose();
                Console.WriteLine("Client '" + webSocket.RemoteEndpoint + "' disconnected.");
            }
        }

        private static string WebSocketHandler(string receivedString)
        {
            switch (receivedString)
            {
                case "HelloAgent2":
                    return "HelloFromAgent2";
                case "GetName":
                    return $@"Name: {GenEstate} {Environment.UserDomainName}\{Environment.UserName}";
                default:
                    return "Unknown request";
            }
        }
    }
}
