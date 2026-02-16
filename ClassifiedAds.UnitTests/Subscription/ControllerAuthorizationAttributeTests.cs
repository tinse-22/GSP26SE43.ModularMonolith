using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Controllers;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ClassifiedAds.UnitTests.Subscription;

public class ControllerAuthorizationAttributeTests
{
    public static IEnumerable<object[]> ProtectedEndpoints()
    {
        yield return Endpoint(typeof(PlansController), "Get", new[] { typeof(bool?), typeof(string) }, Permissions.GetPlans);
        yield return Endpoint(typeof(PlansController), "Get", new[] { typeof(Guid) }, Permissions.GetPlans);
        yield return Endpoint(typeof(PlansController), "Post", new[] { typeof(CreateUpdatePlanModel) }, Permissions.AddPlan);
        yield return Endpoint(typeof(PlansController), "Put", new[] { typeof(Guid), typeof(CreateUpdatePlanModel) }, Permissions.UpdatePlan);
        yield return Endpoint(typeof(PlansController), "Delete", new[] { typeof(Guid) }, Permissions.DeletePlan);
        yield return Endpoint(typeof(PlansController), "GetAuditLogs", new[] { typeof(Guid) }, Permissions.GetPlanAuditLogs);

        yield return Endpoint(typeof(SubscriptionsController), "Get", new[] { typeof(Guid) }, Permissions.GetSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "GetPlans", new[] { typeof(bool?), typeof(string) }, Permissions.GetPlans);
        yield return Endpoint(typeof(SubscriptionsController), "GetCurrentByUser", new[] { typeof(Guid) }, Permissions.GetCurrentSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "GetCurrent", Type.EmptyTypes, Permissions.GetCurrentSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "Post", new[] { typeof(CreateUpdateSubscriptionModel) }, Permissions.AddSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "Put", new[] { typeof(Guid), typeof(CreateUpdateSubscriptionModel) }, Permissions.UpdateSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "Cancel", new[] { typeof(Guid), typeof(CancelSubscriptionModel) }, Permissions.CancelSubscription);
        yield return Endpoint(typeof(SubscriptionsController), "GetHistory", new[] { typeof(Guid) }, Permissions.GetSubscriptionHistory);
        yield return Endpoint(typeof(SubscriptionsController), "GetPayments", new[] { typeof(Guid), typeof(PaymentStatus?) }, Permissions.GetPaymentTransactions);
        yield return Endpoint(typeof(SubscriptionsController), "AddPayment", new[] { typeof(Guid), typeof(AddPaymentTransactionModel) }, Permissions.AddPaymentTransaction);
        yield return Endpoint(typeof(SubscriptionsController), "GetUsage", new[] { typeof(Guid), typeof(DateOnly?), typeof(DateOnly?) }, Permissions.GetUsageTracking);
        yield return Endpoint(typeof(SubscriptionsController), "UpsertUsage", new[] { typeof(Guid), typeof(UpsertUsageTrackingModel) }, Permissions.UpdateUsageTracking);

        yield return Endpoint(typeof(PaymentsController), "Subscribe", new[] { typeof(Guid), typeof(CreateSubscriptionPaymentModel), typeof(CancellationToken) }, Permissions.CreateSubscriptionPayment);
        yield return Endpoint(typeof(PaymentsController), "GetPlans", new[] { typeof(bool?), typeof(string), typeof(CancellationToken) }, Permissions.GetPlans);
        yield return Endpoint(typeof(PaymentsController), "Get", new[] { typeof(Guid), typeof(CancellationToken) }, Permissions.GetPaymentIntent);
        yield return Endpoint(typeof(PaymentsController), "CreatePayOsCheckout", new[] { typeof(PayOsCheckoutRequestModel), typeof(CancellationToken) }, Permissions.CreatePayOsCheckout);
        yield return Endpoint(typeof(PaymentsController), "CheckPaymentStatus", new[] { typeof(Guid), typeof(CancellationToken) }, Permissions.GetPaymentIntent);
        yield return Endpoint(typeof(PaymentsController), "SyncPaymentFromPayOs", new[] { typeof(Guid), typeof(CancellationToken) }, Permissions.SyncPayment);
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public void Endpoint_ShouldRequireExpectedPermissionPolicy(
        Type controllerType,
        string methodName,
        Type[] parameterTypes,
        string expectedPolicy)
    {
        // Arrange
        var method = controllerType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        // Assert
        method.Should().NotBeNull($"{controllerType.Name}.{methodName} should exist");

        var authorizePolicy = method!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Policy));

        authorizePolicy.Should().NotBeNull($"{controllerType.Name}.{methodName} should define [Authorize(policy)]");
        authorizePolicy!.Policy.Should().Be(expectedPolicy);
    }

    [Fact]
    public void PaymentsWebhookEndpoints_ShouldAllowAnonymous()
    {
        // Arrange
        var webhookMethod = typeof(PaymentsController).GetMethod(
            "PayOsWebhook",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(CancellationToken) },
            null);

        var returnMethod = typeof(PaymentsController).GetMethod(
            "PayOsReturn",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(CancellationToken) },
            null);

        // Assert
        webhookMethod.Should().NotBeNull();
        returnMethod.Should().NotBeNull();

        webhookMethod!.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Should().NotBeEmpty();
        returnMethod!.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Should().NotBeEmpty();
    }

    [Fact]
    public void Controllers_ShouldRequireAuthorizeAtClassLevel()
    {
        typeof(PlansController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty();
        typeof(SubscriptionsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty();
        typeof(PaymentsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty();
    }

    private static object[] Endpoint(Type controllerType, string methodName, Type[] parameterTypes, string expectedPolicy)
    {
        return new object[] { controllerType, methodName, parameterTypes, expectedPolicy };
    }
}
