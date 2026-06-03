using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class DeleteAssetResultTests
    {
        [Fact]
        public void Deserializes_Deleted_With_Table()
        {
            var json = "{\"status\":\"deleted\",\"table\":\"meshes\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("deleted", result.Status);
            Assert.Equal("meshes", result.Table);
        }

        [Fact]
        public void Deserializes_NotFound_Without_Table()
        {
            var json = "{\"status\":\"not_found\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("not_found", result.Status);
            Assert.Equal("", result.Table);
        }

        [Fact]
        public void Deserializes_Forbidden()
        {
            var json = "{\"status\":\"forbidden\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("forbidden", result.Status);
        }
    }
}
