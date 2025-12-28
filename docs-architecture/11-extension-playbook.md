# 11 - Extension Playbook

> **Purpose**: Step-by-step guides for common development tasks when extending the application.

---

## Table of Contents

- [Adding a New Module](#adding-a-new-module)
- [Adding a New Entity](#adding-a-new-entity)
- [Adding a New Command](#adding-a-new-command)
- [Adding a New Query](#adding-a-new-query)
- [Adding a Domain Event Handler](#adding-a-domain-event-handler)
- [Adding an API Endpoint](#adding-an-api-endpoint)
- [Adding a Background Worker](#adding-a-background-worker)
- [Adding a Message Bus Consumer](#adding-a-message-bus-consumer)
- [Enabling Outbox for a Module](#enabling-outbox-for-a-module)
- [Adding Authorization Permissions](#adding-authorization-permissions)

---

## Adding a New Module

### Step 1: Create Project

```powershell
# Create new project
dotnet new classlib -n ClassifiedAds.Modules.{ModuleName} -f net10.0

# Add to solution
dotnet sln add ClassifiedAds.Modules.{ModuleName}
```

### Step 2: Add Project References

Edit `ClassifiedAds.Modules.{ModuleName}.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClassifiedAds.Application\ClassifiedAds.Application.csproj" />
    <ProjectReference Include="..\ClassifiedAds.Contracts\ClassifiedAds.Contracts.csproj" />
    <ProjectReference Include="..\ClassifiedAds.CrossCuttingConcerns\ClassifiedAds.CrossCuttingConcerns.csproj" />
    <ProjectReference Include="..\ClassifiedAds.Domain\ClassifiedAds.Domain.csproj" />
    <ProjectReference Include="..\ClassifiedAds.Infrastructure\ClassifiedAds.Infrastructure.csproj" />
    <ProjectReference Include="..\ClassifiedAds.Persistence.SqlServer\ClassifiedAds.Persistence.SqlServer.csproj" />
  </ItemGroup>
</Project>
```

### Step 3: Create Folder Structure

```
ClassifiedAds.Modules.{ModuleName}/
├── Authorization/
├── Commands/
├── ConfigurationOptions/
├── Controllers/
├── DbConfigurations/
├── Entities/
├── EventHandlers/
├── Persistence/
├── Queries/
└── ServiceCollectionExtensions.cs
```

### Step 4: Create Configuration Options

```csharp
// ConfigurationOptions/{ModuleName}ModuleOptions.cs
namespace ClassifiedAds.Modules.{ModuleName}.ConfigurationOptions;

public class {ModuleName}ModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }
}

public class ConnectionStringsOptions
{
    public string Default { get; set; }
    public string MigrationsAssembly { get; set; }
    public int? CommandTimeout { get; set; }
}
```

### Step 5: Create DbContext

```csharp
// Persistence/{ModuleName}DbContext.cs
using ClassifiedAds.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ClassifiedAds.Modules.{ModuleName}.Persistence;

public class {ModuleName}DbContext : DbContextUnitOfWork<{ModuleName}DbContext>
{
    public {ModuleName}DbContext(DbContextOptions<{ModuleName}DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

### Step 6: Create ServiceCollectionExtensions

```csharp
// ServiceCollectionExtensions.cs
using ClassifiedAds.Modules.{ModuleName}.ConfigurationOptions;
using ClassifiedAds.Modules.{ModuleName}.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add{ModuleName}Module(
        this IServiceCollection services, 
        Action<{ModuleName}ModuleOptions> configureOptions)
    {
        var settings = new {ModuleName}ModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<{ModuleName}DbContext>(options => 
            options.UseSqlServer(settings.ConnectionStrings.Default, sql =>
            {
                if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
                {
                    sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
                }
            }));

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IMvcBuilder Add{ModuleName}Module(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void Migrate{ModuleName}Db(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider
            .GetRequiredService<{ModuleName}DbContext>()
            .Database.Migrate();
    }
}
```

### Step 7: Register in Host

```csharp
// ClassifiedAds.WebAPI/Program.cs
services.Add{ModuleName}Module(opt => 
    configuration.GetSection("Modules:{ModuleName}").Bind(opt));

// For controllers
.Add{ModuleName}Module();

// ClassifiedAds.Migrator/Program.cs
app.Migrate{ModuleName}Db();
```

### Step 8: Add Configuration

```json
// appsettings.json
{
  "Modules": {
    "{ModuleName}": {
      "ConnectionStrings": {
        "Default": "Server=.;Database=ClassifiedAds;..."
      }
    }
  }
}
```

---

## Adding a New Entity

### Step 1: Create Entity Class

```csharp
// Entities/{EntityName}.cs
using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Entities;

public class {EntityName} : Entity<Guid>, IAggregateRoot
{
    public string Name { get; set; }
    public string Description { get; set; }
    // Add your properties
}
```

### Step 2: Create EF Configuration

```csharp
// DbConfigurations/{EntityName}Configuration.cs
using ClassifiedAds.Modules.{ModuleName}.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.{ModuleName}.DbConfigurations;

public class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.ToTable("{EntityName}s");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();
            
        builder.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
```

### Step 3: Register Repository

```csharp
// ServiceCollectionExtensions.cs
services.AddScoped<IRepository<{EntityName}, Guid>, Repository<{EntityName}, Guid>>();
```

### Step 4: Create Migration

```powershell
dotnet ef migrations add Add{EntityName} `
    --context {ModuleName}DbContext `
    --project ClassifiedAds.Modules.{ModuleName} `
    --startup-project ClassifiedAds.Migrator
```

---

## Adding a New Command

### Step 1: Create Command and Handler

```csharp
// Commands/Create{EntityName}Command.cs
using ClassifiedAds.Application;
using ClassifiedAds.Modules.{ModuleName}.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Commands;

public class Create{EntityName}Command : ICommand
{
    public {EntityName} Entity { get; set; }
}

public class Create{EntityName}CommandHandler : ICommandHandler<Create{EntityName}Command>
{
    private readonly ICrudService<{EntityName}> _service;

    public Create{EntityName}CommandHandler(ICrudService<{EntityName}> service)
    {
        _service = service;
    }

    public async Task HandleAsync(
        Create{EntityName}Command command, 
        CancellationToken cancellationToken = default)
    {
        await _service.AddOrUpdateAsync(command.Entity, cancellationToken);
    }
}
```

### Step 2: Use in Controller

```csharp
await _dispatcher.DispatchAsync(new Create{EntityName}Command 
{ 
    Entity = entity 
});
```

---

## Adding a New Query

### Step 1: Create Query and Handler

```csharp
// Queries/Get{EntityName}sQuery.cs
using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.{ModuleName}.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Queries;

public class Get{EntityName}sQuery : IQuery<List<{EntityName}>>
{
}

public class Get{EntityName}sQueryHandler : IQueryHandler<Get{EntityName}sQuery, List<{EntityName}>>
{
    private readonly IRepository<{EntityName}, Guid> _repository;

    public Get{EntityName}sQueryHandler(IRepository<{EntityName}, Guid> repository)
    {
        _repository = repository;
    }

    public Task<List<{EntityName}>> HandleAsync(
        Get{EntityName}sQuery query, 
        CancellationToken cancellationToken = default)
    {
        return _repository.ToListAsync(_repository.GetQueryableSet());
    }
}
```

### Step 2: Use in Controller

```csharp
var entities = await _dispatcher.DispatchAsync(new Get{EntityName}sQuery());
```

---

## Adding a Domain Event Handler

### Step 1: Create Event Handler

```csharp
// EventHandlers/{EntityName}CreatedEventHandler.cs
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.{ModuleName}.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.EventHandlers;

public class {EntityName}CreatedEventHandler : IDomainEventHandler<EntityCreatedEvent<{EntityName}>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<AuditLogEntry, Guid> _auditLogRepository;

    public {EntityName}CreatedEventHandler(
        ICurrentUser currentUser,
        IRepository<AuditLogEntry, Guid> auditLogRepository)
    {
        _currentUser = currentUser;
        _auditLogRepository = auditLogRepository;
    }

    public async Task HandleAsync(
        EntityCreatedEvent<{EntityName}> domainEvent, 
        CancellationToken cancellationToken = default)
    {
        // Create audit log
        var auditLog = new AuditLogEntry
        {
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty,
            CreatedDateTime = domainEvent.EventDateTime,
            Action = "CREATED_{ENTITYNAME}",
            ObjectId = domainEvent.Entity.Id.ToString(),
        };

        await _auditLogRepository.AddOrUpdateAsync(auditLog, cancellationToken);
        await _auditLogRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

Event handlers are auto-registered via `AddMessageHandlers()`.

---

## Adding an API Endpoint

### Step 1: Create Controller

```csharp
// Controllers/{EntityName}sController.cs
using ClassifiedAds.Application;
using ClassifiedAds.Modules.{ModuleName}.Commands;
using ClassifiedAds.Modules.{ModuleName}.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class {EntityName}sController : ControllerBase
{
    private readonly Dispatcher _dispatcher;

    public {EntityName}sController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Entities.{EntityName}>>> Get()
    {
        var entities = await _dispatcher.DispatchAsync(new Get{EntityName}sQuery());
        return Ok(entities);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Entities.{EntityName}>> Get(Guid id)
    {
        var entity = await _dispatcher.DispatchAsync(
            new Get{EntityName}Query { Id = id });
        
        if (entity == null)
            return NotFound();
            
        return Ok(entity);
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] Entities.{EntityName} entity)
    {
        await _dispatcher.DispatchAsync(
            new Create{EntityName}Command { Entity = entity });
        
        return Created($"/api/{entityname}s/{entity.Id}", entity);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entity = await _dispatcher.DispatchAsync(
            new Get{EntityName}Query { Id = id });
        
        if (entity == null)
            return NotFound();
            
        await _dispatcher.DispatchAsync(
            new Delete{EntityName}Command { Entity = entity });
        
        return Ok();
    }
}
```

---

## Adding a Background Worker

### Step 1: Create Hosted Service

```csharp
// HostedServices/{WorkerName}Worker.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.HostedServices;

public class {WorkerName}Worker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<{WorkerName}Worker> _logger;

    public {WorkerName}Worker(
        IServiceProvider services, 
        ILogger<{WorkerName}Worker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{WorkerName}Worker starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetDispatcher();
                
                // Do work here
                
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {WorkerName}Worker");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }
}
```

### Step 2: Register in ServiceCollectionExtensions

```csharp
public static IServiceCollection AddHostedServices{ModuleName}Module(
    this IServiceCollection services)
{
    services.AddHostedService<{WorkerName}Worker>();
    return services;
}
```

### Step 3: Register in Background Host

```csharp
// ClassifiedAds.Background/Program.cs
services.AddHostedServices{ModuleName}Module();
```

---

## Adding a Message Bus Consumer

### Step 1: Create Consumer Class

```csharp
// MessageBusConsumers/{MessageName}Consumer.cs
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.{ModuleName}.DTOs;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.MessageBusConsumers;

public class {MessageName}Consumer : IMessageBusConsumer<{MessageName}Event>
{
    private readonly ILogger<{MessageName}Consumer> _logger;

    public {MessageName}Consumer(ILogger<{MessageName}Consumer> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        {MessageName}Event message, 
        MetaData metaData, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received {MessageName}Event: {Id}", message.Id);
        
        // Process message
        
        return Task.CompletedTask;
    }
}
```

### Step 2: Register Consumer

```csharp
// ClassifiedAds.Background/Program.cs
services.AddMessageBusReceiver<{MessageName}Consumer, {MessageName}Event>(appSettings.Messaging);
```

---

## Enabling Outbox for a Module

### Step 1: Add OutboxMessage Entity

```csharp
// Entities/OutboxMessage.cs
using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Entities;

public class OutboxMessage : Entity<Guid>, IAggregateRoot
{
    public string EventType { get; set; }
    public Guid TriggeredById { get; set; }
    public string ObjectId { get; set; }
    public string Payload { get; set; }
    public bool Published { get; set; }
    public string ActivityId { get; set; }
}
```

### Step 2: Update DbContext

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    SetOutboxActivityId();
    return await base.SaveChangesAsync(cancellationToken);
}

private void SetOutboxActivityId()
{
    var entities = ChangeTracker.Entries<OutboxMessage>();
    foreach (var entity in entities.Where(e => e.State == EntityState.Added))
    {
        if (string.IsNullOrWhiteSpace(entity.Entity.ActivityId))
        {
            entity.Entity.ActivityId = System.Diagnostics.Activity.Current?.Id;
        }
    }
}
```

### Step 3: Register OutboxMessage Repository

```csharp
services.AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();
```

### Step 4: Create PublishEventWorker

Copy from Product module and adapt.

### Step 5: Update Event Handlers to Write to Outbox

---

## Adding Authorization Permissions

### Step 1: Define Permissions

```csharp
// Authorization/Permissions.cs
namespace ClassifiedAds.Modules.{ModuleName}.Authorization;

public static class Permissions
{
    public const string Get{EntityName}s = "Permission:Get{EntityName}s";
    public const string Get{EntityName} = "Permission:Get{EntityName}";
    public const string Add{EntityName} = "Permission:Add{EntityName}";
    public const string Update{EntityName} = "Permission:Update{EntityName}";
    public const string Delete{EntityName} = "Permission:Delete{EntityName}";
}
```

### Step 2: Apply to Controller

```csharp
[Authorize(Permissions.Get{EntityName}s)]
[HttpGet]
public async Task<ActionResult<IEnumerable<{EntityName}>>> Get()
{
    // ...
}
```

### Step 3: Register Policies

```csharp
// ServiceCollectionExtensions.cs
services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());
```

---

## Quick Reference Checklist

### New Module Checklist

- [ ] Create project and add to solution
- [ ] Add project references
- [ ] Create folder structure
- [ ] Create ConfigurationOptions
- [ ] Create DbContext
- [ ] Create ServiceCollectionExtensions
- [ ] Register in WebAPI Program.cs
- [ ] Register in Background Program.cs
- [ ] Register in Migrator Program.cs
- [ ] Add configuration to appsettings.json
- [ ] Create initial migration

### New Entity Checklist

- [ ] Create Entity class
- [ ] Create EF Configuration
- [ ] Register Repository
- [ ] Create migration
- [ ] Create Commands (Add/Update/Delete)
- [ ] Create Queries (Get/GetById)
- [ ] Create Event Handlers (optional)
- [ ] Create Controller
- [ ] Define Permissions
- [ ] Write tests

---

*Previous: [10 - DevOps & Local Development](10-devops-and-local-development.md) | Next: [Appendix - Glossary](appendix-glossary.md)*
