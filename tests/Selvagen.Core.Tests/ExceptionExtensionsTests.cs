using System;
using Selvagen.Core.Api;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class ExceptionExtensionsTests
    {
        [Fact]
        public void Unwrap_ReturnsDeepestMessage()
        {
            var deep = new InvalidOperationException("root cause");
            var mid = new Exception("mid", deep);
            var top = new AggregateException("agg", mid);
            Assert.Equal("root cause", top.Unwrap().Message);
        }

        [Fact]
        public void Unwrap_PlainException_ReturnsItself()
        {
            var ex = new Exception("solo");
            Assert.Equal("solo", ex.Unwrap().Message);
        }
    }
}
