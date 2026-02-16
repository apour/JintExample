// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.LineItem
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[System.ComponentModel.Description("Items, products or services, being invoiced.  Item, product or service, being invoiced. ")]
public class LineItem
{
  [JsonProperty("attributeGroup", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("attributeGroup")]
  public List<Attribute> AttributeGroup { get; set; }

  

  [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("description")]
  public LanguageBlockString Description { get; set; }

  
  [JsonProperty("lineId", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("lineId")]
  public Id LineId { get; set; }

  

  [JsonProperty("originCountryCode", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("originCountryCode")]
  public Code OriginCountryCode { get; set; }

  
}
