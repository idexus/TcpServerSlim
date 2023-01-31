﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SlimMessenger;

public class SlimServer
{
    // private

    readonly ConcurrentDictionary<Guid, SlimClient> clientDictionary = new ConcurrentDictionary<Guid, SlimClient>();
    TcpListener listener;
    CancellationTokenSource cancellationTokenSource;

    // public

    public const int DefaultServerPort = 5095;

    public event ServerEventHandler? ServerStarted;
    public event ServerEventHandler? ServerStopped;
    public event ClientEventHandler? ClientConnected;
    public event ClientEventHandler? ClientDisconnected;

    public bool IsRunning { get; private set; }

    public bool IsStopRequested => cancellationTokenSource.IsCancellationRequested;

    // constructor

    public SlimServer(int serverPort = DefaultServerPort)
    {
        cancellationTokenSource = new CancellationTokenSource();

        var ipEndPoint = new IPEndPoint(IPAddress.Any, serverPort);
        listener = new TcpListener(ipEndPoint);
    }

    // start server

    public void Start()
    {
        listener.Start();
        IsRunning = true;
        ServerStarted?.Invoke(this);
        Task.Factory.StartNew(RunLoop, TaskCreationOptions.LongRunning);
    }

    // stop server

    public void Stop() => cancellationTokenSource.Cancel();

    // run loop

    private async Task RunLoop()
    {
        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);

                var clientGuid = Guid.NewGuid();
                var clientSlim = new SlimClient(client, clientGuid, CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token));
                clientDictionary[clientGuid] = clientSlim;

                clientSlim.ClientConnected += e => ClientConnected?.Invoke(e);
                clientSlim.ClientDisconnected += e =>
                {
                    clientDictionary.Remove(clientGuid, out _);
                    ClientDisconnected?.Invoke(e);
                };

                clientSlim.StartRunLoop();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        foreach (var client in clientDictionary.Values)
            client.Disconnect();
        listener.Stop();

        clientDictionary.Clear();

        ServerStopped?.Invoke(this);
        IsRunning = false;
    }
}
