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

    private ITCPBase tcp;
    private NetworkStream ns;
    private NetworkState state = NetworkState.None;
    private string serverIP = string.Empty;
    private bool connectTrigger;//接続完了した最初のフレームのみtrue

    private void Update()
    {
        //接続完了したら
        if(state == NetworkState.Client && tcp != null && tcp.isConnected)
        {
            state = NetworkState.Connecting;
            connectTrigger = true;
        }

        //接続完了後1回のみ呼ばれる
        if(connectTrigger)
            ns = tcp.GetStream();

        //接続が完了したクライアント・サーバの動作
        if(state == NetworkState.Connecting)
        {
            Debug.Log("かんりょおおおお");
        }
    }

    private void OnApplicationQuit()
    {
        //ストリームを閉じる
        if(ns != null)
            ns.Dispose();

        if(tcp != null)
            tcp.Close();
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
                    tcp = new TCPServer(IPAddress.Any, APP_PORT);
                    tcp.ConnectAsync();
                }
                //クライアントになるボタン表示
                if(GUI.Button(new Rect(Screen.width / 10, Screen.height / 5, 150, 30), "クライアントになる"))
                {
                    state = NetworkState.Client;
                }
                break;

            case NetworkState.Server:
                GUI.TextField(new Rect(Screen.width / 5, Screen.height / 5, 100, 30), "接続を待っています");
                break;

            case NetworkState.Client:
                serverIP = GUI.TextArea(new Rect(Screen.width / 10, Screen.height / 10, 150, 30), serverIP);
                //接続ボタンを押したら接続する
                if(GUI.Button(new Rect(Screen.width / 5, Screen.height / 5, 100, 30), "接続"))
                {
                    tcp = new TCPClient(IPAddress.Parse(serverIP), APP_PORT);
                    tcp.ConnectAsync();
                }
                break;

            case NetworkState.Connecting:
                //チャット欄を表示
                break;

            default:
                break;
        }
    }
}