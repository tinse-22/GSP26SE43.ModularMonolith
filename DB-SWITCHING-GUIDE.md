# Database Switching Guide: Supabase Cloud <-> Local PostgreSQL

## Muc tieu

Tai lieu nay la playbook de AI Agent (va dev) chuyen nhanh giua:

1. Supabase Cloud (final deploy mode)
2. Local PostgreSQL (dev fallback)

Repo hien tai da duoc chuyen sang Supabase-first.

## Nguon cau hinh chinh

1. `.env` (khong commit) cho local runtime va docker compose
2. `.env.render` cho template deploy
3. `docker-compose.yml` cho stack container
4. `ClassifiedAds.Migrator/appsettings.json` cho fallback `CheckDependency:Host`

## Luu y quan trong

1. AppHost khong dung `APPHOST_DATABASE_MODE` de chon DB.
2. AppHost se chay external DB khi co `ConnectionStrings__Default` trong process env.
3. Standalone hosts (`WebAPI`, `Background`, `Migrator`) dung `STANDALONE_DATABASE_MODE`:
   - `external`: uu tien `ConnectionStrings__Default` (Supabase)
   - `local`: tu build connection tu `POSTGRES_*`
4. Docker compose da dung bien rieng `SUPABASE_CONNECTION_STRING` va `SUPABASE_CHECKDEPENDENCY_HOST` de tranh loi precedence tu shell env.

## Quick switch: Supabase Cloud (ACTIVE)

Cap nhat `.env`:

```env
STANDALONE_DATABASE_MODE=external

ConnectionStrings__Default=Host=db.pdmbghlidmoobbjsuxgx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<YOUR_SUPABASE_DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=60
CheckDependency__Host=db.pdmbghlidmoobbjsuxgx.supabase.co:5432

SUPABASE_CONNECTION_STRING=Host=db.pdmbghlidmoobbjsuxgx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<YOUR_SUPABASE_DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=60
SUPABASE_CHECKDEPENDENCY_HOST=db.pdmbghlidmoobbjsuxgx.supabase.co:5432

# Local fallback (comment)
# STANDALONE_DATABASE_MODE=local
# POSTGRES_HOST=127.0.0.1
# POSTGRES_HOST_PORT=55432
# POSTGRES_USER=postgres
# POSTGRES_PASSWORD=Postgre123@
# POSTGRES_DB=ClassifiedAds
# ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=Postgre123@;Trust Server Certificate=true;Timeout=30;Command Timeout=60
# CheckDependency__Host=127.0.0.1:55432
```

Chay lai stack (neu dang chay local cu):

```powershell
docker compose down
docker compose up -d rabbitmq redis mailhog
dotnet run --project ClassifiedAds.Migrator
dotnet run --project ClassifiedAds.WebAPI
dotnet run --project ClassifiedAds.Background
```

Neu deploy bang full compose image:

```powershell
docker compose up -d --build
```

## Quick switch: Local PostgreSQL (INACTIVE fallback)

Cap nhat `.env`:

```env
STANDALONE_DATABASE_MODE=local

POSTGRES_HOST=127.0.0.1
POSTGRES_HOST_PORT=55432
POSTGRES_USER=postgres
POSTGRES_PASSWORD=Postgre123@
POSTGRES_DB=ClassifiedAds

ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=Postgre123@;Trust Server Certificate=true;Timeout=30;Command Timeout=60
CheckDependency__Host=127.0.0.1:55432

# Supabase fallback (comment)
# STANDALONE_DATABASE_MODE=external
# ConnectionStrings__Default=Host=db.pdmbghlidmoobbjsuxgx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<YOUR_SUPABASE_DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=60
# CheckDependency__Host=db.pdmbghlidmoobbjsuxgx.supabase.co:5432
# SUPABASE_CONNECTION_STRING=Host=db.pdmbghlidmoobbjsuxgx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<YOUR_SUPABASE_DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=60
# SUPABASE_CHECKDEPENDENCY_HOST=db.pdmbghlidmoobbjsuxgx.supabase.co:5432
```

Khoi dong local PostgreSQL neu can:

```powershell
docker run -d --name classifiedads-dev-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=Postgre123@ \
  -e POSTGRES_DB=ClassifiedAds \
  -p 55432:5432 \
  postgres:16
```

## AI Agent checklist (bat buoc)

1. Doc `.env` va xac nhan mode dang active (`STANDALONE_DATABASE_MODE`).
2. Kiem tra `ConnectionStrings__Default` va `CheckDependency__Host` co cung target.
3. Neu dung docker compose, uu tien `SUPABASE_CONNECTION_STRING` / `SUPABASE_CHECKDEPENDENCY_HOST`.
4. Chay verify migration:

```powershell
dotnet build ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj --no-restore
dotnet ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll --verify-migrations
```

5. Kiem tra compose config:

```powershell
docker compose config
```

6. Neu doi mode, restart service/process truoc khi test.

## Trang thai hien tai

1. Mode mac dinh: Supabase Cloud
2. Host mac dinh: `db.pdmbghlidmoobbjsuxgx.supabase.co:5432`
3. Local PostgreSQL giu lai o dang comment de rollback nhanh