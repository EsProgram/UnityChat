using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Chat : MonoBehaviour
{
    private enum NetworkState
    {
        None,
        Server,
        Client,
    }

    private TCPServer server;
    private TCPClient client;
    private NetworkStream ns;
    private NetworkState state = NetworkState.None;

    private void Update()
    {
        switch(state)
        {
            case NetworkState.None:
                break;

            case NetworkState.Server:
                if(server.Accepted) { }

                break;

            case NetworkState.Client:
                if(client.isConnected) { }
                break;

            default:
                break;
        }
    }

    private void OnGUI()
    {
        //サーバ・クライアントが決定していない状態だったら
        if(state == NetworkState.None)
        {
            //サーバになるボタン表示
            if(GUI.Button(new Rect(10, 10, 150, 30), "サーバになる"))
            {
                state = NetworkState.Server;
                server = new TCPServer();
                server.AcceptConnectAsync();
            }
            //クライアントになるボタン表示
            if(GUI.Button(new Rect(10, 50, 150, 30), "クライアントになる"))
            {
                state = NetworkState.Client;
                client = new TCPClient();
                client.ConnectAsync(IPAddress.Loopback);
            }
        }
    }
}