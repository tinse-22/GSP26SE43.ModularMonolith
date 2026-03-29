# 🚀 Hướng Dẫn Chạy Dự Án ClassifiedAds.ModularMonolith

## Mục lục

- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Thiết lập lần đầu](#thiết-lập-lần-đầu-chỉ-cần-làm-1-lần)
- [Cách A: Chạy bằng Docker Compose + .NET CLI (Khuyến nghị)](#cách-a-chạy-bằng-docker-compose--net-cli-khuyến-nghị-)
- [Cách B: Chạy bằng .NET Aspire AppHost](#cách-b-chạy-bằng-net-aspire-apphost)
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
POSTGRES_PASSWORD=your_local_postgres_password

# Connection string (password phải khớp với POSTGRES_PASSWORD ở trên)
ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=your_local_postgres_password

# JWT Secret Key (phải >= 32 ký tự)
Modules__Identity__Jwt__SecretKey=MySecretKeyForJwtTokenMustBe32CharsOrMore!
```

> 💡 Các giá trị khác có thể giữ mặc định. Xem chi tiết tại [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md).

---

## Cách A: Chạy bằng Docker Compose + .NET CLI (Khuyến nghị ⭐)

Đây là flow local **ổn định nhất** để tránh tình trạng app đang ghi vào một database nhưng sau restart lại đọc database khác.

Nguyên tắc của mode này:
- chỉ dùng **một** database local từ `docker compose`
- `ConnectionStrings__Default` trong `.env` là source of truth
- **không** chạy `ClassifiedAds.AppHost` cùng lúc với WebAPI/Background standalone

### Bước 1: Khởi động infrastructure

```powershell
cd D:\GSP26SE43.ModularMonolith
docker compose up -d db rabbitmq redis mailhog
```

Kiểm tra các container đã chạy:

```powershell
docker compose ps
```

### Bước 2: Chạy database migration

```powershell
dotnet run --project ClassifiedAds.Migrator
```

> Database của mode này được persist bởi Docker volume `postgres_data`, nên dữ liệu CRUD sẽ còn sau khi restart stack nếu bạn không chạy `docker compose down -v`.

### Bước 3: Chạy Web API

```powershell
dotnet run --project ClassifiedAds.WebAPI
```

### Bước 4: Chạy Background Worker

Mở **terminal mới** rồi chạy:

```powershell
cd D:\GSP26SE43.ModularMonolith
dotnet run --project ClassifiedAds.Background
```

### Truy cập

1. **WebAPI Docs**: `https://localhost:44312/docs`
2. **RabbitMQ UI**: `http://localhost:15672`
3. **MailHog**: `http://localhost:8025`
4. **PostgreSQL**: `localhost:55432`

### Dừng dự án

```powershell
# Dừng WebAPI/Background: Ctrl + C trong terminal tương ứng

# Dừng Docker containers nhưng GIỮ data
docker compose down

# Dừng Docker containers VÀ xoá data (reset hoàn toàn)
docker compose down -v
```

---

## Cách B: Chạy bằng .NET Aspire AppHost

Mode này phù hợp khi bạn cần Aspire Dashboard hoặc muốn orchestration toàn bộ service bằng một lệnh.

Điều quan trọng cần nhớ:
- AppHost **mặc định dùng database local riêng** của nó
- database local của AppHost nay được persist bằng Docker volume `classifiedads_apphost_postgres_data`
- PostgreSQL local của AppHost bind cố định tại `localhost:5432`
- khi chạy qua Aspire, `docker ps` vẫn có thể hiện port container ngẫu nhiên; hãy dùng `localhost:5432` từ host tools như pgAdmin/DBeaver
- **không** chạy AppHost cùng lúc với WebAPI/Background standalone nếu chưa chủ động ép cả hai mode dùng cùng `ConnectionStrings__Default`

### Bước chạy

```powershell
cd D:\GSP26SE43.ModularMonolith
dotnet run --project ClassifiedAds.AppHost
```

### Khi nào AppHost dùng cùng DB với standalone?

Chỉ khi bạn export `ConnectionStrings__Default` trong **chính shell đang chạy AppHost**:

```powershell
$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=<same value as .env>"
dotnet run --project ClassifiedAds.AppHost
```

Nếu bạn **không** set biến trên, AppHost sẽ tự tạo PostgreSQL local riêng, bind nó tại `localhost:5432`, và giữ dữ liệu của nó trong volume `classifiedads_apphost_postgres_data`.

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
2. **WebAPI Docs**: Mở Aspire Dashboard → click vào endpoint của **webapi** → thêm `/docs` vào URL
3. **PgAdmin**: Tự động khởi động, truy cập qua Aspire Dashboard

### Dừng dự án

Nhấn `Ctrl + C` trong terminal đang chạy Aspire.

---

## Danh sách URL các service

### Khi chạy bằng Docker Compose + .NET CLI (Cách A)

| Service            | URL                       | Ghi chú                          |
| ------------------ | ------------------------- | --------------------------------- |
| **WebAPI Docs**    | `https://localhost:44312/docs` | Chạy `ClassifiedAds.WebAPI` local |
| **RabbitMQ UI**    | `http://localhost:15672`  | guest / guest                     |
| **MailHog**        | `http://localhost:8025`   | Xem email test                    |
| **PostgreSQL**     | `localhost:55432`         | postgres / giá trị `POSTGRES_PASSWORD` trong `.env` |
| **Redis**          | `localhost:6379`          | Cache phân tán                    |

### Khi chạy bằng Aspire AppHost (Cách B)

| Service            | URL                                  | Credentials       |
| ------------------ | ------------------------------------ | ------------------ |
| **Aspire Dashboard** | `https://localhost:17280`           | Xem trạng thái các service |
| **WebAPI**          | Xem trong Aspire Dashboard          | Port tự động gán   |
| **PgAdmin**        | Xem trong Aspire Dashboard           | Quản lý PostgreSQL |
| **RabbitMQ UI**    | Xem trong Aspire Dashboard           | guest / guest      |

---

## Các lệnh thường dùng

| Mục đích                          | Lệnh                                                  |
| --------------------------------- | ------------------------------------------------------ |
| **Chạy local chuẩn**             | `docker compose up -d db rabbitmq redis mailhog`       |
| **Chạy AppHost**                 | `dotnet run --project ClassifiedAds.AppHost`           |
| **Build kiểm tra lỗi**           | `dotnet build`                                         |
| **Chạy tất cả test**             | `dotnet test`                                          |
| **Chạy architecture test**       | `dotnet test --filter "FullyQualifiedName~Architecture"` |
| **Chạy test với coverage**       | `dotnet test --collect:"XPlat Code Coverage"`          |
| **Khôi phục packages**           | `dotnet restore`                                       |
| **Xem .NET SDK version**         | `dotnet --version`                                     |
| **Xem Docker containers**        | `docker compose ps`                                    |
| **Xem Docker logs**              | `docker compose logs -f <service_name>`                |

---

## Xử lý lỗi thường gặp

### 1. ❌ `Cannot connect to PostgreSQL` / Connection refused

**Nguyên nhân**: Docker chưa chạy hoặc container PostgreSQL chưa sẵn sàng.

**Cách fix**:
```powershell
# Kiểm tra Docker Desktop đang chạy
docker info

# Nếu dùng Cách A, kiểm tra container
docker compose ps
docker compose up -d db
```

### 2. ❌ `Port already in use`

**Nguyên nhân**: Port đã bị chiếm bởi process khác.

**Cách fix**:
```powershell
# Tìm process chiếm port (VD: port 55432)
netstat -ano | findstr :55432

# Dừng tất cả Docker containers cũ
docker compose down
```

### 3. ❌ `Docker image not found` / Build error

**Nguyên nhân**: Docker images chưa được build.

**Cách fix**:
```powershell
# Build lại Docker images
docker compose build
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

> **Flow local khuyến nghị để tránh mất dữ liệu sau restart:**
>
> ```powershell
> cd D:\GSP26SE43.ModularMonolith
> docker compose up -d db rabbitmq redis mailhog
> dotnet run --project ClassifiedAds.Migrator
> dotnet run --project ClassifiedAds.WebAPI
> ```
>
> Mở thêm terminal mới nếu cần chạy `dotnet run --project ClassifiedAds.Background`.
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
docker compose up -d db rabbitmq redis mailhog
dotnet run --project ClassifiedAds.Migrator
dotnet run --project ClassifiedAds.WebAPI
```

Neu can xu ly async/background thi mo them terminal va chay `dotnet run --project ClassifiedAds.Background`.

- Mode B (AppHost):

```powershell
dotnet run --project ClassifiedAds.AppHost
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
- Neu chay AppHost, mac dinh no dung DB rieng va da persist qua Docker volume `classifiedads_apphost_postgres_data`.
