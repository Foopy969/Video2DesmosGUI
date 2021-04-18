using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Video2DesmosGUI
{
    public static class Extensions
    {
        public static string Truncate(this string value, int maxChars)
        {
            return value.Length < maxChars ? value : "..." + value[^maxChars..];
        }
    }
}
