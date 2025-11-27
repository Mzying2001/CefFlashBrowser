#include "utils.h"
#include <fstream>

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

bool utils::IsBigEndian()
{
    union { uint16_t a; uint8_t b[2]; } test = { 0x1234 };
    return test.b[0] == 0x12;
}
