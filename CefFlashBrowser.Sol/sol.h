#ifndef __SOL_H__
#define __SOL_H__

#include <cstdint>
#include <string>
#include <vector>
#include <map>
#include <variant>
#include <stdexcept>
#include <type_traits>

namespace sol
{
    struct SolValue;
    struct SolArray;
    struct SolObject;

    using SolNull = std::nullptr_t;
    using SolBoolean = bool;
    using SolInteger = int32_t;
    using SolDouble = double;
    using SolString = std::string;
    using SolBinary = std::vector<uint8_t>;


    enum class SolType : uint8_t
    {
        Undefined = 0x00,
        Null = 0x01,
        BooleanFalse = 0x02,
        BooleanTrue = 0x03,
        Integer = 0x04,
        Double = 0x05,
        String = 0x06,
        XmlDoc = 0x07,
        Date = 0x08,
        Array = 0x09,
        Object = 0x0A,
        Xml = 0x0B,
        Binary = 0x0C,
    };


    enum class SolVersion : uint32_t
    {
        AMF0 = 0,
        AMF3 = 3,
    };


    struct SolArray
    {
        std::map<std::string, SolValue> assoc;
        std::vector<SolValue> dense;
    };


    struct SolClassDef
    {
        bool dynamic;
        bool externalizable;
        std::string name;
        std::vector<std::string> members;
    };


    struct SolObject
    {
        SolClassDef classdef;
        std::map<std::string, SolValue> props;
    };


    struct SolValue
    {
        SolType type;
        std::variant<SolNull, SolBoolean, SolInteger, SolDouble, SolString, SolArray, SolObject, SolBinary> value;

        SolValue(SolNull = nullptr) : type(SolType::Null), value(nullptr) {}
        SolValue(SolBoolean v) : type(v ? SolType::BooleanTrue : SolType::BooleanFalse), value(v) {}
        SolValue(SolInteger v) : type(SolType::Integer), value(v) {}
        SolValue(SolDouble v) : type(SolType::Double), value(v) {}
        SolValue(const SolString& v) : type(SolType::String), value(v) {}
        SolValue(const SolArray& v) : type(SolType::Array), value(v) {}
        SolValue(const SolObject& v) : type(SolType::Object), value(v) {}
        SolValue(const SolBinary& v) : type(SolType::Binary), value(v) {}
        SolValue(const SolValue& v) : type(v.type), value(v.value) {}

        template <typename T>
        SolValue(SolType t, const T& v) : type(t), value(v) {}

        template <typename T>
        const T& get() const { return std::get<T>(value); }

        template <typename T>
        T& get() { return std::get<T>(value); }

        template <typename T>
        bool is() const
        {
            switch (type)
            {
            case SolType::Undefined:
            case SolType::Null:
                return std::is_same_v<T, SolNull>;
            case SolType::BooleanFalse:
            case SolType::BooleanTrue:
                return std::is_same_v<T, SolBoolean>;
            case SolType::Integer:
                return std::is_same_v<T, SolInteger>;
            case SolType::Double:
            case SolType::Date:
                return std::is_same_v<T, SolDouble>;
            case SolType::String:
            case SolType::XmlDoc:
            case SolType::Xml:
                return std::is_same_v<T, SolString>;
            case SolType::Array:
                return std::is_same_v<T, SolArray>;
            case SolType::Object:
                return std::is_same_v<T, SolObject>;
            case SolType::Binary:
                return std::is_same_v<T, SolBinary>;
            default:
                return false;
            }
        }
    };


    struct SolFile
    {
        std::string path;
        std::string errmsg;
        std::string solname;
        SolVersion version;
        std::map<std::string, SolValue> data;

        bool valid() const { return errmsg.empty(); }
    };


    struct SolRefTable
    {
        std::vector<std::string> strpool;
        std::vector<SolValue> objpool;
        std::vector<SolClassDef> classpool;
    };


    struct SolWriteRefTable
    {
        std::map<std::string, int> strpool;
        //std::map<std::string, int> objpool; // not supported
        std::map<std::string, int> classpool;
    };


    bool IsKnownType(SolType type);

    bool ReadSolFile(SolFile& file);

    SolType ReadSolType(uint8_t* data, int size, int& index);

    SolInteger ReadSolInteger(uint8_t* data, int size, int& index, bool unsign = false);

    SolDouble ReadSolDouble(uint8_t* data, int size, int& index);

    SolString ReadSolString(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolValue ReadSolXml(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType xmltype);

    SolBinary ReadSolBinary(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolValue ReadSolDate(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolArray ReadSolArray(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolObject ReadSolObject(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolValue ReadSolValue(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType type);


    bool WriteSolFile(SolFile& file);

    void WriteSolType(std::vector<uint8_t>& buffer, SolType type);

    void WriteSolInteger(std::vector<uint8_t>& buffer, SolInteger value, bool unsign = false);

    void WriteSolDouble(std::vector<uint8_t>& buffer, SolDouble value);

    void WriteSolString(std::vector<uint8_t>& buffer, const SolString& value, SolWriteRefTable& reftable);

    void WriteSolXml(std::vector<uint8_t>& buffer, const SolString& value, SolWriteRefTable& reftable, SolType xmltype);

    void WriteSolBinary(std::vector<uint8_t>& buffer, const SolBinary& value, SolWriteRefTable& reftable);

    void WriteSolDate(std::vector<uint8_t>& buffer, SolDouble value, SolWriteRefTable& reftable);

    void WriteSolArray(std::vector<uint8_t>& buffer, const SolArray& value, SolWriteRefTable& reftable);

    void WriteSolObject(std::vector<uint8_t>& buffer, const SolObject& value, SolWriteRefTable& reftable);

    void WriteSolValue(std::vector<uint8_t>& buffer, const SolValue& value, SolWriteRefTable& reftable);
}

#endif // !__SOL_H__
