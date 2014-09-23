using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Chat : MonoBehaviour
{
    private TCPServer server;
    private TCPClient client;
    private NetworkStream ns;

    private void Update()
    {
        //サーバの作業
        if(server != null)
        {
            if(server.Accepted)
            {
            }
        }

        //クライアントの処理
        if(client != null)
        {
            if(client.isConnected)
            {
            }
        }
    }

    private void OnGUI()
    {
        //サーバになるボタン
        if(GUI.Button(new Rect(10, 10, 150, 30), "サーバになる"))
        {
            server = new TCPServer();
            server.AcceptConnectAsync();
        }
        //クライアントになるボタン
        if(GUI.Button(new Rect(10, 50, 150, 30), "クライアントになる"))
        {
            client = new TCPClient();
            client.ConnectAsync(IPAddress.Loopback);
        }
    }
}