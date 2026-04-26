using ClassifiedAds.Modules.TestGeneration.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class CreateSrsDocumentRequest
{
    public string Title { get; set; }

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

    public bool? IsReviewed { get; set; }
}

public class AnswerClarificationRequest
{
    public string UserAnswer { get; set; }
}
