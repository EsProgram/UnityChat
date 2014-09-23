using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Chat : MonoBehaviour
{
    private const int APP_PORT = 55555;

    private enum NetworkState
    {
        None,
        Server,
        Client,
        Connecting,
    }

    private TCPServer server;
    private TCPClient client;
    private NetworkStream ns;
    private NetworkState state = NetworkState.None;
    private string serverIP = string.Empty;

    private void Update()
    {
        //クライアントが接続完了したら
        if(state == NetworkState.Client && client.isConnected)
            state = NetworkState.Connecting;
        //サーバが接続完了したら
        if(state == NetworkState.Server && server.isConnected)
            state = NetworkState.Connecting;

        //接続が完了したクライアント・サーバの動作
        if(state == NetworkState.Connecting)
        {
            Debug.Log("もくひょーかんりょおおおおおおおお");
        }
    }

    private void OnApplicationQuit()
    {
        if(client != null)
            client.Close();
        if(server != null)
            server.Close();
    }

    private void OnGUI()
    {
        switch(state)
        {
            case NetworkState.None:
                //サーバになるボタン表示
                if(GUI.Button(new Rect(Screen.width / 10, Screen.height / 10, 150, 30), "サーバになる"))
                {
                    state = NetworkState.Server;
                    server = new TCPServer(APP_PORT);
                    server.AcceptConnectAsync();
                }
                //クライアントになるボタン表示
                if(GUI.Button(new Rect(Screen.width / 10, Screen.height / 5, 150, 30), "クライアントになる"))
                {
                    state = NetworkState.Client;
                    client = new TCPClient();
                }
                break;

            case NetworkState.Server:
                GUI.TextField(new Rect(Screen.width / 5, Screen.height / 5, 100, 30), "接続を待っています");
                break;

            case NetworkState.Client:
                serverIP = GUI.TextArea(new Rect(Screen.width / 10, Screen.height / 10, 150, 30), serverIP);
                //接続ボタンを押したら接続する
                if(GUI.Button(new Rect(Screen.width / 5, Screen.height / 5, 100, 30), "接続"))
                    client.ConnectAsync(IPAddress.Parse(serverIP), APP_PORT);
                break;

            case NetworkState.Connecting:
                //チャット欄を表示
                break;

            default:
                break;
        }
    }
}