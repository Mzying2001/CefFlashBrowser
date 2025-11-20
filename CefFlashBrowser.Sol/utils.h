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

    template <typename T>
    std::enable_if_t<std::is_integral_v<T>, T> FromBigEndian(T value)
    {
        return IsBigEndian() ? value : ReverseEndian(value);
    }

    template <typename T>
    std::enable_if_t<std::is_integral_v<T>, T> ToBigEndian(T value)
    {
        return IsBigEndian() ? value : ReverseEndian(value);
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
