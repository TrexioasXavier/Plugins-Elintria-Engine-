/*
    ElintriaNet - header-only C++ convenience wrapper.
    --------------------------------------------------
    Everything here compiles into YOUR binary and only ever calls the flat C
    ABI in ElintriaNet.h, so std::string / std::vector never cross the DLL
    boundary. That means you can mix Debug and Release, or use a completely
    different compiler than the one that built the DLL.

    Typical server frame:

        en::Server server;
        server.Start(7777);

        en::Event e;
        while (server.Poll(e))
        {
            switch (e.type)
            {
            case en::EventType::Connected:
                printf("peer %u joined from %s\n",
                       e.peer, server.PeerAddress(e.peer).c_str());
                break;

            case en::EventType::Message:
            {
                en::Packet in(e.data, e.size);
                std::string text;
                in >> text;
                server.Broadcast(in, en::Channel::Reliable, e.peer);
                break;
            }

            case en::EventType::Disconnected:
                printf("peer %u left\n", e.peer);
                break;

            default:
                break;
            }
        }

    Typical client frame:

        en::Client client;
        if (!client.Connect("127.0.0.1", 7777))
            printf("failed: %s\n", en::LastError().c_str());

        en::Packet p;
        p << std::string("hello");
        client.Send(p);

        // per frame, unreliable position updates:
        en::Packet move;
        move << x << y << z;
        client.Send(move, en::Channel::Unreliable);
*/
#ifndef ELINTRIA_NET_HPP
#define ELINTRIA_NET_HPP

#include "ElintriaNet.h"

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

namespace en
{
    typedef EnPeerId PeerId;
    static const PeerId InvalidPeer = EN_INVALID_PEER;

    enum class Channel
    {
        Reliable   = EN_CHANNEL_RELIABLE,
        Unreliable = EN_CHANNEL_UNRELIABLE
    };

    enum class EventType
    {
        None         = EN_EVENT_NONE,
        Connected    = EN_EVENT_CONNECTED,
        Disconnected = EN_EVENT_DISCONNECTED,
        Message      = EN_EVENT_MESSAGE
    };

    /* ------------------------------------------------------------------ */
    /* Library lifetime                                                   */
    /* ------------------------------------------------------------------ */

    inline bool Initialize()          { return EnInitialize() == EN_OK; }
    inline void Shutdown()            { EnShutdown(); }
    inline std::string LastError()    { return EnGetLastError(); }
    inline std::string VersionString(){ return EnVersionString(); }

    /* RAII helper: initialises on construction, shuts down on destruction. */
    class Context
    {
    public:
        Context()  { m_ok = Initialize(); }
        ~Context() { if (m_ok) Shutdown(); }

        Context(const Context&)            = delete;
        Context& operator=(const Context&) = delete;

        bool Ok() const { return m_ok; }
        explicit operator bool() const { return m_ok; }

    private:
        bool m_ok = false;
    };

    /* ------------------------------------------------------------------ */
    /* Packet - a tiny binary reader/writer                               */
    /* ------------------------------------------------------------------ */

    /*
        Values are stored little-endian with fixed widths, so a packet written
        on one machine reads back identically on another.

        Reads are bounds checked. Rather than throwing, a failed read leaves
        the value untouched and latches a failure flag - check Ok() once after
        a group of reads instead of testing every one:

            en::Packet in(e.data, e.size);
            uint32_t id; float x, y, z;
            in >> id >> x >> y >> z;
            if (!in.Ok())
                return;             // truncated or malformed, ignore it
    */
    class Packet
    {
    public:
        Packet() = default;

        Packet(const void* data, size_t size)
        {
            if (data != nullptr && size > 0)
            {
                const unsigned char* bytes = (const unsigned char*)data;
                m_bytes.assign(bytes, bytes + size);
            }
        }

        /* ---- writing ---- */

        Packet& WriteRaw(const void* data, size_t size)
        {
            if (data != nullptr && size > 0)
            {
                const unsigned char* bytes = (const unsigned char*)data;
                m_bytes.insert(m_bytes.end(), bytes, bytes + size);
            }
            return *this;
        }

        Packet& operator<<(bool value)
        {
            m_bytes.push_back(value ? 1u : 0u);
            return *this;
        }

        Packet& operator<<(int8_t value)   { return WriteInt((uint8_t)value, 1); }
        Packet& operator<<(uint8_t value)  { return WriteInt(value, 1); }
        Packet& operator<<(int16_t value)  { return WriteInt((uint16_t)value, 2); }
        Packet& operator<<(uint16_t value) { return WriteInt(value, 2); }
        Packet& operator<<(int32_t value)  { return WriteInt((uint32_t)value, 4); }
        Packet& operator<<(uint32_t value) { return WriteInt(value, 4); }
        Packet& operator<<(int64_t value)  { return WriteInt((uint64_t)value, 8); }
        Packet& operator<<(uint64_t value) { return WriteInt(value, 8); }

        Packet& operator<<(float value)
        {
            uint32_t bits = 0;
            memcpy(&bits, &value, 4);
            return WriteInt(bits, 4);
        }

        Packet& operator<<(double value)
        {
            uint64_t bits = 0;
            memcpy(&bits, &value, 8);
            return WriteInt(bits, 8);
        }

        /* Length-prefixed, so strings may contain embedded nulls. */
        Packet& operator<<(const std::string& value)
        {
            WriteInt((uint32_t)value.size(), 4);
            return WriteRaw(value.data(), value.size());
        }

        Packet& operator<<(const char* value)
        {
            return *this << std::string(value != nullptr ? value : "");
        }

        /* ---- reading ---- */

        bool ReadRaw(void* destination, size_t size)
        {
            if (m_failed || m_read + size > m_bytes.size())
            {
                m_failed = true;
                return false;
            }
            memcpy(destination, m_bytes.data() + m_read, size);
            m_read += size;
            return true;
        }

        Packet& operator>>(bool& value)
        {
            uint8_t raw = 0;
            if (ReadInt(raw, 1))
                value = (raw != 0);
            return *this;
        }

        Packet& operator>>(int8_t& value)   { uint8_t  r=0; if (ReadInt(r,1)) value=(int8_t)r;   return *this; }
        Packet& operator>>(uint8_t& value)  { uint8_t  r=0; if (ReadInt(r,1)) value=r;           return *this; }
        Packet& operator>>(int16_t& value)  { uint16_t r=0; if (ReadInt(r,2)) value=(int16_t)r;  return *this; }
        Packet& operator>>(uint16_t& value) { uint16_t r=0; if (ReadInt(r,2)) value=r;           return *this; }
        Packet& operator>>(int32_t& value)  { uint32_t r=0; if (ReadInt(r,4)) value=(int32_t)r;  return *this; }
        Packet& operator>>(uint32_t& value) { uint32_t r=0; if (ReadInt(r,4)) value=r;           return *this; }
        Packet& operator>>(int64_t& value)  { uint64_t r=0; if (ReadInt(r,8)) value=(int64_t)r;  return *this; }
        Packet& operator>>(uint64_t& value) { uint64_t r=0; if (ReadInt(r,8)) value=r;           return *this; }

        Packet& operator>>(float& value)
        {
            uint32_t bits = 0;
            if (ReadInt(bits, 4))
                memcpy(&value, &bits, 4);
            return *this;
        }

        Packet& operator>>(double& value)
        {
            uint64_t bits = 0;
            if (ReadInt(bits, 8))
                memcpy(&value, &bits, 8);
            return *this;
        }

        Packet& operator>>(std::string& value)
        {
            uint32_t length = 0;
            if (!ReadInt(length, 4))
                return *this;

            if (m_read + length > m_bytes.size())
            {
                m_failed = true;
                return *this;
            }

            value.assign((const char*)m_bytes.data() + m_read, length);
            m_read += length;
            return *this;
        }

        /* ---- state ---- */

        /* False once any read has run past the end of the buffer. */
        bool Ok() const { return !m_failed; }

        const unsigned char* Data() const { return m_bytes.empty() ? nullptr : m_bytes.data(); }
        size_t Size() const               { return m_bytes.size(); }
        bool   Empty() const              { return m_bytes.empty(); }

        /* Bytes not yet consumed by reads. */
        size_t Remaining() const { return m_bytes.size() - m_read; }

        void Clear()
        {
            m_bytes.clear();
            m_read   = 0;
            m_failed = false;
        }

        /* Rewind so the same packet can be read again. */
        void ResetRead()
        {
            m_read   = 0;
            m_failed = false;
        }

    private:
        template <typename T>
        Packet& WriteInt(T value, size_t width)
        {
            for (size_t i = 0; i < width; ++i)
                m_bytes.push_back((unsigned char)((value >> (i * 8)) & 0xFF));
            return *this;
        }

        template <typename T>
        bool ReadInt(T& value, size_t width)
        {
            if (m_failed || m_read + width > m_bytes.size())
            {
                m_failed = true;
                return false;
            }

            T result = 0;
            for (size_t i = 0; i < width; ++i)
                result |= (T)((T)m_bytes[m_read + i] << (i * 8));

            m_read += width;
            value = result;
            return true;
        }

        std::vector<unsigned char> m_bytes;
        size_t m_read   = 0;
        bool   m_failed = false;
    };

    /* ------------------------------------------------------------------ */
    /* Event                                                              */
    /* ------------------------------------------------------------------ */

    struct Event
    {
        EventType type    = EventType::None;
        PeerId    peer    = InvalidPeer;
        Channel   channel = Channel::Reliable;

        /* Valid only until the next Poll() on the same Server/Client. */
        const unsigned char* data = nullptr;
        unsigned int         size = 0;

        /* Copies the payload into a packet you can keep and read from. */
        Packet AsPacket() const { return Packet(data, size); }

        /* Copies the payload into a string you can keep. */
        std::string AsString() const
        {
            return (data != nullptr) ? std::string((const char*)data, size)
                                     : std::string();
        }
    };

    namespace detail
    {
        inline void FillEvent(Event& out, const EnEvent& raw)
        {
            out.type    = (EventType)raw.type;
            out.peer    = raw.peer;
            out.channel = (Channel)raw.channel;
            out.data    = raw.data;
            out.size    = raw.size;
        }
    }

    /* ------------------------------------------------------------------ */
    /* Server                                                             */
    /* ------------------------------------------------------------------ */

    class Server
    {
    public:
        Server() = default;

        ~Server()
        {
            if (m_handle != nullptr)
                EnServerDestroy(m_handle);
        }

        Server(const Server&)            = delete;
        Server& operator=(const Server&) = delete;

        Server(Server&& other) noexcept : m_handle(other.m_handle)
        {
            other.m_handle = nullptr;
        }

        Server& operator=(Server&& other) noexcept
        {
            if (this != &other)
            {
                if (m_handle != nullptr)
                    EnServerDestroy(m_handle);
                m_handle       = other.m_handle;
                other.m_handle = nullptr;
            }
            return *this;
        }

        /* Pass port 0 to let the OS pick one, then read it back via Port(). */
        bool Start(unsigned short port, int maxPeers = 32)
        {
            if (m_handle == nullptr)
                m_handle = EnServerCreate();
            if (m_handle == nullptr)
                return false;

            return EnServerStart(m_handle, port, maxPeers) == EN_OK;
        }

        void Stop()
        {
            if (m_handle != nullptr)
                EnServerStop(m_handle);
        }

        bool IsRunning() const
        {
            return m_handle != nullptr && EnServerIsRunning(m_handle) != 0;
        }

        /* Returns false when there is nothing left to report this frame. */
        bool Poll(Event& out)
        {
            if (m_handle == nullptr)
                return false;

            EnEvent raw;
            if (EnServerPoll(m_handle, &raw) == 0)
                return false;

            detail::FillEvent(out, raw);
            return true;
        }

        bool Send(PeerId peer, const void* data, unsigned int size,
                  Channel channel = Channel::Reliable)
        {
            if (m_handle == nullptr)
                return false;
            return EnServerSend(m_handle, peer, data, size,
                                (EnChannel)channel) == EN_OK;
        }

        bool Send(PeerId peer, const Packet& packet,
                  Channel channel = Channel::Reliable)
        {
            return Send(peer, packet.Data(), (unsigned int)packet.Size(), channel);
        }

        bool Send(PeerId peer, const std::string& text,
                  Channel channel = Channel::Reliable)
        {
            return Send(peer, text.data(), (unsigned int)text.size(), channel);
        }

        bool Broadcast(const void* data, unsigned int size,
                       Channel channel = Channel::Reliable,
                       PeerId except = InvalidPeer)
        {
            if (m_handle == nullptr)
                return false;
            return EnServerBroadcast(m_handle, data, size,
                                     (EnChannel)channel, except) == EN_OK;
        }

        bool Broadcast(const Packet& packet,
                       Channel channel = Channel::Reliable,
                       PeerId except = InvalidPeer)
        {
            return Broadcast(packet.Data(), (unsigned int)packet.Size(),
                             channel, except);
        }

        bool Broadcast(const std::string& text,
                       Channel channel = Channel::Reliable,
                       PeerId except = InvalidPeer)
        {
            return Broadcast(text.data(), (unsigned int)text.size(),
                             channel, except);
        }

        void Disconnect(PeerId peer)
        {
            if (m_handle != nullptr)
                EnServerDisconnect(m_handle, peer);
        }

        int PeerCount() const
        {
            return (m_handle != nullptr) ? EnServerPeerCount(m_handle) : 0;
        }

        std::string PeerAddress(PeerId peer) const
        {
            return (m_handle != nullptr) ? EnServerPeerAddress(m_handle, peer)
                                         : std::string();
        }

        unsigned short Port() const
        {
            return (m_handle != nullptr) ? EnServerPort(m_handle) : 0;
        }

        /* Escape hatch to the C API. */
        EnServer* Handle() const { return m_handle; }

    private:
        EnServer* m_handle = nullptr;
    };

    /* ------------------------------------------------------------------ */
    /* Client                                                             */
    /* ------------------------------------------------------------------ */

    class Client
    {
    public:
        Client() = default;

        ~Client()
        {
            if (m_handle != nullptr)
                EnClientDestroy(m_handle);
        }

        Client(const Client&)            = delete;
        Client& operator=(const Client&) = delete;

        Client(Client&& other) noexcept : m_handle(other.m_handle)
        {
            other.m_handle = nullptr;
        }

        Client& operator=(Client&& other) noexcept
        {
            if (this != &other)
            {
                if (m_handle != nullptr)
                    EnClientDestroy(m_handle);
                m_handle       = other.m_handle;
                other.m_handle = nullptr;
            }
            return *this;
        }

        /* Blocks until connected or the timeout expires. */
        bool Connect(const std::string& host, unsigned short port,
                     unsigned int timeoutMs = 0)
        {
            if (m_handle == nullptr)
                m_handle = EnClientCreate();
            if (m_handle == nullptr)
                return false;

            return EnClientConnect(m_handle, host.c_str(), port, timeoutMs) == EN_OK;
        }

        void Disconnect()
        {
            if (m_handle != nullptr)
                EnClientDisconnect(m_handle);
        }

        bool IsConnected() const
        {
            return m_handle != nullptr && EnClientIsConnected(m_handle) != 0;
        }

        bool Poll(Event& out)
        {
            if (m_handle == nullptr)
                return false;

            EnEvent raw;
            if (EnClientPoll(m_handle, &raw) == 0)
                return false;

            detail::FillEvent(out, raw);
            return true;
        }

        bool Send(const void* data, unsigned int size,
                  Channel channel = Channel::Reliable)
        {
            if (m_handle == nullptr)
                return false;
            return EnClientSend(m_handle, data, size, (EnChannel)channel) == EN_OK;
        }

        bool Send(const Packet& packet, Channel channel = Channel::Reliable)
        {
            return Send(packet.Data(), (unsigned int)packet.Size(), channel);
        }

        bool Send(const std::string& text, Channel channel = Channel::Reliable)
        {
            return Send(text.data(), (unsigned int)text.size(), channel);
        }

        /* The id the server assigned to us. */
        PeerId Id() const
        {
            return (m_handle != nullptr) ? EnClientPeerId(m_handle) : InvalidPeer;
        }

        EnClient* Handle() const { return m_handle; }

    private:
        EnClient* m_handle = nullptr;
    };
}

#endif /* ELINTRIA_NET_HPP */
