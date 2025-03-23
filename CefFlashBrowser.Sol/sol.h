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

    using SolNull = std::nullptr_t;
    using SolBoolean = bool;
    using SolInteger = int32_t;
    using SolDouble = double;
    using SolString = std::string;
    using SolArray = std::vector<SolValue>;
    using SolObject = std::map<std::string, SolValue>;
    using SolBinary = std::vector<uint8_t>;


    enum class SolType : uint8_t
    {
        Null = 0x01,
        BooleanFalse = 0x02,
        BooleanTrue = 0x03,
        Integer = 0x04,
        Double = 0x05,
        String = 0x06,
        XmlDoc = 0x07,
        Array = 0x09,
        Object = 0x0A,
        Xml = 0x0B,
        Binary = 0x0C,
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
        bool is() const
        {
            switch (type)
            {
            case SolType::Null:
                return std::is_same_v<T, SolNull>;
            case SolType::BooleanFalse:
            case SolType::BooleanTrue:
                return std::is_same_v<T, SolBoolean>;
            case SolType::Integer:
                return std::is_same_v<T, SolInteger>;
            case SolType::Double:
                return std::is_same_v<T, SolDouble>;
            case SolType::String:
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

        template <typename T>
        T get() const
        {
            return std::get<T>(value);
        }
    };


    struct SolFile
    {
        std::string path;
        std::string errmsg;
        std::string solname;
        uint32_t version;
        SolObject data;

        bool valid() const { return errmsg.empty(); }
    };


    struct SolRefTable
    {
        std::vector<std::string> strpool;
    };


    bool IsKnownType(SolType type);

    bool ReadSolFile(SolFile& file);

    SolInteger ReadSolInteger(uint8_t* data, int size, int& index, bool unsign = false);

    SolDouble ReadSolDouble(uint8_t* data, int size, int& index);

    SolString ReadSolString(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolBinary ReadSolBinary(uint8_t* data, int size, int& index);

    SolArray ReadSolArray(uint8_t* data, int size, int& index, SolRefTable& reftable);

    SolObject ReadSolObject(uint8_t* data, int size, int& index, SolRefTable& reftable, bool istop = false);

    SolValue ReadSolXml(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType xmltype);

    SolValue ReadSolValue(uint8_t* data, int size, int& index, SolRefTable& reftable, SolType type, bool istop = false);
}

#endif // !__SOL_H__
