---
name: hostedservices
description: "Skill for the HostedServices area of GSP26SE43.ModularMonolith. 22 symbols across 12 files."
---

# HostedServices

22 symbols | 12 files | Cohesion: 79%

## When to Use

- Working with code in `ClassifiedAds.Infrastructure/`
- Understanding how PublishEventsCommand, PublishEventsCommand, User work
- Modifying hostedservices-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs` | StartAsync, EnsureRoleAsync, EnsureUserAsync, RedactEmail |
| `ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs` | OnStarted, OnStopping, OnStopped, GetMessagePrefix |
| `ClassifiedAds.Modules.Subscription/HostedServices/PublishEventWorker.cs` | ExecuteAsync, DoWork |
| `ClassifiedAds.Modules.Storage/HostedServices/PublishEventWorker.cs` | ExecuteAsync, DoWork |
| `ClassifiedAds.Modules.ApiDocumentation/HostedServices/PublishEventWorker.cs` | ExecuteAsync, DoWork |
| `ClassifiedAds.Infrastructure/HostedServices/CronJobBackgroundService.cs` | ExecuteAsync, DoWork |
| `ClassifiedAds.Modules.Subscription/Commands/PublishEventsCommand.cs` | PublishEventsCommand |
| `ClassifiedAds.Modules.Storage/Commands/PublishEventsCommand.cs` | PublishEventsCommand |
| `ClassifiedAds.Application/FeatureToggles/IOutboxPublishingToggle.cs` | IsEnabled |
| `ClassifiedAds.Infrastructure/FeatureToggles/OutboxPublishingToggle/FileBasedOutboxPublishingToggle.cs` | IsEnabled |

## Entry Points

Start here when exploring this area:

- **`PublishEventsCommand`** (Class) — `ClassifiedAds.Modules.Subscription/Commands/PublishEventsCommand.cs:13`
- **`PublishEventsCommand`** (Class) — `ClassifiedAds.Modules.Storage/Commands/PublishEventsCommand.cs:13`
- **`User`** (Class) — `ClassifiedAds.Modules.Identity/Entities/User.cs:6`
- **`IsEnabled`** (Method) — `ClassifiedAds.Infrastructure/FeatureToggles/OutboxPublishingToggle/FileBasedOutboxPublishingToggle.cs:16`
- **`StartAsync`** (Method) — `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs:37`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `PublishEventsCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/PublishEventsCommand.cs` | 13 |
| `PublishEventsCommand` | Class | `ClassifiedAds.Modules.Storage/Commands/PublishEventsCommand.cs` | 13 |
| `User` | Class | `ClassifiedAds.Modules.Identity/Entities/User.cs` | 6 |
| `IsEnabled` | Method | `ClassifiedAds.Infrastructure/FeatureToggles/OutboxPublishingToggle/FileBasedOutboxPublishingToggle.cs` | 16 |
| `StartAsync` | Method | `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs` | 37 |
| `Configure` | Method | `ClassifiedAds.Modules.Identity/DbConfigurations/UserConfiguration.cs` | 10 |
| `ExecuteAsync` | Method | `ClassifiedAds.Modules.Subscription/HostedServices/PublishEventWorker.cs` | 26 |
| `DoWork` | Method | `ClassifiedAds.Modules.Subscription/HostedServices/PublishEventWorker.cs` | 32 |
| `ExecuteAsync` | Method | `ClassifiedAds.Modules.Storage/HostedServices/PublishEventWorker.cs` | 26 |
| `DoWork` | Method | `ClassifiedAds.Modules.Storage/HostedServices/PublishEventWorker.cs` | 32 |
| `ExecuteAsync` | Method | `ClassifiedAds.Modules.ApiDocumentation/HostedServices/PublishEventWorker.cs` | 26 |
| `DoWork` | Method | `ClassifiedAds.Modules.ApiDocumentation/HostedServices/PublishEventWorker.cs` | 32 |
| `IsEnabled` | Method | `ClassifiedAds.Application/FeatureToggles/IOutboxPublishingToggle.cs` | 4 |
| `EnsureRoleAsync` | Method | `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs` | 68 |
| `EnsureUserAsync` | Method | `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs` | 90 |
| `RedactEmail` | Method | `ClassifiedAds.Modules.Identity/HostedServices/DevelopmentIdentityBootstrapper.cs` | 167 |
| `OnStarted` | Method | `ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs` | 44 |
| `OnStopping` | Method | `ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs` | 59 |
| `OnStopped` | Method | `ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs` | 65 |
| `GetMessagePrefix` | Method | `ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs` | 71 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Controllers | 3 calls |
| Identity | 1 calls |
| ApiDocumentation | 1 calls |

## How to Explore

1. `gitnexus_context({name: "PublishEventsCommand"})` — see callers and callees
2. `gitnexus_query({query: "hostedservices"})` — find related execution flows
3. Read key files listed above for implementation details
