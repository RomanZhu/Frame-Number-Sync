using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    public Server Server;
    public Client Client;

    private string _ip = "localhost";
    private void OnGUI()
    {
        _ip = GUILayout.TextField(_ip);
        
        if (GUILayout.Button("Server"))
        {
            Server.Ip = _ip;
            Server.enabled = true;
            enabled = false;
        }

        if (GUILayout.Button("Client"))
        {
            Client.Ip = _ip;
            Client.enabled = true;
            enabled = false;
        }
    }
}
