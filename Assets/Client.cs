using System;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;
using Logger = Sources.Tools.Logger;

public class Client : MonoBehaviour
{
    public string Ip = "localhost";
    [Tooltip("When _tick is less than _targetTick by that amount - snap will happen.")]
    public int MaxTicksBehind = 4;
    [Tooltip("How far prediction can go without receiving packets from server (in seconds)")]
    public float MaxPredictedTime = 1f;
    [Tooltip("Delay between sending request to server (in seconds)")]
    public float TimeUpdateDelay = 0.1f;
    
    [Header("1/TickRate")]
    public float NormalDelta = 0.05f;
    [Tooltip("Should be slightly bigger than Normal Delta")]
    public float LowerDelta  = 0.052f;
    [Tooltip("Should be slightly smaller than Normal Delta")]
    public float HigherDelta = 0.048f;


    private readonly RingBuffer<Event>    _eventsToHandle = new RingBuffer<Event>(1024);
    private readonly RingBuffer<SendData> _sendData       = new RingBuffer<SendData>(1024);
    private readonly Host                 _host           = new Host();
    private readonly byte[]               _bytes          = new byte[100];

    public  Peer   ServerConnection;
    private Thread _networkThread;

    private float _nextTimeUpdate;
    private float _offsetTime;
    private float _currentDelta = 0.05f;
    private float _currentLocalTime;
    private float _lastReceivedTime;
    private float _localTimeAtLastReceivedTime;
    private float _approximatedTime;
    private float _lastDelay;
    private float _timeChange;
    private int   _tick;
    private int   _targetTick;

    void Start()
    {
        Time.fixedDeltaTime = NormalDelta;
        _offsetTime         = GetLocalTime();

        _host.Create();
        var address = new Address();
        address.SetHost(Ip);
        address.Port = 9500;
        _host.Connect(address, 1);

        _networkThread = NetworkThread();
        _networkThread.Start();
    }

    private void OnDestroy()
    {
        _networkThread?.Abort();
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

    private void OnGUI()
    {
        GUILayout.Label($"LastReceivedTime {_lastReceivedTime:F3}");
        GUILayout.Label($"LocalAtLastReceivedTime {_localTimeAtLastReceivedTime:F3}");
        GUILayout.Label($"ApproximatedTime {_approximatedTime:F3}");
        GUILayout.Label($"Delay {_lastDelay:F3}");
        GUILayout.Label($"TimeChange {_timeChange:F3}");
        GUILayout.Label($"Delta {_currentDelta:F3}");
        GUILayout.Label($"Tick {_tick}");
        GUILayout.Label($"TargetTick {_targetTick}");
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
                        var packet            = @event.Packet;
                        var span              = new ReadOnlySpan<float>(packet.Data.ToPointer(), 2);
                        var returnedLocalTime = span[0];
                        var serverTime        = span[1];

                        if (_lastReceivedTime < serverTime)
                        {
                            _lastDelay                   = (_currentLocalTime - returnedLocalTime) / 2;
                            _lastReceivedTime            = serverTime;
                            _localTimeAtLastReceivedTime = _currentLocalTime;
                        }

                        packet.Dispose();
                        break;
                    }

                case EventType.Timeout:
                    OnDisconnected(@event.Peer);
                    break;
            }
        }

        var lastApproximatedTime = _approximatedTime;
        _approximatedTime = _lastReceivedTime + (_currentLocalTime - _localTimeAtLastReceivedTime) + _lastDelay;
        _targetTick       = (int) (_approximatedTime / NormalDelta);

        _timeChange = _approximatedTime - lastApproximatedTime;

        var tickDifference = _tick - _targetTick;
        if (tickDifference > 0)
        {
            Time.fixedDeltaTime = LowerDelta;
            _currentDelta        = LowerDelta;
        }

        if (tickDifference == 0)
        {
            Time.fixedDeltaTime = NormalDelta;
            _currentDelta        = NormalDelta;
        }

        if (tickDifference < 0)
        {
            Time.fixedDeltaTime = HigherDelta;
            _currentDelta        = HigherDelta;
        }

        if (ServerConnection.IsSet)
        {
            if (_nextTimeUpdate < _currentLocalTime)
            {
                _nextTimeUpdate = _currentLocalTime + TimeUpdateDelay;
                unsafe
                {
                    fixed (byte* destination = &_bytes[0])
                    {
                        var span = new Span<float>(destination, 1);
                        span[0] = _currentLocalTime;
                    }

                    var packet = new Packet();
                    packet.Create(_bytes, 4, PacketFlags.None);
                    _sendData.Enqueue(new SendData {Packet = packet, Peer = ServerConnection});
                }
            }
        }
        
    }

    void FixedUpdate()
    {
        if (ServerConnection.IsSet)
        {
            _currentLocalTime = GetLocalTime() - _offsetTime;
            _approximatedTime = _lastReceivedTime + (_currentLocalTime - _localTimeAtLastReceivedTime) + _lastDelay;
            _targetTick       = (int) (_approximatedTime / NormalDelta);

            if (_approximatedTime - _lastReceivedTime > MaxPredictedTime)
            {
                Logger.I.Log(this, $"Ticks stalled {_tick}");
            }
            else
            {
                var tickDifference = _tick - _targetTick;

                if (tickDifference < -MaxTicksBehind)
                {
                    Logger.I.Log(this, $"Tick reset were {_tick} now {_targetTick}");
                    _tick = _targetTick;
                }
                else
                {
                    if (tickDifference > 10)
                    {
                        //Shouldn't happen ever 
                        Logger.I.Log(this, $"Ticks stalled {_tick}");
                    }
                    else
                    {
                        _tick++;
                    }
                }
            }
        }
    }
    
    private float GetLocalTime()
    {
        return Library.Time * 0.001f;
    }
    
    private void OnDisconnected(Peer eventPeer)
    {
        ServerConnection = new Peer();
    }

    private void OnConnected(Peer eventPeer)
    {
        ServerConnection = eventPeer;
    }
}

public struct SendData
{
    public Peer   Peer;
    public Packet Packet;
}