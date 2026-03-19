namespace SeguroAuto.Data;

public class DbOperationLog
{
    public int Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public string Status { get; set; } = "OK";
    public string? ErrorMessage { get; set; }
    public bool Exported { get; set; }
}
