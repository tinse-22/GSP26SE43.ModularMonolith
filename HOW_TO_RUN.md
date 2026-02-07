# 🚀 Hướng Dẫn Chạy Dự Án ClassifiedAds.ModularMonolith

## Mục lục

- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Thiết lập lần đầu](#thiết-lập-lần-đầu-chỉ-cần-làm-1-lần)
- [Cách A: Chạy bằng .NET Aspire (Khuyến nghị)](#cách-a-chạy-bằng-net-aspire-khuyến-nghị-)
- [Cách B: Chạy bằng Docker Compose + .NET CLI](#cách-b-chạy-bằng-docker-compose--net-cli)
- [Danh sách URL các service](#danh-sách-url-các-service)
- [Các lệnh thường dùng](#các-lệnh-thường-dùng)
- [Xử lý lỗi thường gặp](#xử-lý-lỗi-thường-gặp)

---

## Yêu cầu hệ thống

| Phần mềm        | Phiên bản tối thiểu | Kiểm tra bằng lệnh     |
| ---------------- | -------------------- | ----------------------- |
| **.NET SDK**     | 10.0+                | `dotnet --version`      |
| **Docker Desktop** | Latest            | `docker --version`      |

> ⚠️ **Docker Desktop phải đang chạy** trước khi khởi động dự án (cả 2 cách đều cần Docker).

---

## Thiết lập lần đầu (chỉ cần làm 1 lần)

### 1. Tạo file `.env`

```powershell
cd D:\GSP26SE43.ModularMonolith
copy .env.example .env
```

### 2. Cấu hình file `.env`

Mở file `.env` và cập nhật các giá trị quan trọng:

```dotenv
# Database password (đặt password mong muốn)
POSTGRES_PASSWORD=postgres

# Connection string (password phải khớp với POSTGRES_PASSWORD ở trên)
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=postgres

# JWT Secret Key (phải >= 32 ký tự)
Modules__Identity__Jwt__SecretKey=MySecretKeyForJwtTokenMustBe32CharsOrMore!
```

> 💡 Các giá trị khác có thể giữ mặc định. Xem chi tiết tại [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md).

---

## Cách A: Chạy bằng .NET Aspire (Khuyến nghị ⭐)

Đây là cách **đơn giản nhất** — chỉ cần **1 lệnh duy nhất**. Aspire sẽ tự động:
- Khởi động PostgreSQL, RabbitMQ, Redis, MailHog (qua Docker)
- Chạy Database Migration
- Khởi động WebAPI + Background Worker
- Cung cấp Dashboard để xem logs, traces, metrics

### Bước chạy

```powershell
cd D:\GSP26SE43.ModularMonolith
dotnet run --project ClassifiedAds.AppHost
```

### Kết quả mong đợi

Khi thấy dòng log như sau nghĩa là đã chạy thành công:

```
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: https://localhost:17280
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at https://localhost:17280/login?t=<token>
```

### Truy cập

1. **Aspire Dashboard**: Mở URL `https://localhost:17280` (hiện trong console)
   - Xem trạng thái tất cả services (PostgreSQL, RabbitMQ, Redis, WebAPI, Background...)
   - Xem logs, traces, metrics của từng service
   - Xem environment variables được inject vào mỗi service

2. **WebAPI Docs**: Mở Aspire Dashboard → click vào endpoint của **webapi** → thêm `/docs` vào URL

3. **PgAdmin**: Tự động khởi động, truy cập qua Aspire Dashboard

### Dừng dự án

Nhấn `Ctrl + C` trong terminal đang chạy Aspire.

---

## Cách B: Chạy bằng Docker Compose + .NET CLI

Cách này chạy từng thành phần riêng lẻ, phù hợp khi muốn kiểm soát chi tiết hơn.

### Bước 1: Khởi động Infrastructure (PostgreSQL, RabbitMQ, Redis, MailHog)

```powershell
cd D:\GSP26SE43.ModularMonolith
docker-compose up -d db rabbitmq redis mailhog
```

Kiểm tra các container đã chạy:

```powershell
docker-compose ps
```

### Bước 2: Chạy Database Migration

```powershell
dotnet run --project ClassifiedAds.Migrator
```

> Lệnh này tạo/cập nhật schema database cho tất cả modules. Chỉ cần chạy lại khi có migration mới.

### Bước 3: Chạy Web API

```powershell
dotnet run --project ClassifiedAds.WebAPI
```

### Bước 4 (Tùy chọn): Chạy Background Worker

Mở **terminal mới** rồi chạy:

```powershell
cd D:\GSP26SE43.ModularMonolith
dotnet run --project ClassifiedAds.Background
```

> Background Worker xử lý: gửi email, publish outbox messages, consume message bus events.

### Dừng dự án

```powershell
# Dừng WebAPI/Background: Ctrl + C trong terminal tương ứng

# Dừng Docker containers
docker-compose down

# Dừng Docker containers VÀ xóa data (reset database)
docker-compose down -v
```

---

## Danh sách URL các service

### Khi chạy bằng Aspire (Cách A)

| Service            | URL                                  | Ghi chú                          |
| ------------------ | ------------------------------------ | --------------------------------- |
| **Aspire Dashboard** | `https://localhost:17280`           | Xem tất cả services, logs, traces |
| **WebAPI**          | Xem trong Aspire Dashboard          | Port tự động gán                  |
| **PgAdmin**        | Xem trong Aspire Dashboard           | Quản lý PostgreSQL                |
| **RabbitMQ UI**    | Xem trong Aspire Dashboard           | Quản lý Message Queue             |

### Khi chạy bằng Docker Compose (Cách B)

| Service            | URL                       | Credentials       |
| ------------------ | ------------------------- | ------------------ |
| **WebAPI Docs**    | `http://localhost:9002/docs` | —               |
| **RabbitMQ UI**    | `http://localhost:15672`  | guest / guest      |
| **MailHog**        | `http://localhost:8025`   | — (xem email test) |
| **PostgreSQL**     | `localhost:5432`          | postgres / postgres |
| **Redis**          | `localhost:6379`          | —                  |

---

## Các lệnh thường dùng

| Mục đích                          | Lệnh                                                  |
| --------------------------------- | ------------------------------------------------------ |
| **Chạy toàn bộ (nhanh nhất)**    | `dotnet run --project ClassifiedAds.AppHost`           |
| **Build kiểm tra lỗi**           | `dotnet build`                                         |
| **Chạy tất cả test**             | `dotnet test`                                          |
| **Chạy architecture test**       | `dotnet test --filter "FullyQualifiedName~Architecture"` |
| **Chạy test với coverage**       | `dotnet test --collect:"XPlat Code Coverage"`          |
| **Khôi phục packages**           | `dotnet restore`                                       |
| **Xem .NET SDK version**         | `dotnet --version`                                     |
| **Xem Docker containers**        | `docker-compose ps`                                    |
| **Xem Docker logs**              | `docker-compose logs -f <service_name>`                |

---

## Xử lý lỗi thường gặp

### 1. ❌ `Cannot connect to PostgreSQL` / Connection refused

**Nguyên nhân**: Docker chưa chạy hoặc container PostgreSQL chưa sẵn sàng.

**Cách fix**:
```powershell
# Kiểm tra Docker Desktop đang chạy
docker info

# Nếu dùng Cách B, kiểm tra container
docker-compose ps
docker-compose up -d db
```

### 2. ❌ `Port already in use`

**Nguyên nhân**: Port đã bị chiếm bởi process khác.

**Cách fix**:
```powershell
# Tìm process chiếm port (VD: port 5432)
netstat -ano | findstr :5432

# Dừng tất cả Docker containers cũ
docker-compose down
```

### 3. ❌ `Docker image not found` / Build error

**Nguyên nhân**: Docker images chưa được build.

**Cách fix**:
```powershell
# Build lại Docker images
docker-compose build
```

### 4. ❌ `.env file not found` / Configuration missing

**Nguyên nhân**: Chưa tạo file `.env`.

**Cách fix**:
```powershell
copy .env.example .env
# Rồi sửa các giá trị trong .env
```

### 5. ❌ `SDK version not found`

**Nguyên nhân**: Chưa cài .NET SDK 10.0.

**Cách fix**: Tải và cài từ https://dotnet.microsoft.com/download/dotnet/10.0

### 6. ❌ Aspire Dashboard không mở được (HTTPS certificate error)

**Cách fix**:
```powershell
# Trust dev certificate
dotnet dev-certs https --trust
```

---

## Cấu trúc các Host trong dự án

```
ClassifiedAds.AppHost/       → .NET Aspire orchestration (chạy TẤT CẢ)
ClassifiedAds.WebAPI/        → REST API server (Scalar API docs tại /docs)
ClassifiedAds.Background/    → Background worker (email, messaging)
ClassifiedAds.Migrator/      → Database migration tool
```

### Thứ tự khởi động (Aspire tự quản lý)

```
PostgreSQL → Migrator → WebAPI + Background
RabbitMQ ──────────────→ WebAPI + Background  
Redis    ──────────────→ WebAPI + Background
MailHog  ──────────────→ Background
```

---

## Tóm tắt nhanh 🎯

> **Chỉ cần nhớ 1 lệnh duy nhất để chạy toàn bộ dự án:**
>
> ```powershell
> cd D:\GSP26SE43.ModularMonolith
> dotnet run --project ClassifiedAds.AppHost
> ```
>
> Rồi mở link Aspire Dashboard hiện trong console.
> 
> Nhấn `Ctrl + C` để dừng.

---

## QUICK FIX: login dung mat khau nhung van bao sai

Neu gap loi `/api/Auth/login` tra ve `{"error":"Invalid email or password."}` du ban nhap dung:

### Nguyen nhan thuong gap nhat

Ban dang chay dong thoi 2 mode:
- `ClassifiedAds.AppHost` (Aspire)
- `ClassifiedAds.WebAPI` standalone

Khi do request login de bi goi nham sang API instance/DB khac.

### Cach fix nhanh (Windows PowerShell)

1. Tat toan bo process WebAPI/AppHost dang chay:

```powershell
Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
  Where-Object { $_.CommandLine -match 'ClassifiedAds\\.AppHost|ClassifiedAds\\.WebAPI|ClassifiedAds\\.Migrator' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

2. Chon 1 mode duy nhat:

- Mode A (khuyen nghi):

```powershell
dotnet run --project ClassifiedAds.AppHost
```

Sau do lay dung URL WebAPI trong Aspire Dashboard (khong hardcode `localhost:9002` khi chay Aspire).

- Mode B (standalone):

```powershell
docker-compose up -d db rabbitmq redis mailhog
dotnet run --project ClassifiedAds.Migrator
dotnet run --project ClassifiedAds.WebAPI
```

3. Test lai login:

```http
POST /api/Auth/login
{
  "email": "tinvtse@gmail.com",
  "password": "Admin@123",
  "rememberMe": true
}
```

### Ghi nho

- KHONG chay dong thoi AppHost va WebAPI standalone.
- Neu chay standalone, phai chay `ClassifiedAds.Migrator` truoc khi login.
