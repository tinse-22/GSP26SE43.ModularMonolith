using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for bulk soft-deleting test cases.
/// </summary>
public class BulkDeleteTestCasesRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> TestCaseIds { get; set; }
}
