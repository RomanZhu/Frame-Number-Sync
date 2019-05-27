using System;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;
using Logger = Sources.Tools.Logger;

public class Server : MonoBehaviour
{
    public string Ip    = "localhost";
    public float  Delta = 0.05f;

    private readonly RingBuffer<Event>    _eventsToHandle = new RingBuffer<Event>(1024);
    private readonly RingBuffer<SendData> _sendData       = new RingBuffer<SendData>(1024);
    private readonly Host                 _host           = new Host();
    private readonly byte[]               _bytes          = new byte[100];

    private Peer   _client;
    private Thread _networkThread;

    private float  _offsetTime;
    private float  _currentLocalTime;
    private ushort _tick;

    void Start()
    {
        Time.fixedDeltaTime = Delta;
        _offsetTime         = GetLocalTime();
        
        var address = new Address();
        address.SetHost(Ip);
        address.Port = 9500;
        _host.Create(address, 1);
        
        _networkThread = NetworkThread();
        _networkThread.Start();
    }

    private void OnDestroy()
    {
        _networkThread?.Abort();
    }

    private void OnGUI()
    {
        GUILayout.Label($"Tick {_tick}");
        GUILayout.Label($"Time {_currentLocalTime:F3}");
    }

    private Thread NetworkThread()
    {
        return new Thread(() =>
        {
            while (true)
            {
                while (_sendData.TryDequeue(out var data)) data.Peer.Send(0, ref data.Packet);


                if (!_host.IsSet) Thread.Sleep(15);
                if (_host.Service(15, out var @event) > 0) _eventsToHandle.Enqueue(@event);
            }
        });
    }

    private void Update()
    {
        _currentLocalTime = GetLocalTime() - _offsetTime;
        while (_eventsToHandle.TryDequeue(out var @event))
        {
            switch (@event.Type)
            {
                case EventType.Connect:
                    OnConnected(@event.Peer);
                    break;
                case EventType.Disconnect:
                    OnDisconnected(@event.Peer);
                    break;
                case EventType.Receive:
                    unsafe
                    {
                        var packet = @event.Packet;
                        var span   = new ReadOnlySpan<float>(packet.Data.ToPointer(), 1);
                        var time   = span[0];

                        fixed (byte* destination = &_bytes[0])
                        {
                            var responseSpan = new Span<float>(destination, 2);
                            responseSpan[0] = time;
                            responseSpan[1] = _currentLocalTime;
                        }

                        var newPacket = new Packet();
                        newPacket.Create(_bytes, 8, PacketFlags.None);
                        _sendData.Enqueue(new SendData {Packet = newPacket, Peer = _client});

                        packet.Dispose();
                        break;
                    }

                case EventType.Timeout:
                    OnDisconnected(@event.Peer);
                    break;
            }
        }
    }

    void FixedUpdate()
    {
        _currentLocalTime = GetLocalTime() - _offsetTime;

        //That way predicted tick count will match actual tick count
        if ((int) (_currentLocalTime / Delta) < _tick)
        {
            Logger.I.Log(this, "Skipped frame");
        }
        else
        {
            _tick++;
        }
    }

    private float GetLocalTime()
    {
        return Library.Time * 0.001f;
    }

    private void OnDisconnected(Peer eventPeer)
    {
        _client = new Peer();
    }

    private void OnConnected(Peer eventPeer)
    {
        _client = eventPeer;
    }
}