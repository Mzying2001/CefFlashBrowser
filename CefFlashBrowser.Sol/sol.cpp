#include "sol.h"
#include "utils.h"


constexpr uint8_t SOL_MAGIC[] = { 0x00, 0xBF };
constexpr uint8_t SOL_CONSTANT[] = { 0x54, 0x43, 0x53, 0x4F, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 };


namespace
{
    [[noreturn]] void ThrowFileEndedImproperly()
    {
        throw std::runtime_error(
            "File ended improperly");
    }

    [[noreturn]] void ThrowFileEndedImproperlyOnReadingType(sol::SolType type)
    {
        throw std::runtime_error(utils::FormatString(
            "File ended improperly on reading type %d", static_cast<int>(type)));
    }

    [[noreturn]] void ThrowUnknownType(sol::SolType type)
    {
        throw std::runtime_error(utils::FormatString(
            "Unknown type: %d", static_cast<int>(type)));
    }

    [[noreturn]] void ThrowBadFormatOfType(sol::SolType type, int index, int read, int desire)
    {
        throw std::runtime_error(utils::FormatString(
            "Bad format of type %d at index %d: read %d, desire %d", static_cast<int>(type), index, read, desire));
    }

    [[noreturn]] void ThrowEndRequired(int index, int read, int desire = 0)
    {
        throw std::runtime_error(utils::FormatString(
            "End required at index %d: read %d, desire %d", index, read, desire));
    }

    template <typename T>
    void CheckRefIndex(const std::vector<T>& pool, int index)
    {
        if (index >= pool.size()) {
            throw std::runtime_error(utils::FormatString(
                "Reference index %d not found", index));
        }
    }
}


bool sol::IsKnownType(SolType type)
{
    switch (type)
    {
    case SolType::Undefined:
    case SolType::Null:
    case SolType::BooleanFalse:
    case SolType::BooleanTrue:
    case SolType::Integer:
    case SolType::Double:
    case SolType::String:
    case SolType::XmlDoc:
    case SolType::Date:
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
            file.errmsg = "File too small";
            return false;
        }

        if (memcmp(data, SOL_MAGIC, 2) != 0) {
            file.errmsg = "File magic mismatch";
            return false;
        }
        index += 2;

        uint32_t chunksize = utils::ReverseEndian(
            *reinterpret_cast<uint32_t*>(data + index));

        if (chunksize != size - 6) {
            file.errmsg = "Chunk size mismatch";
            return false;
        }
        index += 4;

        if (memcmp(data + index, SOL_CONSTANT, 10) != 0) {
            file.errmsg = "File constant mismatch";
            return false;
        }
        index += 10;

        if (index + 2 > size) {
            ThrowFileEndedImproperly();
        }

        uint16_t namesize = utils::ReverseEndian(
            *reinterpret_cast<uint16_t*>(data + index));
        index += 2;

        if (index + namesize > size) {
            ThrowFileEndedImproperly();
        }

        file.solname = std::string(data + index, data + index + namesize);
        index += namesize;

        if (index + 4 > size) {
            ThrowFileEndedImproperly();
        }

        file.version = utils::ReverseEndian(
            *reinterpret_cast<uint32_t*>(data + index));
        index += 4;

        std::string key;
        SolRefTable reftable;

        while (index < size) {
            key = ReadSolString(data, size, index, reftable);

            if (index >= size) {
                ThrowFileEndedImproperly();
            }

            SolType type = static_cast<SolType>(data[index++]);
            file.data[key] = ReadSolValue(data, size, index, reftable, type, true);
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
    int32_t result = 0;

    int i = 0;
    for (; i < 3; ++i) {
        if (index + i >= size) {
            ThrowFileEndedImproperlyOnReadingType(SolType::Integer);
        }
        result = result << 7 | (data[index + i] & 0x7F);
        if (!(data[index + i] & 0x80)) break;
    }

    if (i == 4) {
        if (index + i >= size) {
            ThrowFileEndedImproperlyOnReadingType(SolType::Integer);
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
        ThrowFileEndedImproperlyOnReadingType(SolType::Double);
    }

    uint64_t tmp = utils::ReverseEndian(
        *reinterpret_cast<uint64_t*>(data + index));

    index += 8;
    return *reinterpret_cast<double*>(&tmp);
}

sol::SolString sol::ReadSolString(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.strpool, ref >> 1);
        return reftable.strpool[ref >> 1];
    }

    int len = ref >> 1;

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::String);
    }

    if (len == 0) {
        return std::string();
    }

    std::string result(data + index, data + index + len);
    reftable.strpool.push_back(result);

    index += len;
    return result;
}

sol::SolValue sol::ReadSolXml(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType xmltype)
{
    int len = ReadSolInteger(data, size, index, true) >> 1;

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(xmltype);
    }

    std::string xml(data + index, data + index + len);
    index += len;
    return SolValue(xmltype, xml);
}

sol::SolBinary sol::ReadSolBinary(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int len = ReadSolInteger(data, size, index, true) >> 1;

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::Binary);
    }

    std::vector<uint8_t> result(data + index, data + index + len);
    index += len;
    return result;
}

sol::SolValue sol::ReadSolDate(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    if (index >= size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::Date);
    }

    if (data[index] != 0x01) {
        ThrowBadFormatOfType(SolType::Date, index, data[index], 0x01);
    }
    ++index;

    double value = ReadSolDouble(data, size, index);
    return SolValue(SolType::Date, value);
}

sol::SolArray sol::ReadSolArray(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int len = ReadSolInteger(data, size, index, true) >> 1;

    if (index >= size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::Array);
    }

    if (data[index] != 0x01) {
        ThrowBadFormatOfType(SolType::Array, index, data[index], 0x01);
    }
    ++index;

    std::vector<SolValue> result;
    result.reserve(len);

    for (int i = 0; i < len; ++i) {
        if (index >= size) {
            ThrowFileEndedImproperlyOnReadingType(SolType::Array);
        }
        SolType type = static_cast<SolType>(data[index++]);
        result.push_back(ReadSolValue(data, size, index, reftable, type));
    }

    return result;
}

sol::SolObject sol::ReadSolObject(uint8_t* data, int size, int& index, SolRefTable& reftable, bool istop)
{
    if (istop) {
        if (index >= size) {
            ThrowFileEndedImproperlyOnReadingType(SolType::Object);
        }
        if (data[index] != 0x0B) {
            ThrowBadFormatOfType(SolType::Object, index, data[index], 0x0B);
        }
        ++index;
    }

    if (index >= size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::Object);
    }

    if (data[index] != 0x01) {
        ThrowBadFormatOfType(SolType::Object, index, data[index], 0x01);
    }
    ++index;

    std::string key;
    SolObject result;

    while (index < size) {
        key = ReadSolString(data, size, index, reftable);

        if (key.empty()) {
            break;
        }
        if (index >= size) {
            ThrowFileEndedImproperlyOnReadingType(SolType::Object);
        }

        SolType type = static_cast<SolType>(data[index++]);
        result[key] = ReadSolValue(data, size, index, reftable, type);
    }

    return result;
}

sol::SolValue sol::ReadSolValue(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType type, bool istop)
{
    SolValue result;

    switch (type)
    {
    case SolType::Undefined:
        result.type = SolType::Undefined;
        break;

    case SolType::Null:
        //result = nullptr;
        break;

    case SolType::BooleanFalse:
        result = false;
        break;

    case SolType::BooleanTrue:
        result = true;
        break;

    case SolType::Integer:
        result = ReadSolInteger(data, size, index);
        break;

    case SolType::Double:
        result = ReadSolDouble(data, size, index);
        break;

    case SolType::String:
        result = ReadSolString(data, size, index, reftable);
        break;

    case SolType::XmlDoc:
        result = ReadSolXml(data, size, index, reftable, sol::SolType::XmlDoc);
        break;

    case SolType::Date:
        result = ReadSolDate(data, size, index, reftable);
        break;

    case SolType::Array:
        result = ReadSolArray(data, size, index, reftable);
        break;

    case SolType::Object:
        result = ReadSolObject(data, size, index, reftable, istop);
        break;

    case SolType::Xml:
        result = ReadSolXml(data, size, index, reftable, SolType::Xml);
        break;

    case SolType::Binary:
        result = ReadSolBinary(data, size, index, reftable);
        break;

    default:
        ThrowUnknownType(type);
    }

    if (istop) {
        if (index >= size) {
            ThrowFileEndedImproperly();
        }
        if (data[index] != 0x00) {
            ThrowEndRequired(index, data[index]);
        }
        ++index;
    }

    return result;
}
