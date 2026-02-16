// Decompiled with JetBrains decompiler
// Type: JsonQiifConverterGenerated.Qiif.QiifDataObject
// Assembly: JsonQiifConverter, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 315824A2-5917-4C88-B7C9-2B6B36A6DF2E
// Assembly location: C:\Users\a.pour\.nuget\packages\jsonqiifconverter\4.2.0\lib\net10.0\JsonQiifConverter.dll

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

#nullable disable
namespace JsonQiifConverterGenerated.Qiif;

[Description("Document used to request payment. ")]
public class QiifDataObject
{
  [JsonProperty("invoiceDocument", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("invoiceDocument")]
  public InvoiceDocument InvoiceDocument { get; set; }

  [JsonProperty("noteGroup", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("noteGroup")]
  public List<Note> NoteGroup { get; set; }

  public List<Note> CreateNoteGroup()
  {
    if (NoteGroup == null)
    {
      NoteGroup = new List<Note>();
    }
    return NoteGroup;
  }

  [JsonProperty("processControl", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("processControl")]
  public ProcessControl ProcessControl { get; set; }

  [JsonProperty("tradeTransaction", NullValueHandling = NullValueHandling.Ignore)]
  [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  [JsonPropertyName("tradeTransaction")]
  public TradeTransaction TradeTransaction { get; set; }

  public static QiifDataObject FromJson(string json)
  {
    return JsonConvert.DeserializeObject<QiifDataObject>(json);
  }

  public void SetActualSpecificationProfile()
  {
    
    ProcessControl processControl1 = this.ProcessControl;
    if (processControl1.SpecificationProfileId == null)
    {
      Id id;
      processControl1.SpecificationProfileId = id = new Id();
    }
    
  }
}
