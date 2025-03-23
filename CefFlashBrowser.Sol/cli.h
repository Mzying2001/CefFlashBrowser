#ifndef __CLI_H__
#define __CLI_H__

#include "sol.h"

namespace CefFlashBrowser::Sol
{
    using namespace System;
    using namespace System::Collections::Generic;


    public ref class SolValueWrapper
    {
    private:
        sol::SolValue* _pval;

    internal:
        SolValueWrapper(sol::SolValue* _pval);

    public:
        ~SolValueWrapper();

    public:
        property Type^ Type { System::Type^ get(); }
        Object^ GetValue();
        void SetValue(Object^ value);
        void SetXmlValue(String^ value);
    };


    public ref class SolFileWrapper
    {
    private:
        sol::SolFile* _pfile;
        Dictionary<String^, SolValueWrapper^>^ _data;

    internal:
        SolFileWrapper(sol::SolFile* _pfile);

    public:
        ~SolFileWrapper();

    public:
        property String^ Path { String^ get(); }
        property String^ SolName { String^ get(); }
        property UInt32 Version { UInt32 get(); }
        property Dictionary<String^, SolValueWrapper^>^ Data { Dictionary<String^, SolValueWrapper^>^ get(); }
        static SolFileWrapper^ Read(String^ path);
    };
}

#endif // !__CLI_H__
