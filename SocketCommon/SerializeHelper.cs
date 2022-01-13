using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace SocketCommon
{
    public static class SerializeHelper
    {
        public static byte[] BeginSerializable(this object Obj)
        {
            MemoryStream mStream = new MemoryStream();
            BinaryFormatter bFormatter = new BinaryFormatter();
            bFormatter.Serialize(mStream, Obj);
            return mStream.GetBuffer();
        }

        public static T BeginDeserialize<T>(this byte[] Bytes) where T : class
        {
            try
            {
                BinaryFormatter bFormatter = new BinaryFormatter();
                return (T)bFormatter.Deserialize(new MemoryStream(Bytes));
            }
            catch
            {
                return null;
            }

        }
        public static string BeginJsonSerializable(this object Obj, bool includeDefaultValue = false)
        {
            if (Obj == null) return "";
            IsoDateTimeConverter timeFormat = new IsoDateTimeConverter();
            timeFormat.DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
            Newtonsoft.Json.JsonSerializerSettings jsettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                Converters = new[] { timeFormat }
                    ,
                DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore
            };
            if (includeDefaultValue)
                jsettings.DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include;
            return Newtonsoft.Json.JsonConvert.SerializeObject(Obj
                , Newtonsoft.Json.Formatting.None
                , jsettings);
        }

        public static T BeginJsonDeserialize<T>(this string str)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(str)) return default(T);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(str);
            }
            catch
            {
                return default(T);
            }
        }
        public static string EncodeBase64(string code)
        {
            string encode = "";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(code);
            try
            {
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = code;
            }
            return encode;
        }
        public static string DecodeBase64(string code)
        {
            string decode = "";
            byte[] bytes = Convert.FromBase64String(code);
            try
            {
                decode = System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                decode = code;
            }
            return decode;
        }
    }
}
