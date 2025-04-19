#include "sol.h"
#include "utils.h"
#include <sstream>


constexpr uint8_t SOL_MAGIC[] = { 0x00, 0xBF };
constexpr uint8_t SOL_CONSTANT[] = { 0x54, 0x43, 0x53, 0x4F, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 };

constexpr uint16_t AMF0_SHORTSTRING_MAXLEN = 0xFFFF;
constexpr uint16_t AMF0_LONGSTRING_MAXLEN = 0xFFFFFFFF;


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

    [[noreturn]] void ThrowFileEndedImproperlyOnReadingType(sol::AMF0Type type)
    {
        throw std::runtime_error(utils::FormatString(
            "File ended improperly on reading AMF0 type %d", static_cast<int>(type)));
    }

    [[noreturn]] void ThrowUnknownType(sol::SolType type, int index = -1)
    {
        if (index < 0) {
            throw std::runtime_error(utils::FormatString(
                "Unknown type %d", static_cast<int>(type)));
        }
        else {
            throw std::runtime_error(utils::FormatString(
                "Unknown type %d at index %d", static_cast<int>(type), index));
        }
    }

    [[noreturn]] void ThrowUnknownType(sol::AMF0Type type, int index = -1)
    {
        if (index < 0) {
            throw std::runtime_error(utils::FormatString(
                "Unknown AMF0 type %d", static_cast<int>(type)));
        }
        else {
            throw std::runtime_error(utils::FormatString(
                "Unknown AMF0 type %d at index %d", static_cast<int>(type), index));
        }
    }

    [[noreturn]] void ThrowBadFormatOfType(sol::SolType type, int index, int read, int desire)
    {
        throw std::runtime_error(utils::FormatString(
            "Bad format of type %d at index %d: read %d, desire %d", static_cast<int>(type), index, read, desire));
    }

    [[noreturn]] void ThrowBadFormatOfType(sol::AMF0Type type, int index, int read, int desire)
    {
        throw std::runtime_error(utils::FormatString(
            "Bad format of AMF0 type %d at index %d: read %d, desire %d", static_cast<int>(type), index, read, desire));
    }

    [[noreturn]] void ThrowEndRequired(int index, int read, int desire = 0)
    {
        throw std::runtime_error(utils::FormatString(
            "End required at index %d: read %d, desire %d", index, read, desire));
    }

    [[noreturn]] void ThrowUnsupportedVersion(sol::SolVersion version)
    {
        throw std::runtime_error(utils::FormatString(
            "Unsupported version: %d", static_cast<int>(version)));
    }

    template <typename T>
    void CheckRefIndex(const std::vector<T>& pool, int index)
    {
        if (index >= pool.size()) {
            throw std::runtime_error(utils::FormatString(
                "Reference index %d not found", index));
        }
    }

    std::string GetClassDefUniqueStr(const sol::SolClassDef& classdef)
    {
        std::stringstream ss;
        ss << classdef.name << ';'
            << classdef.dynamic << ';'
            << classdef.externalizable << ';';
        for (const auto& member : classdef.members) {
            ss << member << ';';
        }
        return ss.str();
    }

    int GetRefIndex(std::map<std::string, int>& pool, const std::string& id)
    {
        if (pool.count(id)) {
            return pool[id];
        }
        else {
            pool[id] = static_cast<int>(pool.size());
            return -1;
        }
    }

    template <typename T>
    std::enable_if_t<std::is_integral_v<T>, T> ReadBigEndian(uint8_t* data, int size, int& index)
    {
        if (index + sizeof(T) > size) {
            throw std::runtime_error(utils::FormatString(
                "File ended improperly on reading %c%d", std::is_unsigned_v<T> ? 'u' : 'i', sizeof(T) * 8));
        }
        T result = utils::ReverseEndian(
            *reinterpret_cast<T*>(data + index));
        index += sizeof(T);
        return result;
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

        file.version = (SolVersion)utils::ReverseEndian(
            *reinterpret_cast<uint32_t*>(data + index));
        index += 4;

        std::string key;
        SolRefTable reftable;

        switch (file.version)
        {
        case SolVersion::AMF0: {
            while (index < size) {
                key = ReadAMF0ShortString(data, size, index);

                AMF0Type type = ReadAMF0Type(data, size, index);
                file.data[key] = ReadAMF0Value(data, size, index, reftable, type);

                if (index >= size) {
                    ThrowFileEndedImproperly();
                }
                if (data[index] != 0x00) {
                    ThrowEndRequired(index, data[index]);
                }
                ++index;
            }
            return true;
        }

        case SolVersion::AMF3: {
            while (index < size) {
                key = ReadSolString(data, size, index, reftable);

                SolType type = ReadSolType(data, size, index);
                file.data[key] = ReadSolValue(data, size, index, reftable, type);

                if (index >= size) {
                    ThrowFileEndedImproperly();
                }
                if (data[index] != 0x00) {
                    ThrowEndRequired(index, data[index]);
                }
                ++index;
            }
            return true;
        }

        default: {
            ThrowUnsupportedVersion(file.version);
        }
        }
    }
    catch (const std::exception& e) {
        file.errmsg = e.what();
        return false;
    }
}

sol::SolType sol::ReadSolType(uint8_t* data, int size, int& index)
{
    if (index >= size) {
        throw std::runtime_error("File ended improperly, type expected");
    }
    return static_cast<SolType>(data[index++]);
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

    if (i == 3) {
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
    index += len;

    reftable.strpool.push_back(result);
    return result;
}

sol::SolValue sol::ReadSolXml(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType xmltype)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.objpool, ref >> 1);
        return reftable.objpool[ref >> 1];
    }

    int len = ref >> 1;

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(xmltype);
    }

    SolValue result(xmltype, std::string(data + index, data + index + len));
    index += len;

    reftable.objpool.push_back(result);
    return result;
}

sol::SolBinary sol::ReadSolBinary(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.objpool, ref >> 1);
        return reftable.objpool[ref >> 1].get<SolBinary>();
    }

    int len = ref >> 1;

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(SolType::Binary);
    }

    std::vector<uint8_t> result(data + index, data + index + len);
    index += len;

    reftable.objpool.push_back(result);
    return result;
}

sol::SolValue sol::ReadSolDate(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.objpool, ref >> 1);
        return reftable.objpool[ref >> 1];
    }

    SolValue result(SolType::Date, ReadSolDouble(data, size, index));
    reftable.objpool.push_back(result);
    return result;
}

sol::SolArray sol::ReadSolArray(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.objpool, ref >> 1);
        return reftable.objpool[ref >> 1].get<SolArray>();
    }

    int len = ref >> 1;

    SolArray result;
    result.dense.reserve(len);

    std::string name;
    while (!(name = ReadSolString(data, size, index, reftable)).empty()) {
        SolType type = ReadSolType(data, size, index);
        result.assoc[name] = ReadSolValue(data, size, index, reftable, type);
    }

    for (int i = 0; i < len; ++i) {
        SolType type = ReadSolType(data, size, index);
        result.dense.push_back(ReadSolValue(data, size, index, reftable, type));
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolObject sol::ReadSolObject(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    int ref = ReadSolInteger(data, size, index, true);

    if ((ref & 1) == 0) {
        CheckRefIndex(reftable.objpool, ref >> 1);
        return reftable.objpool[ref >> 1].get<SolObject>();
    }

    SolObject result;
    int classref = ref >> 1;

    if ((classref & 1) == 0) {
        int classindex = classref >> 1;
        CheckRefIndex(reftable.classpool, classindex);
        result.classdef = reftable.classpool[classindex];
    }
    else {
        result.classdef.externalizable = (classref >> 1) & 1;
        result.classdef.dynamic = (classref >> 2) & 1;

        int membernum = classref >> 3;
        result.classdef.members.reserve(membernum);

        if (result.classdef.externalizable) {
            throw std::runtime_error("Externalizable class is not supported");
        }

        result.classdef.name = ReadSolString(data, size, index, reftable);

        for (int i = 0; i < membernum; ++i) {
            result.classdef.members.push_back(ReadSolString(data, size, index, reftable));
        }

        reftable.classpool.push_back(result.classdef);
    }

    for (auto& member : result.classdef.members) {
        SolType type = ReadSolType(data, size, index);
        result.props[member] = ReadSolValue(data, size, index, reftable, type);
    }

    if (result.classdef.dynamic) {
        std::string key;
        while (!(key = ReadSolString(data, size, index, reftable)).empty()) {
            SolType type = ReadSolType(data, size, index);
            result.props[key] = ReadSolValue(data, size, index, reftable, type);
        }
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolValue sol::ReadSolValue(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType type)
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
        result = ReadSolObject(data, size, index, reftable);
        break;

    case SolType::Xml:
        result = ReadSolXml(data, size, index, reftable, SolType::Xml);
        break;

    case SolType::Binary:
        result = ReadSolBinary(data, size, index, reftable);
        break;

    default:
        ThrowUnknownType(type, index - 1);
    }

    return result;
}

bool sol::WriteSolFile(SolFile& file)
{
    try {
        std::vector<uint8_t> buffer;
        buffer.reserve(1024 * 100); // 100 KB

        // magic
        buffer.insert(buffer.end(), std::begin(SOL_MAGIC), std::end(SOL_MAGIC));

        // chunk size, to be filled later
        buffer.insert(buffer.end(), 4, 0);

        // constant
        buffer.insert(buffer.end(), std::begin(SOL_CONSTANT), std::end(SOL_CONSTANT));

        // sol name
        uint16_t namesize = utils::ReverseEndian((uint16_t)file.solname.size());
        buffer.insert(buffer.end(), reinterpret_cast<uint8_t*>(&namesize), reinterpret_cast<uint8_t*>(&namesize) + 2);
        buffer.insert(buffer.end(), file.solname.begin(), file.solname.end());

        // version
        uint32_t version = utils::ReverseEndian((uint32_t)file.version);
        buffer.insert(buffer.end(), reinterpret_cast<uint8_t*>(&version), reinterpret_cast<uint8_t*>(&version) + 4);

        // encode data
        switch (file.version)
        {
        case SolVersion::AMF0: {
            // TODO: AMF0 support
            file.errmsg = "AMF0 is currently not supported";
            return false;
        }

        case SolVersion::AMF3: {
            // ref table
            SolWriteRefTable reftable;

            // data
            for (auto& [key, value] : file.data) {
                // key
                WriteSolString(buffer, key, reftable);
                // value
                WriteSolType(buffer, value.type);
                WriteSolValue(buffer, value, reftable);
                // end
                buffer.push_back(0x00);
            }
            break;
        }

        default: {
            ThrowUnsupportedVersion(file.version);
        }
        }

        // fill chunk size
        uint32_t chunksize = utils::ReverseEndian((uint32_t)buffer.size() - 6);
        std::copy_n(reinterpret_cast<uint8_t*>(&chunksize), 4, buffer.begin() + 2);

        // write to file
        utils::WriteFile(file.path, buffer);
        return true;
    }
    catch (const std::exception& e) {
        file.errmsg = e.what();
        return false;
    }
}

void sol::WriteSolType(std::vector<uint8_t>& buffer, SolType type)
{
    buffer.push_back(static_cast<uint8_t>(type));
}

void sol::WriteSolInteger(std::vector<uint8_t>& buffer, SolInteger value, bool unsign)
{
    if (!unsign && value < 0) {
        value += 0x20000000;
    }

    if (value < 0x80) {
        buffer.push_back(value);
    }
    else if (value < 0x4000) {
        buffer.push_back((value >> 7) | 0x80);
        buffer.push_back(value & 0x7F);
    }
    else if (value < 0x200000) {
        buffer.push_back((value >> 14) | 0x80);
        buffer.push_back((value >> 7) | 0x80);
        buffer.push_back(value & 0x7F);
    }
    else {
        buffer.push_back((value >> 22) | 0x80);
        buffer.push_back((value >> 15) | 0x80);
        buffer.push_back((value >> 8) | 0x80);
        buffer.push_back(value & 0xFF);
    }
}

void sol::WriteSolDouble(std::vector<uint8_t>& buffer, SolDouble value)
{
    uint64_t tmp = *reinterpret_cast<uint64_t*>(&value);
    tmp = utils::ReverseEndian(tmp);
    buffer.insert(buffer.end(), reinterpret_cast<uint8_t*>(&tmp), reinterpret_cast<uint8_t*>(&tmp) + 8);
}

void sol::WriteSolString(std::vector<uint8_t>& buffer, const SolString& value, SolWriteRefTable& reftable)
{
    if (value.empty()) {
        WriteSolInteger(buffer, 1, true);
        return;
    }

    int ref = GetRefIndex(reftable.strpool, value);

    if (ref >= 0) {
        WriteSolInteger(buffer, ref << 1, true);
        return;
    }

    int len = (int)value.size();
    WriteSolInteger(buffer, (len << 1) | 1, true);
    buffer.insert(buffer.end(), value.begin(), value.end());
}

void sol::WriteSolXml(std::vector<uint8_t>& buffer, const SolString& value, SolWriteRefTable& reftable, SolType xmltype)
{
    int len = (int)value.size();
    WriteSolInteger(buffer, (len << 1) | 1, true);
    buffer.insert(buffer.end(), value.begin(), value.end());
}

void sol::WriteSolBinary(std::vector<uint8_t>& buffer, const SolBinary& value, SolWriteRefTable& reftable)
{
    int len = (int)value.size();
    WriteSolInteger(buffer, (len << 1) | 1, true);
    buffer.insert(buffer.end(), value.begin(), value.end());
}

void sol::WriteSolDate(std::vector<uint8_t>& buffer, SolDouble value, SolWriteRefTable& reftable)
{
    WriteSolInteger(buffer, 1, true);
    WriteSolDouble(buffer, value);
}

void sol::WriteSolArray(std::vector<uint8_t>& buffer, const SolArray& value, SolWriteRefTable& reftable)
{
    int len = (int)value.dense.size();
    WriteSolInteger(buffer, (len << 1) | 1, true);

    for (auto& [key, val] : value.assoc) {
        WriteSolString(buffer, key, reftable);
        WriteSolType(buffer, val.type);
        WriteSolValue(buffer, val, reftable);
    }

    WriteSolString(buffer, std::string(), reftable);

    for (auto& val : value.dense) {
        WriteSolType(buffer, val.type);
        WriteSolValue(buffer, val, reftable);
    }
}

void sol::WriteSolObject(std::vector<uint8_t>& buffer, const SolObject& value, SolWriteRefTable& reftable)
{
    if (value.classdef.externalizable) {
        throw std::runtime_error("Externalizable class is not supported");
    }

    auto classid = GetClassDefUniqueStr(value.classdef);
    int classindex = GetRefIndex(reftable.classpool, classid);

    if (classindex >= 0) {
        int classref = classindex << 1;
        WriteSolInteger(buffer, (classref << 1) | 1, true);
    }
    else {
        int classref = (int)value.classdef.members.size();
        classref = (classref << 1) | !!(int)value.classdef.dynamic;
        classref = (classref << 1) | !!(int)value.classdef.externalizable;
        classref = (classref << 1) | 1;

        WriteSolInteger(buffer, (classref << 1) | 1, true);
        WriteSolString(buffer, value.classdef.name, reftable);

        for (auto& member : value.classdef.members) {
            WriteSolString(buffer, member, reftable);
        }
    }

    std::map<std::string, const SolValue*> members;
    std::map<std::string, const SolValue*> dynamicprops;

    for (auto& member : value.classdef.members) {
        members[member] = value.props.count(member) ? &value.props.at(member) : nullptr;
    }
    for (auto& [key, val] : value.props) {
        if (!members.count(key)) {
            dynamicprops[key] = &val;
        }
    }

    for (auto& [member, val] : members) {
        WriteSolType(buffer, val ? val->type : SolType::Undefined);
        WriteSolValue(buffer, val ? *val : SolValue(SolType::Undefined, nullptr), reftable);
    }
    if (value.classdef.dynamic) {
        for (auto& [key, val] : dynamicprops) {
            WriteSolString(buffer, key, reftable);
            WriteSolType(buffer, val->type);
            WriteSolValue(buffer, *val, reftable);
        }
    }

    WriteSolString(buffer, std::string(), reftable);
}

void sol::WriteSolValue(std::vector<uint8_t>& buffer, const SolValue& value, SolWriteRefTable& reftable)
{
    switch (value.type)
    {
    case SolType::Undefined:
    case SolType::Null:
    case SolType::BooleanFalse:
    case SolType::BooleanTrue:
        // nothing to write
        break;

    case SolType::Integer:
        WriteSolInteger(buffer, value.get<SolInteger>());
        break;

    case SolType::Double:
        WriteSolDouble(buffer, value.get<SolDouble>());
        break;

    case SolType::String:
        WriteSolString(buffer, value.get<SolString>(), reftable);
        break;

    case SolType::XmlDoc:
    case SolType::Xml:
        WriteSolXml(buffer, value.get<SolString>(), reftable, value.type);
        break;

    case SolType::Date:
        WriteSolDate(buffer, value.get<SolDouble>(), reftable);
        break;

    case SolType::Array:
        WriteSolArray(buffer, value.get<SolArray>(), reftable);
        break;

    case SolType::Object:
        WriteSolObject(buffer, value.get<SolObject>(), reftable);
        break;

    case SolType::Binary:
        WriteSolBinary(buffer, value.get<SolBinary>(), reftable);
        break;

    default:
        ThrowUnknownType(value.type);
    }
}

sol::AMF0Type sol::GetAMF0Type(const SolValue& value)
{
    switch (value.type)
    {
    case SolType::Undefined:
        return AMF0Type::Undefined;

    case SolType::Null:
        return AMF0Type::Null;

    case SolType::BooleanFalse:
    case SolType::BooleanTrue:
        return AMF0Type::Boolean;

    case SolType::Integer:
    case SolType::Double:
        return AMF0Type::Number;

    case SolType::String:
        return value.get<SolString>().size() > AMF0_SHORTSTRING_MAXLEN
            ? AMF0Type::LongString : AMF0Type::String; \

    case SolType::XmlDoc:
    case SolType::Xml:
        return AMF0Type::XMLDoc;

    case SolType::Date:
        return AMF0Type::Date;

    case SolType::Array:
        return value.get<SolArray>().assoc.empty()
            ? AMF0Type::StrictArray : AMF0Type::EcmaArray;

    case SolType::Object:
        return value.get<SolObject>().classdef.name.empty()
            ? AMF0Type::Object : AMF0Type::TypedObject;

    default:
        throw std::runtime_error(utils::FormatString(
            "Unable to convert type %d to AMF0 type", static_cast<int>(value.type)));
    }
}

sol::AMF0Type sol::ReadAMF0Type(uint8_t* data, int size, int& index)
{
    if (index >= size) {
        throw std::runtime_error("File ended improperly, AMF0 type expected");
    }
    return static_cast<AMF0Type>(data[index++]);
}

sol::SolDouble sol::ReadAMF0Number(uint8_t* data, int size, int& index)
{
    return ReadSolDouble(data, size, index);
}

sol::SolBoolean sol::ReadAMF0Boolean(uint8_t* data, int size, int& index)
{
    if (index >= size) {
        ThrowFileEndedImproperlyOnReadingType(AMF0Type::Boolean);
    }
    return data[index++] != 0x00;
}

sol::SolString sol::ReadAMF0ShortString(uint8_t* data, int size, int& index)
{
    uint16_t len = ReadBigEndian<uint16_t>(data, size, index);

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(AMF0Type::String);
    }
    if (len == 0) {
        return std::string();
    }

    std::string result(data + index, data + index + len);
    index += len;
    return result;
}

sol::SolString sol::ReadAMF0LongString(uint8_t* data, int size, int& index)
{
    uint32_t len = ReadBigEndian<uint32_t>(data, size, index);

    if (index + len > size) {
        ThrowFileEndedImproperlyOnReadingType(AMF0Type::LongString);
    }
    if (len == 0) {
        return std::string();
    }

    std::string result(data + index, data + index + len);
    index += len;
    return result;
}

sol::SolValue sol::ReadAMF0XmlDoc(uint8_t* data, int size, int& index)
{
    return SolValue(SolType::XmlDoc, ReadAMF0LongString(data, size, index));
}

sol::SolValue sol::ReadAMF0Date(uint8_t* data, int size, int& index)
{
    int16_t zone = ReadBigEndian<int16_t>(data, size, index); // unused
    return SolValue(SolType::Date, ReadSolDouble(data, size, index));
}

sol::SolValue sol::ReadAMF0Reference(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    uint16_t ref = ReadBigEndian<uint16_t>(data, size, index);
    CheckRefIndex(reftable.objpool, ref);
    return reftable.objpool[ref];
}

sol::SolArray sol::ReadAMF0EcmaArray(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    uint32_t len = ReadBigEndian<uint32_t>(data, size, index);

    SolArray result;
    std::string key;

    for (uint32_t i = 0; i < len; ++i) {
        key = ReadAMF0ShortString(data, size, index);
        AMF0Type type = ReadAMF0Type(data, size, index);
        result.assoc[key] = ReadAMF0Value(data, size, index, reftable, type);
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolArray sol::ReadAMF0StrictArray(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    uint32_t len = ReadBigEndian<uint32_t>(data, size, index);

    SolArray result;
    result.dense.reserve(len);

    for (uint32_t i = 0; i < len; ++i) {
        AMF0Type type = ReadAMF0Type(data, size, index);
        result.dense.push_back(ReadAMF0Value(data, size, index, reftable, type));
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolObject sol::ReadAMF0Object(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    SolObject result;
    std::string key;

    while (!(key = ReadAMF0ShortString(data, size, index)).empty()) {
        AMF0Type type = ReadAMF0Type(data, size, index);
        result.props[key] = ReadAMF0Value(data, size, index, reftable, type);
    }

    if (ReadAMF0Type(data, size, index) != AMF0Type::ObjectEnd) {
        ThrowBadFormatOfType(AMF0Type::Object, index - 1, data[index], (int)AMF0Type::ObjectEnd);
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolValue sol::ReadAMF0TypedObject(uint8_t* data, int size, int& index, SolRefTable& reftable)
{
    SolObject result;
    std::string key;

    std::string classname = ReadAMF0ShortString(data, size, index);
    result.classdef.name = classname;

    while (!(key = ReadAMF0ShortString(data, size, index)).empty()) {
        AMF0Type type = ReadAMF0Type(data, size, index);
        result.props[key] = ReadAMF0Value(data, size, index, reftable, type);
    }

    if (ReadAMF0Type(data, size, index) != AMF0Type::ObjectEnd) {
        ThrowBadFormatOfType(AMF0Type::TypedObject, index - 1, data[index], static_cast<int>(AMF0Type::ObjectEnd));
    }

    reftable.objpool.push_back(result);
    return result;
}

sol::SolValue sol::ReadAMF0Value(uint8_t* data, int size, int& index, SolRefTable& reftable, AMF0Type type)
{
    switch (type)
    {
    case AMF0Type::Number:
        return ReadAMF0Number(data, size, index);

    case AMF0Type::Boolean:
        return ReadAMF0Boolean(data, size, index);

    case AMF0Type::String:
        return ReadAMF0ShortString(data, size, index);

    case AMF0Type::Object:
        return ReadAMF0Object(data, size, index, reftable);

    case AMF0Type::Null:
        return nullptr;

    case AMF0Type::Undefined:
        return SolValue(SolType::Undefined, nullptr);

    case AMF0Type::Reference:
        return ReadAMF0Reference(data, size, index, reftable);

    case AMF0Type::EcmaArray:
        return ReadAMF0EcmaArray(data, size, index, reftable);

    case AMF0Type::StrictArray:
        return ReadAMF0StrictArray(data, size, index, reftable);

    case AMF0Type::Date:
        return ReadAMF0Date(data, size, index);

    case AMF0Type::LongString:
        return ReadAMF0LongString(data, size, index);

    case AMF0Type::XMLDoc:
        return ReadAMF0XmlDoc(data, size, index);

    case AMF0Type::TypedObject:
        return ReadAMF0TypedObject(data, size, index, reftable);

    case AMF0Type::MovieClip:
    case AMF0Type::ObjectEnd:
    case AMF0Type::Unsupported:
    case AMF0Type::Recordset:
        throw std::runtime_error(utils::FormatString(
            "Unsupported AMF0 type %d", static_cast<int>(type)));

    default:
        ThrowUnknownType(type);
    }
}
