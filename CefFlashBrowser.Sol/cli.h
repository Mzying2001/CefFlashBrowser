#ifndef __CLI_H__
#define __CLI_H__

#include "sol.h"

namespace CefFlashBrowser::Sol
{
    using namespace System;
    using namespace System::Collections::Generic;
    using namespace System::Xml;


    public ref class SolValueWrapper
    {
    private:
        sol::SolValue* _pval;

    internal:
        SolValueWrapper(sol::SolValue* _pval);

    public:
        SolValueWrapper();
        ~SolValueWrapper();

    public:
        property Type^ Type { System::Type^ get(); }
        Object^ GetValue();
        void SetValue(Object^ value);
    };


    public ref class SolFileWrapper
    {
    private:
        sol::SolFile* _pfile;
        Dictionary<String^, SolValueWrapper^>^ _data;

    public:
        SolFileWrapper(String^ path);
        ~SolFileWrapper();

    public:
        property String^ Path { String^ get(); }
        property String^ SolName { String^ get(); }
        property UInt32 Version { UInt32 get(); }
        property Dictionary<String^, SolValueWrapper^>^ Data { Dictionary<String^, SolValueWrapper^>^ get(); }
    };
}

#endif // !__CLI_H__
