using IntelliFin.KycDocumentService.Models;

namespace IntelliFin.KycDocumentService.Services;

public interface IAzureOcrService
{
    Task<OcrExtractionResult> ExtractDataAsync(Stream documentStream, KycDocumentType documentType, CancellationToken cancellationToken = default);
    Task<OcrExtractionResult> ExtractDataFromUrlAsync(string documentUrl, KycDocumentType documentType, CancellationToken cancellationToken = default);
    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
    Task<DocumentLayoutAnalysis> AnalyzeLayoutAsync(Stream documentStream, CancellationToken cancellationToken = default);
}

public class OcrExtractionResult
{
    public Dictionary<string, object> ExtractedFields { get; set; } = new();
    public float OverallConfidence { get; set; }
    public Dictionary<string, float> FieldConfidences { get; set; } = new();
    public string DocumentType { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public DocumentQualityMetrics? QualityMetrics { get; set; }
}

public class DocumentLayoutAnalysis
{
    public List<DocumentPage> Pages { get; set; } = new();
    public List<DocumentTable> Tables { get; set; } = new();
    public List<DocumentKeyValuePair> KeyValuePairs { get; set; } = new();
    public float OverallConfidence { get; set; }
}

public class DocumentPage
{
    public int PageNumber { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public List<DocumentLine> Lines { get; set; } = new();
    public List<DocumentWord> Words { get; set; } = new();
}

public class DocumentLine
{
    public string Text { get; set; } = string.Empty;
    public List<float> BoundingBox { get; set; } = new();
    public float Confidence { get; set; }
}

public class DocumentWord
{
    public string Text { get; set; } = string.Empty;
    public List<float> BoundingBox { get; set; } = new();
    public float Confidence { get; set; }
}

public class DocumentTable
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<DocumentTableCell> Cells { get; set; } = new();
}

public class DocumentTableCell
{
    public string Text { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public int ColumnIndex { get; set; }
    public float Confidence { get; set; }
}

public class DocumentKeyValuePair
{
    public DocumentField Key { get; set; } = new();
    public DocumentField Value { get; set; } = new();
    public float Confidence { get; set; }
}

public class DocumentField
{
    public string Text { get; set; } = string.Empty;
    public List<float> BoundingBox { get; set; } = new();
    public float Confidence { get; set; }
}

public class DocumentQualityMetrics
{
    public float ImageQuality { get; set; }
    public float TextClarity { get; set; }
    public bool HasBlur { get; set; }
    public bool HasSkew { get; set; }
    public float SkewAngle { get; set; }
    public int DpiEstimate { get; set; }
}