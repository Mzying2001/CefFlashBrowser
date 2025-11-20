#ifndef __UTILSCLI_H__
#define __UTILSCLI_H__

#include <cstdint>
#include <string>
#include <vector>

namespace utils
{
    System::String^ ToSystemString(const std::string& str, bool utf8 = true);

    std::string ToStdString(System::String^ str, bool utf8 = true);

    array<System::Byte>^ ToByteArray(const std::vector<uint8_t>& vec);

    std::vector<uint8_t> ToByteVector(array<System::Byte>^ arr);

    System::DateTime ToSystemDateTime(double timestamp);

    double ToTimestamp(System::DateTime datetime);
}

#endif // !__UTILSCLI_H__
