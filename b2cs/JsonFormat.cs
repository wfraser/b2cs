using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace b2cs
{
    /// <summary>
    /// !!! DANGER !!!
    /// No escaping or sanitizing is done here. Don't feed this any untrusted inputs!
    /// !!! DANGER !!!
    /// </summary>
    public static class JsonFormat
    {
        // TODO: use Json.NET or something

        public static string Format(ExpandoObject obj)
        {
            var entries = obj.Select(pair => string.Format("\"{0}\": {1}", pair.Key, JsonFormat.Format(pair.Value)));
            return "{" + string.Join(",", entries) + "}";
        }

        public static string Format(object o)
        {
            if (o is ExpandoObject)
            {
                return Format(o as ExpandoObject);
            }
            else if (o is int)
            {
                return o.ToString();
            }
            else
            {
                return "\"" + o.ToString() + "\"";
            }
        }
    }
}
