FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /ClassifiedAds.ModularMonolith

# Copy csproj and restore as distinct layers
COPY ./ClassifiedAds.ServiceDefaults/*.csproj ./ClassifiedAds.ServiceDefaults/
COPY ./ClassifiedAds.Application/*.csproj ./ClassifiedAds.Application/
COPY ./ClassifiedAds.CrossCuttingConcerns/*.csproj ./ClassifiedAds.CrossCuttingConcerns/
COPY ./ClassifiedAds.Domain/*.csproj ./ClassifiedAds.Domain/
COPY ./ClassifiedAds.Infrastructure/*.csproj ./ClassifiedAds.Infrastructure/
COPY ./ClassifiedAds.Persistence.PostgreSQL/*.csproj ./ClassifiedAds.Persistence.PostgreSQL/
COPY ./ClassifiedAds.Contracts/*.csproj ./ClassifiedAds.Contracts/
COPY ./ClassifiedAds.Modules.AuditLog/*.csproj ./ClassifiedAds.Modules.AuditLog/
COPY ./ClassifiedAds.Modules.Configuration/*.csproj ./ClassifiedAds.Modules.Configuration/
COPY ./ClassifiedAds.Modules.Identity/*.csproj ./ClassifiedAds.Modules.Identity/
COPY ./ClassifiedAds.Modules.Notification/*.csproj ./ClassifiedAds.Modules.Notification/
COPY ./ClassifiedAds.Modules.Product/*.csproj ./ClassifiedAds.Modules.Product/
COPY ./ClassifiedAds.Modules.Storage/*.csproj ./ClassifiedAds.Modules.Storage/
COPY ./ClassifiedAds.Modules.Subscription/*.csproj ./ClassifiedAds.Modules.Subscription/
COPY ./ClassifiedAds.Modules.TestGeneration/*.csproj ./ClassifiedAds.Modules.TestGeneration/
COPY ./ClassifiedAds.Modules.TestExecution/*.csproj ./ClassifiedAds.Modules.TestExecution/
COPY ./ClassifiedAds.Modules.TestReporting/*.csproj ./ClassifiedAds.Modules.TestReporting/

COPY ./ClassifiedAds.Migrator/*.csproj ./ClassifiedAds.Migrator/

RUN dotnet restore ./ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj

# Copy everything else and build
COPY . ./
RUN dotnet publish ./ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /ClassifiedAds.ModularMonolith
COPY --from=build-env /ClassifiedAds.ModularMonolith/out .

ENTRYPOINT ["dotnet", "ClassifiedAds.Migrator.dll"]