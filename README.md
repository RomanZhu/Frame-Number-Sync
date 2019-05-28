# Frame-Number-Sync

## Features
- Time synchronization
- Client tick frequency modification
- [Uses ENet for networking](https://github.com/nxrighthere/ENet-CSharp)
- Native memory allocations
- Networking is handled by separate thread, lockless communication with that thread
- Simple logger

## Overview
Project keeps time in sync between server and client. Both sides know how many ticks were performed in a certain time.


Algorithm can survive throught ping spikes and bad network conditions. 
For simplicity server supports only one client.

| Overview  |  
|--|
| [![][preview1]](https://www.youtube.com/watch?v=tZppqKbpuL4) |

## Client
Client sends request to server 1/TimeUpdateDelay times per second. Request contains client local time.

When he recieves response - he finds delta between local time and first float in the packet. 
That delta is added to second float in the packet, then resulting time is increased on each update.
From that approximation client calculates count of ticks which happend on the server.
Client modifies his tick frequency to smoothly reach approximated tick value. 

#### Ideas for more advanced approximation (Will try to implement in the future)
##### 1
Client keeps smooth delta averege and spikes counter.
 
If new delta is close to or less than averege and new calculated approximation is close to predicted approximation, then predicted value is not updated with new approximation. After that delta averege is updated with new delta, spikes counter is reduced.

If new delta is bigger by significant amount, then that update is skipped entirely, but spikes counter is increased.

When spikes counter reaches certain value, client will set smooth delta averege to be equal to last spike's delta and predicted approximation to be equal to last spike's calculated approximation.
##### 2
In that implementation only server will send his local time to clients with specified frequency.
Client can utilize ENet.Peer's RTT value to understand how far behind he is generally.

When client receives first packet with server time, he will add RTT/2 to it and store local time and offset(RTT/2).
When client receives next packets, he will calculate delta between current local time and last stored local time, then total time will be equal to receivedTime + offset + delta. Then local time is stored again.

In that approach the most important part is getting correct RTT value at first packet. 

### Fields
- IP - Client will try to connect to that IP.
- Max Ticks Behind - If current tick is less than approximated tick by more than that value, then tick will be snapped to approximated tick.
- Max Predicted Time  - If approximated time is greater than last received time by more than that value, then client will stop executing ticks.
- Time Update Delay - Client will send request to get server time 1/TimeUpdateDelay times per second.
- Normal Delta - Ticks executed 1/Delta times per second. This delta is used when local tick count and approximated tick count are equal.
- Lower Delta - When count of executed ticks is greater than approximated tick count, then client will use this delta to slow down. 
- Higher Delta - When count of executed ticks is less than approximated tick count, then client will use this delta to speed up. 

## Server
Server increases his local tick count. When he receives request from client - he will send back time from request and his local time.

### Fields
- IP - Server will bind itself to that IP.
- Delta - Ticks executed 1/Delta times per second.

## Dependencies
- Unity 2019.1
- ENet CSharp 2.2.6 

[preview1]: https://i.imgur.com/tzS55KM.png
