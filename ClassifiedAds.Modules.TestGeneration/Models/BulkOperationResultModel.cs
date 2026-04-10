using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Shared result model for bulk delete/restore operations on TestCases and LlmSuggestions.
/// </summary>
public class BulkOperationResultModel
{
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// Operation performed: "Delete" or "Restore".
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// Entity type: "TestCase" or "LlmSuggestion".
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Total number of IDs requested in the operation.
    /// </summary>
    public int RequestedCount { get; set; }

    /// <summary>
    /// Number of items successfully processed.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Number of items skipped (already in target state or not found).
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// IDs that were successfully processed.
    /// </summary>
    public List<Guid> ProcessedIds { get; set; } = new();

    /// <summary>
    /// IDs that were skipped.
    /// </summary>
    public List<Guid> SkippedIds { get; set; } = new();

    /// <summary>
    /// Reason why items were skipped (e.g. "Đã ở trạng thái xóa" or "Không tìm thấy").
    /// </summary>
    public string SkipReason { get; set; }

    /// <summary>
    /// Timestamp of the operation.
    /// </summary>
    public DateTimeOffset OperatedAt { get; set; }
}
