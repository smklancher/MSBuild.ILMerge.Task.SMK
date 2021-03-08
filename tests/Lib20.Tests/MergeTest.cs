using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lib20.Tests
{
    [TestClass]
    public class MergeTest
    {
  
        [TestMethod]
        public void Referenced20_is_merged()
        {
            var top = new Lib20.TestClass();
            var nested = top.ReferencedB;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreSame(topAssembly, nestedAssembly);
        }

        [TestMethod]
        public void ReferenceNotLocal20_not_merged()
        {
            var top = new Lib20.TestClass();
            var nested = top.ReferencedD;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreNotSame(topAssembly, nestedAssembly);
        }
    }
}
