# Hướng dẫn cài và dùng `rtk.exe` trong repository

Tập tin `rtk.exe` được đặt kỳ vọng ở: `rtk-x86_64-pc-windows-msvc\rtk.exe` (tức: `D:\GSP26SE43.ModularMonolith\rtk-x86_64-pc-windows-msvc\rtk.exe`).

- Nếu bạn muốn chạy ngay từ repository, để nguyên như vậy.
- Nếu muốn gọi từ bất kỳ chỗ nào trên máy, thêm thư mục chứa `rtk.exe` vào `PATH`.

Chạy nhanh (không cần thêm vào PATH):

```powershell
# Từ thư mục repo
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-rtk.ps1 -- --help
```

Hoặc dùng Task trong VS Code (Open Command Palette -> Tasks: Run Task -> chọn "Run rtk")

Thêm vào PATH (thay đường dẫn nếu cần):

```powershell
setx PATH "$env:PATH;D:\GSP26SE43.ModularMonolith\rtk-x86_64-pc-windows-msvc"
# Mở lại terminal/PowerShell để thay đổi PATH có hiệu lực
```

Cảnh báo:
- Tôi KHÔNG chạy hoặc cài đặt file `rtk.exe` tự động vì đó là thực thi nhị phân không rõ nguồn gốc; nếu bạn muốn, tôi có thể chạy nó sau khi bạn xác nhận.

Muốn tôi tiếp tục và chạy `rtk.exe` trên project (ví dụ: quét hoặc áp dụng một lệnh cụ thể)? Trả lời lệnh bạn muốn chạy hoặc xác nhận nếu cho phép chạy nhị phân.
