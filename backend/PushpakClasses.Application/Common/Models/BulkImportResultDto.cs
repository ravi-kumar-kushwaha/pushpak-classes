namespace PushpakClasses.Application.Common.Models;

public class BulkImportResultDto
{
    public int TotalImported { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Message => TotalImported > 0
        ? $"Successfully imported {TotalImported} questions. (Skipped: {SkippedCount})"
        : $"Import failed. No questions were imported. (Errors: {Errors.Count})";
}
