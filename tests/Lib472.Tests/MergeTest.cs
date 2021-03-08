using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lib472.Tests
{
    [TestClass]
    public class MergeTest
    {
        [TestMethod]
        public void Referenced472_is_merged()
        {
            var top = new Lib472.TestClass();
            var nested = top.ReferencedA;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreSame(topAssembly, nestedAssembly);
        }

        [TestMethod]
        public void Referenced20_is_merged()
        {
            var top = new Lib472.TestClass();
            var nested = top.ReferencedB;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreSame(topAssembly, nestedAssembly);
        }

        [TestMethod]
        public void ReferenceNotLocal472_not_merged()
        {
            var top = new Lib472.TestClass();
            var nested = top.ReferencedC;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreNotSame(topAssembly, nestedAssembly);
        }


        [TestMethod]
        public void ReferenceNotLocal20_not_merged()
        {
            var top = new Lib472.TestClass();
            var nested = top.ReferencedD;

            var topAssembly = top.GetType().Assembly;
            var nestedAssembly = nested.GetType().Assembly;

            Assert.AreNotSame(topAssembly, nestedAssembly);
        }
    }
}
