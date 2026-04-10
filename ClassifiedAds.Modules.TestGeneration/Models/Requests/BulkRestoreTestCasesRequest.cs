using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for bulk restoring soft-deleted test cases.
/// </summary>
public class BulkRestoreTestCasesRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> TestCaseIds { get; set; }
}
