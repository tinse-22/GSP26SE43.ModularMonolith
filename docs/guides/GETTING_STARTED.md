# Hướng Dẫn Chạy Dự Án Từ A-Z (Sau Khi Clone)

> Tài liệu này hướng dẫn **từng bước** để chạy dự án `GSP26SE43.ModularMonolith` sau khi clone từ GitHub, dành cho developer mới tham gia dự án.

## Mục lục

- [1. Yêu cầu hệ thống](#1-yêu-cầu-hệ-thống)
- [2. Clone dự án](#2-clone-dự-án)
- [3. Cài đặt .NET SDK 10](#3-cài-đặt-net-sdk-10)
- [4. Cài đặt Docker Desktop](#4-cài-đặt-docker-desktop)
- [5. Cấu hình Environment Variables](#5-cấu-hình-environment-variables)
- [6. Cách A — Chạy bằng .NET Aspire (Khuyến nghị)](#6-cách-a--chạy-bằng-net-aspire-khuyến-nghị)
- [7. Cách B — Chạy bằng Docker Compose + .NET CLI](#7-cách-b--chạy-bằng-docker-compose--net-cli)
- [8. Cách C — Chạy toàn bộ bằng Docker Compose](#8-cách-c--chạy-toàn-bộ-bằng-docker-compose)
- [9. Kiểm tra dự án đã chạy thành công](#9-kiểm-tra-dự-án-đã-chạy-thành-công)
- [10. Chạy Test](#10-chạy-test)
- [11. Các lệnh thường dùng](#11-các-lệnh-thường-dùng)
- [12. Cấu trúc dự án tổng quan](#12-cấu-trúc-dự-án-tổng-quan)
- [13. Xử lý lỗi thường gặp](#13-xử-lý-lỗi-thường-gặp)
- [14. Lưu ý quan trọng](#14-lưu-ý-quan-trọng)

---

## 1. Yêu cầu hệ thống

| Phần mềm           | Phiên bản      | Link tải                                              | Kiểm tra                |
| ------------------- | -------------- | ----------------------------------------------------- | ----------------------- |
| **Git**             | 2.x+           | https://git-scm.com/downloads                         | `git --version`         |
| **.NET SDK**        | **10.0+**      | https://dotnet.microsoft.com/download/dotnet/10.0     | `dotnet --version`      |
| **Docker Desktop**  | Latest         | https://www.docker.com/products/docker-desktop        | `docker --version`      |
| **IDE (tuỳ chọn)** | —              | Visual Studio 2022+ / VS Code / JetBrains Rider       | —                       |

> **Lưu ý**: Dự án sử dụng .NET 10. File `global.json` yêu cầu SDK version `10.0.100` trở lên với `rollForward: latestFeature`.

---

## 2. Clone dự án

```powershell
# Clone repository
git clone https://github.com/tinse-22/GSP26SE43.ModularMonolith.git

# Di chuyển vào thư mục dự án
cd GSP26SE43.ModularMonolith
```

---

## 3. Cài đặt .NET SDK 10

### Windows

1. Truy cập https://dotnet.microsoft.com/download/dotnet/10.0
2. Tải **.NET SDK 10.0** (installer cho Windows x64)
3. Chạy file `.exe` và làm theo hướng dẫn
4. Mở **PowerShell mới** và kiểm tra:

```powershell
dotnet --version
# Kết quả mong đợi: 10.0.xxx
```

### Kiểm tra .NET Aspire workload

Aspire workload thường được cài tự động khi restore project. Nếu cần cài thủ công:

```powershell
dotnet workload install aspire
```

---

## 4. Cài đặt Docker Desktop

### Windows

1. Truy cập https://www.docker.com/products/docker-desktop
2. Tải và cài đặt **Docker Desktop for Windows**
3. Sau khi cài xong, **khởi động Docker Desktop**
4. Đợi Docker Desktop hiện trạng thái **"Running"** (icon cá voi xanh ở system tray)
5. Kiểm tra:

```powershell
docker --version
# Kết quả mong đợi: Docker version 2x.x.x

docker compose version
# Kết quả mong đợi: Docker Compose version v2.x.x
```

> **Quan trọng**: Docker Desktop **phải đang chạy** trước khi thực hiện các bước tiếp theo. Cả 2 cách chạy (Aspire và Docker Compose) đều cần Docker.

---

## 5. Cấu hình Environment Variables

Dự án sử dụng file `.env` để quản lý biến môi trường. File `.env` **KHÔNG được commit** lên Git (đã có trong `.gitignore`).

### Bước 5.1: Tạo file `.env` từ template

```powershell
cd GSP26SE43.ModularMonolith
copy .env.example .env
```

### Bước 5.2: Chỉnh sửa file `.env`

Mở file `.env` bằng editor và cập nhật các giá trị quan trọng:

```dotenv
# ==============================
# Database (giá trị mặc định OK cho local dev)
# ==============================
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=ClassifiedAds

# ==============================
# Connection String
# - Host=127.0.0.1 cho local dev (Aspire hoặc .NET CLI)
# - Host=db         cho Docker Compose (toàn bộ chạy trong Docker)
# ==============================
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=postgres

# ==============================
# JWT Secret Key (>= 32 ký tự)
# ==============================
Modules__Identity__Jwt__SecretKey=ClassifiedAds-JWT-Secret-Key-Must-Be-At-Least-32-Characters-Long-2026!
Modules__Identity__Jwt__Issuer=ClassifiedAds
Modules__Identity__Jwt__Audience=ClassifiedAds.WebAPI

# ==============================
# Messaging
# ==============================
Messaging__Provider=RabbitMQ
Messaging__RabbitMQ__HostName=localhost

# ==============================
# Storage
# ==============================
Modules__Storage__Provider=Local
Modules__Storage__Local__Path=C:\Data\files
```

> Xem danh sách đầy đủ biến môi trường tại [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md).

### Bước 5.3 (Nếu chạy toàn bộ bằng Docker Compose): Tạo file `.env.docker`

```powershell
copy .env.docker.example .env.docker
```

Chỉnh sửa `.env.docker` — chú ý **Host phải là tên Docker service** (`db`, `rabbitmq`,...) thay vì `127.0.0.1`:

```dotenv
ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=postgres
Messaging__RabbitMQ__HostName=rabbitmq
```

---

## 6. Cách A — Chạy bằng .NET Aspire (Khuyến nghị)

> Đây là cách **đơn giản nhất** — chỉ cần **1 lệnh duy nhất**. Aspire tự động khởi động tất cả infrastructure (PostgreSQL, RabbitMQ, Redis, MailHog), chạy migration, và khởi động ứng dụng.

### Bước 6.1: Restore packages

```powershell
cd GSP26SE43.ModularMonolith
dotnet restore
```

### Bước 6.2: Trust HTTPS dev certificate

```powershell
dotnet dev-certs https --trust
```

> Chỉ cần chạy 1 lần. Giúp truy cập Aspire Dashboard qua HTTPS mà không bị lỗi certificate.

### Bước 6.3: Chạy Aspire AppHost

```powershell
dotnet run --project ClassifiedAds.AppHost
```

### Bước 6.4: Kiểm tra kết quả

Khi thấy log như sau nghĩa là đã chạy thành công:

```
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: https://localhost:17280
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at https://localhost:17280/login?t=<token>
```

### Bước 6.5: Truy cập các service

| Service              | Cách truy cập                                                               |
| -------------------- | ---------------------------------------------------------------------------- |
| **Aspire Dashboard** | Mở URL `https://localhost:17280` (hiện trong console output)                 |
| **WebAPI Docs**      | Trong Aspire Dashboard → click endpoint của **webapi** → thêm `/docs`       |
| **PgAdmin**          | Tự động khởi động, truy cập qua Aspire Dashboard                            |
| **RabbitMQ UI**      | Truy cập qua Aspire Dashboard                                               |

### Thứ tự khởi động (Aspire tự quản lý)

```
PostgreSQL ──→ Migrator ──→ WebAPI
                         ──→ Background Worker
RabbitMQ   ─────────────────→ WebAPI + Background
Redis      ─────────────────→ WebAPI + Background
MailHog    ─────────────────→ Background
```

### Dừng dự án

Nhấn `Ctrl + C` trong terminal đang chạy Aspire. Tất cả services sẽ tự động dừng.

---

## 7. Cách B — Chạy bằng Docker Compose + .NET CLI

> Cách này chạy **infrastructure** trong Docker, còn **ứng dụng .NET** chạy trực tiếp trên máy. Phù hợp khi muốn debug code.

### Bước 7.1: Khởi động Infrastructure

```powershell
cd GSP26SE43.ModularMonolith
docker compose up -d db rabbitmq redis mailhog
```

Kiểm tra các container đã chạy:

```powershell
docker compose ps
```

Kết quả mong đợi: 4 container ở trạng thái `Up`.

### Bước 7.2: Đợi PostgreSQL sẵn sàng (~5-10 giây)

```powershell
docker compose logs db
# Đợi thấy: "database system is ready to accept connections"
```

### Bước 7.3: Chạy Database Migration

```powershell
dotnet run --project ClassifiedAds.Migrator
```

> Migration tạo/cập nhật schema database cho tất cả modules. Chạy xong sẽ tự thoát.

### Bước 7.4: Chạy Web API

```powershell
dotnet run --project ClassifiedAds.WebAPI
```

WebAPI sẽ chạy tại `https://localhost:44312` (hoặc `http://localhost:5099`).

### Bước 7.5 (Tuỳ chọn): Chạy Background Worker

Mở **terminal mới** (giữ terminal WebAPI đang chạy):

```powershell
cd GSP26SE43.ModularMonolith
dotnet run --project ClassifiedAds.Background
```

> Background Worker xử lý: gửi email, publish outbox messages, consume message bus events.

### Truy cập các service

| Service          | URL                          | Credentials        |
| ---------------- | ---------------------------- | ------------------- |
| **WebAPI Docs**  | `https://localhost:44312/docs` | —                 |
| **RabbitMQ UI**  | `http://localhost:15672`     | guest / guest       |
| **MailHog**      | `http://localhost:8025`      | — (xem email test)  |
| **PostgreSQL**   | `localhost:5432`             | postgres / postgres  |
| **Redis**        | `localhost:6379`             | —                   |

### Dừng dự án

```powershell
# Dừng WebAPI/Background: Ctrl + C trong terminal tương ứng

# Dừng Docker containers
docker compose down

# Dừng containers VÀ xoá data (reset database hoàn toàn)
docker compose down -v
```

---

## 8. Cách C — Chạy toàn bộ bằng Docker Compose

> Cách này build và chạy **tất cả** (infrastructure + application) trong Docker containers. Phù hợp cho demo hoặc khi không cần debug.

### Bước 8.1: Tạo file `.env.docker`

```powershell
copy .env.docker.example .env.docker
```

Chỉnh sửa file `.env.docker` với các giá trị phù hợp (chú ý hostname dùng tên Docker service).

### Bước 8.2: Build và chạy tất cả

```powershell
docker compose --env-file .env.docker up --build -d
```

### Bước 8.3: Theo dõi logs

```powershell
# Xem logs tất cả services
docker compose logs -f

# Xem logs của service cụ thể
docker compose logs -f webapi
docker compose logs -f migrator
```

### Truy cập các service

| Service          | URL                         | Credentials         |
| ---------------- | --------------------------- | -------------------- |
| **WebAPI Docs**  | `http://localhost:9002/docs` | —                  |
| **RabbitMQ UI**  | `http://localhost:15672`    | guest / guest        |
| **MailHog**      | `http://localhost:8025`     | — (xem email test)   |
| **PostgreSQL**   | `localhost:5432`            | Theo `.env.docker`   |
| **Redis**        | `localhost:6379`            | —                    |

### Dừng dự án

```powershell
docker compose down

# Dừng VÀ xoá data
docker compose down -v
```

---

## 9. Kiểm tra dự án đã chạy thành công

### 9.1. Kiểm tra API Health

Truy cập URL API docs:
- **Aspire**: Xem endpoint trong Aspire Dashboard → thêm `/docs`
- **Cách B**: `https://localhost:44312/docs`
- **Cách C**: `http://localhost:9002/docs`

Nếu thấy trang **Scalar API Documentation** hiện danh sách endpoints → dự án đã chạy thành công.

### 9.2. Test đăng nhập (nếu đã có seed data)

```http
POST /api/Auth/login
Content-Type: application/json

{
  "email": "tinvtse@gmail.com",
  "password": "Admin@123",
  "rememberMe": true
}
```

### 9.3. Kiểm tra Database

Kết nối PostgreSQL bằng tool (PgAdmin, DBeaver, hoặc pgcli):

```
Host: localhost (hoặc xem trong Aspire Dashboard)
Port: 5432
Database: ClassifiedAds
Username: postgres
Password: postgres
```

Kiểm tra migration đã chạy:

```sql
SELECT "MigrationId" FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId" DESC LIMIT 5;
```

---

## 10. Chạy Test

```powershell
# Chạy tất cả test
dotnet test

# Chạy architecture test
dotnet test --filter "FullyQualifiedName~Architecture"

# Chạy unit test
dotnet test --project ClassifiedAds.UnitTests

# Chạy integration test
dotnet test --project ClassifiedAds.IntegrationTests

# Chạy test với code coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 11. Các lệnh thường dùng

| Mục đích                        | Lệnh                                                     |
| -------------------------------- | --------------------------------------------------------- |
| **Chạy toàn bộ (nhanh nhất)**  | `dotnet run --project ClassifiedAds.AppHost`              |
| **Build kiểm tra lỗi**         | `dotnet build`                                            |
| **Restore packages**            | `dotnet restore`                                          |
| **Chạy tất cả test**           | `dotnet test`                                             |
| **Xem .NET SDK version**        | `dotnet --version`                                        |
| **Xem Docker containers**       | `docker compose ps`                                       |
| **Xem Docker logs**             | `docker compose logs -f <service_name>`                   |
| **Dừng Docker containers**      | `docker compose down`                                     |
| **Reset database (xoá data)**  | `docker compose down -v`                                  |
| **Rebuild Docker images**       | `docker compose build --no-cache`                         |
| **Aspire workload**             | `dotnet workload install aspire`                          |
| **Trust HTTPS cert**            | `dotnet dev-certs https --trust`                          |

---

## 12. Cấu trúc dự án tổng quan

```
GSP26SE43.ModularMonolith/
│
├── Hosts (Entry Points)
│   ├── ClassifiedAds.AppHost/           # .NET Aspire orchestration (chạy TẤT CẢ)
│   ├── ClassifiedAds.WebAPI/            # REST API server (Scalar docs tại /docs)
│   ├── ClassifiedAds.Background/        # Background worker (email, messaging)
│   └── ClassifiedAds.Migrator/          # Database migration tool
│
├── Shared Layers
│   ├── ClassifiedAds.Application/       # CQRS (Dispatcher, ICommand, IQuery)
│   ├── ClassifiedAds.Contracts/         # Shared interfaces/DTOs giữa modules
│   ├── ClassifiedAds.CrossCuttingConcerns/  # Utilities (CSV, PDF, JSON)
│   ├── ClassifiedAds.Domain/            # Domain entities, events, interfaces
│   ├── ClassifiedAds.Infrastructure/    # Messaging, storage, LLM clients
│   ├── ClassifiedAds.Persistence.PostgreSQL/  # EF Core + PostgreSQL
│   └── ClassifiedAds.ServiceDefaults/   # Aspire service defaults
│
├── Modules (Business Logic)
│   ├── ClassifiedAds.Modules.Identity/        # Authentication, users, roles
│   ├── ClassifiedAds.Modules.Configuration/   # App settings management
│   ├── ClassifiedAds.Modules.Storage/         # File upload/storage
│   ├── ClassifiedAds.Modules.Notification/    # Email, SMS, Web notifications
│   ├── ClassifiedAds.Modules.AuditLog/        # Action tracking
│   ├── ClassifiedAds.Modules.Product/         # Product management
│   ├── ClassifiedAds.Modules.Subscription/    # Plans, billing, usage
│   ├── ClassifiedAds.Modules.ApiDocumentation/ # API spec import/parse
│   ├── ClassifiedAds.Modules.TestGeneration/  # Test case generation
│   ├── ClassifiedAds.Modules.TestExecution/   # Test runner
│   ├── ClassifiedAds.Modules.TestReporting/   # Reports (PDF/CSV)
│   └── ClassifiedAds.Modules.LlmAssistant/   # LLM integration
│
├── Tests
│   ├── ClassifiedAds.UnitTests/         # Unit + Architecture tests
│   └── ClassifiedAds.IntegrationTests/  # Integration tests
│
├── Config Files
│   ├── .env.example                     # Template biến môi trường (local dev)
│   ├── .env.docker.example              # Template biến môi trường (Docker)
│   ├── docker-compose.yml               # Docker Compose definition
│   ├── global.json                      # .NET SDK version constraint
│   └── ClassifiedAds.ModularMonolith.slnx  # Solution file
│
└── docs/                                # Tài liệu dự án
```

---

## 13. Xử lý lỗi thường gặp

### 13.1. `Cannot connect to PostgreSQL` / Connection refused

**Nguyên nhân**: Docker chưa chạy hoặc container PostgreSQL chưa sẵn sàng.

```powershell
# Kiểm tra Docker đang chạy
docker info

# Nếu dùng Cách B/C, kiểm tra container
docker compose ps
docker compose up -d db

# Đợi PostgreSQL sẵn sàng
docker compose logs db
```

### 13.2. `Port already in use`

**Nguyên nhân**: Port đã bị chiếm bởi process khác.

```powershell
# Tìm process chiếm port (VD: port 5432)
netstat -ano | findstr :5432

# Dừng tất cả containers cũ
docker compose down
```

### 13.3. `SDK version not found` / `global.json requires .NET 10`

**Nguyên nhân**: Chưa cài .NET SDK 10.0.

```powershell
# Kiểm tra SDK đã cài
dotnet --list-sdks

# Nếu chưa có 10.0.x → Tải tại https://dotnet.microsoft.com/download/dotnet/10.0
```

### 13.4. Aspire Dashboard lỗi HTTPS certificate

```powershell
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### 13.5. `.env file not found` / Configuration missing

```powershell
# Tạo file .env từ template
copy .env.example .env
# Rồi chỉnh sửa các giá trị trong .env
```

### 13.6. `ConnectionStrings:Default is missing`

**Nguyên nhân**: File `.env` không có hoặc thiếu biến `ConnectionStrings__Default`.

```powershell
# Kiểm tra file .env tồn tại và có nội dung
type .env | findstr ConnectionStrings
```

### 13.7. Login trả về `Invalid email or password` dù nhập đúng

**Nguyên nhân phổ biến**: Đang chạy đồng thời 2 mode (AppHost + WebAPI standalone) → request bị gửi nhầm instance.

**Cách fix**:

```powershell
# 1. Tắt tất cả process .NET đang chạy
Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
  Where-Object { $_.CommandLine -match 'ClassifiedAds' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

# 2. Chọn 1 mode duy nhất và chạy lại
dotnet run --project ClassifiedAds.AppHost
```

### 13.8. Docker build thất bại

```powershell
# Build lại từ đầu (xoá cache)
docker compose build --no-cache

# Nếu vẫn lỗi, thử xoá images cũ
docker compose down --rmi local
docker compose up --build -d
```

---

## 14. Lưu ý quan trọng

### KHÔNG chạy đồng thời 2 mode

Chỉ chọn **1 trong 3 cách** (Aspire / Docker Compose + CLI / Full Docker) tại một thời điểm. Chạy đồng thời sẽ gây conflict port và nhầm lẫn database.

### Single Source of Truth cho Database

- Connection string luôn lấy từ biến `ConnectionStrings__Default` trong file `.env` (hoặc `.env.docker`).
- Khi chạy **Aspire**, connection string được Aspire tự inject — không cần cấu hình thủ công.
- Khi chạy **standalone**, đảm bảo `Host=127.0.0.1` trong `.env`.
- Khi chạy **full Docker**, đảm bảo `Host=db` trong `.env.docker`.

### Database Schema

Mỗi module sử dụng schema riêng trong cùng 1 database `ClassifiedAds`:

| Module         | Schema           |
| -------------- | ---------------- |
| Identity       | `identity`       |
| Subscription   | `subscription`   |
| Configuration  | `configuration`  |
| AuditLog       | `audit_log`      |
| Storage        | `storage`        |
| Notification   | `notification`   |
| Product        | `product`        |
| EF Migrations  | `public`         |

### File `.env` KHÔNG được commit lên Git

File `.env` và `.env.docker` chứa thông tin nhạy cảm → đã được thêm vào `.gitignore`. Chỉ commit file `.env.example` và `.env.docker.example`.

---

## Tóm tắt nhanh

```
┌─────────────────────────────────────────────────────────────────┐
│                    QUICK START (1 LỆNH DUY NHẤT)                │
│                                                                  │
│   1. git clone https://github.com/tinse-22/GSP26SE43.ModularMonolith.git │
│   2. cd GSP26SE43.ModularMonolith                                │
│   3. copy .env.example .env                                      │
│   4. dotnet run --project ClassifiedAds.AppHost                  │
│   5. Mở link Aspire Dashboard hiện trong console                 │
│                                                                  │
│   Nhấn Ctrl + C để dừng.                                         │
└─────────────────────────────────────────────────────────────────┘
```
