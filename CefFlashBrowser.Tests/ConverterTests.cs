using CefFlashBrowser.Utils.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class ConverterTests
    {
        #region BinaryToSizeString

        [TestMethod]
        public void BinaryToSizeString_GetReadableSize_Bytes()
        {
            var data = new byte[500];
            Assert.AreEqual("500 B", BinaryToSizeString.GetReadableSize(data));
        }

        [TestMethod]
        public void BinaryToSizeString_GetReadableSize_Kilobytes()
        {
            var data = new byte[1024];
            Assert.AreEqual("1 KB", BinaryToSizeString.GetReadableSize(data));
        }

        [TestMethod]
        public void BinaryToSizeString_GetReadableSize_Megabytes()
        {
            var data = new byte[1024 * 1024];
            Assert.AreEqual("1 MB", BinaryToSizeString.GetReadableSize(data));
        }

        [TestMethod]
        public void BinaryToSizeString_GetReadableSize_FractionalKB()
        {
            var data = new byte[1536]; // 1.5 KB
            Assert.AreEqual("1.5 KB", BinaryToSizeString.GetReadableSize(data));
        }

        [TestMethod]
        public void BinaryToSizeString_GetReadableSize_EmptyArray()
        {
            var data = new byte[0];
            Assert.AreEqual("0 B", BinaryToSizeString.GetReadableSize(data));
        }

        [TestMethod]
        public void BinaryToSizeString_Convert_WithoutReadableSize()
        {
            var converter = new BinaryToSizeString { UseReadableSize = false };
            var data = new byte[1024];
            var result = converter.Convert(data, null, CultureInfo.InvariantCulture);
            Assert.AreEqual("1024 B", result);
        }

        [TestMethod]
        public void BinaryToSizeString_Convert_WithReadableSize()
        {
            var converter = new BinaryToSizeString { UseReadableSize = true };
            var data = new byte[1024];
            var result = converter.Convert(data, null, CultureInfo.InvariantCulture);
            Assert.AreEqual("1 KB", result);
        }

        #endregion

        #region ZoomLevelToScale

        [TestMethod]
        public void ZoomLevelToScale_Convert_ZeroReturnsOne()
        {
            var converter = new ZoomLevelToScale();
            var result = converter.Convert(0.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(1.0, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_Convert_PositiveValue()
        {
            var converter = new ZoomLevelToScale();
            // value >= 0: scale = value + 1
            var result = converter.Convert(1.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(2.0, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_Convert_NegativeValue()
        {
            var converter = new ZoomLevelToScale();
            // value < 0: scale = 1 / (1 - value)
            var result = converter.Convert(-1.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(0.5, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_ConvertBack_OneReturnsZero()
        {
            var converter = new ZoomLevelToScale();
            var result = converter.ConvertBack(1.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(0.0, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_ConvertBack_PositiveScale()
        {
            var converter = new ZoomLevelToScale();
            // scale >= 1: level = scale - 1
            var result = converter.ConvertBack(2.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(1.0, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_ConvertBack_FractionalScale()
        {
            var converter = new ZoomLevelToScale();
            // scale < 1: level = 1 - (1 / scale)
            var result = converter.ConvertBack(0.5, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(-1.0, result, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_Roundtrip_PositiveValue()
        {
            var converter = new ZoomLevelToScale();
            double original = 2.5;
            double scale = converter.Convert(original, null, CultureInfo.InvariantCulture);
            double roundtrip = converter.ConvertBack(scale, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(original, roundtrip, 0.001);
        }

        [TestMethod]
        public void ZoomLevelToScale_Roundtrip_NegativeValue()
        {
            var converter = new ZoomLevelToScale();
            double original = -0.5;
            double scale = converter.Convert(original, null, CultureInfo.InvariantCulture);
            double roundtrip = converter.ConvertBack(scale, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(original, roundtrip, 0.001);
        }

        #endregion

        #region MultiplyConverter

        [TestMethod]
        public void MultiplyConverter_Convert_DefaultMultiplierReturnsInput()
        {
            var converter = new MultiplyConverter();
            var result = converter.Convert(100.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(100.0, result, 0.001);
        }

        [TestMethod]
        public void MultiplyConverter_Convert_TwoThirds()
        {
            var converter = new MultiplyConverter { Multiplier = 2d / 3d };
            var result = converter.Convert(300.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(200.0, result, 0.001);
        }

        [TestMethod]
        public void MultiplyConverter_ConvertBack_TwoThirds()
        {
            var converter = new MultiplyConverter { Multiplier = 2d / 3d };
            var result = converter.ConvertBack(200.0, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(300.0, result, 0.001);
        }

        [TestMethod]
        public void MultiplyConverter_Roundtrip()
        {
            var converter = new MultiplyConverter { Multiplier = 0.5 };
            double original = 42.0;
            double converted = converter.Convert(original, null, CultureInfo.InvariantCulture);
            double roundtrip = converter.ConvertBack(converted, null, CultureInfo.InvariantCulture);
            Assert.AreEqual(original, roundtrip, 0.001);
        }

        #endregion
    }
}
