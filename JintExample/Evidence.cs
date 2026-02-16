// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.Evidence
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("Documents supporting case of this invoice. ")]
public class Evidence
{
  [JsonProperty("bid", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("bid")]
  public DocumentReference Bid { get; set; }

  [JsonProperty("contract", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("contract")]
  public DocumentReference Contract { get; set; }

  [JsonProperty("despatch", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("despatch")]
  public DocumentReference Despatch { get; set; }

  [JsonProperty("invoiceGroup", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("invoiceGroup")]
  public List<RelatedInvoice> InvoiceGroup { get; set; }

  [JsonProperty("otherGroup", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("otherGroup")]
  public List<DocumentReference> OtherGroup { get; set; }

  [JsonProperty("project", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("project")]
  public DocumentReference Project { get; set; }

  [JsonProperty("purchaseOrder", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("purchaseOrder")]
  public DocumentReference PurchaseOrder { get; set; }

  [JsonProperty("receipt", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("receipt")]
  public DocumentReference Receipt { get; set; }

  [JsonProperty("salesOrder", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("salesOrder")]
  public DocumentReference SalesOrder { get; set; }
}
