using System;

namespace ClassifiedAds.Modules.ApiDocumentation.IntegrationEvents;

public class SpecUploadedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid SpecId { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string SourceType { get; set; }

    public new string Version { get; set; }

    public Guid? OriginalFileId { get; set; }

    public string ParseStatus { get; set; }
}

public class SpecActivatedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid SpecId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? PreviousActiveSpecId { get; set; }
}

public class SpecDeletedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid SpecId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? OriginalFileId { get; set; }
}
