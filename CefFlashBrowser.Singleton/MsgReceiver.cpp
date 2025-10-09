#include "MsgReceiver.h"
#include <gcroot.h>

using namespace CefFlashBrowser::Singleton;
using namespace System::Runtime::InteropServices;


constexpr WCHAR MsgReceiverClassName[] = L"CefFlashBrowser.Singleton.MsgReceiver";
constexpr WCHAR MsgReceiverWindowName[] = L"MsgReceiver";


struct CefFlashBrowser::Singleton::NativeWnd
{
    HWND hWnd;
    HINSTANCE hInstance;

    gcroot<Action<array<Byte>^>^> receivedDataCallback;

    NativeWnd()
        : hInstance(GetModuleHandleW(NULL))
    {
        static thread_local ATOM wndClsAtom = 0;

        if (wndClsAtom == 0)
        {
            WNDCLASSEXW wc{};
            wc.cbSize = sizeof(wc);
            wc.hInstance = hInstance;
            wc.lpfnWndProc = WndProcW;
            wc.lpszClassName = MsgReceiverClassName;
            wndClsAtom = RegisterClassExW(&wc);
        }

        hWnd = CreateWindowExW(
            0,
            MsgReceiverClassName,
            MsgReceiverWindowName,
            WS_POPUP,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            NULL,
            NULL,
            hInstance,
            this);
    }

    ~NativeWnd()
    {
        if (hWnd != NULL) {
            DestroyWindow(hWnd);
            hWnd = NULL;
        }
    }

    static LRESULT CALLBACK WndProcW(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
    {
        constexpr WCHAR SelfProp[] = L"__self";

        auto pWnd = reinterpret_cast<NativeWnd*>(
            GetPropW(hWnd, SelfProp));

        if ((uMsg == WM_NCCREATE || uMsg == WM_CREATE) && lParam != 0)
        {
            auto pCreate = reinterpret_cast<LPCREATESTRUCT>(lParam);

            if (pWnd == nullptr) {
                pWnd = reinterpret_cast<NativeWnd*>(pCreate->lpCreateParams);
                SetPropW(hWnd, SelfProp, reinterpret_cast<HANDLE>(pWnd));
            }

            if (pWnd) {
                pWnd->hWnd = hWnd;
            }
        }

        if (pWnd) {
            return pWnd->WndProcW(uMsg, wParam, lParam);
        }
        else {
            return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }
    }

    LRESULT WndProcW(UINT uMsg, WPARAM wParam, LPARAM lParam)
    {
        switch (uMsg)
        {
        case WM_COPYDATA: {
            auto pCopyData = reinterpret_cast<PCOPYDATASTRUCT>(lParam);
            OnCopyData(pCopyData);
            return TRUE;
        }

        case WM_CLOSE: {
            DestroyWindow(hWnd);
            return 0;
        }

        case WM_NCDESTROY: {
            hWnd = NULL;
            return 0;
        }

        default: {
            return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }
        }
    }

    void OnCopyData(PCOPYDATASTRUCT pCopyData)
    {
        Action<array<Byte>^>^ callback = receivedDataCallback;

        if (callback != nullptr) {
            auto data = gcnew array<Byte>(pCopyData->cbData);
            Marshal::Copy(IntPtr(pCopyData->lpData), data, 0, pCopyData->cbData);
            callback->Invoke(data);
        }
    }

    static void SendCopyData(HWND hWnd, array<Byte>^ data)
    {
        pin_ptr<Byte> pinnedData = &data[0];

        COPYDATASTRUCT copyData{};
        copyData.cbData = data->Length;
        copyData.lpData = pinnedData;
        SendMessageW(hWnd, WM_COPYDATA, 0, reinterpret_cast<LPARAM>(&copyData));
    }
};


CefFlashBrowser::Singleton::MsgReceiver::MsgReceiver()
    : _pNativeWnd(new NativeWnd)
{
    _pNativeWnd->receivedDataCallback =
        gcnew Action<array<Byte>^>(this, &MsgReceiver::OnReceivedData);
}

CefFlashBrowser::Singleton::MsgReceiver::~MsgReceiver()
{
    delete _pNativeWnd;
    _pNativeWnd = nullptr;
}

void CefFlashBrowser::Singleton::MsgReceiver::SendGlobalData(array<Byte>^ data)
{
    HWND hWnd = FindWindowW(
        MsgReceiverClassName,
        /*MsgReceiverWindowName*/ NULL);

    if (hWnd != NULL) {
        NativeWnd::SendCopyData(hWnd, data);
    }
}

void CefFlashBrowser::Singleton::MsgReceiver::OnReceivedData(array<Byte>^ data)
{
    ReceivedData::raise(this, gcnew ReceivedDataEventArgs(data));
}
