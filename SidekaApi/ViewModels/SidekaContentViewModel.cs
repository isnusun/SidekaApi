using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SidekaApi.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.ViewModels
{
    public class SidekaContentViewModel
    {

        public SidekaContentViewModel(JObject jObject)
        {
            diffs = new Dictionary<string, SidekaDiff[]>();
            data = new Dictionary<string, object[]>();
            columns = new Dictionary<string, SidekaColumnConfig>();

            var columnsDict = (JObject)jObject["columns"];
            foreach (var key in columnsDict.Properties().Select(p => p.Name))
            {
                columns[key] = new SidekaColumnConfig(columnsDict[key]);
            }

            var diffsDict = (JObject)jObject["diffs"];
            if (diffsDict != null)
            {
                foreach (var key in diffsDict.Properties().Select(p => p.Name))
                {
                    diffs[key] = diffsDict[key]
                        .Select(d => new SidekaDiff((JObject)d))
                        .ToArray();
                }
            }

            var dataDict = (JObject)jObject["data"];
            if (dataDict != null)
            {
                foreach (var key in dataDict.Properties().Select(p => p.Name))
                {
                    data[key] = ParseData((JArray)dataDict[key]);
                }
            }
        }

        public Dictionary<string, SidekaDiff[]> diffs { get; set; }
        public Dictionary<string, object[]> data { get; set; }
        public Dictionary<string, SidekaColumnConfig> columns { get; set; }

        public static object ParseDatum(JToken datum)
        {
            if (datum is JArray)
            {
                return ((JArray)datum).ToArray();
            }
            return ((JObject)datum).ToDictionary();
        }

        public static object[] ParseData(JArray data)
        {
            return data.Select(d => ParseDatum(d)).ToArray();
        }
    }

    public class SidekaDiff
    {
        public SidekaDiff(JObject jObject)
        {
            total = (int)jObject["total"];
            added = SidekaContentViewModel.ParseData((JArray)jObject["added"]);
            modified = SidekaContentViewModel.ParseData((JArray)jObject["modified"]);
            deleted = SidekaContentViewModel.ParseData((JArray)jObject["deleted"]);
        }

        public object[] added { get; set; }
        public object[] modified { get; set; }
        public object[] deleted { get; set; }
        public int total { get; set; }
    }

    [JsonConverter(typeof(SidekaColumnConfigSerializer))]
    public class SidekaColumnConfig
    {
        public SidekaColumnConfig(JToken jToken)
        {
            if (jToken is JArray)
            {
                columns = ((JArray)jToken).Select(s => (string)s).ToArray();
            }
        }

        public bool IsDict
        {
            get
            {
                return columns != null;
            }
        }

        public string[] columns { get; set; }
    }

    public class SidekaColumnConfigSerializer : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var config = (SidekaColumnConfig)value;
            if (config.IsDict)
            {
                writer.WriteValue("dict");
            }
            else
            {
                //writer.WriteStartArray();
                serializer.Serialize(writer, config.columns);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(SidekaColumnConfig).IsAssignableFrom(objectType);
        }

        public override bool CanRead => false;
    }
}

