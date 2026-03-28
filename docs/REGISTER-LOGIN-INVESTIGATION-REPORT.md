# Report dieu tra su co Register/Login

Ngay lap: 2026-03-27
Nguon phan tich: codebase hien tai + log WebAPI trong repo

## 1) Tom tat van de

Hien tuong duoc mo ta:
- Dang ky tai khoan xong co the dang nhap.
- Sau khi logout hoac tat BE (Backend), dang nhap lai bao sai mat khau.

Ket luan nhanh:
- Co dau hieu rat ro cua viec dang chay mixed runtime mode (hoac doi DB target giua cac lan chay), dan den viec user vua tao nam o DB A, nhung lan login tiep theo lai truy van DB B.
- Message tra ve cua endpoint login dung chung cho ca 2 truong hop user khong ton tai va sai password, nen de gay hieu nham la "sai mat khau".

## 2) Bang chung tu code va cau hinh

### 2.1 Login tra cung 1 loi cho 2 tinh huong

Trong AuthController:
- Neu khong tim thay user theo email thi tra Unauthorized voi thong diep "Email hoac mat khau khong dung".
- Neu check password fail thi cung tra thong diep y het.

He qua:
- Khi user khong ton tai (vi dang truy van nham DB), FE van nhin thay loi giong sai password.

### 2.2 Dang ky tao user khong xac nhan email, login bat buoc email da xac nhan

Trong AuthController:
- Register dat EmailConfirmed = false.
- Login co check user.EmailConfirmed, neu false thi tra loi "Vui long xac nhan dia chi email truoc khi dang nhap".

He qua:
- Neu FE mapping loi khong dung (hoac bo qua noi dung loi), co the bi hieu nham thanh "sai mat khau".

### 2.3 Cac host nap bien moi truong tu file .env khi khong chay trong container

Trong WebAPI Program:
- Neu khong co DOTNET_RUNNING_IN_CONTAINER=true thi host se DotEnv.Load va dung bien tu .env.

He qua:
- Chay local process (F5/dotnet run) se su dung .env.
- Chay bang docker-compose se su dung gia tri env theo docker-compose/.env.docker.
- Neu doi cach chay ma khong dong bo DB target, du lieu user se bi "mat" theo goc nhin cua runtime moi.

### 2.4 Cau hinh DB target dang khac nhau giua .env va .env.docker

Quan sat trong repo:
- .env dang tro ConnectionStrings__Default toi host Supabase (remote).
- .env.docker dang tro ConnectionStrings__Default toi db:5432 (Postgres trong compose).

He qua:
- Ban dang ky o mode local process co the tao user o Supabase.
- Sau do chuyen sang docker-compose hoac restart theo mode khac se login vao DB khac, tim khong ra user va bao loi giong sai password.

### 2.5 Bang chung bo sung trong log

Log WebAPI co cac dau hieu khong on dinh ket noi/schema DB:
- password authentication failed for user postgres
- relation identity.DataProtectionKeys does not exist

Dieu nay cung cu kha nang runtime dang tro khac DB/schema hoac migration/chuoi ket noi khong dong nhat giua cac lan chay.

## 3) Gia thuyet nguyen nhan goc (xep hang theo kha nang)

Muc do cao (kha nang cao nhat):
1. Mixed runtime mode, khong co single source of truth cho ConnectionStrings__Default giua local process va docker-compose.
2. Vi login tra chung message cho user-not-found va wrong-password, trieu chung bi nham la sai mat khau.

Muc do trung binh:
3. Account chua confirm email, nhung FE hien thi thong diep chung "sai mat khau".
4. DB co luc ket noi fail hoac schema chua day du (the hien qua log DataProtectionKeys/PG auth fail), gay hanh vi dang nhap khong on dinh.

Muc do thap:
5. Loi hash password trong Identity (hien chua thay bang chung truc tiep).

## 4) Cach tai hien chuan de xac minh

Buoc 1: Chon 1 runtime mode duy nhat
- Chi local process, hoac chi docker-compose.
- Khong tron 2 mode trong cung mot vong test.

Buoc 2: Ghi lai chinh xac DB target
- In ra ConnectionStrings__Default cua host dang chay.
- Xac nhan DB name + host + port.

Buoc 3: Kich ban test
1. Register account moi.
2. Confirm email neu luong yeu cau.
3. Login thanh cong.
4. Logout.
5. Login lai ngay lap tuc trong cung runtime mode.
6. Restart backend cung mode do, login lai.

Buoc 4: Truy van DB cung luc test
- Kiem tra user co trong schema identity hay khong.
- Kiem tra EmailConfirmed va PasswordHash co gia tri.

Neu pass o Buoc 5-6 khi giu nguyen mode, nhung fail khi doi mode => ket luan nguyen nhan do mismatch DB target.

## 5) Khuyen nghi khac phuc

Khac phuc ngay:
1. Chot 1 mode dev chinh va 1 ConnectionStrings__Default duy nhat cho mode do.
2. Neu chay docker-compose thi uu tien .env.docker; neu chay local process thi dam bao .env trung DB voi mode mong muon.
3. Khong de tinh trang local process tro remote DB, con docker tro local DB trong cung 1 chu ky test register/login.

Khac phuc de tranh tai dien:
4. Them logging tai login de phan biet ro:
   - user not found
   - email not confirmed
   - password mismatch
   (chi log noi bo, response cho client van co the generic vi security).
5. FE map thong diep loi theo ma loi/HTTP status thay vi gom chung thanh "sai mat khau".
6. Bo sung integration test cho scenario:
   - register -> confirm -> login -> logout -> login lai -> restart host -> login lai.

## 6) Ket luan

Khong thay dau hieu ro rang cho thay mat khau bi hash sai ngay luc register.
Dau hieu manh nhat la backend dang thay doi DB target giua cac lan chay (mixed runtime mode), trong khi endpoint login tra message chung cho user-not-found va wrong-password, dan den cam giac "dung mat khau nhung van sai".
