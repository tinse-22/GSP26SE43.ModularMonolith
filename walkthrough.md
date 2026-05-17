/ DBML converted from the uploaded Mermaid ERD
// Composite unique constraints were inferred from columns marked UK2 in the source.

// ────── IDENTITY ──────
Table Users {
  Id uuid [pk]
  UserName varchar [not null, unique]
  NormalizedUserName varchar [not null, unique]
  Email varchar [not null]
  NormalizedEmail varchar [not null, unique]
  EmailConfirmed boolean [not null]
  PasswordHash text
  PhoneNumber varchar [unique]
  PhoneNumberConfirmed boolean [not null]
  TwoFactorEnabled boolean [not null]
  ConcurrencyStamp varchar [not null]
  SecurityStamp varchar
  LockoutEnabled boolean [not null]
  LockoutEnd timestamptz
  AccessFailedCount integer [not null, note: 'CHK>=0']
  Auth0UserId varchar [unique]
  AzureAdB2CUserId varchar [unique]
  CreatedDateTime timestamptz [not null]
  UpdatedDateTime timestamptz
}

Table UserProfiles {
  Id uuid [pk]
  UserId uuid [not null, unique]
  DisplayName varchar
  AvatarUrl text
  Timezone varchar
  CreatedDateTime timestamptz [not null]
}

Table PasswordHistories {
  Id uuid [pk]
  UserId uuid [not null]
  PasswordHash text [not null]
  CreatedDateTime timestamptz [not null]
}

Table Roles {
  Id uuid [pk]
  Name varchar [not null, unique]
  NormalizedName varchar [not null, unique]
  ConcurrencyStamp varchar [not null]
  CreatedDateTime timestamptz [not null]
}

Table UserRoles {
  Id uuid [pk]
  UserId uuid [not null]
  RoleId uuid [not null]
  CreatedDateTime timestamptz [not null]

  Indexes {
    (UserId, RoleId) [unique]
  }
}

Table UserClaims {
  Id uuid [pk]
  UserId uuid [not null]
  Type varchar [not null]
  Value text [not null]
}

Table RoleClaims {
  Id uuid [pk]
  RoleId uuid [not null]
  Type varchar [not null]
  Value text [not null]
}

Table UserTokens {
  Id uuid [pk]
  UserId uuid [not null]
  LoginProvider varchar [not null]
  TokenName varchar [not null]
  TokenValue text [not null]
}

Table UserLogins {
  Id uuid [pk]
  UserId uuid [not null]
  LoginProvider varchar [not null]
  ProviderKey varchar [not null]
  ProviderDisplayName varchar

  Indexes {
    (LoginProvider, ProviderKey) [unique]
  }
}

// ────── SUBSCRIPTION ──────
Table SubscriptionPlans {
  Id uuid [pk]
  Name varchar [not null, unique]
  DisplayName varchar [not null]
  Description text
  PriceMonthly decimal [note: 'CHK>=0']
  PriceYearly decimal [note: 'CHK>=0']
  Currency varchar [not null]
  IsActive boolean [not null]
  SortOrder integer [not null]
  CreatedDateTime timestamptz [not null]
}

Table PlanLimits {
  Id uuid [pk]
  PlanId uuid [not null]
  LimitType integer [not null]
  LimitValue integer [note: 'CHK>0']
  IsUnlimited boolean [not null]
  CreatedDateTime timestamptz [not null]

  Indexes {
    (PlanId, LimitType) [unique]
  }
}

Table UserSubscriptions {
  Id uuid [pk]
  UserId uuid [not null]
  PlanId uuid [not null]
  Status integer [not null]
  BillingCycle integer
  StartDate date [not null]
  EndDate date [note: 'CHK>=StartDate']
  NextBillingDate date
  TrialEndsAt timestamptz
  CancelledAt timestamptz
  AutoRenew boolean [not null]
  ExternalSubId varchar [unique]
  ExternalCustId varchar
  CreatedDateTime timestamptz [not null]
}

Table PaymentIntents {
  Id uuid [pk]
  UserId uuid [not null]
  Amount decimal [not null, note: 'CHK>0']
  Currency varchar [not null]
  Purpose integer [not null]
  PlanId uuid [not null]
  BillingCycle integer [not null]
  SubscriptionId uuid
  Status integer [not null]
  CheckoutUrl text
  ExpiresAt timestamptz [not null]
  OrderCode bigint [unique]
  CreatedDateTime timestamptz [not null]
}

Table SubscriptionHistories {
  Id uuid [pk]
  SubscriptionId uuid [not null]
  OldPlanId uuid
  NewPlanId uuid [not null]
  ChangeType integer [not null]
  ChangeReason text
  EffectiveDate date [not null]
  CreatedDateTime timestamptz [not null]
}

Table PaymentTransactions {
  Id uuid [pk]
  UserId uuid [not null]
  SubscriptionId uuid [not null]
  PaymentIntentId uuid
  Amount decimal [not null, note: 'CHK>0']
  Currency varchar [not null]
  Status integer [not null]
  PaymentMethod varchar [not null]
  Provider varchar
  ProviderRef varchar
  ExternalTxnId varchar [unique]
  InvoiceUrl text
  FailureReason text
  CreatedDateTime timestamptz [not null]

  Indexes {
    (Provider, ProviderRef) [unique]
  }
}

Table UsageTrackings {
  Id uuid [pk]
  UserId uuid [not null]
  PeriodStart date [not null]
  PeriodEnd date [not null, note: 'CHK>=PeriodStart']
  ProjectCount integer [not null, note: 'CHK>=0']
  EndpointCount integer [not null, note: 'CHK>=0']
  TestSuiteCount integer [not null, note: 'CHK>=0']
  TestCaseCount integer [not null, note: 'CHK>=0']
  TestRunCount integer [not null, note: 'CHK>=0']
  LlmCallCount integer [not null, note: 'CHK>=0']
  StorageUsedMB decimal [not null, note: 'CHK>=0']
  CreatedDateTime timestamptz [not null]

  Indexes {
    (UserId, PeriodStart, PeriodEnd) [unique]
  }
}

// ────── API DOCUMENTATION ──────
Table Projects {
  Id uuid [pk]
  OwnerId uuid [not null]
  ActiveSpecId uuid
  Name varchar [not null]
  Description text
  BaseUrl varchar
  Status integer [not null]
  CreatedDateTime timestamptz [not null]
  UpdatedDateTime timestamptz
}

Table ApiSpecifications {
  Id uuid [pk]
  ProjectId uuid [not null]
  OriginalFileId uuid
  Name varchar [not null]
  SourceType integer [not null]
  Version varchar
  IsActive boolean [not null]
  ParsedAt timestamptz
  ParseStatus integer [not null]
  ParseErrors text
  CreatedDateTime timestamptz [not null]
}

Table ApiEndpoints {
  Id uuid [pk]
  ApiSpecId uuid [not null]
  HttpMethod integer [not null]
  Path varchar [not null]
  OperationId varchar
  Summary varchar
  Description text
  Tags jsonb
  IsDeprecated boolean [not null]
  CreatedDateTime timestamptz [not null]

  Indexes {
    (ApiSpecId, HttpMethod, Path) [unique]
  }
}

Table EndpointParameters {
  Id uuid [pk]
  EndpointId uuid [not null]
  Name varchar [not null]
  Location integer [not null]
  DataType varchar
  Format varchar
  IsRequired boolean [not null]
  DefaultValue text
  Schema jsonb
  Examples jsonb
}

Table EndpointResponses {
  Id uuid [pk]
  EndpointId uuid [not null]
  StatusCode integer [not null]
  Description text
  Schema jsonb
  Examples jsonb
  Headers jsonb
}

Table SecuritySchemes {
  Id uuid [pk]
  ApiSpecId uuid [not null]
  Name varchar [not null]
  Type integer [not null]
  Scheme varchar
  BearerFormat varchar
  In integer
  ParameterName varchar
  Configuration jsonb

  Indexes {
    (ApiSpecId, Name) [unique]
  }
}

Table EndpointSecurityReqs {
  Id uuid [pk]
  EndpointId uuid [not null]
  SecurityType integer [not null]
  SchemeName varchar [not null]
  Scopes jsonb
}

// ────── SRS & TRACEABILITY ──────
Table SrsDocuments {
  Id uuid [pk]
  ProjectId uuid [not null]
  TestSuiteId uuid
  Title varchar [not null]
  SourceType integer [not null]
  RawContent text
  StorageFileId uuid
  ParsedMarkdown text
  AnalysisStatus integer [not null]
  AnalyzedAt timestamptz
  CreatedDateTime timestamptz [not null]
}

Table SrsRequirements {
  Id uuid [pk]
  SrsDocumentId uuid [not null]
  RequirementCode varchar [not null]
  Title varchar [not null]
  Description text
  RequirementType integer [not null]
  TestableConstraints jsonb
  Assumptions jsonb
  Ambiguities jsonb
  ConfidenceScore real [note: 'CHK 0-1']
  EndpointId uuid
  MappedEndpointPath varchar
  SortOrder integer [not null, note: 'CHK>=0']
  CreatedDateTime timestamptz [not null]

  Indexes {
    (SrsDocumentId, RequirementCode) [unique]
  }
}

Table SrsRequirementClarifications {
  Id uuid [pk]
  SrsRequirementId uuid [not null]
  AmbiguitySource text [not null]
  Question text [not null]
  SuggestedOptions jsonb
  UserAnswer text
  IsAnswered boolean [not null]
  AnsweredAt timestamptz
  AnsweredById uuid
  ClarificationRound integer [not null, note: 'CHK>=1']
  CreatedDateTime timestamptz [not null]
}

Table SrsAnalysisJobs {
  Id uuid [pk]
  SrsDocumentId uuid [not null]
  Status integer [not null]
  TriggeredById uuid [not null]
  QueuedAt timestamptz [not null]
  TriggeredAt timestamptz
  CompletedAt timestamptz
  RequirementsExtracted integer [note: 'CHK>=0']
  ErrorMessage text
  CreatedDateTime timestamptz [not null]
}

Table TestCaseRequirementLinks {
  Id uuid [pk]
  TestCaseId uuid [not null]
  SrsRequirementId uuid [not null]
  TraceabilityScore real [note: 'CHK 0-1']
  MappingRationale text
  CreatedDateTime timestamptz [not null]

  Indexes {
    (TestCaseId, SrsRequirementId) [unique]
  }
}

// ────── TEST GENERATION ──────
Table TestSuites {
  Id uuid [pk]
  ProjectId uuid [not null]
  ApiSpecId uuid
  Name varchar [not null]
  Description text
  GenerationType integer [not null]
  Status integer [not null]
  CreatedById uuid [not null]
  ApprovalStatus integer [not null]
  ApprovedById uuid
  ApprovedAt timestamptz
  Version integer [not null, note: 'CHK>=1']
  LastModifiedById uuid
  CreatedDateTime timestamptz [not null]
  UpdatedDateTime timestamptz
}

Table TestCases {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  EndpointId uuid
  Name varchar [not null]
  Description text
  TestType integer [not null]
  Priority integer [not null]
  IsEnabled boolean [not null]
  IsDeleted boolean [not null]
  DeletedAt timestamptz
  OrderIndex integer [not null, note: 'CHK>=0']
  CustomOrderIndex integer [note: 'CHK>=0']
  IsOrderCustomized boolean [not null]
  Tags jsonb
  Version integer [not null, note: 'CHK>=1']
  LastModifiedById uuid
  CreatedDateTime timestamptz [not null]
  UpdatedDateTime timestamptz
}

Table TestCaseRequests {
  Id uuid [pk]
  TestCaseId uuid [not null, unique]
  HttpMethod integer [not null]
  Url text [not null]
  Headers jsonb
  PathParams jsonb
  QueryParams jsonb
  BodyType integer [not null]
  Body text
  Timeout integer [not null, note: 'CHK>0']
}

Table TestCaseExpectations {
  Id uuid [pk]
  TestCaseId uuid [not null, unique]
  ExpectedStatus jsonb [not null]
  ResponseSchema jsonb
  HeaderChecks jsonb
  BodyContains jsonb
  BodyNotContains jsonb
  JsonPathChecks jsonb
  MaxResponseTime integer [note: 'CHK>0']
}

Table TestCaseVariables {
  Id uuid [pk]
  TestCaseId uuid [not null]
  VariableName varchar [not null]
  ExtractFrom integer [not null]
  JsonPath varchar
  HeaderName varchar
  Regex varchar
  DefaultValue text
}

Table TestDataSets {
  Id uuid [pk]
  TestCaseId uuid [not null]
  Name varchar [not null]
  Data jsonb [not null]
  IsEnabled boolean [not null]
}

Table TestCaseChangeLogs {
  Id uuid [pk]
  TestCaseId uuid [not null]
  ChangedById uuid [not null]
  ChangeType integer [not null]
  FieldName varchar
  OldValue jsonb
  NewValue jsonb
  ChangeReason text
  VersionAfterChange integer [not null, note: 'CHK>=1']
  IpAddress varchar
  UserAgent text
}

Table TestCaseDependencies {
  Id uuid [pk]
  TestCaseId uuid [not null]
  DependsOnTestCaseId uuid [not null, note: 'CHK!=TestCaseId']

  Indexes {
    (TestCaseId, DependsOnTestCaseId) [unique]
  }
}

Table TestSuiteVersions {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  VersionNumber integer [not null, note: 'CHK>=1']
  ChangedById uuid [not null]
  ChangeType integer [not null]
  ChangeDescription text
  TestCaseOrderSnapshot jsonb
  ApprovalStatusSnapshot integer [not null]
  PreviousState jsonb
  NewState jsonb
  CreatedDateTime timestamptz [not null]
}

Table TestOrderProposals {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  ProposalNumber integer [not null, note: 'CHK>=1']
  Source integer [not null]
  Status integer [not null]
  ProposedOrder jsonb [not null]
  AiReasoning text
  ConsideredFactors text
  ReviewedById uuid
  ReviewedAt timestamptz
  ReviewNotes text
  UserModifiedOrder jsonb
  AppliedOrder jsonb
  AppliedAt timestamptz
  LlmModel varchar
  CreatedDateTime timestamptz [not null]
}

Table TestGenerationJobs {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  ProposalId uuid
  Status integer [not null]
  TriggeredById uuid [not null]
  QueuedAt timestamptz [not null]
  TriggeredAt timestamptz
  CompletedAt timestamptz
  TestCasesGenerated integer [note: 'CHK>=0']
  ErrorMessage text
  CreatedDateTime timestamptz [not null]
}

Table LlmSuggestions {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  EndpointId uuid
  CacheKey varchar
  DisplayOrder integer [not null, note: 'CHK>=0']
  SuggestionType integer [not null]
  TestType integer [not null]
  SuggestedName varchar [not null]
  SuggestedDescription text
  SuggestedRequest jsonb
  SuggestedExpectation jsonb
  SuggestedVariables jsonb
  SuggestedTags jsonb
  Priority integer [not null]
  ReviewStatus integer [not null]
  ReviewedById uuid
  ReviewedAt timestamptz
  IsDeleted boolean [not null]
  DeletedAt timestamptz
  CreatedDateTime timestamptz [not null]
}

Table LlmSuggestionFeedbacks {
  Id uuid [pk]
  SuggestionId uuid [not null]
  TestSuiteId uuid [not null]
  EndpointId uuid
  UserId uuid [not null]
  FeedbackSignal integer [not null]
  Notes text

  Indexes {
    (SuggestionId, UserId) [unique]
  }
}

// ────── TEST EXECUTION & REPORTING ──────
Table ExecutionEnvironments {
  Id uuid [pk]
  ProjectId uuid [not null]
  Name varchar [not null]
  BaseUrl varchar [not null]
  Variables jsonb
  Headers jsonb
  AuthConfig text
  IsDefault boolean [not null]
  CreatedDateTime timestamptz [not null]
  UpdatedDateTime timestamptz

  Indexes {
    (ProjectId, Name) [unique]
  }
}

Table TestRuns {
  Id uuid [pk]
  TestSuiteId uuid [not null]
  EnvironmentId uuid [not null]
  TriggeredById uuid [not null]
  RunNumber integer [not null, note: 'CHK>=1']
  Status integer [not null]
  StartedAt timestamptz
  CompletedAt timestamptz
  TotalTests integer [not null, note: 'CHK>=0']
  PassedCount integer [not null, note: 'CHK>=0']
  FailedCount integer [not null, note: 'CHK>=0']
  SkippedCount integer [not null, note: 'CHK>=0']
  DurationMs bigint [note: 'CHK>=0']
  RedisKey varchar
  ResultsExpireAt timestamptz
  CreatedDateTime timestamptz [not null]
}

Table TestCaseResults {
  Id uuid [pk]
  TestRunId uuid [not null]
  TestCaseId uuid [not null]
  EndpointId uuid
  Name varchar [not null]
  OrderIndex integer [not null, note: 'CHK>=0']
  Status varchar [not null]
  HttpStatusCode integer [note: 'CHK 100-599']
  DurationMs bigint [note: 'CHK>=0']
  ResolvedUrl text
  RequestHeaders jsonb
  CreatedDateTime timestamptz [not null]
}

Table TestReports {
  Id uuid [pk]
  TestRunId uuid [not null]
  GeneratedById uuid [not null]
  FileId uuid [not null]
  ReportType integer [not null]
  Format integer [not null]
  GeneratedAt timestamptz [not null]
  ExpiresAt timestamptz
  CreatedDateTime timestamptz [not null]
}

Table CoverageMetrics {
  Id uuid [pk]
  TestRunId uuid [not null, unique]
  TotalEndpoints integer [not null, note: 'CHK>=0']
  TestedEndpoints integer [not null, note: 'CHK>=0']
  CoveragePercent decimal [not null, note: 'CHK 0-100']
  ByMethod jsonb
  ByTag jsonb
  UncoveredPaths jsonb
  CalculatedAt timestamptz [not null]
  CreatedDateTime timestamptz [not null]
}

// ────── STORAGE & NOTIFICATIONS ──────
Table FileEntries {
  Id uuid [pk]
  OwnerId uuid
  Name varchar [not null]
  Description text
  Size bigint [not null, note: 'CHK>=0']
  UploadedTime timestamptz [not null]
  FileName varchar [not null]
  FileLocation text [not null]
  ContentType varchar [not null]
  FileCategory integer [not null]
  Encrypted boolean [not null]
  EncryptionKey text
  EncryptionIV text
  Archived boolean [not null]
  ArchivedDate timestamptz
  Deleted boolean [not null]
  DeletedDate timestamptz
  ExpiresAt timestamptz
  CreatedDateTime timestamptz [not null]
}

Table EmailMessages {
  Id uuid [pk]
  From varchar
  Tos text
  CCs text
  BCCs text
  Subject varchar
  Body text
  AttemptCount integer [not null, note: 'CHK>=0']
  MaxAttemptCount integer [not null, note: 'CHK>=1']
  NextAttemptDateTime timestamptz
  ExpiredDateTime timestamptz
  Log text
  SentDateTime timestamptz
  CopyFromId uuid
  CreatedDateTime timestamptz [not null]
}

Table EmailMessageAttachments {
  Id uuid [pk]
  EmailMessageId uuid [not null]
  FileEntryId uuid [not null]
  Name varchar [not null]
  CreatedDateTime timestamptz [not null]
}

Table SmsMessages {
  Id uuid [pk]
  Message text
  PhoneNumber varchar
  AttemptCount integer [not null, note: 'CHK>=0']
  MaxAttemptCount integer [not null, note: 'CHK>=1']
  NextAttemptDateTime timestamptz
  ExpiredDateTime timestamptz
  Log text
  SentDateTime timestamptz
  CopyFromId uuid
  CreatedDateTime timestamptz [not null]
}

// ────── LLM ASSISTANT ──────
Table LlmInteractions {
  Id uuid [pk]
  UserId uuid [not null]
  InteractionType integer [not null]
  InputContext text [not null]
  LlmResponse text
  ModelUsed varchar [not null]
  TokensUsed integer [not null, note: 'CHK>=0']
  LatencyMs integer [not null, note: 'CHK>=0']
  CreatedDateTime timestamptz [not null]
}

Table LlmSuggestionCaches {
  Id uuid [pk]
  EndpointId uuid [not null]
  SuggestionType integer [not null]
  CacheKey varchar [not null, unique]
  Suggestions jsonb [not null]
  ExpiresAt timestamptz [not null]
  CreatedDateTime timestamptz [not null]
}

// ────── RELATIONSHIPS ──────
Ref: UserProfiles.UserId - Users.Id
Ref: UserRoles.UserId > Users.Id
Ref: UserClaims.UserId > Users.Id
Ref: UserTokens.UserId > Users.Id
Ref: UserLogins.UserId > Users.Id
Ref: PasswordHistories.UserId > Users.Id
Ref: UserRoles.RoleId > Roles.Id
Ref: RoleClaims.RoleId > Roles.Id
Ref: PlanLimits.PlanId > SubscriptionPlans.Id
Ref: UserSubscriptions.PlanId > SubscriptionPlans.Id
Ref: SubscriptionHistories.SubscriptionId > UserSubscriptions.Id
Ref: PaymentTransactions.SubscriptionId > UserSubscriptions.Id
Ref: PaymentTransactions.PaymentIntentId > PaymentIntents.Id
Ref: UserSubscriptions.UserId > Users.Id
Ref: PaymentIntents.UserId > Users.Id
Ref: PaymentIntents.PlanId > SubscriptionPlans.Id
Ref: PaymentIntents.SubscriptionId > UserSubscriptions.Id
Ref: PaymentTransactions.UserId > Users.Id
Ref: UsageTrackings.UserId > Users.Id
Ref: ApiSpecifications.ProjectId > Projects.Id
Ref: ApiEndpoints.ApiSpecId > ApiSpecifications.Id
Ref: SecuritySchemes.ApiSpecId > ApiSpecifications.Id
Ref: EndpointParameters.EndpointId > ApiEndpoints.Id
Ref: EndpointResponses.EndpointId > ApiEndpoints.Id
Ref: EndpointSecurityReqs.EndpointId > ApiEndpoints.Id
Ref: Projects.OwnerId > Users.Id
Ref: ApiSpecifications.OriginalFileId > FileEntries.Id
Ref: SrsRequirements.SrsDocumentId > SrsDocuments.Id
Ref: SrsAnalysisJobs.SrsDocumentId > SrsDocuments.Id
Ref: SrsRequirementClarifications.SrsRequirementId > SrsRequirements.Id
Ref: TestCaseRequirementLinks.SrsRequirementId > SrsRequirements.Id
Ref: TestCaseRequirementLinks.TestCaseId > TestCases.Id
Ref: SrsDocuments.ProjectId > Projects.Id
Ref: SrsDocuments.StorageFileId > FileEntries.Id
Ref: SrsRequirements.EndpointId > ApiEndpoints.Id
Ref: SrsAnalysisJobs.TriggeredById > Users.Id
Ref: SrsRequirementClarifications.AnsweredById > Users.Id
Ref: TestCases.TestSuiteId > TestSuites.Id
Ref: TestSuiteVersions.TestSuiteId > TestSuites.Id
Ref: TestOrderProposals.TestSuiteId > TestSuites.Id
Ref: TestGenerationJobs.TestSuiteId > TestSuites.Id
Ref: LlmSuggestions.TestSuiteId > TestSuites.Id
Ref: LlmSuggestionFeedbacks.SuggestionId > LlmSuggestions.Id
Ref: TestCaseRequests.TestCaseId - TestCases.Id
Ref: TestCaseExpectations.TestCaseId - TestCases.Id
Ref: TestCaseVariables.TestCaseId > TestCases.Id
Ref: TestDataSets.TestCaseId > TestCases.Id
Ref: TestCaseChangeLogs.TestCaseId > TestCases.Id
Ref: TestCaseDependencies.TestCaseId > TestCases.Id
Ref: TestCaseDependencies.DependsOnTestCaseId > TestCases.Id
Ref: TestSuites.ProjectId > Projects.Id
Ref: TestSuites.ApiSpecId > ApiSpecifications.Id
Ref: TestCases.EndpointId > ApiEndpoints.Id
Ref: TestSuites.CreatedById > Users.Id
Ref: LlmSuggestions.ReviewedById > Users.Id
Ref: LlmSuggestionFeedbacks.UserId > Users.Id
Ref: TestRuns.EnvironmentId > ExecutionEnvironments.Id
Ref: TestCaseResults.TestRunId > TestRuns.Id
Ref: TestReports.TestRunId > TestRuns.Id
Ref: CoverageMetrics.TestRunId - TestRuns.Id
Ref: TestRuns.TestSuiteId > TestSuites.Id
Ref: TestRuns.TriggeredById > Users.Id
Ref: TestCaseResults.TestCaseId > TestCases.Id
Ref: TestCaseResults.EndpointId > ApiEndpoints.Id
Ref: TestReports.GeneratedById > Users.Id
Ref: TestReports.FileId > FileEntries.Id
Ref: EmailMessageAttachments.FileEntryId > FileEntries.Id
Ref: EmailMessageAttachments.EmailMessageId > EmailMessages.Id
Ref: FileEntries.OwnerId > Users.Id
Ref: SubscriptionHistories.OldPlanId > SubscriptionPlans.Id
Ref: SubscriptionHistories.NewPlanId > SubscriptionPlans.Id
Ref: Projects.ActiveSpecId > ApiSpecifications.Id
Ref: SrsDocuments.TestSuiteId > TestSuites.Id
Ref: TestSuites.ApprovedById > Users.Id
Ref: TestSuites.LastModifiedById > Users.Id
Ref: TestCases.LastModifiedById > Users.Id
Ref: TestCaseChangeLogs.ChangedById > Users.Id
Ref: TestSuiteVersions.ChangedById > Users.Id
Ref: TestOrderProposals.ReviewedById > Users.Id
Ref: TestGenerationJobs.ProposalId > TestOrderProposals.Id
Ref: TestGenerationJobs.TriggeredById > Users.Id
Ref: LlmSuggestions.EndpointId > ApiEndpoints.Id
Ref: LlmSuggestionFeedbacks.TestSuiteId > TestSuites.Id
Ref: LlmSuggestionFeedbacks.EndpointId > ApiEndpoints.Id
Ref: ExecutionEnvironments.ProjectId > Projects.Id
Ref: EmailMessages.CopyFromId > EmailMessages.Id
Ref: SmsMessages.CopyFromId > SmsMessages.Id
Ref: LlmInteractions.UserId > Users.Id
Ref: LlmSuggestionCaches.EndpointId > ApiEndpoints.Id
