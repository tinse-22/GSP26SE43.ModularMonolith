# RENDER DEPLOYMENT GUIDE (WebAPI + Background + Migrator)

Tai lieu nay huong dan deploy repo nay len Render theo cach chuan, on dinh, va de mo rong.

## 1) Tong quan quan trong

- Tren Render, moi service chi co 1 Dockerfile Path.
- Vi repo co 3 executable chinh, can tao 3 service rieng:
  - WebAPI (public web service)
  - Background (private worker)
  - Migrator (private worker/job de chay migration)
- Khong truyen env qua Dockerfile Path.
- Dockerfile Path chi dung de Render build image.
- Env vars duoc set o Environment Group hoac Environment cua tung service.

## 2) Dockerfile Path dung cho tung service

- WebAPI:
  - `ClassifiedAds.WebAPI/Dockerfile`
- Background:
  - `ClassifiedAds.Background/Dockerfile`
- Migrator:
  - `ClassifiedAds.Migrator/Dockerfile`

Luu y:
- Khong dung `./Dockerfile` vi repo khong co Dockerfile o root.

## 3) Chuan bi truoc khi tao service

Tao cac managed service truoc:
- PostgreSQL
- Redis
- RabbitMQ

Sau do lay internal host/port/credential de dien env.

## 4) Tao Environment Group dung chung

Tao 1 Environment Group, vi du: `classifiedads-shared`.

Import tu file `.env.render`, sau do thay TOAN BO placeholder `YOUR_...` va `REPLACE_WITH_...`.

### 4.1) Cac bien bat buoc nen co trong group chung

- `ASPNETCORE_ENVIRONMENT=Production`
- `DOTNET_ENVIRONMENT=Production`
- `ConnectionStrings__Default=Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`
- `Modules__Identity__BootstrapDevelopmentData=false`
- `Modules__Identity__ConnectionStrings__CommandTimeout=60`
- `Caching__Distributed__Provider=Redis`
- `Caching__Distributed__Redis__Configuration=<redis-host>:6379`
- `Caching__Distributed__Redis__InstanceName=ClassifiedAds_`
- `Messaging__Provider=RabbitMQ`
- `Messaging__RabbitMQ__HostName=<rabbit-host>`
- `Messaging__RabbitMQ__UserName=<rabbit-user>`
- `Messaging__RabbitMQ__Password=<rabbit-password>`
- `Messaging__RabbitMQ__ExchangeName=amq.direct`
- `Modules__Storage__Provider=Local`
- `Modules__Storage__Local__Path=/var/data/storage`
- `Modules__Storage__MasterEncryptionKey=<base64-32-byte-key>`
- `Modules__Identity__Jwt__SecretKey=<strong-secret>`
- `Modules__Identity__Jwt__Issuer=https://<webapi-service>.onrender.com`
- `Modules__Identity__Jwt__Audience=ClassifiedAds.WebAPI`
- `Modules__Identity__Jwt__AccessTokenExpirationMinutes=60`
- `Modules__Identity__Jwt__RefreshTokenExpirationDays=7`
- `CORS__AllowAnyOrigin=false`
- `CORS__AllowedOrigins__0=https://<frontend-domain>`
- `Modules__Notification__Email__Provider=Fake`
- `Modules__Notification__Sms__Provider=Fake`
- `Modules__Notification__Web__Provider=Fake`

### 4.2) Bien rieng cho Migrator

- `CheckDependency__Enabled=true`
- `CheckDependency__Host=<postgres-host>:5432`

Co the dat 2 bien nay trong group chung, hoac dat rieng o service Migrator.

## 5) Tao service WebAPI (public)

1. Render Dashboard -> New -> Web Service
2. Chon repo nay va branch can deploy
3. Environment:
   - Docker
4. Dockerfile Path:
   - `ClassifiedAds.WebAPI/Dockerfile`
5. Attach Environment Group:
   - `classifiedads-shared`
6. Port:
   - de app bind qua env `ASPNETCORE_URLS=http://0.0.0.0:8080`
7. Health Check Path:
   - `/health`
8. Create Web Service va deploy

## 6) Tao service Background (private worker)

1. Render Dashboard -> New -> Background Worker
2. Chon cung repo va branch
3. Environment:
   - Docker
4. Dockerfile Path:
   - `ClassifiedAds.Background/Dockerfile`
5. Attach Environment Group:
   - `classifiedads-shared`
6. Khong can public port
7. Create worker va deploy

## 7) Tao service Migrator (worker/job)

Co 2 cach:

- Cach A: Background Worker (chay lien tuc)
- Cach B: Job (chay theo trigger/schedule)

Voi repos migration gate, khuyen nghi bat dau bang Background Worker de de quan sat log khoi tao.

1. Render Dashboard -> New -> Background Worker (hoac Job)
2. Chon cung repo va branch
3. Environment:
   - Docker
4. Dockerfile Path:
   - `ClassifiedAds.Migrator/Dockerfile`
5. Attach Environment Group:
   - `classifiedads-shared`
6. Dam bao co 2 bien:
   - `CheckDependency__Enabled=true`
   - `CheckDependency__Host=<postgres-host>:5432`
7. Create service va deploy

## 8) Thu tu deploy de an toan

Khuyen nghi:

1. Deploy Migrator truoc
2. Kiem tra log migrator thanh cong
3. Deploy WebAPI
4. Deploy Background

Neu Migrator fail, khong nen cho WebAPI/Background vao production traffic.

## 9) Kiem tra sau deploy

### 9.1) WebAPI

- Open URL service
- Kiem tra:
  - `GET /health` phai 200
  - `GET /alive` phai 200 (liveness)
- Mo swagger neu da mo route:
  - `/swagger`

### 9.2) Background

- Kiem tra log khong co loop reconnect DB/Rabbit/Redis

### 9.3) Migrator

- Kiem tra log khong bao loi ket noi DB
- Kiem tra migration da apply thanh cong

## 10) Loi thuong gap va cach xu ly nhanh

### Loi 1: Build fail vi Dockerfile Path sai

Trieu chung:
- Render bao khong tim thay Dockerfile

Xu ly:
- Doi Dockerfile Path dung theo service:
  - WebAPI -> `ClassifiedAds.WebAPI/Dockerfile`
  - Background -> `ClassifiedAds.Background/Dockerfile`
  - Migrator -> `ClassifiedAds.Migrator/Dockerfile`

### Loi 2: App chay nhung crash vi thieu env

Trieu chung:
- Startup fail
- JWT/Storage/Rabbit/Redis error

Xu ly:
- Soat lai Environment Group
- Dam bao da thay het placeholder
- Dam bao key dung chinh ta voi pattern `__`

### Loi 3: WebAPI healthy fail

Trieu chung:
- Health check khong pass

Xu ly:
- Dat health check path la `/health`
- Dam bao `ASPNETCORE_URLS=http://0.0.0.0:8080`
- Dam bao WebAPI la `Web Service`, khong phai `Background Worker`
- Docker image nen `EXPOSE 8080`

### Loi 4: Migrator khong doi duoc DB readiness

Trieu chung:
- Migrator fail check dependency

Xu ly:
- Kiem tra:
  - `CheckDependency__Enabled=true`
  - `CheckDependency__Host=<postgres-host>:5432`
  - `ConnectionStrings__Default` dung host noi bo cua Render DB

### Loi 5: `libgssapi_krb5.so.2` khong ton tai

Trieu chung:
- WebAPI / Background / Migrator crash rat som khi khoi tao Npgsql
- Log co dong `libgssapi_krb5.so.2: cannot open shared object file`

Xu ly bat buoc:
- Neu runtime image la Debian/Ubuntu base:

```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
```

- Neu runtime image la Alpine base:

```dockerfile
RUN apk add --no-cache krb5-libs
```

Ghi chu:
- Repo hien tai dang dung image `.NET` base theo Debian, nen can `libgssapi-krb5-2`.
- `ClassifiedAds.Migrator/Dockerfile` can cai package nay o ca build stage va runtime stage, vi build stage co chay `--verify-migrations`.

### Loi 6: Production lai chay `DevelopmentIdentityBootstrapper`

Trieu chung:
- Stack trace di qua `DevelopmentIdentityBootstrapper`
- Query role/user chay ngay luc startup

Xu ly bat buoc:
- Khong dang ky bootstrapper dev mac dinh trong production
- Chi bat bootstrap nay khi co flag ro rang:
  - Local IDE: `Modules__Identity__BootstrapDevelopmentData=true`
  - Render/Production: `Modules__Identity__BootstrapDevelopmentData=false`

Neu khong can seed dev user tren Docker local, co the de flag nay la false o moi noi ngoai IDE.

## 11) Security checklist toi thieu

- Khong dung secret mac dinh
- JWT secret dai va random
- Gioi han CORS dung domain frontend that
- Khong bat `CORS__AllowAnyOrigin=true` tren production
- Khong commit env that vao git

## 12) Ban tom tat copy nhanh

- 1 service Render = 1 Dockerfile Path
- Repo nay can 3 service:
  - WebAPI: `ClassifiedAds.WebAPI/Dockerfile`
  - Background: `ClassifiedAds.Background/Dockerfile`
  - Migrator: `ClassifiedAds.Migrator/Dockerfile`
- Env dat trong Environment Group, khong dat qua Dockerfile Path
- Health check WebAPI: `/health`
- Deploy order: Migrator -> WebAPI -> Background
- Render Postgres runtime: uu tien dung internal host, khong dung external URL neu service cung nam tren Render
