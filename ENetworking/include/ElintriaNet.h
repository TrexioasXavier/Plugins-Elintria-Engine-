/*
    ElintriaNet - a small, standalone TCP+UDP networking DLL.
    ---------------------------------------------------------
    This is the PUBLIC C ABI. It is deliberately a flat C API using opaque
    handles and plain-old-data types only, so the DLL can be consumed from:

        - any C or C++ compiler (MSVC, clang, gcc, MinGW)
        - a Debug build of your app against a Release build of the DLL
        - other languages (C#/P.Invoke, Python/ctypes, Rust, ...)

    Nothing from the C++ standard library crosses the DLL boundary, so there
    is no "must match the exact toolset" trap.

    If you are writing C++, prefer ElintriaNet.hpp - a header-only wrapper
    that gives you Server/Client/Packet classes on top of this API. That
    wrapper is compiled into YOUR binary, so it is always ABI-safe.

    Using this library outside of the Elintria engine:
        1. copy ElintriaNet.h, ElintriaNet.hpp, ElintriaNetworking.lib and
           ElintriaNetworking.dll into your project
        2. #include "ElintriaNet.hpp"
        3. done - the .lib is auto-linked on MSVC (see EN_NO_AUTO_LINK)
*/
#ifndef ELINTRIA_NET_H
#define ELINTRIA_NET_H

/* ------------------------------------------------------------------ */
/* Linkage                                                            */
/* ------------------------------------------------------------------ */

#if defined(_WIN32)
#  if defined(ELINTRIANET_BUILD)
#    define EN_API __declspec(dllexport)
#  elif defined(ELINTRIANET_STATIC)
#    define EN_API
#  else
#    define EN_API __declspec(dllimport)
#    if defined(_MSC_VER) && !defined(EN_NO_AUTO_LINK)
#      pragma comment(lib, "ElintriaNetworking.lib")
#    endif
#  endif
#  define EN_CALL __cdecl
#else
#  define EN_API  __attribute__((visibility("default")))
#  define EN_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* ------------------------------------------------------------------ */
/* Limits and version                                                 */
/* ------------------------------------------------------------------ */

#define EN_VERSION_MAJOR 1
#define EN_VERSION_MINOR 0
#define EN_VERSION_PATCH 0

/* Largest payload for a reliable (TCP) message. */
#define EN_MAX_RELIABLE_SIZE   1048576u   /* 1 MiB */

/* Largest payload for an unreliable (UDP) message. Kept under a typical
   1500-byte MTU so datagrams are not fragmented on the wire. */
#define EN_MAX_UNRELIABLE_SIZE 1200u

/* Upper bound on peers a single server may host (select() fd_set limit). */
#define EN_MAX_PEERS 1023

/* ------------------------------------------------------------------ */
/* Types                                                              */
/* ------------------------------------------------------------------ */

typedef struct EnServer EnServer;   /* opaque */
typedef struct EnClient EnClient;   /* opaque */

/* Identifies one connected client. Ids are never reused within a session.
   EN_INVALID_PEER (0) is never a valid peer. */
typedef unsigned int EnPeerId;
#define EN_INVALID_PEER 0u

typedef enum EnResult
{
    EN_OK                =  0,
    EN_ERR_GENERIC       = -1,
    EN_ERR_NOT_INIT      = -2,  /* EnInitialize() was not called          */
    EN_ERR_INVALID_ARG   = -3,
    EN_ERR_NO_PEER       = -4,  /* unknown / already disconnected peer    */
    EN_ERR_NOT_CONNECTED = -5,
    EN_ERR_TOO_LARGE     = -6,  /* payload exceeds the channel limit      */
    EN_ERR_SOCKET        = -7,  /* underlying socket failure              */
    EN_ERR_TIMEOUT       = -8
} EnResult;

typedef enum EnEventType
{
    EN_EVENT_NONE         = 0,
    EN_EVENT_CONNECTED    = 1,  /* peer joined (server) / we joined (client) */
    EN_EVENT_DISCONNECTED = 2,
    EN_EVENT_MESSAGE      = 3
} EnEventType;

typedef enum EnChannel
{
    /* Ordered, guaranteed delivery over TCP. Use for chat, spawn/despawn,
       inventory - anything that must not be lost. */
    EN_CHANNEL_RELIABLE   = 0,
    /* Fire-and-forget over UDP. May be dropped, duplicated or reordered.
       Use for per-frame state such as transforms. */
    EN_CHANNEL_UNRELIABLE = 1
} EnChannel;

typedef struct EnEvent
{
    EnEventType   type;
    /* On a server: the peer the event concerns.
       On a client: the id the server assigned to this client. */
    EnPeerId      peer;
    EnChannel     channel;     /* only meaningful for EN_EVENT_MESSAGE    */
    const unsigned char* data; /* payload; NULL unless EN_EVENT_MESSAGE   */
    unsigned int  size;        /* payload length in bytes                 */
} EnEvent;

/* IMPORTANT: the 'data' pointer refers to memory owned by the server/client
   object and stays valid only until the NEXT call to EnServerPoll/
   EnClientPoll on that same object. Copy it if you need to keep it. */

/* ------------------------------------------------------------------ */
/* Library lifetime                                                   */
/* ------------------------------------------------------------------ */

/* Must be called once before anything else (starts Winsock).
   Reference counted, so nested Initialize/Shutdown pairs are safe. */
EN_API EnResult    EN_CALL EnInitialize(void);
EN_API void        EN_CALL EnShutdown(void);

/* Human-readable description of the last failure on the calling thread. */
EN_API const char* EN_CALL EnGetLastError(void);

/* e.g. "ElintriaNet 1.0.0" */
EN_API const char* EN_CALL EnVersionString(void);

/* ------------------------------------------------------------------ */
/* Server                                                             */
/* ------------------------------------------------------------------ */

EN_API EnServer*   EN_CALL EnServerCreate(void);
EN_API void        EN_CALL EnServerDestroy(EnServer* server);

/* Binds TCP + UDP on 'port' and begins listening. maxPeers is clamped to
   [1, EN_MAX_PEERS]. Non-blocking: returns immediately. */
EN_API EnResult    EN_CALL EnServerStart(EnServer* server,
                                         unsigned short port,
                                         int maxPeers);

/* Closes all connections and the listening sockets. The server object can
   be started again afterwards. */
EN_API void        EN_CALL EnServerStop(EnServer* server);

EN_API int         EN_CALL EnServerIsRunning(EnServer* server);

/* Pumps the sockets and reports one queued event.
   Returns 1 and fills 'out' when an event was dequeued, 0 when idle.
   Never blocks. Call it in a loop until it returns 0, once per frame. */
EN_API int         EN_CALL EnServerPoll(EnServer* server, EnEvent* out);

EN_API EnResult    EN_CALL EnServerSend(EnServer* server,
                                        EnPeerId peer,
                                        const void* data,
                                        unsigned int size,
                                        EnChannel channel);

/* Sends to every connected peer. Pass EN_INVALID_PEER for 'except' to
   include everyone (typically you pass the originating peer). */
EN_API EnResult    EN_CALL EnServerBroadcast(EnServer* server,
                                             const void* data,
                                             unsigned int size,
                                             EnChannel channel,
                                             EnPeerId except);

/* Drops a peer. An EN_EVENT_DISCONNECTED event is still reported. */
EN_API void        EN_CALL EnServerDisconnect(EnServer* server, EnPeerId peer);

EN_API int         EN_CALL EnServerPeerCount(EnServer* server);

/* "127.0.0.1:53124" - valid until that peer disconnects. */
EN_API const char* EN_CALL EnServerPeerAddress(EnServer* server, EnPeerId peer);

/* The port the server actually bound (useful when you passed port 0). */
EN_API unsigned short EN_CALL EnServerPort(EnServer* server);

/* ------------------------------------------------------------------ */
/* Client                                                             */
/* ------------------------------------------------------------------ */

EN_API EnClient*   EN_CALL EnClientCreate(void);
EN_API void        EN_CALL EnClientDestroy(EnClient* client);

/* Establishes the TCP connection to host:port.
   'host' may be a dotted IPv4 address or a hostname.
   Blocks for at most timeoutMs while connecting (pass 0 for 5000 ms).

   The session handshake then completes asynchronously. EnClientPoll reports
   EN_EVENT_CONNECTED once it lands, and only from that point on does
   EnClientPeerId() return your id and the unreliable channel accept sends.
   The reliable channel is usable as soon as this call succeeds.

   The handshake is not waited for here on purpose: blocking would deadlock
   a "listen server" that hosts and plays in one process, because the server
   could not accept the connection while the same thread sits in Connect. */
EN_API EnResult    EN_CALL EnClientConnect(EnClient* client,
                                           const char* host,
                                           unsigned short port,
                                           unsigned int timeoutMs);

EN_API void        EN_CALL EnClientDisconnect(EnClient* client);

/* True while the TCP connection is up. Note this becomes true before the
   handshake completes - see EnClientConnect. */
EN_API int         EN_CALL EnClientIsConnected(EnClient* client);

/* Same contract as EnServerPoll. */
EN_API int         EN_CALL EnClientPoll(EnClient* client, EnEvent* out);

EN_API EnResult    EN_CALL EnClientSend(EnClient* client,
                                        const void* data,
                                        unsigned int size,
                                        EnChannel channel);

/* The id the server assigned to this client, or EN_INVALID_PEER. */
EN_API EnPeerId    EN_CALL EnClientPeerId(EnClient* client);

/* ------------------------------------------------------------------ */
/* Keeping a connection alive across the internet                     */
/* ------------------------------------------------------------------ */

/* Both ends exchange a heartbeat while otherwise idle. It does two jobs
   that only matter once traffic leaves the local network:

     - a peer that vanished (cable pulled, laptop slept, route died) is
       noticed in seconds instead of the minutes TCP would take, and
     - it keeps the NAT mapping for the UDP channel open. A home router
       forgets an idle outbound UDP mapping after a minute or so, and when
       it does, everything the server sends on the unreliable channel is
       dropped silently while the reliable channel still works - the
       failure looks like "movement stopped updating but chat still
       works".

   The defaults suit a game: a heartbeat every second, and a peer declared
   gone after 10 seconds of silence. Timeouts are in milliseconds; pass 0
   to restore the default. A timeout of 0 after that disables the check,
   which is only sensible when debugging with breakpoints. */
#define EN_DEFAULT_HEARTBEAT_MS 1000u
#define EN_DEFAULT_TIMEOUT_MS   10000u

EN_API void EN_CALL EnServerSetTimeout(EnServer* server,
                                       unsigned int timeoutMs,
                                       unsigned int heartbeatMs);
EN_API void EN_CALL EnClientSetTimeout(EnClient* client,
                                       unsigned int timeoutMs,
                                       unsigned int heartbeatMs);

/* Round-trip time in milliseconds, measured from the heartbeat exchange.
   Returns -1 before the first reply has come back. */
EN_API int EN_CALL EnClientPing(EnClient* client);
EN_API int EN_CALL EnServerPeerPing(EnServer* server, EnPeerId peer);

/* ------------------------------------------------------------------ */
/* Finding servers on the local network                               */
/* ------------------------------------------------------------------ */

/* A server that has been given a name answers discovery probes broadcast
   on its port, so a client on the same wifi can list games without anyone
   typing an address. Pass NULL or "" to stop answering.

   Only the local network: a broadcast does not cross a router, so playing
   over the internet still means entering an address. */
EN_API void EN_CALL EnServerSetDiscoveryName(EnServer* server,
                                             const char* name);

typedef struct EnDiscoveredServer
{
    char         name[64];    /* what the server called itself       */
    char         address[64]; /* dotted quad, ready to connect to    */
    unsigned short port;
    int          players;     /* peers currently connected           */
    int          maxPlayers;
} EnDiscoveredServer;

/* Opens a broadcast socket and starts probing |port| on the local
   network. Call EnDiscoverPoll each frame to collect answers, then
   EnDiscoverEnd. Only one scan may be open at a time. */
EN_API EnResult EN_CALL EnDiscoverBegin(unsigned short port);

/* Re-broadcasts when due and reports one newly found server per call,
   returning 1 when it filled |out| and 0 when there is nothing new.
   A server already reported is not reported twice. */
EN_API int EN_CALL EnDiscoverPoll(EnDiscoveredServer* out);

EN_API void EN_CALL EnDiscoverEnd(void);

/* ------------------------------------------------------------------ */
/* Local addresses                                                    */
/* ------------------------------------------------------------------ */

/* This machine's address on the local network, for telling another
   player what to connect to. Writes a dotted quad into |buf| and returns
   1, or returns 0 when no non-loopback IPv4 address exists. */
EN_API int EN_CALL EnGetLocalAddress(char* buf, int bufSize);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* ELINTRIA_NET_H */
