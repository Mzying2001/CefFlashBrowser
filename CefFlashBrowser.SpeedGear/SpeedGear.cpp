#include <windows.h>
#include <psapi.h>
#include <mmsystem.h>
#include <cstdint>
#include <cstring>

namespace
{
    struct SharedState
    {
        volatile long long generation;
        double speed;
        unsigned char reserved[16];
    };

    using SleepFn = void (WINAPI*)(DWORD);
    using SleepExFn = DWORD (WINAPI*)(DWORD, BOOL);
    using GetTickCountFn = DWORD (WINAPI*)();
    using GetTickCount64Fn = ULONGLONG (WINAPI*)();
    using QueryPerformanceCounterFn = BOOL (WINAPI*)(LARGE_INTEGER*);
    using GetSystemTimeAsFileTimeFn = void (WINAPI*)(LPFILETIME);
    using SetTimerFn = UINT_PTR (WINAPI*)(HWND, UINT_PTR, UINT, TIMERPROC);
    using TimeGetTimeFn = DWORD (WINAPI*)();

    SharedState* g_shared = nullptr;
    long long g_generation = 0;
    double g_speed = 1.0;

    SleepFn RealSleep = nullptr;
    SleepExFn RealSleepEx = nullptr;
    GetTickCountFn RealGetTickCount = nullptr;
    GetTickCount64Fn RealGetTickCount64 = nullptr;
    QueryPerformanceCounterFn RealQueryPerformanceCounter = nullptr;
    GetSystemTimeAsFileTimeFn RealGetSystemTimeAsFileTime = nullptr;
    SetTimerFn RealSetTimer = nullptr;
    TimeGetTimeFn RealTimeGetTime = nullptr;

    DWORD g_baseTick = 0;
    ULONGLONG g_baseTick64 = 0;
    DWORD g_baseTimeGetTime = 0;
    LARGE_INTEGER g_baseQpc{};
    FILETIME g_baseFileTime{};

    static ULONGLONG FileTimeToU64(FILETIME ft)
    {
        return (static_cast<ULONGLONG>(ft.dwHighDateTime) << 32) | ft.dwLowDateTime;
    }

    static FILETIME U64ToFileTime(ULONGLONG value)
    {
        FILETIME ft{};
        ft.dwLowDateTime = static_cast<DWORD>(value);
        ft.dwHighDateTime = static_cast<DWORD>(value >> 32);
        return ft;
    }

    static FARPROC Resolve(const wchar_t* moduleName, const char* functionName)
    {
        auto module = GetModuleHandleW(moduleName);
        if (!module)
            module = LoadLibraryW(moduleName);
        return module ? GetProcAddress(module, functionName) : nullptr;
    }

    static void ResetBases()
    {
        g_baseTick = RealGetTickCount ? RealGetTickCount() : ::GetTickCount();
        g_baseTick64 = RealGetTickCount64 ? RealGetTickCount64() : ::GetTickCount64();
        g_baseTimeGetTime = RealTimeGetTime ? RealTimeGetTime() : ::timeGetTime();
        if (RealQueryPerformanceCounter)
            RealQueryPerformanceCounter(&g_baseQpc);
        else
            ::QueryPerformanceCounter(&g_baseQpc);
        if (RealGetSystemTimeAsFileTime)
            RealGetSystemTimeAsFileTime(&g_baseFileTime);
        else
            ::GetSystemTimeAsFileTime(&g_baseFileTime);
    }

    static double CurrentSpeed()
    {
        if (g_shared && g_shared->generation != g_generation)
        {
            g_generation = g_shared->generation;
            g_speed = g_shared->speed > 0 ? g_shared->speed : 1.0;
            ResetBases();
        }
        return g_speed;
    }

    void WINAPI HookSleep(DWORD ms)
    {
        const auto speed = CurrentSpeed();
        RealSleep(static_cast<DWORD>(ms / speed));
    }

    DWORD WINAPI HookSleepEx(DWORD ms, BOOL alertable)
    {
        const auto speed = CurrentSpeed();
        return RealSleepEx(static_cast<DWORD>(ms / speed), alertable);
    }

    DWORD WINAPI HookGetTickCount()
    {
        const auto speed = CurrentSpeed();
        const auto now = RealGetTickCount();
        return static_cast<DWORD>(g_baseTick + (now - g_baseTick) * speed);
    }

    ULONGLONG WINAPI HookGetTickCount64()
    {
        const auto speed = CurrentSpeed();
        const auto now = RealGetTickCount64();
        return static_cast<ULONGLONG>(g_baseTick64 + (now - g_baseTick64) * speed);
    }

    BOOL WINAPI HookQueryPerformanceCounter(LARGE_INTEGER* out)
    {
        if (!out)
            return FALSE;

        const auto speed = CurrentSpeed();
        LARGE_INTEGER now{};
        const auto ok = RealQueryPerformanceCounter(&now);
        out->QuadPart = static_cast<LONGLONG>(g_baseQpc.QuadPart + (now.QuadPart - g_baseQpc.QuadPart) * speed);
        return ok;
    }

    void WINAPI HookGetSystemTimeAsFileTime(LPFILETIME out)
    {
        if (!out)
            return;

        const auto speed = CurrentSpeed();
        FILETIME now{};
        RealGetSystemTimeAsFileTime(&now);
        const auto delta = FileTimeToU64(now) - FileTimeToU64(g_baseFileTime);
        const auto value = FileTimeToU64(g_baseFileTime) + static_cast<ULONGLONG>(delta * speed);
        *out = U64ToFileTime(value);
    }

    UINT_PTR WINAPI HookSetTimer(HWND hwnd, UINT_PTR id, UINT elapse, TIMERPROC proc)
    {
        const auto speed = CurrentSpeed();
        auto scaled = static_cast<UINT>(elapse / speed);
        if (scaled < 1)
            scaled = 1;
        return RealSetTimer(hwnd, id, scaled, proc);
    }

    DWORD WINAPI HookTimeGetTime()
    {
        const auto speed = CurrentSpeed();
        const auto now = RealTimeGetTime();
        return static_cast<DWORD>(g_baseTimeGetTime + (now - g_baseTimeGetTime) * speed);
    }

    static void PatchImport(HMODULE module, const char* importedName, FARPROC replacement)
    {
        auto base = reinterpret_cast<std::uint8_t*>(module);
        auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (!dos || dos->e_magic != IMAGE_DOS_SIGNATURE)
            return;

        auto nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
        if (!nt || nt->Signature != IMAGE_NT_SIGNATURE)
            return;

        auto& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (!dir.VirtualAddress)
            return;

        auto desc = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(base + dir.VirtualAddress);
        for (; desc->Name; ++desc)
        {
            auto thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(base + desc->FirstThunk);
            auto orig = desc->OriginalFirstThunk
                ? reinterpret_cast<IMAGE_THUNK_DATA*>(base + desc->OriginalFirstThunk)
                : thunk;

            for (; thunk->u1.Function && orig->u1.AddressOfData; ++thunk, ++orig)
            {
                if (IMAGE_SNAP_BY_ORDINAL(orig->u1.Ordinal))
                    continue;

                auto byName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(base + orig->u1.AddressOfData);
                if (std::strcmp(reinterpret_cast<const char*>(byName->Name), importedName) != 0)
                    continue;

                DWORD oldProtect = 0;
                if (VirtualProtect(&thunk->u1.Function, sizeof(void*), PAGE_READWRITE, &oldProtect))
                {
                    thunk->u1.Function = reinterpret_cast<ULONG_PTR>(replacement);
                    VirtualProtect(&thunk->u1.Function, sizeof(void*), oldProtect, &oldProtect);
                }
            }
        }
    }

    static void PatchAllModules()
    {
        HMODULE modules[1024]{};
        DWORD needed = 0;
        if (!EnumProcessModules(GetCurrentProcess(), modules, sizeof(modules), &needed))
            return;

        const auto count = needed / sizeof(HMODULE);
        for (DWORD i = 0; i < count; ++i)
        {
            PatchImport(modules[i], "Sleep", reinterpret_cast<FARPROC>(HookSleep));
            PatchImport(modules[i], "SleepEx", reinterpret_cast<FARPROC>(HookSleepEx));
            PatchImport(modules[i], "GetTickCount", reinterpret_cast<FARPROC>(HookGetTickCount));
            PatchImport(modules[i], "GetTickCount64", reinterpret_cast<FARPROC>(HookGetTickCount64));
            PatchImport(modules[i], "QueryPerformanceCounter", reinterpret_cast<FARPROC>(HookQueryPerformanceCounter));
            PatchImport(modules[i], "GetSystemTimeAsFileTime", reinterpret_cast<FARPROC>(HookGetSystemTimeAsFileTime));
            PatchImport(modules[i], "SetTimer", reinterpret_cast<FARPROC>(HookSetTimer));
            PatchImport(modules[i], "timeGetTime", reinterpret_cast<FARPROC>(HookTimeGetTime));
        }
    }

    static void InitSharedState()
    {
        auto mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(SharedState), L"Local\\CefFlashBrowser.SpeedGear");
        if (!mapping)
            return;

        g_shared = reinterpret_cast<SharedState*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(SharedState)));
        if (g_shared)
        {
            g_generation = g_shared->generation;
            g_speed = g_shared->speed > 0 ? g_shared->speed : 1.0;
        }
    }
}

extern "C" __declspec(dllexport) void WINAPI CefFlashBrowserSpeedGearLoaded() {}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        RealSleep = reinterpret_cast<SleepFn>(Resolve(L"kernel32.dll", "Sleep"));
        RealSleepEx = reinterpret_cast<SleepExFn>(Resolve(L"kernel32.dll", "SleepEx"));
        RealGetTickCount = reinterpret_cast<GetTickCountFn>(Resolve(L"kernel32.dll", "GetTickCount"));
        RealGetTickCount64 = reinterpret_cast<GetTickCount64Fn>(Resolve(L"kernel32.dll", "GetTickCount64"));
        RealQueryPerformanceCounter = reinterpret_cast<QueryPerformanceCounterFn>(Resolve(L"kernel32.dll", "QueryPerformanceCounter"));
        RealGetSystemTimeAsFileTime = reinterpret_cast<GetSystemTimeAsFileTimeFn>(Resolve(L"kernel32.dll", "GetSystemTimeAsFileTime"));
        RealSetTimer = reinterpret_cast<SetTimerFn>(Resolve(L"user32.dll", "SetTimer"));
        RealTimeGetTime = reinterpret_cast<TimeGetTimeFn>(Resolve(L"winmm.dll", "timeGetTime"));
        InitSharedState();
        ResetBases();
        PatchAllModules();
    }
    return TRUE;
}
