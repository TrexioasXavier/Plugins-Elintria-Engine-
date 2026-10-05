#pragma once
#include <Component.h>
#include <EProperty.h>
#include <GameObject.h>
#include <ElintriaTime.h>
#include <string>

// NetPing - a sample from the ENetworking package.
//
// Sends a short message over the session the editor's Networking tab is
// running, once every |interval| seconds, and prints whatever arrives. Start
// a server in one editor and connect a client in another, put this script on
// an object in both, and you should see each side print the other's pings.
//
// This talks to NetworkSession, which is the engine's own wrapper around the
// connection the Networking tab manages, so it shares that one connection
// rather than opening a second. If you want your own socket instead, include
// <ElintriaNet.hpp> - the package puts it on your include path and links the
// library for you. See the package README.
class NetPing : public ElintriaEngine::Component
{
public:
    NetPing() : ElintriaEngine::Component("NetPing") {}
    virtual ~NetPing() = default;

    // Seconds between pings.
    float interval = 2.0f;
    // What to put in the message, so two machines can be told apart.
    std::string label = "hello";

    void Start()  override;
    void Update() override;
    void FixedUpdate() override {}

    std::string GetInspectorName() const override { return "NetPing"; }
    std::vector<std::shared_ptr<ElintriaEngine::EGUI::PropertyBase>>
        GetProperties() override;

private:
    float m_timer = 0.0f;
    int   m_sent  = 0;
};

extern "C" __declspec(dllexport)
ElintriaEngine::Component* CreateComponent_NetPing(ElintriaEngine::GameObject* go);
