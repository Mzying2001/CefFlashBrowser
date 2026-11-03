#include <windows.h>
#include <cstdio>
#include <new>
#include <string>
#include <string_view>
#include <vector>


namespace
{
    bool IsSpace(wchar_t value)
    {
        return value == L' ' || value == L'\t';
    }

    std::wstring_view Trim(std::wstring_view value)
    {
        while (!value.empty() && IsSpace(value.front())) {
            value.remove_prefix(1);
        }
        while (!value.empty() && IsSpace(value.back())) {
            value.remove_suffix(1);
        }
        return value;
    }

    bool IsFlashDiagnostic(std::wstring_view arguments)
    {
        arguments = Trim(arguments);

        if (arguments.size() < 3 || arguments[0] != L'/' ||
            (arguments[1] != L'c' && arguments[1] != L'C') || !IsSpace(arguments[2])) {
            return false;
        }

        arguments = Trim(arguments.substr(3));
        return arguments == L"echo NOT SANDBOXED" || arguments == L"\"echo NOT SANDBOXED\"";
    }

    std::wstring_view GetArguments()
    {
        std::wstring_view commandLine(GetCommandLineW());

        // CRT system() does not quote argv[0], even when COMSPEC contains spaces.
        std::vector<wchar_t> modulePath(32768);

        DWORD length = GetModuleFileNameW(
            nullptr,
            modulePath.data(),
            static_cast<DWORD>(modulePath.size()));

        if (length != 0 && length < modulePath.size() && commandLine.size() >= length &&
            CompareStringOrdinal(commandLine.data(), length, modulePath.data(), length, TRUE) == CSTR_EQUAL &&
            (commandLine.size() == length || IsSpace(commandLine[length]))) {
            return commandLine.substr(length);
        }

        bool quoted = false;
        size_t end = 0;

        for (; end < commandLine.size(); ++end) {
            if (commandLine[end] == L'"') {
                quoted = !quoted;
            }
            else if (!quoted && IsSpace(commandLine[end])) {
                break;
            }
        }
        return commandLine.substr(end);
    }

    DWORD ReportError(const char* operation, DWORD error)
    {
        if (error == ERROR_SUCCESS) {
            error = ERROR_GEN_FAILURE;
        }

        char message[192];

        int length = sprintf_s(
            message,
            "CefFlashBrowser.CommandProxy: %s failed (Win32 error %lu).\r\n",
            operation,
            error);

        DWORD written;

        if (length > 0) {
            WriteFile(
                GetStdHandle(STD_ERROR_HANDLE),
                message,
                static_cast<DWORD>(length),
                &written,
                nullptr);
        }
        return error;
    }

    BOOL WINAPI HandleConsoleControl(DWORD control)
    {
        // cmd receives the same event; keep the proxy alive to collect its exit code.
        return control == CTRL_C_EVENT || control == CTRL_BREAK_EVENT;
    }

    struct ProcessHandles
    {
        PROCESS_INFORMATION value{};

        ~ProcessHandles()
        {
            if (value.hThread) {
                CloseHandle(value.hThread);
            }
            if (value.hProcess) {
                CloseHandle(value.hProcess);
            }
        }
    };

    DWORD Forward(std::wstring_view arguments)
    {
        std::vector<wchar_t> directory(32768);
        UINT length = GetSystemDirectoryW(directory.data(), static_cast<UINT>(directory.size()));

        if (length == 0 || length >= directory.size()) {
            return ReportError(
                "GetSystemDirectoryW",
                length == 0 ? GetLastError() : ERROR_INSUFFICIENT_BUFFER);
        }

        std::wstring commandPath(directory.data(), length);
        commandPath += L"\\cmd.exe";

        // This changes only this short-lived proxy and its descendants, not the caller.
        if (!SetEnvironmentVariableW(L"ComSpec", commandPath.c_str())) {
            return ReportError("SetEnvironmentVariableW", GetLastError());
        }

        STARTUPINFOW startup{};
        GetStartupInfoW(&startup);
        startup.cb = sizeof(startup);
        startup.lpReserved = nullptr;

        // CRT descriptor metadata belongs to the proxy's runtime, not cmd's runtime.
        startup.cbReserved2 = 0;
        startup.lpReserved2 = nullptr;

        HANDLE originalHandles[] = {
            GetStdHandle(STD_INPUT_HANDLE),
            GetStdHandle(STD_OUTPUT_HANDLE),
            GetStdHandle(STD_ERROR_HANDLE)
        };

        bool attached = AttachConsole(ATTACH_PARENT_PROCESS) != FALSE;

        if (attached || GetLastError() == ERROR_ACCESS_DENIED) {
            if (!SetConsoleCtrlHandler(HandleConsoleControl, TRUE)) {
                return ReportError("SetConsoleCtrlHandler", GetLastError());
            }
        }

        // AttachConsole may replace standard handles. Preserve inherited files and pipes.
        constexpr DWORD handleIds[] = { STD_INPUT_HANDLE, STD_OUTPUT_HANDLE, STD_ERROR_HANDLE };

        for (size_t i = 0; i < 3; ++i) {
            if (originalHandles[i] != nullptr && originalHandles[i] != INVALID_HANDLE_VALUE) {
                SetStdHandle(handleIds[i], originalHandles[i]);
            }
        }

        if (startup.dwFlags & STARTF_USESTDHANDLES) {
            startup.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
            startup.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
            startup.hStdError = GetStdHandle(STD_ERROR_HANDLE);
        }

        // Keep the tail byte-for-byte (UTF-16); cmd has its own quoting grammar.
        std::wstring commandLine = L"\"" + commandPath + L"\"";
        commandLine.append(arguments);

        ProcessHandles process;

        if (!CreateProcessW(
            commandPath.c_str(),
            commandLine.data(),
            nullptr,
            nullptr,
            TRUE,
            0,
            nullptr,
            nullptr,
            &startup,
            &process.value)) {
            return ReportError("CreateProcessW", GetLastError());
        }

        if (WaitForSingleObject(process.value.hProcess, INFINITE) == WAIT_FAILED) {
            return ReportError("WaitForSingleObject", GetLastError());
        }

        DWORD exitCode;

        if (!GetExitCodeProcess(process.value.hProcess, &exitCode)) {
            return ReportError("GetExitCodeProcess", GetLastError());
        }
        return exitCode;
    }
}


int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    DWORD exitCode;

    try {
        auto command = GetArguments();
        exitCode = IsFlashDiagnostic(command) ? ERROR_SUCCESS : Forward(command);
    }
    catch (const std::bad_alloc&) {
        exitCode = ReportError("allocation", ERROR_NOT_ENOUGH_MEMORY);
    }
    ExitProcess(exitCode);
}
