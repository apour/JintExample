// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.DateAndTime
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("A particular point in the progression of time, together with relevant supplementary information. ")]
public class DateAndTime
{
  [Description("One calendar day according the Gregorian calendar. ISO 8601. ")]
  [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("date")]
  public string Date { get; set; }

  [Description("An instance of time that occurs every day. ")]
  [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("time")]
  public string Time { get; set; }

  [Description("Time offset from UTC +/- 23:59. ")]
  [JsonProperty("timeOffset", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("timeOffset")]
  public string TimeOffset { get; set; }
}
