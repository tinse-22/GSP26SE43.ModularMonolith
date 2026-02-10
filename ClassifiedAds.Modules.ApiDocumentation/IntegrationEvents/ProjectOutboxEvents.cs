using System;

namespace ClassifiedAds.Modules.ApiDocumentation.IntegrationEvents;

public class ProjectCreatedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public Guid OwnerId { get; set; }

    public string Status { get; set; }
}

public class ProjectUpdatedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string OldName { get; set; }
}

public class ProjectArchivedOutboxEvent : ApiDocOutboxEventBase
{
    public Guid ProjectId { get; set; }

    public bool IsArchived { get; set; }
}
