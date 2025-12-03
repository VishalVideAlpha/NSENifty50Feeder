using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using WebAPI2;

namespace CQGAPI.Helpers;

public class WebSocketServer
{
    private readonly ClientWebSocket _clientWebSocket;
    private ConcurrentDictionary<string, Action<byte[]>> dctAction = new ConcurrentDictionary<string, Action<byte[]>>();

    public delegate void CloseEvent(string message);
    public event CloseEvent? OnClose;
 
    public WebSocketServer()
    {
        _clientWebSocket = new ClientWebSocket();
      
    }

    public void SetRequestHeader(string headerName, string headerValue)
    {
        _clientWebSocket.Options.SetRequestHeader(headerName, headerValue);
    }

    public async Task Start(string url)
    {
        try
        {

            await _clientWebSocket.ConnectAsync(new Uri(url), CancellationToken.None);

            Console.WriteLine("Connected!");
            var receiveTask = Task.Run(async () =>
            {
                const int bufferSize = 1024 * 5;
                var buffer = new byte[bufferSize];
                var data = new MemoryStream();

                while (true)
                {
                    var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        //await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                        OnClose?.Invoke(result.CloseStatusDescription);
                        Console.WriteLine("Closed");
                        //break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine(message);
                    }
                    else
                    {
                        data.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage)
                        {
                            var receivedData = data.ToArray();
                            data = new MemoryStream(); // Reset memory stream for next message

                            dctAction.ElementAt(0).Value.Invoke(receivedData);
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    internal void On(string eventname, Action<byte[]> action)
    {
        dctAction.TryAdd(eventname, action);
    }

    internal async Task CloseAsync()
    {
        if (_clientWebSocket != null)
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal", CancellationToken.None);
        }
    }

    internal async Task SendAsync(ClientMsg clientMsg)
    {
        if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
        {
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(clientMsg.ToByteArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}
