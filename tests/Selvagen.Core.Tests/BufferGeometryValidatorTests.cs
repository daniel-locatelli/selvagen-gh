using System;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class BufferGeometryValidatorTests
    {
        [Fact]
        public void Throws_When_Bg_Null()
            => Assert.Throws<ArgumentNullException>(() => BufferGeometryValidator.ValidateForDecode(null));

        [Fact]
        public void Throws_When_Data_Missing()
        {
            var bg = new BufferGeometry { Data = null };
            var ex = Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
            Assert.Contains("data", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Throws_When_Position_Array_Missing()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = null } }
                }
            };
            var ex = Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
            Assert.Contains("position", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Throws_When_Position_Not_Multiple_Of_3()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = new double[] { 1, 2 } } }
                }
            };
            Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
        }

        [Fact]
        public void Passes_For_Valid_Geometry()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = new double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 } } }
                }
            };
            BufferGeometryValidator.ValidateForDecode(bg); // does not throw
        }
    }
}
