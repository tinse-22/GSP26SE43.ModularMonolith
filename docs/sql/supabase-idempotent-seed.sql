-- Idempotent seed script for Supabase (pooler-safe execution path)
-- Scope: identity, configuration, subscription schemas

BEGIN;

-- -----------------------------------------------------------------------------
-- identity.Roles
-- -----------------------------------------------------------------------------
INSERT INTO identity."Roles" (
    "Id",
    "Name",
    "NormalizedName",
    "ConcurrencyStamp",
    "CreatedDateTime"
)
VALUES
    (
        '00000000-0000-0000-0000-000000000001'::uuid,
        'Admin',
        'ADMIN',
        '00000000-0000-0000-0000-000000000001',
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    ),
    (
        '00000000-0000-0000-0000-000000000002'::uuid,
        'User',
        'USER',
        '00000000-0000-0000-0000-000000000002',
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    )
ON CONFLICT ("Id") DO UPDATE
SET
    "Name" = EXCLUDED."Name",
    "NormalizedName" = EXCLUDED."NormalizedName",
    "ConcurrencyStamp" = EXCLUDED."ConcurrencyStamp";

-- -----------------------------------------------------------------------------
-- identity.Users
-- -----------------------------------------------------------------------------
INSERT INTO identity."Users" (
    "Id",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "ConcurrencyStamp",
    "SecurityStamp",
    "LockoutEnabled",
    "LockoutEnd",
    "AccessFailedCount",
    "Auth0UserId",
    "AzureAdB2CUserId",
    "CreatedDateTime"
)
VALUES
    (
        '00000000-0000-0000-0000-000000000001'::uuid,
        'tinvtse@gmail.com',
        'TINVTSE@GMAIL.COM',
        'tinvtse@gmail.com',
        'TINVTSE@GMAIL.COM',
        TRUE,
        'AQAAAAIAAYagAAAAEPfBZpUxae9Dzcv3f2lA5qOYSbJhxh5oYiVhS+j9Q7Rppm2ETqZUaEhWsOYisFocEA==',
        NULL,
        FALSE,
        FALSE,
        'c8554266-b401-4519-9aeb-a9283053fc58',
        'VVPCRDAS3MJWQD5CSW2GWPRADBXEZINA',
        TRUE,
        NULL,
        0,
        NULL,
        NULL,
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    ),
    (
        '00000000-0000-0000-0000-000000000002'::uuid,
        'user@example.com',
        'USER@EXAMPLE.COM',
        'user@example.com',
        'USER@EXAMPLE.COM',
        TRUE,
        'AQAAAAIAAYagAAAAEDlFqrwIpQDVVwXus3MatUkO1o3wq0iBqGqnXu5DkliD+ic2jmEAvoCCLoonjCzPdA==',
        NULL,
        FALSE,
        FALSE,
        'd9665377-c512-5620-0bfc-b0394064gd69',
        'XYZPCRDAS3MJWQD5CSW2GWPRADBXEZIN',
        TRUE,
        NULL,
        0,
        NULL,
        NULL,
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    )
ON CONFLICT ("Id") DO UPDATE
SET
    "UserName" = EXCLUDED."UserName",
    "NormalizedUserName" = EXCLUDED."NormalizedUserName",
    "Email" = EXCLUDED."Email",
    "NormalizedEmail" = EXCLUDED."NormalizedEmail",
    "EmailConfirmed" = EXCLUDED."EmailConfirmed",
    "PasswordHash" = EXCLUDED."PasswordHash",
    "PhoneNumber" = EXCLUDED."PhoneNumber",
    "PhoneNumberConfirmed" = EXCLUDED."PhoneNumberConfirmed",
    "TwoFactorEnabled" = EXCLUDED."TwoFactorEnabled",
    "ConcurrencyStamp" = EXCLUDED."ConcurrencyStamp",
    "SecurityStamp" = EXCLUDED."SecurityStamp",
    "LockoutEnabled" = EXCLUDED."LockoutEnabled",
    "LockoutEnd" = EXCLUDED."LockoutEnd",
    "AccessFailedCount" = EXCLUDED."AccessFailedCount",
    "Auth0UserId" = EXCLUDED."Auth0UserId",
    "AzureAdB2CUserId" = EXCLUDED."AzureAdB2CUserId";

-- -----------------------------------------------------------------------------
-- identity.UserProfiles
-- -----------------------------------------------------------------------------
INSERT INTO identity."UserProfiles" (
    "Id",
    "UserId",
    "DisplayName",
    "AvatarUrl",
    "Timezone",
    "CreatedDateTime"
)
VALUES
    (
        '00000000-0000-0000-0000-000000000001'::uuid,
        '00000000-0000-0000-0000-000000000001'::uuid,
        'System Administrator',
        NULL,
        'Asia/Ho_Chi_Minh',
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    )
ON CONFLICT ("UserId") DO UPDATE
SET
    "DisplayName" = EXCLUDED."DisplayName",
    "AvatarUrl" = EXCLUDED."AvatarUrl",
    "Timezone" = EXCLUDED."Timezone";

-- -----------------------------------------------------------------------------
-- identity.UserRoles
-- -----------------------------------------------------------------------------
WITH seed_user_roles AS (
    SELECT *
    FROM (
        VALUES
            (
                '00000000-0000-0000-0000-000000000001'::uuid,
                '00000000-0000-0000-0000-000000000001'::uuid,
                '00000000-0000-0000-0000-000000000001'::uuid
            ),
            (
                '00000000-0000-0000-0000-000000000002'::uuid,
                '00000000-0000-0000-0000-000000000002'::uuid,
                '00000000-0000-0000-0000-000000000002'::uuid
            )
    ) AS v(id, user_id, role_id)
)
INSERT INTO identity."UserRoles" (
    "Id",
    "UserId",
    "RoleId",
    "CreatedDateTime"
)
SELECT
    v.id,
    v.user_id,
    v.role_id,
    TIMESTAMPTZ '0001-01-01 00:00:00+00'
FROM seed_user_roles v
WHERE NOT EXISTS (
    SELECT 1
    FROM identity."UserRoles" ur
    WHERE ur."UserId" = v.user_id
      AND ur."RoleId" = v.role_id
)
ON CONFLICT ("Id") DO UPDATE
SET
    "UserId" = EXCLUDED."UserId",
    "RoleId" = EXCLUDED."RoleId";

-- -----------------------------------------------------------------------------
-- identity.RoleClaims (Admin)
-- -----------------------------------------------------------------------------
WITH admin_permissions AS (
    SELECT *
    FROM (
        VALUES
            (1, 'Permission:GetRoles'),
            (2, 'Permission:GetRole'),
            (3, 'Permission:AddRole'),
            (4, 'Permission:UpdateRole'),
            (5, 'Permission:DeleteRole'),
            (6, 'Permission:GetUsers'),
            (7, 'Permission:GetUser'),
            (8, 'Permission:AddUser'),
            (9, 'Permission:UpdateUser'),
            (10, 'Permission:SetPassword'),
            (11, 'Permission:DeleteUser'),
            (12, 'Permission:SendResetPasswordEmail'),
            (13, 'Permission:SendConfirmationEmailAddressEmail'),
            (14, 'Permission:AssignRole'),
            (15, 'Permission:RemoveRole'),
            (16, 'Permission:LockUser'),
            (17, 'Permission:UnlockUser'),
            (18, 'Permission:GetConfigurationEntries'),
            (19, 'Permission:GetConfigurationEntry'),
            (20, 'Permission:AddConfigurationEntry'),
            (21, 'Permission:UpdateConfigurationEntry'),
            (22, 'Permission:DeleteConfigurationEntry'),
            (23, 'Permission:ExportConfigurationEntries'),
            (24, 'Permission:ImportConfigurationEntries'),
            (25, 'Permission:GetAuditLogs'),
            (26, 'Permission:GetProjects'),
            (27, 'Permission:AddProject'),
            (28, 'Permission:UpdateProject'),
            (29, 'Permission:DeleteProject'),
            (30, 'Permission:ArchiveProject'),
            (31, 'Permission:GetSpecifications'),
            (32, 'Permission:AddSpecification'),
            (33, 'Permission:UpdateSpecification'),
            (34, 'Permission:DeleteSpecification'),
            (35, 'Permission:ActivateSpecification'),
            (36, 'Permission:GetEndpoints'),
            (37, 'Permission:AddEndpoint'),
            (38, 'Permission:UpdateEndpoint'),
            (39, 'Permission:DeleteEndpoint'),
            (40, 'Permission:GetFiles'),
            (41, 'Permission:UploadFile'),
            (42, 'Permission:GetFile'),
            (43, 'Permission:DownloadFile'),
            (44, 'Permission:UpdateFile'),
            (45, 'Permission:DeleteFile'),
            (46, 'Permission:GetFileAuditLogs'),
            (47, 'Permission:GetTestSuites'),
            (48, 'Permission:AddTestSuite'),
            (49, 'Permission:UpdateTestSuite'),
            (50, 'Permission:DeleteTestSuite'),
            (51, 'Permission:ProposeTestOrder'),
            (52, 'Permission:GetTestOrderProposal'),
            (53, 'Permission:ReorderTestOrder'),
            (54, 'Permission:ApproveTestOrder'),
            (55, 'Permission:GenerateTestCases'),
            (56, 'Permission:GetTestCases'),
            (57, 'Permission:GenerateBoundaryNegativeTestCases'),
            (58, 'Permission:AddTestCase'),
            (59, 'Permission:UpdateTestCase'),
            (60, 'Permission:DeleteTestCase'),
            (61, 'Permission:GetExecutionEnvironments'),
            (62, 'Permission:AddExecutionEnvironment'),
            (63, 'Permission:UpdateExecutionEnvironment'),
            (64, 'Permission:DeleteExecutionEnvironment'),
            (65, 'Permission:StartTestRun'),
            (66, 'Permission:GetTestRuns'),
            (67, 'Permission:GetPlans'),
            (68, 'Permission:AddPlan'),
            (69, 'Permission:UpdatePlan'),
            (70, 'Permission:DeletePlan'),
            (71, 'Permission:GetPlanAuditLogs'),
            (72, 'Permission:GetSubscription'),
            (73, 'Permission:GetCurrentSubscription'),
            (74, 'Permission:AddSubscription'),
            (75, 'Permission:UpdateSubscription'),
            (76, 'Permission:CancelSubscription'),
            (77, 'Permission:GetSubscriptionHistory'),
            (78, 'Permission:GetPaymentTransactions'),
            (79, 'Permission:AddPaymentTransaction'),
            (80, 'Permission:GetUsageTracking'),
            (81, 'Permission:UpdateUsageTracking'),
            (82, 'Permission:CreateSubscriptionPayment'),
            (83, 'Permission:GetPaymentIntent'),
            (84, 'Permission:CreatePayOsCheckout'),
            (85, 'Permission:SyncPayment')
    ) AS v(ordinal, permission)
)
INSERT INTO identity."RoleClaims" (
    "Id",
    "RoleId",
    "Type",
    "Value",
    "CreatedDateTime"
)
SELECT
    ('00000000-0000-0000-0001-' || LPAD(ordinal::text, 12, '0'))::uuid,
    '00000000-0000-0000-0000-000000000001'::uuid,
    'Permission',
    permission,
    TIMESTAMPTZ '0001-01-01 00:00:00+00'
FROM admin_permissions
ON CONFLICT ("Id") DO UPDATE
SET
    "RoleId" = EXCLUDED."RoleId",
    "Type" = EXCLUDED."Type",
    "Value" = EXCLUDED."Value";

-- -----------------------------------------------------------------------------
-- identity.RoleClaims (User)
-- -----------------------------------------------------------------------------
WITH user_permissions AS (
    SELECT *
    FROM (
        VALUES
            (1, 'Permission:GetProjects'),
            (2, 'Permission:AddProject'),
            (3, 'Permission:UpdateProject'),
            (4, 'Permission:DeleteProject'),
            (5, 'Permission:ArchiveProject'),
            (6, 'Permission:GetSpecifications'),
            (7, 'Permission:AddSpecification'),
            (8, 'Permission:UpdateSpecification'),
            (9, 'Permission:DeleteSpecification'),
            (10, 'Permission:ActivateSpecification'),
            (11, 'Permission:GetEndpoints'),
            (12, 'Permission:AddEndpoint'),
            (13, 'Permission:UpdateEndpoint'),
            (14, 'Permission:DeleteEndpoint'),
            (15, 'Permission:GetTestSuites'),
            (16, 'Permission:AddTestSuite'),
            (17, 'Permission:UpdateTestSuite'),
            (18, 'Permission:DeleteTestSuite'),
            (19, 'Permission:ProposeTestOrder'),
            (20, 'Permission:GetTestOrderProposal'),
            (21, 'Permission:ReorderTestOrder'),
            (22, 'Permission:ApproveTestOrder'),
            (23, 'Permission:GenerateTestCases'),
            (24, 'Permission:GetTestCases'),
            (25, 'Permission:GenerateBoundaryNegativeTestCases'),
            (26, 'Permission:AddTestCase'),
            (27, 'Permission:UpdateTestCase'),
            (28, 'Permission:DeleteTestCase'),
            (29, 'Permission:GetExecutionEnvironments'),
            (30, 'Permission:AddExecutionEnvironment'),
            (31, 'Permission:UpdateExecutionEnvironment'),
            (32, 'Permission:DeleteExecutionEnvironment'),
            (33, 'Permission:StartTestRun'),
            (34, 'Permission:GetTestRuns'),
            (35, 'Permission:GetFiles'),
            (36, 'Permission:UploadFile'),
            (37, 'Permission:GetFile'),
            (38, 'Permission:DownloadFile'),
            (39, 'Permission:UpdateFile'),
            (40, 'Permission:DeleteFile'),
            (41, 'Permission:GetFileAuditLogs'),
            (42, 'Permission:GetPlans'),
            (43, 'Permission:GetSubscription'),
            (44, 'Permission:GetCurrentSubscription'),
            (45, 'Permission:CancelSubscription'),
            (46, 'Permission:GetSubscriptionHistory'),
            (47, 'Permission:GetPaymentTransactions'),
            (48, 'Permission:GetUsageTracking'),
            (49, 'Permission:CreateSubscriptionPayment'),
            (50, 'Permission:GetPaymentIntent'),
            (51, 'Permission:CreatePayOsCheckout')
    ) AS v(ordinal, permission)
)
INSERT INTO identity."RoleClaims" (
    "Id",
    "RoleId",
    "Type",
    "Value",
    "CreatedDateTime"
)
SELECT
    ('00000000-0000-0000-0002-' || LPAD(ordinal::text, 12, '0'))::uuid,
    '00000000-0000-0000-0000-000000000002'::uuid,
    'Permission',
    permission,
    TIMESTAMPTZ '0001-01-01 00:00:00+00'
FROM user_permissions
ON CONFLICT ("Id") DO UPDATE
SET
    "RoleId" = EXCLUDED."RoleId",
    "Type" = EXCLUDED."Type",
    "Value" = EXCLUDED."Value";

-- -----------------------------------------------------------------------------
-- configuration.ConfigurationEntries
-- -----------------------------------------------------------------------------
INSERT INTO configuration."ConfigurationEntries" (
    "Id",
    "Key",
    "Value",
    "Description",
    "IsSensitive",
    "CreatedDateTime"
)
VALUES
    (
        '8a051aa5-bcd1-ea11-b098-ac728981bd15'::uuid,
        'SecurityHeaders:Test-Read-From-SqlServer',
        'this-is-read-from-sqlserver',
        NULL,
        FALSE,
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    )
ON CONFLICT ("Id") DO UPDATE
SET
    "Key" = EXCLUDED."Key",
    "Value" = EXCLUDED."Value",
    "Description" = EXCLUDED."Description",
    "IsSensitive" = EXCLUDED."IsSensitive";

-- -----------------------------------------------------------------------------
-- configuration.LocalizationEntries
-- -----------------------------------------------------------------------------
INSERT INTO configuration."LocalizationEntries" (
    "Id",
    "Name",
    "Value",
    "Culture",
    "Description",
    "CreatedDateTime"
)
VALUES
    (
        '29a4aacb-4ddf-4f85-aced-c5283a8bdd7f'::uuid,
        'Test',
        'Test',
        'en-US',
        NULL,
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    ),
    (
        '5a262d8a-b0d9-45d3-8c0e-18b2c882b9fe'::uuid,
        'Test',
        'Kiem Tra',
        'vi-VN',
        NULL,
        TIMESTAMPTZ '0001-01-01 00:00:00+00'
    )
ON CONFLICT ("Id") DO UPDATE
SET
    "Name" = EXCLUDED."Name",
    "Value" = EXCLUDED."Value",
    "Culture" = EXCLUDED."Culture",
    "Description" = EXCLUDED."Description";

-- -----------------------------------------------------------------------------
-- subscription.SubscriptionPlans
-- -----------------------------------------------------------------------------
WITH seed_plans AS (
    SELECT *
    FROM (
        VALUES
            (
                '10000000-0000-0000-0000-000000000001'::uuid,
                'Free',
                'Free',
                'Basic plan for individuals getting started',
                0::numeric,
                0::numeric,
                'VND',
                TRUE,
                1
            ),
            (
                '10000000-0000-0000-0000-000000000002'::uuid,
                'Pro',
                'Professional',
                'Professional plan for growing teams',
                299000::numeric,
                2990000::numeric,
                'VND',
                TRUE,
                2
            ),
            (
                '10000000-0000-0000-0000-000000000003'::uuid,
                'Enterprise',
                'Enterprise',
                'Enterprise plan for large organizations',
                999000::numeric,
                9990000::numeric,
                'VND',
                TRUE,
                3
            )
    ) AS v(id, name, display_name, description, price_monthly, price_yearly, currency, is_active, sort_order)
)
INSERT INTO subscription."SubscriptionPlans" (
    "Id",
    "Name",
    "DisplayName",
    "Description",
    "PriceMonthly",
    "PriceYearly",
    "Currency",
    "IsActive",
    "SortOrder",
    "CreatedDateTime"
)
SELECT
    id,
    name,
    display_name,
    description,
    price_monthly,
    price_yearly,
    currency,
    is_active,
    sort_order,
    TIMESTAMPTZ '0001-01-01 00:00:00+00'
FROM seed_plans
ON CONFLICT ("Name") DO UPDATE
SET
    "DisplayName" = EXCLUDED."DisplayName",
    "Description" = EXCLUDED."Description",
    "PriceMonthly" = EXCLUDED."PriceMonthly",
    "PriceYearly" = EXCLUDED."PriceYearly",
    "Currency" = EXCLUDED."Currency",
    "IsActive" = EXCLUDED."IsActive",
    "SortOrder" = EXCLUDED."SortOrder";

-- -----------------------------------------------------------------------------
-- subscription.PlanLimits
-- LimitType enum values:
--   0 MaxProjects
--   1 MaxEndpointsPerProject
--   2 MaxTestCasesPerSuite
--   3 MaxTestRunsPerMonth
--   4 MaxConcurrentRuns
--   5 RetentionDays
--   6 MaxLlmCallsPerMonth
--   7 MaxStorageMB
-- -----------------------------------------------------------------------------
WITH limit_seed AS (
    SELECT *
    FROM (
        VALUES
            -- Free
            ('20000000-0000-0000-0000-000000000001'::uuid, 'Free', 0, 1, FALSE),
            ('20000000-0000-0000-0000-000000000002'::uuid, 'Free', 1, 10, FALSE),
            ('20000000-0000-0000-0000-000000000003'::uuid, 'Free', 2, 20, FALSE),
            ('20000000-0000-0000-0000-000000000004'::uuid, 'Free', 3, 50, FALSE),
            ('20000000-0000-0000-0000-000000000005'::uuid, 'Free', 4, 1, FALSE),
            ('20000000-0000-0000-0000-000000000006'::uuid, 'Free', 5, 7, FALSE),
            ('20000000-0000-0000-0000-000000000007'::uuid, 'Free', 6, 10, FALSE),
            ('20000000-0000-0000-0000-000000000008'::uuid, 'Free', 7, 100, FALSE),

            -- Pro
            ('20000000-0000-0000-0000-000000000011'::uuid, 'Pro', 0, 10, FALSE),
            ('20000000-0000-0000-0000-000000000012'::uuid, 'Pro', 1, 50, FALSE),
            ('20000000-0000-0000-0000-000000000013'::uuid, 'Pro', 2, 100, FALSE),
            ('20000000-0000-0000-0000-000000000014'::uuid, 'Pro', 3, 500, FALSE),
            ('20000000-0000-0000-0000-000000000015'::uuid, 'Pro', 4, 3, FALSE),
            ('20000000-0000-0000-0000-000000000016'::uuid, 'Pro', 5, 30, FALSE),
            ('20000000-0000-0000-0000-000000000017'::uuid, 'Pro', 6, 100, FALSE),
            ('20000000-0000-0000-0000-000000000018'::uuid, 'Pro', 7, 1000, FALSE),

            -- Enterprise
            ('20000000-0000-0000-0000-000000000021'::uuid, 'Enterprise', 0, NULL, TRUE),
            ('20000000-0000-0000-0000-000000000022'::uuid, 'Enterprise', 1, NULL, TRUE),
            ('20000000-0000-0000-0000-000000000023'::uuid, 'Enterprise', 2, NULL, TRUE),
            ('20000000-0000-0000-0000-000000000024'::uuid, 'Enterprise', 3, NULL, TRUE),
            ('20000000-0000-0000-0000-000000000025'::uuid, 'Enterprise', 4, 10, FALSE),
            ('20000000-0000-0000-0000-000000000026'::uuid, 'Enterprise', 5, 365, FALSE),
            ('20000000-0000-0000-0000-000000000027'::uuid, 'Enterprise', 6, NULL, TRUE),
            ('20000000-0000-0000-0000-000000000028'::uuid, 'Enterprise', 7, NULL, TRUE)
    ) AS v(id, plan_name, limit_type, limit_value, is_unlimited)
),
resolved_plan_ids AS (
    SELECT
        sp."Id" AS plan_id,
        sp."Name" AS plan_name
    FROM subscription."SubscriptionPlans" sp
    WHERE sp."Name" IN ('Free', 'Pro', 'Enterprise')
)
INSERT INTO subscription."PlanLimits" (
    "Id",
    "PlanId",
    "LimitType",
    "LimitValue",
    "IsUnlimited",
    "CreatedDateTime"
)
SELECT
    ls.id,
    rp.plan_id,
    ls.limit_type,
    ls.limit_value,
    ls.is_unlimited,
    TIMESTAMPTZ '0001-01-01 00:00:00+00'
FROM limit_seed ls
JOIN resolved_plan_ids rp ON rp.plan_name = ls.plan_name
ON CONFLICT ("PlanId", "LimitType") DO UPDATE
SET
    "LimitValue" = EXCLUDED."LimitValue",
    "IsUnlimited" = EXCLUDED."IsUnlimited";

COMMIT;