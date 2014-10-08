using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

/// <summary>
/// TCPOpponentSingleテスト使用クラス
/// Int32型の大きさを１つのパケットとして、Int32型の範囲内の乱数を送受信する。
/// </summary>
public class TestUseTcp : MonoBehaviour
{
    private const int PACKET_SIZE = sizeof(int);
    private TCPOpponentSingle trans;
    private SceneState scene_state = SceneState.SelectRoll;
    private byte[] recvMessage = new byte[PACKET_SIZE];

    private enum SceneState
    {
        SelectRoll,
        WaitConnect,
        SendAndReceive,
    }

    private void OnGUI()
    {
        switch(scene_state)
        {
            case SceneState.SelectRoll:
                if(GUILayout.Button("サーバになる"))
                {
                    trans = new TCPOpponentSingle(true, PACKET_SIZE);
                    trans.StartServer(54321);
                    scene_state = SceneState.WaitConnect;
                }
                if(GUILayout.Button("クライアントになる"))
                {
                    trans = new TCPOpponentSingle(false, PACKET_SIZE);
                    trans.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
                    scene_state = SceneState.WaitConnect;
                }

                break;

            case SceneState.WaitConnect:
                //生成した通信用ソケットに通信障害エラー時のコールバックを追加
                trans.OnErrorCommunicateEvent += trans_OnErrorCommunicateEvent;
                //接続待ち状態の表示
                GUILayout.Label("接続待ちしています");
                if(trans.IsConnected)
                {
                    trans.RunWorkAsync();
                    scene_state = SceneState.SendAndReceive;
                }
                break;

            case SceneState.SendAndReceive:
                //Sendテスト
                if(GUI.Button(new Rect(1, 1, 100, 40), "乱数を送る"))
                    trans.Send(BitConverter.GetBytes(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
                //Receiveテスト
                if(GUI.Button(new Rect(1, 50, 100, 40), "乱数を受け取る"))
                    trans.Receive(ref recvMessage);

                //結果表示
                GUI.Label(new Rect(1, 100, 150, 40), "受け取った乱数:" + BitConverter.ToInt32(recvMessage, 0));
                break;

            default:
                break;
        }
    }

    private void trans_OnErrorCommunicateEvent()
    {
        Debug.Log("Sock Err Event Called");
        trans.DisConnect();
        scene_state = SceneState.SelectRoll;
    }
}