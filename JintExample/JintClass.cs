using System;
using System.Collections.Generic;

namespace JsInteropDemo
{
    // Jednoduché DTO, které budeme vytvářet v JavaScriptu a používat v C#
    public class Receipt
    {
        public string DocumentId { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public Receipt() { }

        public Receipt(string documentId, string fileName)
        {
            DocumentId = documentId;
            FileName = fileName;
        }

        public override string ToString()
            => $"{DocumentId} / {FileName} / {CreatedUtc:O}";
    }

    // „Backend“ služba, do které se z JS budou posílat data
    public class BackendService
    {
        private readonly List<Receipt> _store = new();

        public void Save(Receipt r)
        {
            _store.Add(r);
            Console.WriteLine($"[Backend] Saved: {r}");
        }

        public int Count() => _store.Count;

        public IReadOnlyList<Receipt> All() => _store.AsReadOnly();

        // Ukázka metody, kterou zavoláme přímo z JS
        public void IncludeDocumentInOutput(string inputFileId, bool include)
        {
            Console.WriteLine($"[Backend] IncludeDocumentInOutput: {inputFileId} => {include}");
        }
    }
}