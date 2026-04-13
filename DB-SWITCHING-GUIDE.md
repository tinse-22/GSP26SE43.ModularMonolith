# Database Switching Guide: Local PostgreSQL ↔ Supabase Cloud

## Tổng quan

Repo này hỗ trợ 2 chế độ database:
1. **Local PostgreSQL** — container Docker chạy trên máy local (`127.0.0.1:55432`)
2. **Supabase Cloud** — session pooler (`aws-1-ap-southeast-2.pooler.supabase.com:5432`)

Tất cả cấu hình tập trung trong file `.env` tại root repo.

---

## Files cần thay đổi khi switch

| File | Thay đổi gì |
|------|-------------|
| `.env` | `ConnectionStrings__Default`, `CheckDependency__Host`, `APPHOST_DATABASE_MODE` |
| `ClassifiedAds.Migrator/appsettings.json` | `CheckDependency.Host` (fallback) |
| `docker-compose.yml` | `CheckDependency__Host` default value |

---

## Chuyển sang LOCAL PostgreSQL

### 1. File `.env` — Bật local, tắt Supabase

```env
# Database mode
APPHOST_DATABASE_MODE=local

# Connection Strings — LOCAL (ACTIVE)
ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=Postgre123@;Trust Server Certificate=true;Timeout=30;Command Timeout=60
CheckDependency__Host=127.0.0.1:55432

# Connection Strings — Supabase (INACTIVE — comment out)
# ConnectionStrings__Default=Host=aws-1-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.pdmbghlidmoobbjsuxgx;Password=0937213289Tin@;SSL Mode=Require;Trust Server Certificate=true;Keepalive=15;Timeout=30;Command Timeout=60
# CheckDependency__Host=aws-1-ap-southeast-2.pooler.supabase.com:5432
```

### 2. File `ClassifiedAds.Migrator/appsettings.json`

```json
"CheckDependency": {
  "Enabled": false,
  "Host": "127.0.0.1:55432"
}
```

### 3. File `docker-compose.yml` — CheckDependency default

```yaml
CheckDependency__Host: "${CheckDependency__Host:-127.0.0.1:55432}"
```

### 4. Đảm bảo local PostgreSQL đang chạy

```powershell
# Kiểm tra container
docker ps --filter "publish=55432"

# Nếu chưa chạy, start bằng docker compose profile
docker compose --profile local-db up -d db

# Hoặc chạy container standalone
docker run -d --name classifiedads-dev-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=Postgre123@ \
  -e POSTGRES_DB=ClassifiedAds \
  -p 55432:5432 \
  postgres:16
```

### 5. Chạy migrations (nếu DB mới)

```powershell
dotnet run --project ClassifiedAds.Migrator
```

---

## Chuyển sang SUPABASE Cloud

### 1. File `.env` — Bật Supabase, tắt local

```env
# Database mode
APPHOST_DATABASE_MODE=external

# Connection Strings — LOCAL (INACTIVE — comment out)
# ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=Postgre123@;Trust Server Certificate=true;Timeout=30;Command Timeout=60
# CheckDependency__Host=127.0.0.1:55432

# Connection Strings — Supabase SESSION POOLER (ACTIVE)
ConnectionStrings__Default=Host=aws-1-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.pdmbghlidmoobbjsuxgx;Password=0937213289Tin@;SSL Mode=Require;Trust Server Certificate=true;Keepalive=15;Timeout=30;Command Timeout=60
CheckDependency__Host=aws-1-ap-southeast-2.pooler.supabase.com:5432
```

### 2. File `ClassifiedAds.Migrator/appsettings.json`

```json
"CheckDependency": {
  "Enabled": false,
  "Host": "aws-1-ap-southeast-2.pooler.supabase.com:5432"
}
```

### 3. File `docker-compose.yml` — CheckDependency default

```yaml
CheckDependency__Host: "${CheckDependency__Host:-aws-1-ap-southeast-2.pooler.supabase.com:5432}"
```

### 4. Lưu ý khi dùng Supabase

- **Session pooler (port 5432)** — full SQL, hỗ trợ prepared statements ✅
- **Transaction pooler (port 6543)** — KHÔNG dùng, gây `ObjectDisposedException` với Npgsql
- **Direct connection (db.*.supabase.co)** — cần IPv6, không hoạt động trên mạng IPv4-only
- `PostgresConnectionStringNormalizer.cs` tự động disable Npgsql pooling khi phát hiện Supabase pooler

---

## Thông tin kỹ thuật

### Connection String khác biệt chính

| Thuộc tính | Local | Supabase |
|-----------|-------|----------|
| Host | `127.0.0.1` | `aws-1-ap-southeast-2.pooler.supabase.com` |
| Port | `55432` | `5432` |
| Database | `ClassifiedAds` | `postgres` |
| Username | `postgres` | `postgres.pdmbghlidmoobbjsuxgx` |
| SSL Mode | không cần | `Require` |
| Keepalive | không cần | `15` |

### APPHOST_DATABASE_MODE

- `local` → AppHost tạo local PostgreSQL container (port 55433 cho AppHost)
- `external` → AppHost dùng `ConnectionStrings__Default` trực tiếp

### Docker Compose profiles

- `local-db` → khởi động `db` service (postgres:16) trên port `${POSTGRES_HOST_PORT:-55432}`
- `local-redis` → khởi động `redis` service

---

## Checklist sau khi switch

1. ✅ Cập nhật `.env` (ConnectionStrings__Default, CheckDependency__Host, APPHOST_DATABASE_MODE)
2. ✅ Cập nhật `ClassifiedAds.Migrator/appsettings.json` CheckDependency.Host
3. ✅ Cập nhật `docker-compose.yml` CheckDependency__Host default
4. ✅ Verify DB đang chạy: `docker ps` hoặc test connection
5. ✅ Build migrator: `dotnet build ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj`
6. ✅ Verify migrations: `dotnet ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll --verify-migrations`
7. ✅ Chạy migrations nếu DB mới: `dotnet run --project ClassifiedAds.Migrator`

---

## Trạng thái hiện tại (2026-04-12)

- **Active mode**: LOCAL PostgreSQL
- **Local DB container**: `classifiedads-dev-db` on `127.0.0.1:55432`
- **Database name**: `ClassifiedAds`
- **Latest migrations**: up to date (verified)
- **Last 5 migrations**:
  - `20260411032249_RenameStorageSchemaForSupabase`
  - `20260410064820_AddSoftDeleteToTestCasesAndLlmSuggestions`
  - `20260407060801_AddTestGenerationJob`
  - `20260406130000_AddTestCaseResults`
  - `20260405141923_AddUserStoragePermissionsAndRoleDefaults`
