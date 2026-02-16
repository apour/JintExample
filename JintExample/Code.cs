// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.Code
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("Code for asset and issuer of its unique assignment. ")]
public class Code
{
  [Description("Character string to identify and uniquely distinguish one instance of an object in an identification scheme from all other objects in the same scheme, together with relevant supplementary information. ")]
  [JsonProperty("identifier", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("identifier")]
  public string Identifier { get; set; }  
}
