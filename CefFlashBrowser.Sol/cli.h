#ifndef __CLI_H__
#define __CLI_H__

#include "sol.h"

namespace CefFlashBrowser::Sol
{
    using namespace System;
    using namespace System::Collections::Generic;


    public ref class SolXmlDoc
    {
    private:
        String^ _xml;

    public:
        SolXmlDoc(String^ xml) : _xml(xml) {}
        ~SolXmlDoc() {}

    public:
        property String^ Data { String^ get() { return _xml; } }
        virtual String^ ToString() override { return _xml; }
    };


    public ref class SolXml
    {
    private:
        String^ _xml;

    public:
        SolXml(String^ xml) : _xml(xml) {}
        ~SolXml() {}

    public:
        property String^ Data { String^ get() { return _xml; } }
        virtual String^ ToString() override { return _xml; }
    };


    public ref class SolValueWrapper
    {
    private:
        sol::SolValue* _pval;

    internal:
        SolValueWrapper(sol::SolValue* pval);

    public:
        SolValueWrapper();
        ~SolValueWrapper();

    public:
        property Type^ Type { System::Type^ get(); }
        property bool IsNull { bool get(); }

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
