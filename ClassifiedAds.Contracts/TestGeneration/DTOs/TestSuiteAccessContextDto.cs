using System;

namespace ClassifiedAds.Contracts.TestGeneration.DTOs;

public class TestSuiteAccessContextDto
{
    public Guid TestSuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ApiSpecId { get; set; }

    public Guid CreatedById { get; set; }

    public string Status { get; set; }

    public string Name { get; set; }
}
