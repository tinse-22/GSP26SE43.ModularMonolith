using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Commands;

public class AddUpdateExecutionEnvironmentCommand : ICommand
{
    public Guid? EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Name { get; set; }

    public string BaseUrl { get; set; }

    public Dictionary<string, string> Variables { get; set; }

    public Dictionary<string, string> Headers { get; set; }

    public ExecutionAuthConfigModel AuthConfig { get; set; }

    public bool IsDefault { get; set; }

    public string RowVersion { get; set; }

    public ExecutionEnvironmentModel Result { get; set; }
}

public class AddUpdateExecutionEnvironmentCommandHandler : ICommandHandler<AddUpdateExecutionEnvironmentCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly IExecutionAuthConfigService _authConfigService;
    private readonly ILogger<AddUpdateExecutionEnvironmentCommandHandler> _logger;

    public AddUpdateExecutionEnvironmentCommandHandler(
        IRepository<ExecutionEnvironment, Guid> envRepository,
        IExecutionAuthConfigService authConfigService,
        ILogger<AddUpdateExecutionEnvironmentCommandHandler> logger)
    {
        _envRepository = envRepository;
        _authConfigService = authConfigService;
        _logger = logger;
    }

    public async Task HandleAsync(AddUpdateExecutionEnvironmentCommand command, CancellationToken cancellationToken = default)
    {
        ValidateInput(command);

        _authConfigService.ValidateAuthConfig(command.AuthConfig);

        if (command.EnvironmentId.HasValue && command.EnvironmentId.Value == Guid.Empty)
        {
            throw new ValidationException("EnvironmentId không hợp lệ.");
        }

        bool isUpdate = command.EnvironmentId.HasValue;

        if (isUpdate)
        {
            await HandleUpdate(command, cancellationToken);
        }
        else
        {
            await HandleCreate(command, cancellationToken);
        }
    }

    private async Task HandleCreate(AddUpdateExecutionEnvironmentCommand command, CancellationToken cancellationToken)
    {
        var env = new ExecutionEnvironment
        {
            ProjectId = command.ProjectId,
            Name = command.Name.Trim(),
            BaseUrl = command.BaseUrl.Trim(),
            Variables = SerializeDictionary(command.Variables),
            Headers = SerializeDictionary(command.Headers),
            AuthConfig = _authConfigService.SerializeAuthConfig(command.AuthConfig),
            IsDefault = command.IsDefault,
        };

        try
        {
            if (command.IsDefault)
            {
                await _envRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await UnsetProjectDefaults(command.ProjectId, ct);
                    await _envRepository.AddAsync(env, ct);
                    await _envRepository.UnitOfWork.SaveChangesAsync(ct);
                    await EnsureSingleDefaultEnvironment(command.ProjectId, ct);
                }, isolationLevel: IsolationLevel.Serializable, cancellationToken: cancellationToken);
            }
            else
            {
                await _envRepository.AddAsync(env, cancellationToken);
                await _envRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (IsSerializableTransactionConflict(ex))
        {
            throw new ConflictException("DEFAULT_ENVIRONMENT_CONFLICT", "Không thể đặt default environment do xung đột đồng thời. Vui lòng thử lại.", ex);
        }

        command.Result = ExecutionEnvironmentModel.FromEntity(env, _authConfigService);

        _logger.LogInformation(
            "Created execution environment. EnvironmentId={EnvironmentId}, ProjectId={ProjectId}, IsDefault={IsDefault}, ActorUserId={ActorUserId}",
            env.Id, command.ProjectId, env.IsDefault, command.CurrentUserId);
    }

    private async Task HandleUpdate(AddUpdateExecutionEnvironmentCommand command, CancellationToken cancellationToken)
    {
        var env = await _envRepository.FirstOrDefaultAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.Id == command.EnvironmentId.Value && x.ProjectId == command.ProjectId));

        if (env == null)
        {
            throw new NotFoundException($"Không tìm thấy execution environment với mã '{command.EnvironmentId}'.");
        }

        if (string.IsNullOrEmpty(command.RowVersion))
        {
            throw new ValidationException("RowVersion là bắt buộc khi cập nhật.");
        }

        try
        {
            _envRepository.SetRowVersion(env, Convert.FromBase64String(command.RowVersion));
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion không hợp lệ.");
        }

        env.Name = command.Name.Trim();
        env.BaseUrl = command.BaseUrl.Trim();
        env.Variables = SerializeDictionary(command.Variables);
        env.Headers = SerializeDictionary(command.Headers);
        env.AuthConfig = _authConfigService.SerializeAuthConfig(command.AuthConfig);
        env.IsDefault = command.IsDefault;

        try
        {
            if (command.IsDefault)
            {
                await _envRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await UnsetProjectDefaults(command.ProjectId, ct, command.EnvironmentId.Value);
                    await _envRepository.UpdateAsync(env, ct);
                    await _envRepository.UnitOfWork.SaveChangesAsync(ct);
                    await EnsureSingleDefaultEnvironment(command.ProjectId, ct);
                }, isolationLevel: IsolationLevel.Serializable, cancellationToken: cancellationToken);
            }
            else
            {
                await _envRepository.UpdateAsync(env, cancellationToken);
                await _envRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.", ex);
        }
        catch (Exception ex) when (IsSerializableTransactionConflict(ex))
        {
            throw new ConflictException("DEFAULT_ENVIRONMENT_CONFLICT", "Không thể đặt default environment do xung đột đồng thời. Vui lòng thử lại.", ex);
        }

        command.Result = ExecutionEnvironmentModel.FromEntity(env, _authConfigService);

        _logger.LogInformation(
            "Updated execution environment. EnvironmentId={EnvironmentId}, ProjectId={ProjectId}, IsDefault={IsDefault}, ActorUserId={ActorUserId}",
            env.Id, command.ProjectId, env.IsDefault, command.CurrentUserId);
    }

    private async Task UnsetProjectDefaults(Guid projectId, CancellationToken cancellationToken, Guid? excludeId = null)
    {
        var currentDefaults = await _envRepository.ToListAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.ProjectId == projectId && x.IsDefault && (excludeId == null || x.Id != excludeId.Value)));

        foreach (var d in currentDefaults)
        {
            d.IsDefault = false;
            await _envRepository.UpdateAsync(d, cancellationToken);
        }
    }

    private async Task EnsureSingleDefaultEnvironment(Guid projectId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _envRepository.ToListAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.ProjectId == projectId && x.IsDefault));

        if (currentDefaults.Count > 1)
        {
            throw new ConflictException("DEFAULT_ENVIRONMENT_CONFLICT", "Mỗi project chỉ được có một default environment.");
        }
    }

    private static void ValidateInput(AddUpdateExecutionEnvironmentCommand command)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId là bắt buộc.");
        }

        if (command.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Tên environment là bắt buộc.");
        }

        if (command.Name.Length > 100)
        {
            throw new ValidationException("Tên environment không được vượt quá 100 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(command.BaseUrl))
        {
            throw new ValidationException("BaseUrl là bắt buộc.");
        }

        if (command.BaseUrl.Length > 500)
        {
            throw new ValidationException("BaseUrl không được vượt quá 500 ký tự.");
        }

        if (!Uri.TryCreate(command.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ValidationException("BaseUrl phải là URL tuyệt đối (http hoặc https).");
        }

        if (command.Headers != null)
        {
            foreach (var kvp in command.Headers)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    throw new ValidationException("Header key không được để trống.");
                }
            }
        }

        if (command.Variables != null)
        {
            foreach (var kvp in command.Variables)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    throw new ValidationException("Variable key không được để trống.");
                }
            }
        }
    }

    private static bool IsSerializableTransactionConflict(Exception ex)
    {
        Exception current = ex;
        while (current != null)
        {
            if (current is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static string SerializeDictionary(Dictionary<string, string> dict)
    {
        if (dict == null || dict.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

}
