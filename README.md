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

Cửa sổ kéo giãn được (kích thước được nhớ cho lần sau), giao diện theo light/dark của Windows, không có phần cài đặt – mọi cài đặt nằm ở menu tray.

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
# chạy thử khi đang phát triển:
dotnet run
# bản chạy gọn (cần .NET 10 Desktop Runtime trên máy đích):
dotnet publish -c Release -o publish -p:PublishSingleFile=true --self-contained false
```

## Bộ cài (Setup.exe)

`.\build-installer.ps1 -Version 1.0.0` làm hai việc:

1. Build bản **self-contained** (đã kèm .NET, khoảng 49 MB) vào `publish-installer\`.
2. Dùng Inno Setup đóng gói thành `dist\QuickTranslator-Setup-1.0.0.exe` (khoảng 44 MB).

Bộ cài cài theo từng người dùng (vào `%LOCALAPPDATA%\Programs\Quick Translator`, **không cần Admin**), tạo shortcut Start Menu, có tùy chọn tự chạy cùng Windows và shortcut Desktop, và có mục gỡ cài đặt trong *Settings → Apps*. Gỡ cài đặt không xóa `%APPDATA%\QuickTranslator` (cài đặt người dùng).

Thêm `-SkipInstaller` nếu chỉ cần file exe self-contained (không cần Inno Setup).

Icon của app nằm ở một file duy nhất: [app.ico](app.ico). File này vừa được nhúng làm icon của file exe/shortcut/bộ cài, vừa được app đọc lúc chạy để làm icon khay hệ thống và icon cửa sổ (`IconFactory.cs`). Muốn đổi icon chỉ cần thay file `app.ico` (nên có các kích thước 16, 24, 32, 48, 256 px) rồi build lại. [tools/make-icon.ps1](tools/make-icon.ps1) chỉ là công cụ đã dùng để tạo `app.ico` ban đầu; script build chỉ gọi nó khi file `app.ico` bị mất.

## Quy trình phát hành và cập nhật phiên bản

Cơ chế: mỗi bản phát hành là một **GitHub Release** có tag `vX.Y.Z` kèm file `QuickTranslator-Setup-X.Y.Z.exe`. Các máy đã cài app tự hỏi GitHub xem có release mới hơn không, tải bộ cài về và cài đè.

Repo nhận release được đặt ở hằng số `AppInfo.GitHubRepo` trong [UpdateService.cs](UpdateService.cs) (hiện là `hovannam1612/QuickTranslatorTool`, phải là repo **public**).

### A. Chuẩn bị một lần trên máy phát hành

| Việc | Lệnh |
|---|---|
| Cài Inno Setup 6 | `winget install JRSoftware.InnoSetup` |
| Cài GitHub CLI | `winget install GitHub.cli` |
| Đăng nhập GitHub | `gh auth login` (kiểm tra bằng `gh auth status`) |

Nên mở terminal mới sau khi cài để nhận lệnh `gh`.

Nếu chạy `.ps1` bị báo *"running scripts is disabled on this system"*, chạy script qua `powershell -ExecutionPolicy Bypass -File .\ten-script.ps1 <tham số>` (chỉ bỏ qua chặn cho lần chạy đó), hoặc cho phép một lần bằng `Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned`. Ở các ví dụ bên dưới, thêm tiền tố `powershell -ExecutionPolicy Bypass -File` nếu máy bạn bị chặn.

### B. Các bước phát hành mỗi lần có bản mới

**Bước 1 – Code và test.** Sửa code, chạy thử bằng `dotnet run`, kiểm tra tính năng đã sửa.

**Bước 2 – Chọn số version `X.Y.Z`.** Phải **lớn hơn** version của release mới nhất đang có trên GitHub, nếu không máy khác sẽ không thấy bản mới.

| Thay đổi | Tăng | Ví dụ |
|---|---|---|
| Sửa lỗi nhỏ | PATCH | 1.0.0 → 1.0.1 |
| Thêm tính năng, không phá cái cũ | MINOR | 1.0.1 → 1.1.0 |
| Thay đổi lớn | MAJOR | 1.1.0 → 2.0.0 |

Chỉ dùng đúng 3 số. **Không** dùng hậu tố như `1.1.0-beta`, vì app không đọc được tag đó. Nếu cần bản thử nghiệm, tạo release trên GitHub và đánh dấu *Pre-release*; app bỏ qua các bản pre-release và bản nháp (draft).

**Bước 3 – Ghi version vào code.** Sửa `<Version>` trong [QuickTranslator.csproj](QuickTranslator.csproj) cho khớp (vd `<Version>1.1.0</Version>`). Script phát hành luôn nhúng đúng version bạn truyền vào bộ cài, nhưng sửa file này giữ cho bản chạy bằng `dotnet run` và code trong repo khớp với bản đã phát hành.

**Bước 4 – Commit và push lên GitHub.** Làm trước khi tạo release, vì GitHub gắn tag vào commit mới nhất trên nhánh mặc định của repo:

```powershell
git add .
git commit -m "Release 1.1.0: <tóm tắt thay đổi>"
git push
```

**Bước 5 – Build và tạo release.**

```powershell
.\publish-release.ps1 -Version 1.1.0 -Notes "- Thêm ...`n- Sửa lỗi ..."
```

Script tự động: build bản self-contained, đóng gói `dist\QuickTranslator-Setup-1.1.0.exe`, rồi chạy `gh release create v1.1.0` đính kèm file đó. Nội dung `-Notes` hiện trong hộp thoại cập nhật trên máy người dùng (tối đa 600 ký tự đầu, chữ thuần).

*Cách làm thủ công (không dùng `gh`):* chạy `.\build-installer.ps1 -Version 1.1.0`, rồi trên GitHub vào **Releases → Draft a new release**, tạo tag `v1.1.0`, kéo file `dist\QuickTranslator-Setup-1.1.0.exe` vào phần Assets, bấm **Publish release** (không chọn draft hay pre-release). Tên file phải có chữ `Setup` và đuôi `.exe`.

**Bước 6 – Kiểm tra.**
- Mở `https://github.com/hovannam1612/QuickTranslatorTool/releases/latest` và xác nhận đúng tag và có file Setup (hoặc chạy `gh release view v1.1.0`).
- Trên một máy đang cài bản cũ: chuột phải icon tray → **Check for updates…** → phải hiện hộp thoại "Quick Translator 1.1.0 is available".

### C. Người dùng nhận bản cập nhật như thế nào

1. Sau khi khởi động khoảng 15 giây, app tự kiểm tra GitHub (một lần mỗi lần mở app). Nếu có bản mới sẽ hiện thông báo trên tray: *"Quick Translator 1.1.0 is available. Click here to update."* Có thể kiểm tra bất cứ lúc nào qua menu tray **Check for updates…**; khi đã biết có bản mới, mục này đổi thành **Update to version 1.1.0…**.
2. Bấm đồng ý → app tải bộ cài (có thanh tiến trình) → chạy ngầm → bộ cài tắt app, cài đè lên bản cũ và **tự mở lại app**. Cài đặt người dùng (ngôn ngữ, phím tắt, engine, kích thước cửa sổ) được giữ nguyên.
3. Menu tray hiện dòng `Quick Translator vX.Y.Z` để biết đang dùng bản nào.

Lưu ý khi triển khai lần đầu: máy nào chưa có app thì cài **một lần** bằng `QuickTranslator-Setup-1.0.0.exe` (hoặc bản mới nhất); từ đó về sau app tự cập nhật.
Bản chạy portable (không cài bằng Setup) chỉ được mở trang tải về chứ không cập nhật tại chỗ.

### D. Xử lý sự cố khi phát hành và cập nhật

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| "No release has been published yet" | Chưa có release nào; release còn ở dạng draft hoặc pre-release; repo trong `AppInfo.GitHubRepo` sai hoặc là private |
| "The latest release has no installer attached" | Quên đính kèm file, hoặc tên file không có chữ `Setup` / không phải `.exe` |
| "Release tag '…' is not a version number" | Tag sai định dạng; phải là `v1.1.0` (chỉ số, không hậu tố) |
| Máy người dùng không báo có bản mới | Version trong tag không lớn hơn bản đang cài; app chưa được mở lại từ lúc phát hành (chỉ tự kiểm tra khi khởi động, có thể bấm **Check for updates…**); hoặc GitHub giới hạn 60 yêu cầu/giờ cho mỗi địa chỉ IP, chờ rồi thử lại |
| Windows SmartScreen cảnh báo "unknown publisher" | Bộ cài chưa được ký số; bấm *More info → Run anyway*. Muốn hết cảnh báo cần chứng chỉ ký mã (trả phí) |
| `publish-release.ps1` báo thiếu `gh` / Inno Setup | Cài theo mục A rồi chạy lại |
| `HTTP 403: Resource not accessible by personal access token` khi tạo release | `gh` đang đăng nhập bằng token không đủ quyền (thường là fine-grained token `github_pat_…`). Chạy `gh auth logout` rồi `gh auth login` và chọn *Login with a web browser*; hoặc cấp cho token quyền **Contents: Read and write** trên repo. Kiểm tra bằng `gh auth status` |
| "running scripts is disabled on this system" | Execution Policy chặn file `.ps1`; xem cách xử lý ở mục A |

**Phát hành nhầm hoặc có lỗi nặng:**
- **Không** dùng lại số version cũ và không thay file trong release đã có người cập nhật.
- Sửa lỗi rồi phát hành số **cao hơn** (vd 1.1.0 lỗi → phát hành 1.1.1). Máy nào đã lên 1.1.0 sẽ tự nhận 1.1.1.
- Nếu release mới đăng và chưa ai cập nhật, có thể gỡ bằng `gh release delete v1.1.0 --cleanup-tag --yes`.

**Những thứ không được đổi** (nếu đổi, máy đã cài sẽ không cập nhật được nữa):
- `AppId` (dòng GUID) trong [installer/QuickTranslator.iss](installer/QuickTranslator.iss): đổi thì bộ cài coi là app khác và cài song song thay vì cài đè.
- Tên file bộ cài phải giữ dạng `QuickTranslator-Setup-X.Y.Z.exe`, tag giữ dạng `vX.Y.Z`.
- Nếu đổi tên/chuyển repo GitHub, phải đổi `AppInfo.GitHubRepo` và phát hành lại bằng bản mới; các máy đang cài bản cũ vẫn trỏ về repo cũ.

## Lưu ý

- Text được lấy bằng cách giả lập `Ctrl+C` rồi khôi phục lại clipboard (chỉ khôi phục được nội dung dạng text).
- Nếu app đích đang chạy quyền Administrator, hãy chạy Quick Translator bằng quyền Administrator để bắt được phím tắt và lấy được text.
- Cài đặt lưu tại `%APPDATA%\QuickTranslator\settings.json`.
