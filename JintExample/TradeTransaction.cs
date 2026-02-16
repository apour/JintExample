// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.TradeTransaction
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("Information on trade transaction. ")]
public class TradeTransaction
{
  [JsonProperty("evidence", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("evidence")]
  public Evidence Evidence { get; set; }

  [JsonProperty("fulfillment", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("fulfillment")]
  public Fulfillment Fulfillment { get; set; }

  [JsonProperty("lineItemGroup", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("lineItemGroup")]
  public List<LineItem> LineItemGroup { get; set; }

  [JsonProperty("monetary", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("monetary")]
  public Monetary Monetary { get; set; }

  [JsonProperty("participant", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("participant")]
  public Participant Participant { get; set; }
  
}
