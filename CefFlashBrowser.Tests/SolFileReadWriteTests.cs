using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class SolFileReadWriteTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SolTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        private string GetTestDataPath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", fileName);
        }

        private string GetTempSolPath(string name = "test.sol")
        {
            return Path.Combine(_testDir, name);
        }

        #region Read AMF0 fixture

        [TestMethod]
        public void ReadAmf0_Settings_VerifySolName()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("settings.sol"));
            Assert.AreEqual("settings", file.SolName);
        }

        [TestMethod]
        public void ReadAmf0_Settings_VerifyVersion()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("settings.sol"));
            Assert.AreEqual(SolVersion.AMF0, file.Version);
        }

        [TestMethod]
        public void ReadAmf0_Settings_HasData()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("settings.sol"));
            Assert.IsTrue(file.Data.Count > 0);
        }

        #endregion

        #region Read AMF3 fixtures

        [TestMethod]
        public void ReadAmf3_Mao_VerifySolName()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("mao.sol"));
            Assert.AreEqual("mao", file.SolName);
        }

        [TestMethod]
        public void ReadAmf3_Mao_VerifyVersion()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("mao.sol"));
            Assert.AreEqual(SolVersion.AMF3, file.Version);
        }

        [TestMethod]
        public void ReadAmf3_Mao_LevelIsInt()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("mao.sol"));
            Assert.IsTrue(file.Data.ContainsKey("level"));
            var value = file.Data["level"].GetValue();
            Assert.IsInstanceOfType(value, typeof(int));
        }

        [TestMethod]
        public void ReadAmf3_Mao_LevelValue()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("mao.sol"));
            Assert.AreEqual(6, (int)file.Data["level"].GetValue());
        }

        [TestMethod]
        public void ReadAmf3_Pvz_VerifySolName()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("pvz.sol"));
            Assert.AreEqual("pvz", file.SolName);
        }

        [TestMethod]
        public void ReadAmf3_Pvz_HasSaveData()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("pvz.sol"));
            Assert.IsTrue(file.Data.ContainsKey("saveData"));
        }

        [TestMethod]
        public void ReadAmf3_Pvz_SaveDataIsObject()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("pvz.sol"));
            var saveData = file.Data["saveData"].GetValue();
            Assert.IsInstanceOfType(saveData, typeof(SolObjectWrapper));
        }

        [TestMethod]
        public void ReadAmf3_Pvz_SaveDataHasExpectedProperties()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("pvz.sol"));
            var saveData = (SolObjectWrapper)file.Data["saveData"].GetValue();
            var props = saveData.Props;

            Assert.IsTrue(props.ContainsKey("musicOn"));
            Assert.IsTrue(props.ContainsKey("level"));
            Assert.IsTrue(props.ContainsKey("soundOn"));
        }

        [TestMethod]
        public void ReadAmf3_FBCookie_VerifySolName()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("FBCookie.sol"));
            Assert.AreEqual("FBCookie", file.SolName);
        }

        [TestMethod]
        public void ReadAmf3_FBCookie_HasStats()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("FBCookie.sol"));
            Assert.IsTrue(file.Data.ContainsKey("stats"));
        }

        [TestMethod]
        public void ReadAmf3_FBCookie_StatsIsArray()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("FBCookie.sol"));
            var stats = file.Data["stats"].GetValue();
            Assert.IsInstanceOfType(stats, typeof(SolArrayWrapper));
        }

        #endregion

        #region Write & round-trip tests

        [TestMethod]
        public void RoundTrip_IntValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["score"] = new SolValueWrapper().SetValue(42);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual("test", read.SolName);
            Assert.AreEqual(SolVersion.AMF3, read.Version);
            Assert.AreEqual(42, (int)read.Data["score"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_DoubleValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["pi"] = new SolValueWrapper().SetValue(3.14159);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual(3.14159, (double)read.Data["pi"].GetValue(), 0.00001);
        }

        [TestMethod]
        public void RoundTrip_BoolValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["enabled"] = new SolValueWrapper().SetValue(true);
            file.Data["disabled"] = new SolValueWrapper().SetValue(false);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual(true, (bool)read.Data["enabled"].GetValue());
            Assert.AreEqual(false, (bool)read.Data["disabled"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_StringValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["name"] = new SolValueWrapper().SetValue("hello world");
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual("hello world", (string)read.Data["name"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_DateTimeValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            // Use local time since SOL stores as UTC timestamp and reads back as local
            var dt = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Local);
            file.Data["date"] = new SolValueWrapper().SetValue(dt);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var readDt = (DateTime)read.Data["date"].GetValue();
            Assert.AreEqual(dt.Year, readDt.Year);
            Assert.AreEqual(dt.Month, readDt.Month);
            Assert.AreEqual(dt.Day, readDt.Day);
            Assert.AreEqual(dt.Hour, readDt.Hour);
            Assert.AreEqual(dt.Minute, readDt.Minute);
        }

        [TestMethod]
        public void RoundTrip_BinaryValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            var data = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0x00 };
            file.Data["bin"] = new SolValueWrapper().SetValue(data);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var readData = (byte[])read.Data["bin"].GetValue();
            CollectionAssert.AreEqual(data, readData);
        }

        [TestMethod]
        public void RoundTrip_UndefinedValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["undef"] = new SolValueWrapper().SetValue(SolUndefined.Value);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.IsTrue(read.Data["undef"].IsUndefined);
        }

        [TestMethod]
        public void RoundTrip_NullValue()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            file.Data["nothing"] = new SolValueWrapper().SetValue(null);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.IsTrue(read.Data["nothing"].IsNull);
        }

        [TestMethod]
        public void RoundTrip_MultipleValues()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "game";
            file.Version = SolVersion.AMF3;
            file.Data["level"] = new SolValueWrapper().SetValue(10);
            file.Data["score"] = new SolValueWrapper().SetValue(9999.5);
            file.Data["name"] = new SolValueWrapper().SetValue("player1");
            file.Data["premium"] = new SolValueWrapper().SetValue(true);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual(4, read.Data.Count);
            Assert.AreEqual(10, (int)read.Data["level"].GetValue());
            Assert.AreEqual(9999.5, (double)read.Data["score"].GetValue());
            Assert.AreEqual("player1", (string)read.Data["name"].GetValue());
            Assert.AreEqual(true, (bool)read.Data["premium"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_AMF0_IntAndString()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "amf0test";
            file.Version = SolVersion.AMF0;
            file.Data["count"] = new SolValueWrapper().SetValue(100);
            file.Data["msg"] = new SolValueWrapper().SetValue("amf0");
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            Assert.AreEqual(SolVersion.AMF0, read.Version);
            Assert.AreEqual("amf0test", read.SolName);
        }

        [TestMethod]
        public void RoundTrip_SolArray_DenseAndAssoc()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "arrtest";
            file.Version = SolVersion.AMF3;

            var arr = new SolArrayWrapper();
            arr.Dense.Add(new SolValueWrapper().SetValue(1));
            arr.Dense.Add(new SolValueWrapper().SetValue(2));
            arr.Dense.Add(new SolValueWrapper().SetValue(3));
            arr.Assoc["key1"] = new SolValueWrapper().SetValue("value1");
            arr.Assoc["key2"] = new SolValueWrapper().SetValue(true);

            file.Data["myArr"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var readArr = (SolArrayWrapper)read.Data["myArr"].GetValue();
            Assert.AreEqual(3, readArr.Dense.Count);
            Assert.AreEqual(1, (int)readArr.Dense[0].GetValue());
            Assert.AreEqual(2, (int)readArr.Dense[1].GetValue());
            Assert.AreEqual(3, (int)readArr.Dense[2].GetValue());
            Assert.AreEqual("value1", (string)readArr.Assoc["key1"].GetValue());
            Assert.AreEqual(true, (bool)readArr.Assoc["key2"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_SolObject_WithProperties()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "objtest";
            file.Version = SolVersion.AMF3;

            var obj = new SolObjectWrapper();
            obj.Class = "Player";
            obj.Props["name"] = new SolValueWrapper().SetValue("hero");
            obj.Props["hp"] = new SolValueWrapper().SetValue(100);
            obj.Props["alive"] = new SolValueWrapper().SetValue(true);

            file.Data["player"] = new SolValueWrapper().SetValue(obj);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var readObj = (SolObjectWrapper)read.Data["player"].GetValue();
            Assert.AreEqual("hero", (string)readObj.Props["name"].GetValue());
            Assert.AreEqual(100, (int)readObj.Props["hp"].GetValue());
            Assert.AreEqual(true, (bool)readObj.Props["alive"].GetValue());
        }

        [TestMethod]
        public void RoundTrip_SolXml()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "xmltest";
            file.Version = SolVersion.AMF3;
            file.Data["xml"] = new SolValueWrapper().SetValue(new SolXml("<root>hello</root>"));
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var xml = (SolXml)read.Data["xml"].GetValue();
            Assert.AreEqual("<root>hello</root>", xml.Data);
        }

        [TestMethod]
        public void RoundTrip_SolXmlDoc()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "xmldoctest";
            file.Version = SolVersion.AMF3;
            file.Data["doc"] = new SolValueWrapper().SetValue(new SolXmlDoc("<doc/>"));
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var doc = (SolXmlDoc)read.Data["doc"].GetValue();
            Assert.AreEqual("<doc/>", doc.Data);
        }

        [TestMethod]
        public void ModifyExistingFile_ChangeValue()
        {
            // Copy fixture to temp
            var srcPath = GetTestDataPath("mao.sol");
            var tempPath = GetTempSolPath("mao_modified.sol");
            File.Copy(srcPath, tempPath);

            // Read, modify, save
            var file = SolFileWrapper.ReadFile(tempPath);
            Assert.AreEqual(6, (int)file.Data["level"].GetValue());
            file.Data["level"] = new SolValueWrapper().SetValue(99);
            file.Save();

            // Re-read and verify
            var read = SolFileWrapper.ReadFile(tempPath);
            Assert.AreEqual(99, (int)read.Data["level"].GetValue());
        }

        [TestMethod]
        public void ModifyExistingFile_AddNewKey()
        {
            var srcPath = GetTestDataPath("mao.sol");
            var tempPath = GetTempSolPath("mao_added.sol");
            File.Copy(srcPath, tempPath);

            var file = SolFileWrapper.ReadFile(tempPath);
            file.Data["newKey"] = new SolValueWrapper().SetValue("newValue");
            file.Save();

            var read = SolFileWrapper.ReadFile(tempPath);
            Assert.IsTrue(read.Data.ContainsKey("level"));
            Assert.IsTrue(read.Data.ContainsKey("newKey"));
            Assert.AreEqual("newValue", (string)read.Data["newKey"].GetValue());
        }

        #endregion

        #region SolValueWrapper type tests

        [TestMethod]
        public void SolValueWrapper_Int_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(42);
            Assert.AreEqual(typeof(int), val.Type);
            Assert.IsFalse(val.IsNull);
            Assert.IsFalse(val.IsUndefined);
        }

        [TestMethod]
        public void SolValueWrapper_Double_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(3.14);
            Assert.AreEqual(typeof(double), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_Bool_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(true);
            Assert.AreEqual(typeof(bool), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_String_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue("hello");
            Assert.AreEqual(typeof(string), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_DateTime_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(DateTime.Now);
            Assert.AreEqual(typeof(DateTime), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_ByteArray_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(new byte[] { 1, 2, 3 });
            Assert.AreEqual(typeof(byte[]), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_Undefined_Properties()
        {
            var val = new SolValueWrapper().SetValue(SolUndefined.Value);
            Assert.IsTrue(val.IsUndefined);
            Assert.IsFalse(val.IsNull);
        }

        [TestMethod]
        public void SolValueWrapper_Null_Properties()
        {
            var val = new SolValueWrapper().SetValue(null);
            Assert.IsTrue(val.IsNull);
            Assert.IsFalse(val.IsUndefined);
        }

        [TestMethod]
        public void SolValueWrapper_SolXml_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(new SolXml("<x/>"));
            Assert.AreEqual(typeof(SolXml), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_SolXmlDoc_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(new SolXmlDoc("<d/>"));
            Assert.AreEqual(typeof(SolXmlDoc), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_Array_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(new SolArrayWrapper());
            Assert.AreEqual(typeof(SolArrayWrapper), val.Type);
        }

        [TestMethod]
        public void SolValueWrapper_Object_TypeIsCorrect()
        {
            var val = new SolValueWrapper().SetValue(new SolObjectWrapper());
            Assert.AreEqual(typeof(SolObjectWrapper), val.Type);
        }

        #endregion

        #region SolHelper bridge tests

        [TestMethod]
        public void SolHelper_GetAllValues_FromFixtureFile()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("mao.sol"));
            var values = SolHelper.GetAllValues(file);

            Assert.IsTrue(values.ContainsKey("level"));
            Assert.AreEqual(6, values["level"]);
        }

        [TestMethod]
        public void SolHelper_GetAllValues_ObjectBecomsSolObject()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("pvz.sol"));
            var values = SolHelper.GetAllValues(file);

            Assert.IsInstanceOfType(values["saveData"], typeof(SolObject));
            var obj = (SolObject)values["saveData"];
            Assert.IsTrue(obj.Properties.ContainsKey("level"));
        }

        [TestMethod]
        public void SolHelper_GetAllValues_ArrayBecomsSolArray()
        {
            var file = SolFileWrapper.ReadFile(GetTestDataPath("FBCookie.sol"));
            var values = SolHelper.GetAllValues(file);

            Assert.IsInstanceOfType(values["stats"], typeof(SolArray));
        }

        [TestMethod]
        public void SolHelper_SetAllValues_RoundTrip()
        {
            var path = GetTempSolPath("helper_roundtrip.sol");
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "helpertest";
            file.Version = SolVersion.AMF3;

            var values = new Dictionary<string, object>
            {
                ["score"] = 100,
                ["name"] = "test",
                ["active"] = true,
                ["ratio"] = 0.75
            };

            SolHelper.SetAllValues(file, values);
            file.Save();

            var read = SolFileWrapper.ReadFile(path);
            var readValues = SolHelper.GetAllValues(read);

            Assert.AreEqual(100, readValues["score"]);
            Assert.AreEqual("test", readValues["name"]);
            Assert.AreEqual(true, readValues["active"]);
            Assert.AreEqual(0.75, readValues["ratio"]);
        }

        [TestMethod]
        public void SolHelper_GetValueWrapper_Int()
        {
            var wrapper = SolHelper.GetValueWrapper(42);
            Assert.AreEqual(42, (int)wrapper.GetValue());
        }

        [TestMethod]
        public void SolHelper_GetValueWrapper_SolObject()
        {
            var obj = new SolObject { ClassName = "Test" };
            obj.Properties["key"] = "value";

            var wrapper = SolHelper.GetValueWrapper(obj);
            var result = (SolObjectWrapper)wrapper.GetValue();
            Assert.AreEqual("value", (string)result.Props["key"].GetValue());
        }

        [TestMethod]
        public void SolHelper_GetValueWrapper_SolArray()
        {
            var arr = new SolArray();
            arr.DensePortion.Add(1);
            arr.DensePortion.Add(2);

            var wrapper = SolHelper.GetValueWrapper(arr);
            var result = (SolArrayWrapper)wrapper.GetValue();
            Assert.AreEqual(2, result.Dense.Count);
        }

        [TestMethod]
        public void SolHelper_GetTypeString_AllKnownTypes()
        {
            Assert.AreEqual("int", SolHelper.GetTypeString(typeof(int)));
            Assert.AreEqual("double", SolHelper.GetTypeString(typeof(double)));
            Assert.AreEqual("bool", SolHelper.GetTypeString(typeof(bool)));
            Assert.AreEqual("string", SolHelper.GetTypeString(typeof(string)));
            Assert.AreEqual("DateTime", SolHelper.GetTypeString(typeof(DateTime)));
            Assert.AreEqual("Array", SolHelper.GetTypeString(typeof(SolArray)));
            Assert.AreEqual("Object", SolHelper.GetTypeString(typeof(SolObject)));
            Assert.AreEqual("Binary", SolHelper.GetTypeString(typeof(byte[])));
            Assert.AreEqual("Xml", SolHelper.GetTypeString(typeof(SolXml)));
            Assert.AreEqual("XmlDocument", SolHelper.GetTypeString(typeof(SolXmlDoc)));
            Assert.AreEqual("undefined", SolHelper.GetTypeString(typeof(SolUndefined)));
            Assert.AreEqual("null", SolHelper.GetTypeString((Type)null));
        }

        [TestMethod]
        public void SolHelper_GetDefaultValueOfType_SolSpecificTypes()
        {
            Assert.AreEqual(SolUndefined.Value, SolHelper.GetDefaultValueOfType(typeof(SolUndefined)));
            Assert.IsInstanceOfType(SolHelper.GetDefaultValueOfType(typeof(SolXml)), typeof(SolXml));
            Assert.IsInstanceOfType(SolHelper.GetDefaultValueOfType(typeof(SolXmlDoc)), typeof(SolXmlDoc));
            Assert.IsInstanceOfType(SolHelper.GetDefaultValueOfType(typeof(SolArray)), typeof(SolArray));
            Assert.IsInstanceOfType(SolHelper.GetDefaultValueOfType(typeof(SolObject)), typeof(SolObject));
        }

        [TestMethod]
        public void SolHelper_GetSupportedTypes_Returns12Types()
        {
            var types = SolHelper.GetSupportedTypes().ToArray();
            Assert.AreEqual(12, types.Length);
        }

        #endregion

        #region SolObject / SolArray model tests

        [TestMethod]
        public void SolObject_FromWrapper_RoundTrip()
        {
            var wrapper = new SolObjectWrapper();
            wrapper.Class = "Enemy";
            wrapper.Props["hp"] = new SolValueWrapper().SetValue(50);
            wrapper.Props["name"] = new SolValueWrapper().SetValue("goblin");

            var model = new SolObject(wrapper);
            Assert.AreEqual("Enemy", model.ClassName);
            Assert.AreEqual(50, model.Properties["hp"]);
            Assert.AreEqual("goblin", model.Properties["name"]);

            var backToWrapper = model.ToObjectWrapper();
            Assert.AreEqual("Enemy", backToWrapper.Class);
            Assert.AreEqual(50, (int)backToWrapper.Props["hp"].GetValue());
            Assert.AreEqual("goblin", (string)backToWrapper.Props["name"].GetValue());
        }

        [TestMethod]
        public void SolObject_DefaultConstructor_EmptyState()
        {
            var obj = new SolObject();
            Assert.AreEqual(string.Empty, obj.ClassName);
            Assert.IsNotNull(obj.Properties);
            Assert.AreEqual(0, obj.Properties.Count);
        }

        [TestMethod]
        public void SolArray_FromWrapper_RoundTrip()
        {
            var wrapper = new SolArrayWrapper();
            wrapper.Dense.Add(new SolValueWrapper().SetValue(10));
            wrapper.Dense.Add(new SolValueWrapper().SetValue(20));
            wrapper.Assoc["key"] = new SolValueWrapper().SetValue("val");

            var model = new SolArray(wrapper);
            Assert.AreEqual(2, model.DensePortion.Count);
            Assert.AreEqual(10, model.DensePortion[0]);
            Assert.AreEqual(20, model.DensePortion[1]);
            Assert.AreEqual("val", model.AssocPortion["key"]);

            var backToWrapper = model.ToArrayWrapper();
            Assert.AreEqual(2, backToWrapper.Dense.Count);
            Assert.AreEqual(10, (int)backToWrapper.Dense[0].GetValue());
            Assert.AreEqual("val", (string)backToWrapper.Assoc["key"].GetValue());
        }

        [TestMethod]
        public void SolArray_DefaultConstructor_EmptyState()
        {
            var arr = new SolArray();
            Assert.IsNotNull(arr.DensePortion);
            Assert.IsNotNull(arr.AssocPortion);
            Assert.AreEqual(0, arr.DensePortion.Count);
            Assert.AreEqual(0, arr.AssocPortion.Count);
        }

        [TestMethod]
        public void SolTypeDesc_CreatesCorrectTypeName()
        {
            var desc = new SolTypeDesc(typeof(int));
            Assert.AreEqual(typeof(int), desc.Type);
            Assert.AreEqual("int", desc.TypeName);
        }

        [TestMethod]
        public void SolTypeDesc_CreateInstance_ReturnsDefaultValue()
        {
            var desc = new SolTypeDesc(typeof(string));
            Assert.AreEqual(string.Empty, desc.CreateInstance());
        }

        [TestMethod]
        public void SolTypeDesc_NullType()
        {
            var desc = new SolTypeDesc(null);
            Assert.IsNull(desc.Type);
            Assert.AreEqual("null", desc.TypeName);
            Assert.IsNull(desc.CreateInstance());
        }

        #endregion

        #region CreateEmpty and constructor tests

        [TestMethod]
        public void CreateEmpty_ReturnsEmptyFile()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            Assert.IsNotNull(file);
            Assert.AreEqual(0, file.Data.Count);
        }

        [TestMethod]
        public void SolFileWrapper_Constructor_ReadsFile()
        {
            var file = new SolFileWrapper(GetTestDataPath("mao.sol"));
            Assert.AreEqual("mao", file.SolName);
            Assert.IsTrue(file.Data.Count > 0);
        }

        [TestMethod]
        public void SolFileWrapper_PathProperty()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            Assert.AreEqual(path, file.Path);
        }

        [TestMethod]
        public void SolFileWrapper_SetSolName()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "customName";
            Assert.AreEqual("customName", file.SolName);
        }

        [TestMethod]
        public void SolFileWrapper_SetVersion()
        {
            var path = GetTempSolPath();
            var file = SolFileWrapper.CreateEmpty(path);
            file.Version = SolVersion.AMF0;
            Assert.AreEqual(SolVersion.AMF0, file.Version);

            file.Version = SolVersion.AMF3;
            Assert.AreEqual(SolVersion.AMF3, file.Version);
        }

        #endregion
    }
}
