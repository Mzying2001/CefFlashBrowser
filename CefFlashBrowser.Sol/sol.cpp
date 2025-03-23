#include "sol.h"
#include "utils.h"


constexpr uint8_t SOLMAGIC[] = { 0x00, 0xBF };
constexpr uint8_t SOLCONSTANT[] = { 0x54, 0x43, 0x53, 0x4F, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 };

constexpr char ERR_FILE_TOO_SMALL[] = "File too small";
constexpr char ERR_INVALID_MAGIC[] = "Invalid file magic";
constexpr char ERR_INVALID_SIZE[] = "Invalid file size";
constexpr char ERR_INVALID_FILE[] = "Invalid file format";
constexpr char ERR_INVALID_TYPE[] = "Invalid value type";
constexpr char ERR_INVALID_OBJECT[] = "Invalid object format";


bool sol::IsKnownType(SolType type)
{
    switch (type)
    {
    case SolType::Null:
    case SolType::BooleanFalse:
    case SolType::BooleanTrue:
    case SolType::Integer:
    case SolType::Double:
    case SolType::String:
    case SolType::Array:
    case SolType::Object:
    case SolType::Xml:
    case SolType::Binary:
        return true;
    default:
        return false;
    }
}

bool sol::ReadSolFile(SolFile& file)
{
    try {
        std::vector<uint8_t> filecontent = utils::ReadFile(file.path);

        uint8_t* data = filecontent.data();
        int size = (int)filecontent.size();
        int index = 0;

        if (size < 18) {
            file.errmsg = ERR_FILE_TOO_SMALL;
            return false;
        }

        if (memcmp(data, SOLMAGIC, 2) != 0) {
            file.errmsg = ERR_INVALID_MAGIC;
            return false;
        }
        index += 2;

        uint32_t chunksize = utils::ReverseEndian(
            *reinterpret_cast<uint32_t*>(data + index));

        if (chunksize != size - 6) {
            file.errmsg = ERR_INVALID_SIZE;
            return false;
        }
        index += 4;

        if (memcmp(data + index, SOLCONSTANT, 10) != 0) {
            file.errmsg = ERR_INVALID_FILE;
            return false;
        }
        index += 10;

        if (index + 2 > size) {
            file.errmsg = ERR_INVALID_FILE;
            return false;
        }

        uint16_t namesize = utils::ReverseEndian(
            *reinterpret_cast<uint16_t*>(data + index));
        index += 2;

        if (index + namesize > size) {
            file.errmsg = ERR_INVALID_FILE;
            return false;
        }

        file.solname = std::string(data + index, data + index + namesize);
        index += namesize;

        std::string key;
        std::vector<std::string> strpool;

        while (index < size) {
            key = ReadSolString(data, size, index, strpool);

            if (index >= size) {
                throw std::runtime_error(ERR_INVALID_FILE);
            }

            SolType type = static_cast<SolType>(data[index++]);
            file.data[key] = ReadSolValue(data, size, index, strpool, type, true);
        }

        return true;
    }
    catch (const std::exception& e) {
        file.errmsg = e.what();
        return false;
    }
}

sol::SolInteger sol::ReadSolInteger(uint8_t* data, int size, int& index, bool unsign)
{
    SolInteger result = 0;

    int i = 0;
    for (; i < 3; ++i) {
        if (index + i >= size) {
            throw std::runtime_error(ERR_INVALID_FILE);
        }
        result = result << 7 | (data[index + i] & 0x7F);
        if (!(data[index + i] & 0x80)) break;
    }

    if (i == 4) {
        if (index + i >= size) {
            throw std::runtime_error(ERR_INVALID_FILE);
        }
        result = result << 8 | data[index + i];
    }

    if (result >= 0x10000000 && !unsign) {
        result -= 0x20000000;
    }

    index += i + 1;
    return result;
}

sol::SolDouble sol::ReadSolDouble(uint8_t* data, int size, int& index)
{
    if (index + 8 > size) {
        throw std::runtime_error(ERR_INVALID_FILE);
    }

    uint64_t tmp = utils::ReverseEndian(
        *reinterpret_cast<uint64_t*>(data + index));

    index += 8;
    return *reinterpret_cast<double*>(&tmp);
}

sol::SolString sol::ReadSolString(uint8_t* data, int size, int& index, std::vector<std::string>& strpool, bool add2pool)
{
    int len = ReadSolInteger(data, size, index, true);

    if (!(len & 1)) {
        return strpool.at(len >> 1);
    }

    len >>= 1;

    if (index + len > size) {
        throw std::runtime_error(ERR_INVALID_FILE);
    }

    std::string result(data + index, data + index + len);

    if (add2pool) {
        strpool.push_back(result);
    }

    index += len;
    return result;
}

sol::SolXml sol::ReadSolXml(uint8_t* data, int size, int& index, std::vector<std::string>& strpool)
{
    return ReadSolString(data, size, index, strpool, false);
}

sol::SolBinary sol::ReadSolBinary(uint8_t* data, int size, int& index)
{
    int len = ReadSolInteger(data, size, index, true) >> 1;

    if (index + len > size) {
        throw std::runtime_error(ERR_INVALID_FILE);
    }

    std::vector<uint8_t> result(data + index, data + index + len);
    index += len;
    return result;
}

sol::SolArray sol::ReadSolArray(uint8_t* data, int size, int& index, std::vector<std::string>& strpool)
{
    int len = ReadSolInteger(data, size, index, true) >> 1;

    if (index >= size || data[index++] != 0x01) {
        throw std::runtime_error(ERR_INVALID_FILE);
    }

    std::vector<SolValue> result;
    result.reserve(len);

    for (int i = 0; i < len; ++i) {
        if (index >= size) {
            throw std::runtime_error(ERR_INVALID_FILE);
        }
        SolType type = static_cast<SolType>(data[index++]);
        result.push_back(ReadSolValue(data, size, index, strpool, type));
    }

    return result;
}

sol::SolObject sol::ReadSolObject(uint8_t* data, int size, int& index, std::vector<std::string>& strpool, bool istop)
{
    if (istop) {
        if (index >= size) {
            throw std::runtime_error(ERR_INVALID_FILE);
        }
        if (data[index++] != 0x0B) {
            throw std::runtime_error(ERR_INVALID_OBJECT);
        }
    }

    if (index >= size || data[index++] != 0x01) {
        throw std::runtime_error(ERR_INVALID_OBJECT);
    }

    std::string key;
    SolObject result;

    while (index < size) {
        if (data[index] == 0x01) {
            ++index;
            break;
        }

        key = ReadSolString(data, size, index, strpool);

        if (index >= size) {
            throw std::runtime_error(ERR_INVALID_OBJECT);
        }

        SolType type = static_cast<SolType>(data[index++]);
        result[key] = ReadSolValue(data, size, index, strpool, type);
    }

    return result;
}

sol::SolValue sol::ReadSolValue(uint8_t* data, int size, int& index, std::vector<std::string>& strpool, SolType type, bool istop)
{
    SolValue result;

    switch (type)
    {
    case sol::SolType::Null:
        result = nullptr;
        break;

    case sol::SolType::BooleanFalse:
        result = false;
        break;

    case sol::SolType::BooleanTrue:
        result = true;
        break;

    case sol::SolType::Integer:
        result = ReadSolInteger(data, size, index);
        break;

    case sol::SolType::Double:
        result = ReadSolDouble(data, size, index);
        break;

    case sol::SolType::String:
        result = ReadSolString(data, size, index, strpool);
        break;

    case sol::SolType::Array:
        result = ReadSolArray(data, size, index, strpool);
        break;

    case sol::SolType::Object:
        result = ReadSolObject(data, size, index, strpool, istop);
        break;

    case sol::SolType::Xml:
        result = ReadSolXml(data, size, index, strpool);
        break;

    case sol::SolType::Binary:
        result = ReadSolBinary(data, size, index);
        break;

    default:
        throw std::runtime_error(ERR_INVALID_TYPE);
    }

    if (istop) {
        if (index >= size || data[index++] != 0x00) {
            throw std::runtime_error(ERR_INVALID_FILE);
        }
    }

    return result;
}
