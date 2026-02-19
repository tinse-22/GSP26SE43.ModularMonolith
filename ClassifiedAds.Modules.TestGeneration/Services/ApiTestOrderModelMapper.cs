using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public static class ApiTestOrderModelMapper
{
    public static ApiTestOrderProposalModel ToModel(
        TestOrderProposal proposal,
        IApiTestOrderService apiTestOrderService)
    {
        if (proposal == null)
        {
            return null;
        }

        return new ApiTestOrderProposalModel
        {
            ProposalId = proposal.Id,
            TestSuiteId = proposal.TestSuiteId,
            ProposalNumber = proposal.ProposalNumber,
            Status = proposal.Status,
            Source = proposal.Source,
            ProposedOrder = apiTestOrderService.DeserializeOrderJson(proposal.ProposedOrder).ToList(),
            UserModifiedOrder = DeserializeNullableOrderJson(proposal.UserModifiedOrder, apiTestOrderService),
            AppliedOrder = DeserializeNullableOrderJson(proposal.AppliedOrder, apiTestOrderService),
            AiReasoning = proposal.AiReasoning,
            ConsideredFactors = DeserializeJsonObject(proposal.ConsideredFactors),
            ReviewedById = proposal.ReviewedById,
            ReviewedAt = proposal.ReviewedAt,
            ReviewNotes = proposal.ReviewNotes,
            AppliedAt = proposal.AppliedAt,
            RowVersion = ToRowVersion(proposal.RowVersion),
        };
    }

    private static List<ApiOrderItemModel> DeserializeNullableOrderJson(
        string json,
        IApiTestOrderService apiTestOrderService)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var items = apiTestOrderService.DeserializeOrderJson(json);
        return items.Count == 0 ? null : items.ToList();
    }

    public static byte[] ParseRowVersion(string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new ValidationException("rowVersion la bat buoc.");
        }

        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException ex)
        {
            throw new ValidationException("rowVersion khong dung dinh dang base64.", ex);
        }
    }

    public static string ToRowVersion(byte[] rowVersion)
    {
        if (rowVersion == null || rowVersion.Length == 0)
        {
            return null;
        }

        return Convert.ToBase64String(rowVersion);
    }

    private static object DeserializeJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(value);
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
