#include "cli.h"
#include "utils.h"

using namespace sol;

CefFlashBrowser::Sol::SolValueWrapper::SolValueWrapper(sol::SolValue* _pval)
    : _pval(_pval)
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
    case SolType::BooleanTrue:
    case SolType::BooleanFalse:
        return Boolean::typeid;

    case SolType::Integer:
        return Int32::typeid;

    case SolType::Double:
        return Double::typeid;

    case SolType::String:
    case SolType::Xml:
        return String::typeid;

    case SolType::Array:
        return array<SolValueWrapper^>::typeid;

    case SolType::Object:
        return Dictionary<String^, SolValueWrapper^>::typeid;

    case SolType::Binary:
        return array<Byte>::typeid;

    case SolType::Null:
    default:
        return Object::typeid;
    }
}

System::Object^ CefFlashBrowser::Sol::SolValueWrapper::GetValue()
{
    switch (_pval->type)
    {
    case SolType::BooleanTrue:
    case SolType::BooleanFalse:
        return gcnew Boolean(_pval->get<SolBoolean>());

    case SolType::Integer:
        return gcnew Int32(_pval->get<SolInteger>());

    case SolType::Double:
        return gcnew Double(_pval->get<SolDouble>());

    case SolType::String:
    case SolType::Xml:
        return utils::ToSystemString(_pval->get<SolString>());

    case SolType::Array: {
        auto arr = _pval->get<SolArray>();
        auto res = gcnew array<SolValueWrapper^>(arr.size());
        for (size_t i = 0; i < arr.size(); i++) {
            res[i] = gcnew SolValueWrapper(new SolValue(arr[i]));
        }
        return res;
    }

    case SolType::Object: {
        auto obj = _pval->get<SolObject>();
        auto res = gcnew Dictionary<String^, SolValueWrapper^>(obj.size());
        for (auto& [key, val] : obj) {
            res->Add(utils::ToSystemString(key), gcnew SolValueWrapper(new SolValue(val)));
        }
        return res;
    }

    case SolType::Null:
    default:
        return nullptr;
    }
}

CefFlashBrowser::Sol::SolFileWrapper::SolFileWrapper(sol::SolFile* _pfile)
    : _pfile(_pfile)
{
}

CefFlashBrowser::Sol::SolFileWrapper::~SolFileWrapper()
{
    delete _pfile;
}

System::String^ CefFlashBrowser::Sol::SolFileWrapper::Path::get()
{
    return utils::ToSystemString(_pfile->path);
}

System::String^ CefFlashBrowser::Sol::SolFileWrapper::SolName::get()
{
    return utils::ToSystemString(_pfile->solname);
}

System::UInt32 CefFlashBrowser::Sol::SolFileWrapper::Version::get()
{
    return _pfile->version;
}

CefFlashBrowser::Sol::SolValueWrapper^ CefFlashBrowser::Sol::SolFileWrapper::Data::get()
{
    return gcnew SolValueWrapper(new SolValue(_pfile->data));
}

CefFlashBrowser::Sol::SolFileWrapper^ CefFlashBrowser::Sol::SolFileWrapper::Read(String^ path)
{
    auto result = gcnew SolFileWrapper(new SolFile());
    result->_pfile->path = utils::ToStdString(path, false);

    if (!sol::ReadSolFile(*result->_pfile)) {
        throw gcnew Exception(utils::ToSystemString(result->_pfile->errmsg));
    }
    return result;
}
