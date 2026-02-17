using ClassifiedAds.Modules.Subscription.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Subscription
{
    [DbContext(typeof(SubscriptionDbContext))]
    [Migration("20260217103000_ReseedAdminEnterpriseSubscriptionForDev")]
    public partial class ReseedAdminEnterpriseSubscriptionForDev : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    admin_role_id uuid := '00000000-0000-0000-0000-000000000001';
                    enterprise_plan_id uuid := '10000000-0000-0000-0000-000000000003';
                    resolved_enterprise_plan_id uuid;
                    now_utc timestamptz := NOW();
                BEGIN
                    SELECT p."Id"
                    INTO resolved_enterprise_plan_id
                    FROM subscription."SubscriptionPlans" p
                    WHERE p."Id" = enterprise_plan_id;

                    IF resolved_enterprise_plan_id IS NULL THEN
                        SELECT p."Id"
                        INTO resolved_enterprise_plan_id
                        FROM subscription."SubscriptionPlans" p
                        WHERE p."Name" = 'Enterprise'
                        ORDER BY p."SortOrder" DESC, p."CreatedDateTime" ASC
                        LIMIT 1;
                    END IF;

                    IF resolved_enterprise_plan_id IS NULL THEN
                        INSERT INTO subscription."SubscriptionPlans"
                        (
                            "Id",
                            "Name",
                            "DisplayName",
                            "Description",
                            "PriceMonthly",
                            "PriceYearly",
                            "Currency",
                            "IsActive",
                            "SortOrder",
                            "CreatedDateTime",
                            "UpdatedDateTime"
                        )
                        VALUES
                        (
                            enterprise_plan_id,
                            'Enterprise',
                            'Enterprise',
                            'Enterprise plan for large organizations',
                            999000,
                            9990000,
                            'VND',
                            TRUE,
                            3,
                            now_utc,
                            NULL
                        );

                        resolved_enterprise_plan_id := enterprise_plan_id;
                    ELSE
                        UPDATE subscription."SubscriptionPlans"
                        SET "Name" = 'Enterprise',
                            "DisplayName" = 'Enterprise',
                            "Description" = 'Enterprise plan for large organizations',
                            "PriceMonthly" = 999000,
                            "PriceYearly" = 9990000,
                            "Currency" = 'VND',
                            "IsActive" = TRUE,
                            "SortOrder" = GREATEST("SortOrder", 3),
                            "UpdatedDateTime" = now_utc
                        WHERE "Id" = resolved_enterprise_plan_id;
                    END IF;

                    INSERT INTO subscription."PlanLimits"
                    (
                        "Id",
                        "PlanId",
                        "LimitType",
                        "LimitValue",
                        "IsUnlimited",
                        "CreatedDateTime",
                        "UpdatedDateTime"
                    )
                    SELECT
                        gen_random_uuid(),
                        resolved_enterprise_plan_id,
                        v."LimitType",
                        v."LimitValue",
                        v."IsUnlimited",
                        now_utc,
                        NULL
                    FROM
                    (
                        VALUES
                            (0, NULL::integer, TRUE),
                            (1, NULL::integer, TRUE),
                            (2, NULL::integer, TRUE),
                            (3, NULL::integer, TRUE),
                            (4, 10, FALSE),
                            (5, 365, FALSE),
                            (6, NULL::integer, TRUE),
                            (7, NULL::integer, TRUE)
                    ) AS v("LimitType", "LimitValue", "IsUnlimited")
                    ON CONFLICT ("PlanId", "LimitType")
                    DO UPDATE
                    SET "LimitValue" = EXCLUDED."LimitValue",
                        "IsUnlimited" = EXCLUDED."IsUnlimited",
                        "UpdatedDateTime" = now_utc;

                    UPDATE subscription."UserSubscriptions" us
                    SET "PlanId" = resolved_enterprise_plan_id,
                        "Status" = 1,
                        "BillingCycle" = NULL,
                        "EndDate" = NULL,
                        "NextBillingDate" = NULL,
                        "TrialEndsAt" = NULL,
                        "CancelledAt" = NULL,
                        "AutoRenew" = FALSE,
                        "SnapshotPriceMonthly" = p."PriceMonthly",
                        "SnapshotPriceYearly" = p."PriceYearly",
                        "SnapshotCurrency" = p."Currency",
                        "SnapshotPlanName" = COALESCE(p."DisplayName", p."Name"),
                        "UpdatedDateTime" = now_utc
                    FROM subscription."SubscriptionPlans" p
                    WHERE p."Id" = resolved_enterprise_plan_id
                      AND EXISTS
                      (
                          SELECT 1
                          FROM identity."UserRoles" ur
                          WHERE ur."RoleId" = admin_role_id
                            AND ur."UserId" = us."UserId"
                      )
                      AND us."Status" IN (0, 1, 2);

                    INSERT INTO subscription."UserSubscriptions"
                    (
                        "Id",
                        "UserId",
                        "PlanId",
                        "Status",
                        "BillingCycle",
                        "StartDate",
                        "EndDate",
                        "NextBillingDate",
                        "TrialEndsAt",
                        "CancelledAt",
                        "AutoRenew",
                        "ExternalSubId",
                        "ExternalCustId",
                        "SnapshotPriceMonthly",
                        "SnapshotPriceYearly",
                        "SnapshotCurrency",
                        "SnapshotPlanName",
                        "CreatedDateTime",
                        "UpdatedDateTime"
                    )
                    SELECT
                        gen_random_uuid(),
                        admin_users."UserId",
                        resolved_enterprise_plan_id,
                        1,
                        NULL,
                        CURRENT_DATE,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        FALSE,
                        NULL,
                        NULL,
                        p."PriceMonthly",
                        p."PriceYearly",
                        p."Currency",
                        COALESCE(p."DisplayName", p."Name"),
                        now_utc,
                        NULL
                    FROM
                    (
                        SELECT DISTINCT ur."UserId"
                        FROM identity."UserRoles" ur
                        WHERE ur."RoleId" = admin_role_id
                    ) admin_users
                    JOIN subscription."SubscriptionPlans" p ON p."Id" = resolved_enterprise_plan_id
                    WHERE NOT EXISTS
                      (
                          SELECT 1
                          FROM subscription."UserSubscriptions" us
                          WHERE us."UserId" = admin_users."UserId"
                            AND us."Status" IN (0, 1, 2)
                      );
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank: this is dev seed data synchronization.
        }
    }
}
