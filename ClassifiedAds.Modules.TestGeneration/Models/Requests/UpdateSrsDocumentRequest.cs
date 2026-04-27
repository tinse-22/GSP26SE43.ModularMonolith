using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for updating an SRS document (currently supports linking to a test suite).
/// </summary>
public class UpdateSrsDocumentRequest
{
    /// <summary>
    /// Set to a valid test suite GUID to link this document to that suite.
    /// Set to null combined with ClearTestSuiteId=true to unlink.
    /// </summary>
    public Guid? TestSuiteId { get; set; }

    /// <summary>
    /// When true, explicitly removes the test suite association regardless of TestSuiteId value.
    /// </summary>
    public bool ClearTestSuiteId { get; set; }
}
