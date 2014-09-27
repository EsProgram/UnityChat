using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Chat : MonoBehaviour
{
    private const int APP_PORT = 55555;
    private const int BYTE_SIZE = 1024;
    private const int MESSAGE_SAVE_NUM = 20;

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
    private byte[] sendByte = new byte[BYTE_SIZE];
    private string receive = string.Empty;//受信文字列
    private byte[] receiveByte = new byte[BYTE_SIZE];
    private List<string> chat = new List<string>();
    private IAsyncResult sendResult;
    private IAsyncResult receiveResult;
    private StringBuilder showMessageBuf = new StringBuilder();

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

        if(state == NetworkState.Connecting)
        {
            //受信動作
            if(receiveResult == null || receiveResult.IsCompleted)
                receiveResult = ns.BeginRead(receiveByte, 0, sendByte.Length, ReadCallback, null);
        }

        //メッセージ保存数を超えたら先頭を削除
        if(chat.Count > MESSAGE_SAVE_NUM)
            chat.RemoveAt(0);
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
                    state = NetworkState.Client;
                break;

            case NetworkState.Server:
                GUI.TextField(new Rect(Screen.width / 10, Screen.height / 10, 200, 30), "接続を待っています");
                break;

            case NetworkState.Client:
                serverIP = GUI.TextField(new Rect(Screen.width / 10, Screen.height / 10, 150, 30), serverIP);
                //接続ボタンを押したら接続する
                if(GUI.Button(new Rect(Screen.width / 5, Screen.height / 5, 100, 30), "接続"))
                {
                    tcp = new TCPClient(IPAddress.Parse(serverIP), APP_PORT);
                    tcp.ConnectAsync();
                }
                break;

            case NetworkState.Connecting:

                //チャット欄に表示するメッセージの加工
                showMessageBuf = new StringBuilder();
                foreach(var s in chat)
                    showMessageBuf.AppendLine(s);

                //チャット欄・チャットウィンドウを表示
                GUI.TextArea(new Rect(10, 110, 300, 200), showMessageBuf.ToString());
                send = GUI.TextArea(new Rect(10, 10, 300, 30), send, 50);

                //送信
                if(send.Contains('\n') || send.Contains('\r'))//なんかEnvironment.NewLineだと反応しない
                {
                    send = send.Split('\n', '\r')[0];
                    SendChatMessage();
                }
                if(GUI.Button(new Rect(310, 10, 50, 30), "送信"))
                    SendChatMessage();
                break;

            default:
                break;
        }
    }

    private void SendChatMessage()
    {
        if(sendResult == null || sendResult.IsCompleted)
            if(send != string.Empty && !send.All(c => char.IsWhiteSpace(c)))
            {
                chat.Add(send);
                sendByte = Encoding.UTF8.GetBytes(send);
                sendResult = ns.BeginWrite(sendByte, 0, sendByte.Length, WriteCallback, null);
                send = string.Empty;
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
        chat.Add(receive);
        receiveByte = new byte[BYTE_SIZE];
    }
}