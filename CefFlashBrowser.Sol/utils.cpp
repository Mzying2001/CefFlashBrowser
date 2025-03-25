#include "utils.h"
#include <fstream>
#include <msclr/marshal.h>
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Text;

std::vector<uint8_t> utils::ReadFile(const std::string& path)
{
    std::vector<uint8_t> result;

    std::ifstream ifs(path, std::ios::binary | std::ios::ate);
    if (!ifs.is_open()) throw std::runtime_error("Failed to open file");

    std::streamsize size = ifs.tellg();
    if (size < 0) throw std::runtime_error("Failed to get file size");
    if (size == 0) return result;

    ifs.seekg(0, std::ios::beg);
    result.resize(size);

    if (!ifs.read(reinterpret_cast<char*>(result.data()), size)) {
        throw std::runtime_error("Failed to read file");
    }
    return result;
}

void utils::WriteFile(const std::string& path, const std::vector<uint8_t>& data)
{
    std::ofstream ofs(path, std::ios::binary);
    if (!ofs.is_open()) throw std::runtime_error("Failed to open file");

    if (!ofs.write(reinterpret_cast<const char*>(data.data()), data.size())) {
        throw std::runtime_error("Failed to write file");
    }
}

System::String^ utils::ToSystemString(const std::string& str, bool utf8)
{
    if (utf8) {
        return gcnew String(str.c_str(), 0, (int)str.size(), Encoding::UTF8);
    }
    else {
        return msclr::interop::marshal_as<String^>(str);
    }
}

std::string utils::ToStdString(System::String^ str, bool utf8)
{
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
    return DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind::Utc).AddMilliseconds(timestamp);
}

double utils::ToTimestamp(System::DateTime datetime)
{
    DateTime utc = datetime.ToUniversalTime();
    return (utc - DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind::Utc)).TotalMilliseconds;
}
