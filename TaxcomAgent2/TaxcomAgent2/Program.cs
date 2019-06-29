using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

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
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:4500/");
            httpListener.Start();
            Console.WriteLine("Started!");
            for (; ; )
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket socket = webSocketContext.WebSocket;
                    while (socket.State == WebSocketState.Open)
                    {
                        const int maxMessageSize = 4096;
                        byte[] receiveBuffer = new byte[maxMessageSize];
                        WebSocketReceiveResult receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        }
                        else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary frame", CancellationToken.None);
                        }
                        else
                        {
                            int count = receiveResult.Count;

                            while (receiveResult.EndOfMessage == false)
                            {
                                if (count >= maxMessageSize)
                                {
                                    string closeMessage = string.Format("Maximum message size: {0} bytes.", maxMessageSize);
                                    await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                                    return;
                                }

                                receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, count, maxMessageSize - count), CancellationToken.None);
                                count += receiveResult.Count;
                            }

                            var receivedString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                            var echoString = WebSocketHandler(receivedString);
                            ArraySegment<byte> outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(echoString));

                            await socket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
                else
                    context.Response.StatusCode = 400;
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
