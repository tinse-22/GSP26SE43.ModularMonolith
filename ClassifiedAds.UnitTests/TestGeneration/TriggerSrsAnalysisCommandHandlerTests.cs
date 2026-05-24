using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TriggerSrsAnalysisCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_TriggerAsyncWebhook_WithCallbackPayload()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var document = new SrsDocument
        {
            Id = documentId,
            ProjectId = projectId,
            SourceType = SrsSourceType.TextInput,
            RawContent = "The API shall reject invalid emails with HTTP 400.",
            AnalysisStatus = SrsAnalysisStatus.Pending,
        };
        var savedJobs = new List<SrsAnalysisJob>();
        N8nSrsAnalysisPayload? capturedPayload = null;

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var documentRepository = new Mock<IRepository<SrsDocument, Guid>>();
        documentRepository.SetupGet(x => x.UnitOfWork).Returns(unitOfWork.Object);
        documentRepository.Setup(x => x.GetQueryableSet()).Returns(new[] { document }.AsQueryable());
        documentRepository.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(document);
        documentRepository.Setup(x => x.UpdateAsync(It.IsAny<SrsDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jobRepository = new Mock<IRepository<SrsAnalysisJob, Guid>>();
        jobRepository.SetupGet(x => x.UnitOfWork).Returns(unitOfWork.Object);
        jobRepository.Setup(x => x.AddAsync(It.IsAny<SrsAnalysisJob>(), It.IsAny<CancellationToken>()))
            .Callback<SrsAnalysisJob, CancellationToken>((job, _) =>
            {
                job.Id = jobId;
                savedJobs.Add(job);
            })
            .Returns(Task.CompletedTask);

        var suiteRepository = new Mock<IRepository<TestSuite, Guid>>();
        var endpointMetadataService = new Mock<IApiEndpointMetadataService>();

        var n8nService = new Mock<IN8nIntegrationService>();
        n8nService.Setup(x => x.TriggerWebhookWithResultAsync(
                "analyze-srs",
                It.IsAny<N8nSrsAnalysisPayload>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, N8nSrsAnalysisPayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new WebhookTriggerResult
            {
                Success = true,
                WebhookName = "analyze-srs",
                ResolvedUrl = "https://n8n.example/webhook/analyze-srs",
            });

        var handler = new TriggerSrsAnalysisCommandHandler(
            documentRepository.Object,
            jobRepository.Object,
            suiteRepository.Object,
            endpointMetadataService.Object,
            n8nService.Object,
            Options.Create(new N8nIntegrationOptions
            {
                BeBaseUrl = "https://api.example.test/",
                CallbackApiKey = "callback-secret",
            }),
            new Mock<IStorageFileGatewayService>().Object,
            new Dispatcher(new Mock<IServiceProvider>().Object),
            new Mock<ILogger<TriggerSrsAnalysisCommandHandler>>().Object);

        var command = new TriggerSrsAnalysisCommand
        {
            ProjectId = projectId,
            SrsDocumentId = documentId,
            CurrentUserId = userId,
        };

        await handler.HandleAsync(command);

        command.JobId.Should().Be(jobId);
        savedJobs.Should().ContainSingle();
        savedJobs[0].Status.Should().Be(SrsAnalysisJobStatus.Processing);
        document.AnalysisStatus.Should().Be(SrsAnalysisStatus.Processing);
        capturedPayload.Should().NotBeNull();
        capturedPayload!.JobId.Should().Be(jobId);
        capturedPayload.CallbackUrl.Should().Be($"https://api.example.test/api/srs-analysis-callback/{jobId}");
        capturedPayload.CallbackApiKey.Should().Be("callback-secret");
        n8nService.Verify(x => x.TriggerWebhookAsync<N8nSrsAnalysisPayload, SrsAnalysisCallbackRequest>(
            It.IsAny<string>(),
            It.IsAny<N8nSrsAnalysisPayload>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ProcessSynchronousN8nResponse_WhenResponseContainsRequirements()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var document = new SrsDocument
        {
            Id = documentId,
            ProjectId = projectId,
            SourceType = SrsSourceType.TextInput,
            RawContent = "The API shall reject invalid emails with HTTP 400.",
            AnalysisStatus = SrsAnalysisStatus.Pending,
        };
        ProcessSrsAnalysisCallbackCommand? dispatchedCommand = null;

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var documentRepository = new Mock<IRepository<SrsDocument, Guid>>();
        documentRepository.SetupGet(x => x.UnitOfWork).Returns(unitOfWork.Object);
        documentRepository.Setup(x => x.GetQueryableSet()).Returns(new[] { document }.AsQueryable());
        documentRepository.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SrsDocument>>()))
            .ReturnsAsync(document);
        documentRepository.Setup(x => x.UpdateAsync(It.IsAny<SrsDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jobRepository = new Mock<IRepository<SrsAnalysisJob, Guid>>();
        jobRepository.SetupGet(x => x.UnitOfWork).Returns(unitOfWork.Object);
        jobRepository.Setup(x => x.AddAsync(It.IsAny<SrsAnalysisJob>(), It.IsAny<CancellationToken>()))
            .Callback<SrsAnalysisJob, CancellationToken>((job, _) => job.Id = jobId)
            .Returns(Task.CompletedTask);

        var n8nService = new Mock<IN8nIntegrationService>();
        n8nService.Setup(x => x.TriggerWebhookWithResultAsync(
                "analyze-srs",
                It.IsAny<N8nSrsAnalysisPayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookTriggerResult
            {
                Success = true,
                WebhookName = "analyze-srs",
                ResponseBody = "{\"requirements\":[{\"requirementCode\":\"REQ-001\",\"title\":\"Email validation\",\"description\":\"Reject invalid email\",\"type\":\"functional\",\"testableConstraints\":\"[]\",\"assumptions\":\"[]\",\"ambiguities\":\"[]\",\"confidenceScore\":0.9}],\"clarificationQuestions\":[]}",
            });

        var callbackHandler = new Mock<ICommandHandler<ProcessSrsAnalysisCallbackCommand>>();
        callbackHandler.Setup(x => x.HandleAsync(
                It.IsAny<ProcessSrsAnalysisCallbackCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProcessSrsAnalysisCallbackCommand, CancellationToken>((command, _) => dispatchedCommand = command)
            .Returns(Task.CompletedTask);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(ICommandHandler<ProcessSrsAnalysisCallbackCommand>)))
            .Returns(callbackHandler.Object);

        var handler = new TriggerSrsAnalysisCommandHandler(
            documentRepository.Object,
            jobRepository.Object,
            new Mock<IRepository<TestSuite, Guid>>().Object,
            new Mock<IApiEndpointMetadataService>().Object,
            n8nService.Object,
            Options.Create(new N8nIntegrationOptions
            {
                BeBaseUrl = "https://api.example.test/",
                CallbackApiKey = "callback-secret",
            }),
            new Mock<IStorageFileGatewayService>().Object,
            new Dispatcher(provider.Object),
            new Mock<ILogger<TriggerSrsAnalysisCommandHandler>>().Object);

        await handler.HandleAsync(new TriggerSrsAnalysisCommand
        {
            ProjectId = projectId,
            SrsDocumentId = documentId,
            CurrentUserId = userId,
        });

        dispatchedCommand.Should().NotBeNull();
        dispatchedCommand!.JobId.Should().Be(jobId);
        dispatchedCommand.Requirements.Should().ContainSingle();
        dispatchedCommand.Requirements[0].RequirementCode.Should().Be("REQ-001");
    }
}
