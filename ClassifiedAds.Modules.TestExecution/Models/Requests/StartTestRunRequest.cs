using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models.Requests;

public class StartTestRunRequest
{
    public Guid? EnvironmentId { get; set; }

    public List<Guid> SelectedTestCaseIds { get; set; }
}
