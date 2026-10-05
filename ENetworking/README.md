# ENetworking

TCP and UDP networking for Elintria. A package, not part of the engine — a
project has it only if you install it.

Installing it adds three things:

- a **Networking tab** where you start a server or connect a client, watch
  who is connected and how far away they are, and scan the local network
- a **Network Identity** component that keeps an object's transform in step
  across the network, with per-player ownership
- the **ElintriaNet** library on your scripts' include path, for when you want
  to send your own data

## Installing

**Window ▸ Plugins**, then either:

- **Install from Folder** and pick this folder, or
- paste its path — or a GitHub link — into the box and press **Install**

It lands in `<your project>/Plugins/ENetworking/`. Nothing in the package runs
during the install; it is a file copy and a manifest read.

Uninstalling deletes that folder. The checkbox beside it turns the package off
without removing it, which is the quicker way to answer "is this what broke my
build?"

## Making two things talk

1. Open **Window ▸ Networking**.
2. Press **Start Server**. The status line turns green and names the port.
3. Somewhere else — a second copy of the editor, or a built game — press
   **Connect** with the server's address. `127.0.0.1` for the same machine.

The peer list fills in as clients arrive, and the session log records joins,
departures and errors.

## Syncing objects

Add **Network Identity** to any object whose position should match on every
machine.

| | |
|---|---|
| **Network Id** | what matches this object to the same object elsewhere. Assigned when you add the component and saved with the scene, so both ends agree as long as both run the same scene. Zero means "not synced". |
| **Sync Position / Rotation / Scale** | what to send. Sending less costs less. |
| **Send Rate** | updates per second, 20 by default |
| **Interpolate** | ease between updates instead of jumping to each one. Worth leaving on for anything that moves continuously. |

### Who drives an object

Every synced object has an owner: the server, or one connected peer. Only the
owner sends its transform; everyone else receives and applies it.

Tick **Player Slot** and the server hands that object to a client as it
joins, and takes it back when it leaves. That is what lets two people each
control their own character over one connection — each client sends only the
object it was given, the server relays it, and the server keeps authority
over everything else.

In a script, the test is `identity->isMine()`, or
`NetworkSession::LocalPlayer()` for the one character this machine was
given. A client that tries to move an object it does not own is ignored by
the server, so this is a rule rather than a convention.

Both ends must be running the same scene, because the ids are the scene's.

### Finding a game on the same wifi

A server given a name answers discovery probes, so a player on the same
network can list games without anyone reading out an address:

```cpp
NetworkSession::StartServer(7777, 8, "My Game");   // advertises

NetworkSession::DiscoverBegin(7777);               // on the other machine
for (auto& s : NetworkSession::DiscoverPoll())
    printf("%s at %s:%u  %d/%d
", s.name.c_str(), s.address.c_str(),
           s.port, s.players, s.maxPlayers);
NetworkSession::DiscoverEnd();
```

A broadcast does not cross a router, so this finds games on the local
network and nothing further. `NetworkSession::LocalAddress()` gives the
address to read out for anyone further away.

### Staying connected across the internet

Both ends exchange a heartbeat while otherwise idle, which does two jobs
that only matter once traffic leaves the local network:

- a peer that vanished is noticed in about ten seconds rather than the
  minutes TCP would take, and
- the UDP mapping in the player's router is kept open. Letting it lapse is
  the classic *"movement stopped updating but chat still works"* failure,
  because only the unreliable channel dies.

`NetworkSession::PingMs()` reports the round trip, and the Networking tab
shows it per peer.

## From a script

`NetworkSession` is the engine's own handle on the connection the Networking
tab manages, so a script shares that one connection rather than opening a
second:

```cpp
#include <ENetworkSync.h>
using ElintriaEngine::Net::NetworkSession;

if (NetworkSession::IsRunning())
    NetworkSession::SendToAll("player jumped");

// Whatever arrived since the last frame.
for (auto& m : NetworkSession::TakeReceived())
    std::cout << "peer " << m.from << ": " << m.text << std::endl;
```

`Samples` in the Plugin Manager copies **NetPing** into your Scripts folder — a
few lines that ping every couple of seconds and print what comes back.

For your own socket, independent of the editor's, the package puts
`ElintriaNet.hpp` on your include path and links the library for you:

```cpp
#include "ElintriaNet.hpp"

en::Context net;            // starts Winsock, stops it on scope exit
en::Server server;
server.Start(7777, 32);

en::Event e;
while (server.Poll(e)) { /* Connected / Disconnected / Message */ }
```

See `include/ElintriaNet.h` for the full C API and `ElintriaNet.hpp` for the
C++ wrapper. Both are the library's own documentation.

## In a built game

The build copies `ElintriaNetworking.dll` next to `Game.exe` and restores every
Network Identity from the scene. Which end the game is comes from the command
line, because one build has to be able to be either:

```
Game.exe                     single player, no socket is opened
Game.exe --server            listen on 7777
Game.exe --server=9000       listen on 9000
Game.exe --connect=host:port join that server
```

## What is in the package

```
plugin.json              the manifest the editor reads
include/ElintriaNet.h    the C API
include/ElintriaNet.hpp  the C++ wrapper (what you normally use)
lib/Win32/               the import library
bin/Win32/               the DLL, copied next to your game
samples/NetPing.*        an example script
```

The binaries are **32-bit**, matching the engine's own build. A 64-bit engine
would need the library rebuilt for it.

They are also a **Release** build, even though the engine you are running is
probably a Debug one. That is deliberate: the library exposes a flat C ABI
with nothing from the C++ standard library crossing the boundary, so mixing
the two is safe - and a Debug DLL would drag in `ucrtbased.dll` and friends,
which are not redistributable and would leave a shipped game unable to start
on a machine without Visual Studio.

## Limits worth knowing

- **The server owns anything not given to a client.** A client cannot spawn
  or claim objects; it drives only what it was handed.
- **No object spawning at runtime.** Both ends work from the same scene, so
  things that come and go are done with a pool, as the Arena sample does
  with its bullets.
- **Reliable messages** are ordered and guaranteed, up to 1 MiB.
  **Unreliable** ones may be dropped or reordered and are capped near 1200
  bytes; transform updates use this channel, and are split across several
  datagrams when a scene has more synced objects than fit in one.
- **No encryption and no authentication.** Anything on the network can connect
  to an open port and send whatever it likes. Fine on a LAN or behind a
  tunnel; do not expose a server to the open internet and trust what arrives.
- **IPv4.**
- **No NAT punch-through or relay.** Internet play means the host is
  reachable: usually port-forwarding TCP and UDP on the chosen port, since
  the game uses both.
