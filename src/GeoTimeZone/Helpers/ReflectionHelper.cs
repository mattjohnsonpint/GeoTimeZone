using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GeoTimeZone.Helpers
{
    public class ReflectionHelper
    {
        public static Assembly GetAssemblyOf(Type type)
        {
#if DNX451
            var assembly = type.Assembly;
#elif DNXCORE50
            var assembly = type.GetTypeInfo().Assembly;
#endif
            return assembly;
        }
    }
}
