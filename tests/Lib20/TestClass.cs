using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReferencedLib20;
using ReferencedNotLocal20;

namespace Lib20
{
    public class TestClass
    {
        public ReferencedClassB ReferencedClassB => new ReferencedClassB();

        public object ReferencedB => (object)ReferencedClassB;

        public ReferencedClassD ReferencedClassD => new ReferencedClassD();

        public object ReferencedD => (object)ReferencedClassD;
    }
}
