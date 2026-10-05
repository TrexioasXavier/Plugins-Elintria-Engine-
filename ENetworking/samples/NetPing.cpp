#include "NetPing.h"
#include <ENetworkSync.h>
#include <iostream>

using ElintriaEngine::Net::NetworkSession;
using ElintriaEngine::Net::NetRole;

void NetPing::Start()
{
    m_timer = 0.0f;
    m_sent  = 0;

    if (!NetworkSession::IsRunning())
    {
        std::cout << "[NetPing] no session yet - start a server or connect a "
                     "client from the Networking tab" << std::endl;
        return;
    }
    std::cout << "[NetPing] running as "
              << (NetworkSession::Role() == NetRole::Server ? "server" : "client")
              << std::endl;
}

void NetPing::Update()
{
    // Anything that arrived since the last frame, whoever sent it.
    std::vector<NetworkSession::Received> inbox = NetworkSession::TakeReceived();
    for (size_t i = 0; i < inbox.size(); ++i)
    {
        std::cout << "[NetPing] from peer " << inbox[i].from << ": "
                  << inbox[i].text << std::endl;
    }

    if (!NetworkSession::IsRunning()) return;

    float dt = ElintriaEngine::Time::deltaTime;
    if (dt <= 0.0f) return;

    m_timer += dt;
    if (m_timer < interval) return;
    m_timer = 0.0f;

    // Reliable, because a ping that silently vanishes tells you nothing
    // about whether the connection is alive.
    ++m_sent;
    NetworkSession::SendToAll(label + " #" + std::to_string(m_sent), true);
}

std::vector<std::shared_ptr<ElintriaEngine::EGUI::PropertyBase>>
    NetPing::GetProperties()
{
    using namespace ElintriaEngine::EGUI;
    return {
        std::make_shared<Property<float>>      ("Interval", PropertyType::Float,  &interval),
        std::make_shared<Property<std::string>>("Label",    PropertyType::String, &label),
    };
}

extern "C" __declspec(dllexport)
ElintriaEngine::Component* CreateComponent_NetPing(ElintriaEngine::GameObject* go)
{
    return go ? go->AddComponent<NetPing>() : nullptr;
}
