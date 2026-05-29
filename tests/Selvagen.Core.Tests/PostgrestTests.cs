using Selvagen.Core.Api;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class PostgrestTests
    {
        [Fact]
        public void Eq_EscapesSpecialCharacters()
        {
            var clause = Postgrest.Eq("id", "abc,def&select=*");
            Assert.Equal("id=eq.abc%2Cdef%26select%3D%2A", clause);
        }

        [Fact]
        public void Eq_LeavesPlainUuidUntouched()
        {
            var clause = Postgrest.Eq("project_id", "0ae6073d-c80a-4eed-a537-5ad8ee51d028");
            Assert.Equal("project_id=eq.0ae6073d-c80a-4eed-a537-5ad8ee51d028", clause);
        }

        [Fact]
        public void InList_EncodesEachValue()
        {
            var clause = Postgrest.InList("key", new[] { "a_b", "c,d" });
            Assert.Equal("key=in.(a_b,c%2Cd)", clause);
        }
    }
}
