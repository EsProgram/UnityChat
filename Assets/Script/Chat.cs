using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    private string send = string.Empty;//送信文字列
    private byte[] sendByte = new byte[1024];
    private string receive = string.Empty;//受信文字列
    private byte[] receiveByte = new byte[1024];
    private IAsyncResult sendResult;
    private IAsyncResult receiveResult;

    private void Update()
    {
        //接続完了したら
        if((state == NetworkState.Client || state == NetworkState.Server) && tcp != null && tcp.isConnected)
        {
            state = NetworkState.Connecting;
            connectTrigger = true;
        }

        //接続完了後1回のみ呼ばれる
        if(connectTrigger)
            ns = tcp.GetStream();

        //受信動作
        if(state == NetworkState.Connecting)
            if(receiveResult == null || receiveResult.IsCompleted)
                receiveResult = ns.BeginRead(receiveByte, 0, sendByte.Length, ReadCallback, null);
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
                GUI.TextField(new Rect(Screen.width / 10, Screen.height / 10, 200, 30), "接続を待っています");
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
                //チャット欄を表示・テキスト送信
                send = GUI.TextField(new Rect(10, 10, 100, 30), send);
                GUI.TextField(new Rect(10, 110, 300, 200), receive);

                //送信ボタン
                if(sendResult == null || sendResult.IsCompleted)
                    if(GUI.Button(new Rect(110, 10, 50, 30), "送信"))
                    {
                        sendByte = Encoding.UTF8.GetBytes(send);
                        sendResult = ns.BeginWrite(sendByte, 0, sendByte.Length, WriteCallback, null);
                        send = string.Empty;
                    }
                break;

            default:
                break;
        }
    }

    private void WriteCallback(IAsyncResult ar)
    {
        ns.EndWrite(ar);
    }

    private void ReadCallback(IAsyncResult ar)
    {
        ns.EndRead(ar);
        receive = Encoding.UTF8.GetString(receiveByte);

        //受信配列の初期化
        receiveByte = new byte[1024];
    }
}