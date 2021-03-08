using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReferencedLib20;
using ReferencedLib472;
using ReferencedNotLocal472;
using ReferencedNotLocal20;

namespace Lib472
{
    public class TestClass
    {
        public ReferencedClassA ReferencedClassA => new ReferencedClassA();

        public object ReferencedA => (object)ReferencedClassA;

        public ReferencedClassB ReferencedClassB => new ReferencedClassB();

        public object ReferencedB => (object)ReferencedClassB;

        public ReferencedClassC ReferencedClassC => new ReferencedClassC();

        public object ReferencedC => (object)ReferencedClassC;

        public ReferencedClassD ReferencedClassD => new ReferencedClassD();

        public object ReferencedD => (object)ReferencedClassD;
    }
}
