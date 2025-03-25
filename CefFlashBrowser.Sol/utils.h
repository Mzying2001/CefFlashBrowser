#ifndef __UTILS_H__
#define __UTILS_H__

#include <cstdint>
#include <cstdio>
#include <string>
#include <vector>
#include <stdexcept>

namespace utils
{
    std::vector<uint8_t> ReadFile(const std::string& path);

    void WriteFile(const std::string& path, const std::vector<uint8_t>& data);

    System::String^ ToSystemString(const std::string& str, bool utf8 = true);

    std::string ToStdString(System::String^ str, bool utf8 = true);

    array<System::Byte>^ ToByteArray(const std::vector<uint8_t>& vec);

    std::vector<uint8_t> ToByteVector(array<System::Byte>^ arr);

    System::DateTime ToSystemDateTime(double timestamp);

    double ToTimestamp(System::DateTime datetime);


    template <typename T>
    std::enable_if_t<std::is_integral_v<T>, T> ReverseEndian(T value)
    {
        T result = 0;
        for (size_t i = 0; i < sizeof(T); i++) {
            result = (result << 8) | (value & 0xFF);
            value >>= 8;
        }
        return result;
    }

    template <typename... Args>
    std::string FormatString(const std::string& fmt, Args... args)
    {
        std::string result(snprintf(nullptr, 0, fmt.c_str(), args...) + 1, '\0');
        result.resize(snprintf(&result[0], result.size(), fmt.c_str(), args...));
        return result;
    }
}

#endif // !__UTILS_H__
