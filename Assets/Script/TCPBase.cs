using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public interface ITCPBase
{
    void ConnectAsync();

    bool isConnected { get; }

    NetworkStream GetStream();

    void Close();
}