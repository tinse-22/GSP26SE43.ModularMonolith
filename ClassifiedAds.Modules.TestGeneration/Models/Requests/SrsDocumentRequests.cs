using ClassifiedAds.Modules.TestGeneration.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class CreateSrsDocumentRequest
{
    public string Title { get; set; }

    public Guid? TestSuiteId { get; set; }

    public SrsSourceType SourceType { get; set; }

    /// <summary>
    /// Raw text content for TextInput source type.
    /// </summary>
    public string RawContent { get; set; }

    /// <summary>
    /// Storage file id for FileUpload source type.
    /// </summary>
    public Guid? StorageFileId { get; set; }
}

public class UpdateSrsRequirementRequest
{
    public string Title { get; set; }

    public string TestableConstraints { get; set; }

    public Guid? EndpointId { get; set; }

    /// <summary>
    /// When true, explicitly removes the endpoint mapping regardless of EndpointId value.
    /// Follows the same pattern as UpdateSrsDocumentRequest.ClearTestSuiteId.
    /// </summary>
    public bool ClearEndpointId { get; set; }

    public bool? IsReviewed { get; set; }
}

public class AddSrsRequirementRequest
{
    public string Title { get; set; }

    public string Description { get; set; }

    public SrsRequirementType RequirementType { get; set; }

    public string TestableConstraints { get; set; }

    public Guid? EndpointId { get; set; }
}

public class AnswerClarificationRequest
{
    public string UserAnswer { get; set; }
}

