#ifndef __UTILS_H__
#define __UTILS_H__

#include <cstdint>
#include <cstdio>
#include <string>
#include <vector>
#include <stdexcept>
#include <type_traits>

namespace utils
{
    std::vector<uint8_t> ReadFile(const std::string& path);

    void WriteFile(const std::string& path, const std::vector<uint8_t>& data);

    bool IsBigEndian();


    /*======================================================================*/


    template <typename T>
    auto ReverseEndian(T value) -> std::enable_if_t<std::is_integral_v<T>, T>
    {
        T result = 0;
        for (size_t i = 0; i < sizeof(T); i++) {
            result = (result << 8) | (value & 0xFF);
            value >>= 8;
        }
        return result;
    }

    template <typename T>
    auto FromBigEndian(T value) -> std::enable_if_t<std::is_integral_v<T>, T>
    {
        return IsBigEndian() ? value : ReverseEndian(value);
    }

    template <typename T>
    auto ToBigEndian(T value) -> std::enable_if_t<std::is_integral_v<T>, T>
    {
        return IsBigEndian() ? value : ReverseEndian(value);
    }

    template <typename... Args>
    std::string FormatString(const char* fmt, Args... args)
    {
        static_assert(
            ((std::is_arithmetic_v<Args> || std::is_pointer_v<Args>) && ...),
            "FormatString only supports arithmetic and pointer types.");
        std::string result(snprintf(nullptr, 0, fmt, args...) + 1, '\0');
        result.resize(snprintf(&result[0], result.size(), fmt, args...));
        return result;
    }
}

#endif // !__UTILS_H__
