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
        public SidekaContentViewModel()
        {
            Diffs = new Dictionary<string, SidekaDiff[]>();
            Data = new Dictionary<string, object[]>();
            Columns = new Dictionary<string, SidekaColumnConfig>();
        }

        public SidekaContentViewModel(JObject jObject)
        {
            Columns = new Dictionary<string, SidekaColumnConfig>();

            var columnsDict = (JObject)jObject["columns"];
            foreach (var key in columnsDict.Properties().Select(p => p.Name))
            {
                Columns[key] = new SidekaColumnConfig(columnsDict[key]);
            }

            var diffsDict = (JObject)jObject["diffs"];
            if (diffsDict != null)
            {
                Diffs = new Dictionary<string, SidekaDiff[]>();
                foreach (var key in diffsDict.Properties().Select(p => p.Name))
                {
                    Diffs[key] = diffsDict[key]
                        .Select(d => new SidekaDiff((JObject)d))
                        .ToArray();
                }
            }

            var dataDict = (JObject)jObject["data"];
            if (dataDict != null)
            {
                Data = new Dictionary<string, object[]>();
                foreach (var key in dataDict.Properties().Select(p => p.Name))
                {
                    Data[key] = ParseData((JArray)dataDict[key]);
                }
            }
        }

        [JsonProperty("diffs")]
        public Dictionary<string, SidekaDiff[]> Diffs { get; set; }

        [JsonProperty("data")]
        public Dictionary<string, object[]> Data { get; set; }

        [JsonProperty("columns")]
        public Dictionary<string, SidekaColumnConfig> Columns { get; set; }

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
        public SidekaDiff() { }

        public SidekaDiff(JObject jObject)
        {
            Total = (int)jObject["total"];
            Added = SidekaContentViewModel.ParseData((JArray)jObject["added"]);
            Modified = SidekaContentViewModel.ParseData((JArray)jObject["modified"]);
            Deleted = SidekaContentViewModel.ParseData((JArray)jObject["deleted"]);
        }

        [JsonProperty("added")]
        public object[] Added { get; set; }

        [JsonProperty("modified")]
        public object[] Modified { get; set; }

        [JsonProperty("deleted")]
        public object[] Deleted { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("rewritten")]
        public bool Rewritten { get; set; }
    }

    [JsonConverter(typeof(SidekaColumnConfigSerializer))]
    public class SidekaColumnConfig
    {
        public SidekaColumnConfig(JToken jToken)
        {
            if (jToken is JArray)
            {
                Columns = ((JArray)jToken).Select(s => (string)s).ToArray();
            }
        }

        public bool IsDict
        {
            get
            {
                return Columns == null;
            }
        }

        public string[] Columns { get; set; }
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
                serializer.Serialize(writer, config.Columns);
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

