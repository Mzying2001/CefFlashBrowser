#ifndef __UTILS_H__
#define __UTILS_H__

#include <cstdint>
#include <string>
#include <vector>
#include <stdexcept>

namespace utils
{
    std::vector<uint8_t> ReadFile(const std::string& path);

    System::String^ ToSystemString(const std::string& str, bool utf8 = true);

    std::string ToStdString(System::String^ str, bool utf8 = true);

    array<System::Byte>^ ToByteArray(const std::vector<uint8_t>& vec);

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
}

#endif // !__UTILS_H__
