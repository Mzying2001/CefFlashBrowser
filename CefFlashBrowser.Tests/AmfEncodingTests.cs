using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class AmfEncodingTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "AmfTests_" + Guid.NewGuid().ToString("N"));
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

        private string GetTempSolPath(string name = "test.sol")
        {
            return Path.Combine(_testDir, name);
        }

        private string GetTestDataPath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", fileName);
        }

        private SolFileWrapper CreateAmf3File(string name = "test.sol")
        {
            var path = GetTempSolPath(name);
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF3;
            return file;
        }

        private SolFileWrapper CreateAmf0File(string name = "test.sol")
        {
            var path = GetTempSolPath(name);
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "test";
            file.Version = SolVersion.AMF0;
            return file;
        }

        private int ReadIntValue(string path, string key)
        {
            var file = SolFileWrapper.ReadFile(path);
            return (int)file.Data[key].GetValue();
        }

        private double ReadDoubleValue(string path, string key)
        {
            var file = SolFileWrapper.ReadFile(path);
            return (double)file.Data[key].GetValue();
        }

        #region AMF3 Variable-Length Integer Encoding — Boundary Values

        [TestMethod]
        public void Amf3Int_Zero_RoundTrip()
        {
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0);
            file.Save();
            Assert.AreEqual(0, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_One_RoundTrip()
        {
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(1);
            file.Save();
            Assert.AreEqual(1, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Max1Byte_RoundTrip()
        {
            // 0x7F = 127, max value for 1-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x7F);
            file.Save();
            Assert.AreEqual(0x7F, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Min2Byte_RoundTrip()
        {
            // 0x80 = 128, min value for 2-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x80);
            file.Save();
            Assert.AreEqual(0x80, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Max2Byte_RoundTrip()
        {
            // 0x3FFF = 16383, max value for 2-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x3FFF);
            file.Save();
            Assert.AreEqual(0x3FFF, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Min3Byte_RoundTrip()
        {
            // 0x4000 = 16384, min value for 3-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x4000);
            file.Save();
            Assert.AreEqual(0x4000, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Max3Byte_RoundTrip()
        {
            // 0x1FFFFF = 2097151, max value for 3-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x1FFFFF);
            file.Save();
            Assert.AreEqual(0x1FFFFF, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Min4Byte_RoundTrip()
        {
            // 0x200000 = 2097152, min value for 4-byte encoding
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0x200000);
            file.Save();
            Assert.AreEqual(0x200000, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_NegativeOne_RoundTrip()
        {
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(-1);
            file.Save();
            Assert.AreEqual(-1, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_Negative256_RoundTrip()
        {
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(-256);
            file.Save();
            Assert.AreEqual(-256, ReadIntValue(file.Path, "v"));
        }

        [TestMethod]
        public void Amf3Int_LargePositive_RoundTrip()
        {
            var file = CreateAmf3File();
            file.Data["v"] = new SolValueWrapper().SetValue(0xFFFFFFF); // 268435455, AMF3 max 29-bit
            file.Save();
            Assert.AreEqual(0xFFFFFFF, ReadIntValue(file.Path, "v"));
        }

        #endregion

        #region AMF3 String Reference Pooling

        [TestMethod]
        public void Amf3_StringReferencePooling_DuplicatesSmallerFile()
        {
            // File with duplicate strings should be smaller than file with unique strings
            var pathDup = GetTempSolPath("dup.sol");
            var fileDup = SolFileWrapper.CreateEmpty(pathDup);
            fileDup.SolName = "dup";
            fileDup.Version = SolVersion.AMF3;

            var obj = new SolObjectWrapper();
            obj.Props["a"] = new SolValueWrapper().SetValue("this_is_a_repeated_string_value");
            obj.Props["b"] = new SolValueWrapper().SetValue("this_is_a_repeated_string_value");
            obj.Props["c"] = new SolValueWrapper().SetValue("this_is_a_repeated_string_value");
            fileDup.Data["obj"] = new SolValueWrapper().SetValue(obj);
            fileDup.Save();

            var pathUniq = GetTempSolPath("uniq.sol");
            var fileUniq = SolFileWrapper.CreateEmpty(pathUniq);
            fileUniq.SolName = "uniq";
            fileUniq.Version = SolVersion.AMF3;

            var obj2 = new SolObjectWrapper();
            obj2.Props["a"] = new SolValueWrapper().SetValue("unique_string_value_number_one_x");
            obj2.Props["b"] = new SolValueWrapper().SetValue("unique_string_value_number_two_x");
            obj2.Props["c"] = new SolValueWrapper().SetValue("unique_string_value_number_three");
            fileUniq.Data["obj"] = new SolValueWrapper().SetValue(obj2);
            fileUniq.Save();

            var dupSize = new FileInfo(pathDup).Length;
            var uniqSize = new FileInfo(pathUniq).Length;

            Assert.IsTrue(dupSize < uniqSize,
                $"Duplicate strings file ({dupSize}B) should be smaller than unique strings file ({uniqSize}B) due to reference pooling");
        }

        [TestMethod]
        public void Amf3_StringReferencePooling_ValuesPreserved()
        {
            var file = CreateAmf3File("strref.sol");
            var obj = new SolObjectWrapper();
            obj.Props["x"] = new SolValueWrapper().SetValue("shared");
            obj.Props["y"] = new SolValueWrapper().SetValue("shared");
            file.Data["obj"] = new SolValueWrapper().SetValue(obj);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readObj = (SolObjectWrapper)read.Data["obj"].GetValue();
            Assert.AreEqual("shared", (string)readObj.Props["x"].GetValue());
            Assert.AreEqual("shared", (string)readObj.Props["y"].GetValue());
        }

        #endregion

        #region SOL Binary Format Validation

        [TestMethod]
        public void BinaryFormat_MagicBytes()
        {
            var file = CreateAmf3File("magic.sol");
            file.Data["x"] = new SolValueWrapper().SetValue(1);
            file.Save();

            var bytes = File.ReadAllBytes(file.Path);
            Assert.AreEqual(0x00, bytes[0]);
            Assert.AreEqual(0xBF, bytes[1]);
        }

        [TestMethod]
        public void BinaryFormat_TcsoConstant()
        {
            var file = CreateAmf3File("tcso.sol");
            file.Data["x"] = new SolValueWrapper().SetValue(1);
            file.Save();

            var bytes = File.ReadAllBytes(file.Path);
            // "TCSO" at offset 6
            Assert.AreEqual((byte)'T', bytes[6]);
            Assert.AreEqual((byte)'C', bytes[7]);
            Assert.AreEqual((byte)'S', bytes[8]);
            Assert.AreEqual((byte)'O', bytes[9]);
        }

        [TestMethod]
        public void BinaryFormat_ChunkSize_MatchesFileSize()
        {
            var file = CreateAmf3File("chunk.sol");
            file.Data["x"] = new SolValueWrapper().SetValue(42);
            file.Save();

            var bytes = File.ReadAllBytes(file.Path);
            // Chunk size at offset 2-5, big-endian uint32 = file_size - 6
            int chunkSize = (bytes[2] << 24) | (bytes[3] << 16) | (bytes[4] << 8) | bytes[5];
            Assert.AreEqual(bytes.Length - 6, chunkSize);
        }

        [TestMethod]
        public void BinaryFormat_SolName_Encoded()
        {
            var path = GetTempSolPath("name.sol");
            var file = SolFileWrapper.CreateEmpty(path);
            file.SolName = "hello";
            file.Version = SolVersion.AMF3;
            file.Data["x"] = new SolValueWrapper().SetValue(1);
            file.Save();

            var bytes = File.ReadAllBytes(path);
            // Sol name encoded as AMF0 short string at offset 16: 2-byte length + string data
            int nameLen = (bytes[16] << 8) | bytes[17];
            Assert.AreEqual(5, nameLen);
            Assert.AreEqual("hello", Encoding.ASCII.GetString(bytes, 18, nameLen));
        }

        [TestMethod]
        public void BinaryFormat_VersionField_AMF3()
        {
            var file = CreateAmf3File("ver3.sol");
            file.Data["x"] = new SolValueWrapper().SetValue(1);
            file.Save();

            var bytes = File.ReadAllBytes(file.Path);
            // Version at offset after sol name: 16 + 2 + nameLen
            int nameLen = (bytes[16] << 8) | bytes[17];
            int versionOffset = 18 + nameLen;
            int version = (bytes[versionOffset] << 24) | (bytes[versionOffset + 1] << 16)
                        | (bytes[versionOffset + 2] << 8) | bytes[versionOffset + 3];
            Assert.AreEqual(3, version);
        }

        [TestMethod]
        public void BinaryFormat_VersionField_AMF0()
        {
            var file = CreateAmf0File("ver0.sol");
            file.Data["x"] = new SolValueWrapper().SetValue(1);
            file.Save();

            var bytes = File.ReadAllBytes(file.Path);
            int nameLen = (bytes[16] << 8) | bytes[17];
            int versionOffset = 18 + nameLen;
            int version = (bytes[versionOffset] << 24) | (bytes[versionOffset + 1] << 16)
                        | (bytes[versionOffset + 2] << 8) | bytes[versionOffset + 3];
            Assert.AreEqual(0, version);
        }

        #endregion

        #region Special Double Values

        [TestMethod]
        public void Double_NaN_RoundTrip()
        {
            var file = CreateAmf3File("nan.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.NaN);
            file.Save();
            Assert.IsTrue(double.IsNaN(ReadDoubleValue(file.Path, "v")));
        }

        [TestMethod]
        public void Double_PositiveInfinity_RoundTrip()
        {
            var file = CreateAmf3File("inf.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.PositiveInfinity);
            file.Save();
            Assert.IsTrue(double.IsPositiveInfinity(ReadDoubleValue(file.Path, "v")));
        }

        [TestMethod]
        public void Double_NegativeInfinity_RoundTrip()
        {
            var file = CreateAmf3File("ninf.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.NegativeInfinity);
            file.Save();
            Assert.IsTrue(double.IsNegativeInfinity(ReadDoubleValue(file.Path, "v")));
        }

        [TestMethod]
        public void Double_PositiveZero_RoundTrip()
        {
            var file = CreateAmf3File("pzero.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(0.0);
            file.Save();
            var val = ReadDoubleValue(file.Path, "v");
            Assert.AreEqual(0.0, val);
            // Positive zero: 1/val == PositiveInfinity
            Assert.IsTrue(double.IsPositiveInfinity(1.0 / val));
        }

        [TestMethod]
        public void Double_NegativeZero_RoundTrip()
        {
            var file = CreateAmf3File("nzero.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(-0.0);
            file.Save();
            var val = ReadDoubleValue(file.Path, "v");
            Assert.AreEqual(0.0, val);
            // Negative zero: 1/val == NegativeInfinity
            Assert.IsTrue(double.IsNegativeInfinity(1.0 / val));
        }

        [TestMethod]
        public void Double_VerySmall_RoundTrip()
        {
            var file = CreateAmf3File("small.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.Epsilon);
            file.Save();
            Assert.AreEqual(double.Epsilon, ReadDoubleValue(file.Path, "v"));
        }

        [TestMethod]
        public void Double_VeryLarge_RoundTrip()
        {
            var file = CreateAmf3File("large.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.MaxValue);
            file.Save();
            Assert.AreEqual(double.MaxValue, ReadDoubleValue(file.Path, "v"));
        }

        [TestMethod]
        public void Double_MinValue_RoundTrip()
        {
            var file = CreateAmf3File("minval.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(double.MinValue);
            file.Save();
            Assert.AreEqual(double.MinValue, ReadDoubleValue(file.Path, "v"));
        }

        #endregion

        #region AMF0 vs AMF3 Format Differences

        [TestMethod]
        public void Amf0VsAmf3_IntegerEncoding_DifferentSizes()
        {
            // AMF0 stores integers as 8-byte doubles; AMF3 uses variable-length ints
            var pathAmf0 = GetTempSolPath("int0.sol");
            var f0 = SolFileWrapper.CreateEmpty(pathAmf0);
            f0.SolName = "test";
            f0.Version = SolVersion.AMF0;
            f0.Data["v"] = new SolValueWrapper().SetValue(42);
            f0.Save();

            var pathAmf3 = GetTempSolPath("int3.sol");
            var f3 = SolFileWrapper.CreateEmpty(pathAmf3);
            f3.SolName = "test";
            f3.Version = SolVersion.AMF3;
            f3.Data["v"] = new SolValueWrapper().SetValue(42);
            f3.Save();

            var sizeAmf0 = new FileInfo(pathAmf0).Length;
            var sizeAmf3 = new FileInfo(pathAmf3).Length;

            // AMF0 should be larger (8-byte double) vs AMF3 (1-byte int for small values)
            Assert.IsTrue(sizeAmf0 > sizeAmf3,
                $"AMF0 ({sizeAmf0}B) should be larger than AMF3 ({sizeAmf3}B) for small integers");
        }

        [TestMethod]
        public void Amf0_BooleanTrueFalse_RoundTrip()
        {
            var file = CreateAmf0File("bool0.sol");
            file.Data["t"] = new SolValueWrapper().SetValue(true);
            file.Data["f"] = new SolValueWrapper().SetValue(false);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual(true, (bool)read.Data["t"].GetValue());
            Assert.AreEqual(false, (bool)read.Data["f"].GetValue());
        }

        [TestMethod]
        public void Amf0_IntStoredAsNumber_RoundTrip()
        {
            // AMF0 doesn't have a native integer type, uses Number (double)
            var file = CreateAmf0File("num0.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(12345);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            // May come back as int or double depending on implementation
            var val = read.Data["v"].GetValue();
            if (val is int intVal)
                Assert.AreEqual(12345, intVal);
            else if (val is double dVal)
                Assert.AreEqual(12345.0, dVal, 0.001);
            else
                Assert.Fail($"Unexpected type: {val.GetType()}");
        }

        [TestMethod]
        public void Amf0_StringValue_RoundTrip()
        {
            var file = CreateAmf0File("str0.sol");
            file.Data["s"] = new SolValueWrapper().SetValue("hello amf0");
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual("hello amf0", (string)read.Data["s"].GetValue());
        }

        [TestMethod]
        public void Amf0_DoubleValue_RoundTrip()
        {
            var file = CreateAmf0File("dbl0.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(3.14159265358979);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var val = read.Data["v"].GetValue();
            Assert.IsInstanceOfType(val, typeof(double));
            Assert.AreEqual(3.14159265358979, (double)val, 0.0000000000001);
        }

        [TestMethod]
        public void Amf0_SettingsFixture_ReadTypes()
        {
            // settings.sol is an AMF0 file with various Flash Player settings
            var file = SolFileWrapper.ReadFile(GetTestDataPath("settings.sol"));
            Assert.AreEqual(SolVersion.AMF0, file.Version);
            Assert.IsTrue(file.Data.Count > 0);
        }

        #endregion

        #region Edge Case Values

        [TestMethod]
        public void EdgeCase_EmptyString_RoundTrip()
        {
            var file = CreateAmf3File("empty.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(string.Empty);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual(string.Empty, (string)read.Data["v"].GetValue());
        }

        [TestMethod]
        public void EdgeCase_UnicodeString_RoundTrip()
        {
            var file = CreateAmf3File("unicode.sol");
            file.Data["v"] = new SolValueWrapper().SetValue("你好世界🎮");
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual("你好世界🎮", (string)read.Data["v"].GetValue());
        }

        [TestMethod]
        public void EdgeCase_LongString_RoundTrip()
        {
            // String longer than 0xFFFF chars (AMF0 long string threshold)
            var longStr = new string('A', 70000);
            var file = CreateAmf0File("longstr.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(longStr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual(longStr, (string)read.Data["v"].GetValue());
        }

        [TestMethod]
        public void EdgeCase_EmptyArray_RoundTrip()
        {
            var file = CreateAmf3File("emptyarr.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(new SolArrayWrapper());
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var arr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(0, arr.Dense.Count);
            Assert.AreEqual(0, arr.Assoc.Count);
        }

        [TestMethod]
        public void EdgeCase_EmptyObject_RoundTrip()
        {
            var file = CreateAmf3File("emptyobj.sol");
            file.Data["v"] = new SolValueWrapper().SetValue(new SolObjectWrapper());
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var obj = (SolObjectWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(0, obj.Props.Count);
        }

        [TestMethod]
        public void EdgeCase_DeepNesting_RoundTrip()
        {
            // Object → Array → Object → value
            var file = CreateAmf3File("nested.sol");

            var innerObj = new SolObjectWrapper();
            innerObj.Props["val"] = new SolValueWrapper().SetValue(42);

            var arr = new SolArrayWrapper();
            arr.Dense.Add(new SolValueWrapper().SetValue(innerObj));

            var outerObj = new SolObjectWrapper();
            outerObj.Props["arr"] = new SolValueWrapper().SetValue(arr);

            file.Data["root"] = new SolValueWrapper().SetValue(outerObj);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readOuter = (SolObjectWrapper)read.Data["root"].GetValue();
            var readArr = (SolArrayWrapper)readOuter.Props["arr"].GetValue();
            var readInner = (SolObjectWrapper)readArr.Dense[0].GetValue();
            Assert.AreEqual(42, (int)readInner.Props["val"].GetValue());
        }

        [TestMethod]
        public void EdgeCase_BinaryAllZeros_RoundTrip()
        {
            var file = CreateAmf3File("bin0.sol");
            var data = new byte[256];
            file.Data["v"] = new SolValueWrapper().SetValue(data);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readData = (byte[])read.Data["v"].GetValue();
            CollectionAssert.AreEqual(data, readData);
        }

        [TestMethod]
        public void EdgeCase_BinaryAllOnes_RoundTrip()
        {
            var file = CreateAmf3File("binFF.sol");
            var data = new byte[256];
            for (int i = 0; i < data.Length; i++) data[i] = 0xFF;
            file.Data["v"] = new SolValueWrapper().SetValue(data);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readData = (byte[])read.Data["v"].GetValue();
            CollectionAssert.AreEqual(data, readData);
        }

        [TestMethod]
        public void EdgeCase_ArrayOnlyDense_RoundTrip()
        {
            var file = CreateAmf3File("dense.sol");
            var arr = new SolArrayWrapper();
            for (int i = 0; i < 10; i++)
                arr.Dense.Add(new SolValueWrapper().SetValue(i * 10));
            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(10, readArr.Dense.Count);
            Assert.AreEqual(0, readArr.Assoc.Count);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(i * 10, (int)readArr.Dense[i].GetValue());
        }

        [TestMethod]
        public void EdgeCase_ArrayOnlyAssoc_RoundTrip()
        {
            var file = CreateAmf3File("assoc.sol");
            var arr = new SolArrayWrapper();
            arr.Assoc["alpha"] = new SolValueWrapper().SetValue(1);
            arr.Assoc["beta"] = new SolValueWrapper().SetValue(2);
            arr.Assoc["gamma"] = new SolValueWrapper().SetValue(3);
            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(0, readArr.Dense.Count);
            Assert.AreEqual(3, readArr.Assoc.Count);
            Assert.AreEqual(1, (int)readArr.Assoc["alpha"].GetValue());
            Assert.AreEqual(2, (int)readArr.Assoc["beta"].GetValue());
            Assert.AreEqual(3, (int)readArr.Assoc["gamma"].GetValue());
        }

        [TestMethod]
        public void EdgeCase_MixedTypesInArray_RoundTrip()
        {
            var file = CreateAmf3File("mixed.sol");
            var arr = new SolArrayWrapper();
            arr.Dense.Add(new SolValueWrapper().SetValue(1));
            arr.Dense.Add(new SolValueWrapper().SetValue("two"));
            arr.Dense.Add(new SolValueWrapper().SetValue(true));
            arr.Dense.Add(new SolValueWrapper().SetValue(4.0));
            arr.Dense.Add(new SolValueWrapper().SetValue(null));
            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(5, readArr.Dense.Count);
            Assert.AreEqual(1, (int)readArr.Dense[0].GetValue());
            Assert.AreEqual("two", (string)readArr.Dense[1].GetValue());
            Assert.AreEqual(true, (bool)readArr.Dense[2].GetValue());
            Assert.AreEqual(4.0, (double)readArr.Dense[3].GetValue());
            Assert.IsTrue(readArr.Dense[4].IsNull);
        }

        [TestMethod]
        public void EdgeCase_MultipleKeyValuePairs_RoundTrip()
        {
            // Many top-level keys to exercise encoding thoroughly
            var file = CreateAmf3File("many.sol");
            for (int i = 0; i < 50; i++)
                file.Data[$"key_{i:D3}"] = new SolValueWrapper().SetValue(i);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            Assert.AreEqual(50, read.Data.Count);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(i, (int)read.Data[$"key_{i:D3}"].GetValue());
        }

        #endregion

        #region Date Encoding

        [TestMethod]
        public void Date_UnixEpoch_RoundTrip()
        {
            var file = CreateAmf3File("epoch.sol");
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
            file.Data["v"] = new SolValueWrapper().SetValue(epoch);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readDt = (DateTime)read.Data["v"].GetValue();
            Assert.AreEqual(epoch.Year, readDt.Year);
            Assert.AreEqual(epoch.Month, readDt.Month);
            Assert.AreEqual(epoch.Day, readDt.Day);
        }

        [TestMethod]
        public void Date_RecentDate_RoundTrip()
        {
            var file = CreateAmf3File("recent.sol");
            var dt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Local);
            file.Data["v"] = new SolValueWrapper().SetValue(dt);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readDt = (DateTime)read.Data["v"].GetValue();
            Assert.AreEqual(dt.Year, readDt.Year);
            Assert.AreEqual(dt.Month, readDt.Month);
            Assert.AreEqual(dt.Day, readDt.Day);
            Assert.AreEqual(dt.Hour, readDt.Hour);
            Assert.AreEqual(dt.Minute, readDt.Minute);
            Assert.AreEqual(dt.Second, readDt.Second);
        }

        [TestMethod]
        public void Date_MillisecondPrecision_RoundTrip()
        {
            var file = CreateAmf3File("ms.sol");
            var dt = new DateTime(2024, 6, 15, 10, 30, 45, 123, DateTimeKind.Local);
            file.Data["v"] = new SolValueWrapper().SetValue(dt);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readDt = (DateTime)read.Data["v"].GetValue();
            // SOL Date encoding stores milliseconds since epoch
            Assert.AreEqual(dt.Year, readDt.Year);
            Assert.AreEqual(dt.Month, readDt.Month);
            Assert.AreEqual(dt.Day, readDt.Day);
            Assert.AreEqual(dt.Hour, readDt.Hour);
            Assert.AreEqual(dt.Minute, readDt.Minute);
            Assert.AreEqual(dt.Second, readDt.Second);
            Assert.AreEqual(dt.Millisecond, readDt.Millisecond);
        }

        [TestMethod]
        public void Date_Amf0_RoundTrip()
        {
            var file = CreateAmf0File("date0.sol");
            var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
            file.Data["v"] = new SolValueWrapper().SetValue(dt);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readDt = (DateTime)read.Data["v"].GetValue();
            Assert.AreEqual(dt.Year, readDt.Year);
            Assert.AreEqual(dt.Month, readDt.Month);
            Assert.AreEqual(dt.Day, readDt.Day);
        }

        #endregion

        #region AMF0 Array/Object Tests

        [TestMethod]
        public void Amf0_EcmaArray_RoundTrip()
        {
            var file = CreateAmf0File("ecma.sol");
            var arr = new SolArrayWrapper();
            arr.Assoc["name"] = new SolValueWrapper().SetValue("test");
            arr.Assoc["count"] = new SolValueWrapper().SetValue(5);
            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual("test", (string)readArr.Assoc["name"].GetValue());
        }

        [TestMethod]
        public void Amf0_StrictArray_RoundTrip()
        {
            var file = CreateAmf0File("strict.sol");
            var arr = new SolArrayWrapper();
            arr.Dense.Add(new SolValueWrapper().SetValue(10));
            arr.Dense.Add(new SolValueWrapper().SetValue(20));
            arr.Dense.Add(new SolValueWrapper().SetValue(30));
            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            Assert.AreEqual(3, readArr.Dense.Count);
            // AMF0 stores integers as Number (double), so cast accordingly
            Assert.AreEqual(10.0, Convert.ToDouble(readArr.Dense[0].GetValue()), 0.001);
            Assert.AreEqual(20.0, Convert.ToDouble(readArr.Dense[1].GetValue()), 0.001);
            Assert.AreEqual(30.0, Convert.ToDouble(readArr.Dense[2].GetValue()), 0.001);
        }

        [TestMethod]
        public void Amf0_Object_RoundTrip()
        {
            var file = CreateAmf0File("obj0.sol");
            var obj = new SolObjectWrapper();
            obj.Props["name"] = new SolValueWrapper().SetValue("player");
            obj.Props["score"] = new SolValueWrapper().SetValue(100);
            file.Data["v"] = new SolValueWrapper().SetValue(obj);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readObj = (SolObjectWrapper)read.Data["v"].GetValue();
            Assert.AreEqual("player", (string)readObj.Props["name"].GetValue());
        }

        [TestMethod]
        public void Amf0_NestedObjectInArray_RoundTrip()
        {
            var file = CreateAmf0File("nested0.sol");

            var obj = new SolObjectWrapper();
            obj.Props["id"] = new SolValueWrapper().SetValue(1);

            var arr = new SolArrayWrapper();
            arr.Dense.Add(new SolValueWrapper().SetValue(obj));

            file.Data["v"] = new SolValueWrapper().SetValue(arr);
            file.Save();

            var read = SolFileWrapper.ReadFile(file.Path);
            var readArr = (SolArrayWrapper)read.Data["v"].GetValue();
            var readObj = (SolObjectWrapper)readArr.Dense[0].GetValue();
            // AMF0 stores integers as Number (double)
            Assert.AreEqual(1.0, Convert.ToDouble(readObj.Props["id"].GetValue()), 0.001);
        }

        #endregion
    }
}
