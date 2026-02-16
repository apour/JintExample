// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.InvoiceDocument
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("Invoice carrier object. ")]
public class InvoiceDocument
{
  [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("date")]
  public DateAndTime Date { get; set; }

  [JsonProperty("documentId", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("documentId")]
  public Id DocumentId { get; set; }

  [JsonProperty("transactionDirectionCode", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("transactionDirectionCode")]
  public Code TransactionDirectionCode { get; set; }

  [JsonProperty("typeCode", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("typeCode")]
  public Code TypeCode { get; set; }
}
