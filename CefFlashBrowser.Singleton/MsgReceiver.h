#pragma once
#include <Windows.h>

using namespace System;

namespace CefFlashBrowser::Singleton
{
    struct NativeWnd;

    /**
     * @brief MsgReceiver, user should ensure only one instance in the entire system.
     */
    public ref class MsgReceiver
    {
    private:
        NativeWnd* _pNativeWnd;
        Action<array<Byte>^>^ _receivedData;

    public:
        MsgReceiver();
        ~MsgReceiver();

    public:
        // Send data to the global receiver
        static void SendGlobalData(array<Byte>^ data);

    protected:
        // Received data
        virtual void OnReceivedData(array<Byte>^ data);

    public:
        // Received data event
        event Action<array<Byte>^>^ ReceivedData
        {
            void add(Action<array<Byte>^>^ handler) {
                _receivedData += handler;
            }
            void remove(Action<array<Byte>^>^ handler) {
                _receivedData -= handler;
            }
            void raise(array<Byte>^ data) {
                if (_receivedData != nullptr) {
                    _receivedData->Invoke(data);
                }
            }
        }
    };
}
