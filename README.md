# Frame-Number-Sync

## Features
- Time synchronization
- Client tick frequency modification
- [Uses ENet for networking](https://github.com/nxrighthere/ENet-CSharp)
- Networking is handled by a separate thread, lockless communication with that thread
- Simple logger

## Overview
The project keeps time in sync between server and client. Both sides know how many ticks were performed at a certain time.


The algorithm can survive through ping spikes and bad network conditions. 
For simplicity, the server supports only one client.

| Overview  |  
|--|
| [![][preview1]](https://www.youtube.com/watch?v=tZppqKbpuL4) |

## Client
Client sends request to server 1/TimeUpdateDelay times per second. Request contains client local time.

When he receives a response - he finds delta between local time and first float in the packet. 
That delta is added to the second float in the packet, the resulting time is increased on each update.
From that approximation, the client calculates the number of ticks that happened on the server.
The client modifies his tick frequency to smoothly reach approximated tick value. 

#### Idea for more advanced approximation (Will try to implement in the future)
In that implementation, only the server will send his local time to clients with specified frequency.
The client can utilize ENet.Peer's RTT value to understand how far behind he is generally.

When client receives the first packet with server time, he will add RTT/2 to it and store local time and offset(RTT/2).
When client receives the next packets, he will calculate the delta between current local time and last stored local time, then the total time will be equal to receivedTime + offset + delta. Then local time is stored again.

In that approach, the most important part is getting the correct RTT value at the first packet. 

### Fields
- IP - The client will try to connect to that IP.
- Max Ticks Behind - If the current tick is less than approximated tick by more than that value, then a tick will be snapped to approximated tick.
- Max Predicted Time  - If approximated time is greater than last received time by more than that value, then the client will stop executing ticks.
- Time Update Delay - Client will send request to get server time 1/TimeUpdateDelay times per second.
- Normal Delta - Ticks executed 1/Delta times per second. This delta is used when local tick count and approximated tick count are equal.
- Lower Delta - When the count of executed ticks is greater than approximated tick count, then the client will use this delta to slow down. 
- Higher Delta - When the count of executed ticks is less than an approximated tick count, then the client will use this delta to speed up. 

## Server
The server increases his local tick count. When he receives a request from a client - he will send back time from request and his local time.

### Fields
- IP - Server will bind itself to that IP.
- Delta - Ticks executed 1/Delta times per second.

## Dependencies
- Unity 2019.1
- ENet CSharp 2.2.6 

[preview1]: https://i.imgur.com/tzS55KM.png
