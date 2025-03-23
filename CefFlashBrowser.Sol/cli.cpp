#include "cli.h"
#include "utils.h"


using namespace sol;


CefFlashBrowser::Sol::SolValueWrapper::SolValueWrapper(sol::SolValue* pval)
    : _pval(pval)
{
}

CefFlashBrowser::Sol::SolValueWrapper::SolValueWrapper()
    : _pval(new SolValue())
{
}

CefFlashBrowser::Sol::SolValueWrapper::~SolValueWrapper()
{
    delete _pval;
}

System::Type^ CefFlashBrowser::Sol::SolValueWrapper::Type::get()
{
    switch (_pval->type)
    {
    case SolType::Undefined:
        return SolUndefined::typeid;

    case SolType::BooleanTrue:
    case SolType::BooleanFalse:
        return Boolean::typeid;

    case SolType::Integer:
        return Int32::typeid;

    case SolType::Double:
        return Double::typeid;

    case SolType::String:
        return String::typeid;

    case SolType::XmlDoc:
        return SolXmlDoc::typeid;

    case SolType::Array:
        return array<SolValueWrapper^>::typeid;

    case SolType::Object:
        return Dictionary<String^, SolValueWrapper^>::typeid;

    case SolType::Xml:
        return SolXml::typeid;

    case SolType::Binary:
        return array<Byte>::typeid;

    case SolType::Null:
    default:
        return Object::typeid;
    }
}

bool CefFlashBrowser::Sol::SolValueWrapper::IsNull::get()
{
    return _pval->type == SolType::Null;
}

System::Object^ CefFlashBrowser::Sol::SolValueWrapper::GetValue()
{
    switch (_pval->type)
    {
    case SolType::Undefined:
        return SolUndefined::Value;

    case SolType::BooleanTrue:
    case SolType::BooleanFalse:
        return gcnew Boolean(_pval->get<SolBoolean>());

    case SolType::Integer:
        return gcnew Int32(_pval->get<SolInteger>());

    case SolType::Double:
        return gcnew Double(_pval->get<SolDouble>());

    case SolType::String:
        return utils::ToSystemString(_pval->get<SolString>());

    case SolType::XmlDoc:
        return gcnew SolXmlDoc(utils::ToSystemString(_pval->get<SolString>()));

    case SolType::Array: {
        auto arr = _pval->get<SolArray>();
        auto res = gcnew array<SolValueWrapper^>((int)arr.size());
        for (int i = 0; i < (int)arr.size(); i++) {
            res[i] = gcnew SolValueWrapper(new SolValue(arr[i]));
        }
        return res;
    }

    case SolType::Object: {
        auto obj = _pval->get<SolObject>();
        auto res = gcnew Dictionary<String^, SolValueWrapper^>((int)obj.size());
        for (auto& [key, val] : obj) {
            res->Add(utils::ToSystemString(key), gcnew SolValueWrapper(new SolValue(val)));
        }
        return res;
    }

    case SolType::Xml:
        return gcnew SolXml(utils::ToSystemString(_pval->get<SolString>()));

    case SolType::Binary:
        return utils::ToByteArray(_pval->get<SolBinary>());

    case SolType::Null:
    default:
        return nullptr;
    }
}

void CefFlashBrowser::Sol::SolValueWrapper::SetValue(Object^ value)
{
    if (value == nullptr) {
        _pval->type = SolType::Null;
        _pval->value = nullptr;
        return;
    }

    auto type = value->GetType();

    if (type == SolUndefined::typeid) {
        _pval->type = SolType::Undefined;
        _pval->value = nullptr;
    }
    else if (type == Boolean::typeid) {
        bool b = (bool)value;
        _pval->type = b ? SolType::BooleanTrue : SolType::BooleanFalse;
        _pval->value = b;
    }
    else if (type == Int32::typeid) {
        _pval->type = SolType::Integer;
        _pval->value = (int32_t)(int)value;
    }
    else if (type == Double::typeid) {
        _pval->type = SolType::Double;
        _pval->value = (double)value;
    }
    else if (type == String::typeid) {
        _pval->type = SolType::String;
        _pval->value = utils::ToStdString((String^)value);
    }
    else if (type == SolXmlDoc::typeid) {
        _pval->type = SolType::XmlDoc;
        _pval->value = utils::ToStdString(((SolXmlDoc^)value)->Data);
    }
    else if (type == array<SolValueWrapper^>::typeid) {
        auto arr = (array<SolValueWrapper^>^)value;
        SolArray res;
        res.reserve(arr->Length);
        for (int i = 0; i < arr->Length; i++) {
            res.push_back(*arr[i]->_pval);
        }
        _pval->type = SolType::Array;
        _pval->value = res;
    }
    else if (type == Dictionary<String^, SolValueWrapper^>::typeid) {
        auto obj = (Dictionary<String^, SolValueWrapper^>^)value;
        SolObject res;
        for each (KeyValuePair<String^, SolValueWrapper^> ^ pair in obj) {
            res[utils::ToStdString(pair->Key)] = *pair->Value->_pval;
        }
        _pval->type = SolType::Object;
        _pval->value = res;
    }
    else if (type == SolXml::typeid) {
        _pval->type = SolType::Xml;
        _pval->value = utils::ToStdString(((SolXml^)value)->Data);
    }
    else if (type == array<Byte>::typeid) {
        _pval->type = SolType::Binary;
        _pval->value = utils::ToByteVector((array<Byte>^)value);
    }
    else {
        throw gcnew ArgumentException("Unsupported type");
    }
}

CefFlashBrowser::Sol::SolFileWrapper::SolFileWrapper(String^ path)
    : _pfile(new SolFile())
{
    _pfile->path = utils::ToStdString(path, false);

    if (!sol::ReadSolFile(*_pfile)) {
        auto errmsg = utils::ToSystemString(_pfile->errmsg);
        delete _pfile;
        _pfile = nullptr;
        throw gcnew Exception(errmsg);
    }

    auto& data = _pfile->data;
    _data = gcnew Dictionary<String^, SolValueWrapper^>((int)data.size());

    for (auto& [key, val] : data) {
        _data->Add(utils::ToSystemString(key), gcnew SolValueWrapper(new SolValue(data[key])));
    }
}

CefFlashBrowser::Sol::SolFileWrapper::~SolFileWrapper()
{
    delete _pfile;
}

System::String^ CefFlashBrowser::Sol::SolFileWrapper::Path::get()
{
    return utils::ToSystemString(_pfile->path, false);
}

System::String^ CefFlashBrowser::Sol::SolFileWrapper::SolName::get()
{
    return utils::ToSystemString(_pfile->solname);
}

System::UInt32 CefFlashBrowser::Sol::SolFileWrapper::Version::get()
{
    return _pfile->version;
}

System::Collections::Generic::Dictionary<System::String^, CefFlashBrowser::Sol::SolValueWrapper^>^
CefFlashBrowser::Sol::SolFileWrapper::Data::get()
{
    return _data;
}
