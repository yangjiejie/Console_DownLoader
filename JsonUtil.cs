
using Newtonsoft.Json;
namespace Assets.Script
{
    public static class JsonUtil
    {
        public static string SerializeObject(Object obj)
        {
            var json = JsonConvert.SerializeObject(obj,Formatting.Indented);
            return json;
        }
        public static T DeserializeObject<T>(string json)
        {
            var t = JsonConvert.DeserializeObject<T>(json);
            return t;
        }
    }
}
