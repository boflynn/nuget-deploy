using System;
using Xunit;

namespace SamplePackage.Tests
{
    public class MathTests
    {
        [Theory]
        [InlineData(1, 2, 3)]
        [InlineData(100, 200, 300)]
        public void Add_Correctly_AddsNumbers(int left, int right, int expected)
        {
            var actual = Math.Add(left, right);

            Assert.Equal(expected, actual);
        }
    }
}
