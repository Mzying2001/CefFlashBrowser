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
}

#endif // !__UTILS_H__
