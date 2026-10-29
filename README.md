# Quick Translator

Công cụ dịch nhanh cho Windows (C# / .NET 10 WinForms). Chạy nền ở khay hệ thống (system tray).

## Cách dùng

1. Chạy `QuickTranslator.exe` – icon xuất hiện ở system tray.
2. Bôi đen (select) text ở bất kỳ ứng dụng nào, nhấn **Ctrl + Ctrl** (nhấn đúp Ctrl) → cửa sổ dịch hiện ra cạnh con trỏ chuột kèm bản dịch.
3. `Esc` hoặc nút đóng chỉ ẩn cửa sổ; app vẫn chạy nền. Thoát hẳn: chuột phải icon tray → **Exit**.

Trong cửa sổ:

| Thao tác | Kết quả |
|---|---|
| Chọn ngôn ngữ nguồn / đích | Có **Auto detect**; lựa chọn được lưu lại |
| ⇄ | Đảo chiều dịch |
| Dropdown ở góc phải ô Translation | Đổi engine dịch (đồng bộ với menu tray) |
| Gõ / dán vào ô trên | Tự dịch sau 0.5s (hoặc `Ctrl+Enter`) |
| `Ctrl+Shift+C` hoặc nút Copy | Copy bản dịch |

Ở chế độ Auto, nếu text đã đúng ngôn ngữ đích (vd bôi đen tiếng Việt khi đích là Vietnamese) thì tự dịch sang English (nếu đích là English thì sang Vietnamese).

Cửa sổ có kích thước cố định, giao diện theo light/dark của Windows, không có phần cài đặt – mọi cài đặt nằm ở menu tray.

Menu chuột phải icon tray:
- **Shortcut**: Ctrl+Ctrl, Alt+Alt, Ctrl+Alt+T hoặc **Custom…** (bấm tổ hợp phím bạn muốn, vd `Ctrl+Shift+Q`; cần Ctrl/Alt/Shift kèm một phím, riêng phím F1–F24 dùng một mình được; phím này sẽ không gửi tới app đang mở).
- **Translation engine**, **Start with Windows**, **Open settings file**.

## Engine dịch

- **Google (free)** – mặc định, không cần key (endpoint web không chính thức, có thể bị giới hạn khi dùng nhiều).
- **Google Cloud (API key)** – API chính thức; điền `GoogleApiKey` trong `%APPDATA%\QuickTranslator\settings.json`.
- **MyMemory (free)** – dự phòng.

Nếu engine đang chọn lỗi, app tự thử các engine còn lại.

## Build

```powershell
dotnet build -c Release
# bản chạy gọn (cần .NET 10 Desktop Runtime):
dotnet publish -c Release -o publish -p:PublishSingleFile=true --self-contained false
```

## Cài đặt và cập nhật

- Bộ cài: `.\build-installer.ps1 -Version 1.0.0` → `dist\QuickTranslator-Setup-1.0.0.exe` (cần Inno Setup 6; cài theo từng người dùng, không cần Admin, đã kèm .NET).
- Tự cập nhật qua **GitHub Releases**: app kiểm tra bản mới khi khởi động và qua menu tray **Check for updates…**. Khi có bản mới, app hỏi, tải bộ cài, cài đè và tự mở lại (cài đặt người dùng được giữ nguyên).
- Phát hành bản mới (cần GitHub CLI `gh`, đã `gh auth login`, repo public):
  1. Repo nhận release được đặt ở `AppInfo.GitHubRepo` trong `UpdateService.cs` (hiện là `hovannam1612/QuickTranslatorTool`).
  2. `.\publish-release.ps1 -Version 1.1.0 -Notes "Nội dung thay đổi"` – build bộ cài và tạo release `v1.1.0` kèm file Setup.
  3. Số version phải **lớn hơn** bản trước; tag phải dạng `v1.1.0`.
- Bản chạy portable (không cài bằng Setup) chỉ được mở trang tải về, không cập nhật tại chỗ.

## Lưu ý

- Text được lấy bằng cách giả lập `Ctrl+C` rồi khôi phục lại clipboard (chỉ khôi phục được nội dung dạng text).
- Nếu app đích đang chạy quyền Administrator, hãy chạy Quick Translator bằng quyền Administrator để bắt được phím tắt và lấy được text.
- Cài đặt lưu tại `%APPDATA%\QuickTranslator\settings.json`.
