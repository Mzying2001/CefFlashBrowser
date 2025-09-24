#pragma once
#include <Windows.h>

using namespace System;

namespace CefFlashBrowser::Singleton
{
    struct NativeWnd;

    /**
     * @brief Event args for received data event.
     */
    public ref class ReceivedDataEventArgs : EventArgs
    {
    private:
        array<Byte>^ _data;

    public:
        // Constructor
        ReceivedDataEventArgs(array<Byte>^ data) : _data(data) {};

    public:
        // The data received
        property array<Byte>^ Data {
            array<Byte>^ get() {
                return _data;
            }
            void set(array<Byte>^ value) {
                _data = value;
            }
        }
    };

    /**
     * @brief MsgReceiver, user should ensure only one instance in the entire system.
     */
    public ref class MsgReceiver
    {
    private:
        NativeWnd* _pNativeWnd;
        EventHandler<ReceivedDataEventArgs^>^ _receivedData;

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
        event EventHandler<ReceivedDataEventArgs^>^ ReceivedData
        {
            void add(EventHandler<ReceivedDataEventArgs^>^ handler) {
                _receivedData += handler;
            }
            void remove(EventHandler<ReceivedDataEventArgs^>^ handler) {
                _receivedData -= handler;
            }
            void raise(Object^ sender, ReceivedDataEventArgs^ e) {
                if (_receivedData != nullptr) {
                    _receivedData->Invoke(sender, e);
                }
            }
        }
    };
}
