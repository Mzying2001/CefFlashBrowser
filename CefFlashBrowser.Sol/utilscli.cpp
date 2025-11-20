#include "utilscli.h"
#include <msclr/marshal.h>
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Text;

System::String^ utils::ToSystemString(const std::string& str, bool utf8)
{
    if (str.empty()) {
        return String::Empty;
    }
    if (utf8) {
        return gcnew String(str.c_str(), 0, (int)str.size(), Encoding::UTF8);
    }
    else {
        return msclr::interop::marshal_as<String^>(str);
    }
}

std::string utils::ToStdString(System::String^ str, bool utf8)
{
    if (String::IsNullOrEmpty(str)) {
        return std::string();
    }
    if (utf8) {
        array<Byte>^ bytes = Encoding::UTF8->GetBytes(str);
        pin_ptr<Byte> pinned = &bytes[0];
        return std::string(reinterpret_cast<const char*>(pinned), bytes->Length);
    }
    else {
        return msclr::interop::marshal_as<std::string>(str);
    }
}

array<System::Byte>^ utils::ToByteArray(const std::vector<uint8_t>& vec)
{
    int i = 0;
    auto arr = gcnew array<Byte>((int)vec.size());

    for (uint8_t b : vec) {
        arr[i++] = b;
    }
    return arr;
}

std::vector<uint8_t> utils::ToByteVector(array<System::Byte>^ arr)
{
    std::vector<uint8_t> vec;
    vec.reserve(arr->Length);

    for each (Byte b in arr) {
        vec.push_back(b);
    }
    return vec;
}

System::DateTime utils::ToSystemDateTime(double timestamp)
{
    DateTime utc = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind::Utc).AddMilliseconds(timestamp);
    return utc.ToLocalTime();
}

double utils::ToTimestamp(System::DateTime datetime)
{
    DateTime utc = datetime.ToUniversalTime();
    return (utc - DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind::Utc)).TotalMilliseconds;
}
