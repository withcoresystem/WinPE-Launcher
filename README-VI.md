# WinPE Launcher

![WinPE Launcher](ref/screenshot.webp)

Launcher dạng **thanh taskbar**, viết bằng **native (C# win-x64)**, dành cho
**Windows PE (WinPE) amd64** và **Windows x64**. Mở ứng dụng/script, cung cấp công cụ
hệ thống (Wi-Fi, BitLocker, đồng bộ giờ), kèm **System Diagnostic**, công cụ **chụp màn
hình**, và **Smart Assistant** (chat với mọi LLM tương thích OpenAI) — tất cả trong một
file exe duy nhất ~4 MB.

> Thuộc dự án **Universal Builder** của [CoreSystem](https://coresystem.vn).

---

## Điểm nổi bật

- **Native C#** (WinForms / .NET Framework 4.5) — không cần cài đặt, không service, không tải runtime.
- **Một file exe khép kín**, khoảng **4 MB**, **RAM sử dụng dưới 30 MB**.
- **Chạy tốt trên WinPE**: thư viện UI (AntdUI) và font (Inter) được **nhúng sẵn** — **không cần DLL** cạnh exe.
- **Cấu hình bằng 1 file**: `apps-config.json` (cho phép comment và thiếu dấu phẩy) điều khiển apps, branding, timezone, endpoint LLM và tỉ lệ UI.
- **Offline-first**: chỉ Smart Assistant và đồng bộ giờ cần mạng.

---

## Tính năng

| Vùng | Nội dung |
|---|---|
| **System** | Shutdown / Reboot (`wpeutil`), Wi-Fi, mở khóa BitLocker, Đồng bộ giờ |
| **System Tools** | Command Prompt, Notepad, PowerShell, Task Manager, System Info |
| **Applications** | Danh sách app/script từ `apps-config.json`; đường dẫn tương đối được dò trên mọi ổ đĩa theo `\CORESYSTEM\Softwares`, `\CORESYSTEM\Scripts`, `\CORESYSTEM` |
| **Opened Windows** | Liệt kê cửa sổ đang mở (trừ shell), bấm để kích hoạt |
| **Smart Assistant** | Chat với LLM tương thích OpenAI: render Markdown, chọn/copy text, lịch sử JSONL, giới hạn ≤ 500 từ, sửa lỗi mojibake UTF-8, báo lỗi rõ khi thiếu endpoint/key |
| **System Diagnostic** | Tổng quan chỉ-đọc qua `Get-CimInstance`: System, CPU, RAM, tình trạng ổ lưu trữ, volume kèm trạng thái BitLocker, GPU, mạng, pin, thiết bị có lỗi |
| **Screenshot** | Chụp toàn màn hình bằng 1 nút (hoặc phím **PrintScreen**), lưu vào `USB:\Screenshots\IMG-<timestamp>.jpg` |
| **Bar** | Tiêu đề whitebox (≤ 15 ký tự), icon trạng thái mạng (Connected / Disconnected), đồng hồ 2 dòng theo timezone, icon chat / diagnostic / screenshot, chỉnh tỉ lệ UI |
| **Dialog** | Wi-Fi, BitLocker, thông báo toast (có viền accent) |

---

## Mục tiêu thiết kế

- **Chạy trên WinPE.** Không phụ thuộc Segoe UI (Inter nhúng sẵn), không cần DLL ngoài
  (AntdUI nhúng sẵn), và mọi file sinh ra chỉ nằm ở `%TEMP%` (RAM disk) hoặc USB.
- **Nhẹ và native.** Một file exe native khoảng **4 MB**, **RAM dưới 30 MB** — đủ nhẹ để
  đóng gói vào boot media và chạy thoải mái trong môi trường WinPE tối giản.
- **1 exe + 1 file cấu hình.** Chỉ ship `Launcher.exe` và `apps-config.json`.
- **Cấu hình dễ sửa tay.** `apps-config.json` cho phép `//` comment và có thể thiếu dấu
  phẩy (parser tự viết) vì config trên WinPE thường được sửa trực tiếp.
- **UI hiện đại, gọn.** Thanh bar cố định đáy màn hình (56 px, DPI-aware), giao diện tối.
- **Không hardcode bí mật.** Endpoint và key LLM theo dạng **BYOK** — người dùng tự nhập.

---

## Cấu trúc thư mục

```
.
├── LICENSE                     Giấy phép MIT
├── README.md                   README tiếng Anh
├── README-VI.md                file này (tiếng Việt)
├── NOTES.md                    nhật ký phát triển / ghi chú kỹ thuật
├── THIRD-PARTY-NOTICES.md      Inter (OFL-1.1), Hack (MIT), AntdUI (Apache-2.0)
├── Launcher.csproj             project (win-x64, .NET Framework 4.5, C# 7.3)
├── build.cmd                   script build MSBuild
├── apps-config.json            cấu hình mẫu
├── src/                        mã C# (Program, MainForm, Dialogs, Services, Engine, Fonts, Resources)
├── lib/                        AntdUI.dll (tham chiếu lúc build; được nhúng vào exe)
├── ref/                        ảnh tham chiếu (screenshot.webp, builder.webp) + ref assemblies .NET 4.5 đóng gói kèm
└── release/                    Launcher.exe + apps-config.json dựng sẵn
```

---

## Build

Yêu cầu: **Windows** với **MSBuild** (Visual Studio 2017+ hoặc Build Tools).

```cmd
build.cmd
:: tương đương: msbuild Launcher.csproj /t:Rebuild /p:Configuration=Release
:: kết quả: bin\Release\Launcher.exe  và  bin\Release\apps-config.json
```

Không cần .NET 4.5 Targeting Pack — repo đóng gói kèm reference assemblies trong
`ref/.NETFramework/v4.5/` và trỏ `TargetFrameworkRootPath` vào đó.

Triển khai: copy **`Launcher.exe` + `apps-config.json`** vào thư mục bất kỳ
(ví dụ `D:\Softwares\Launcher\` trên USB, hoặc nhúng vào boot image).

---

## Cấu hình (`apps-config.json`)

| Khóa | Ý nghĩa |
|---|---|
| `whitebox` | Tiêu đề bên trái thanh bar (tối đa 15 ký tự; dài hơn bị cắt bằng `…`) |
| `timezone` | Timezone áp dụng lúc khởi động (vd `SE Asia Standard Time`) |
| `uiScale` | Tỉ lệ UI cho launcher và các dialog (1 = 100%). FullHD thường hợp với `1` hoặc `1.25`; 2K/4K có thể cần `1.5`/`2`. Để trống → mặc định `1`. |
| `assistant` | Cấu hình LLM (BYOK) — xem bên dưới |
| `apps[]` | Danh sách ứng dụng: `name`, `path`, tùy chọn `args`, `cwd`, `icon` |

### Ứng dụng

`path` **tương đối** được dò trên mọi ổ đĩa cố định (bỏ qua ổ mạng/RAM), theo thứ tự:

1. `<drive>:\CORESYSTEM\Softwares\<path>`
2. `<drive>:\CORESYSTEM\Scripts\<path>`
3. `<drive>:\CORESYSTEM\<path>`

`path` tuyệt đối được dùng nguyên trạng. Cách chạy tùy phần mở rộng:

- `.exe` → chạy trực tiếp (tôn trọng `args` và `cwd`)
- `.ps1` → `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<path>"`
- `.bat` / `.cmd` → `cmd.exe /c "<path>"`

### Smart Assistant — BYOK

```jsonc
"assistant": {
  "endpoint": "https://api.example.com/v1",  // tương thích OpenAI; tự thêm "/chat/completions" nếu thiếu
  "apiKey": "<YOUR-API-KEY>",                // gửi dạng: Authorization: Bearer <key>
  "apiSecret": "",                           // tùy chọn -> header X-Api-Secret
  "model": "your-model-name",                // tên model chính xác endpoint chấp nhận
  "headers": {}                              // tùy chọn -> header bổ sung
}
```

Mọi API tương thích OpenAI đều dùng được (OpenAI, vLLM, Ollama, gateway riêng…). Nếu
endpoint/key thiếu hoặc sai, lỗi hiển thị ngay trong khung chat.

> Config là **văn bản thường cạnh exe** — ai đọc được file là đọc được key. **Không commit
> key thật**; bản trong repo luôn để placeholder rỗng.

---

## Môi trường chạy

- Windows 10/11 x64, hoặc WinPE amd64.
- **.NET Framework 4.5+** (x64). Trên WinPE cần thêm optional component **.NET Framework**
  (`WinPE-NetFx`) — WinPE mặc định không có .NET.
- **Windows PowerShell 5.1** dùng cho engine Smart Assistant và để chạy app `.ps1`
  (cần thêm optional component PowerShell tương ứng trên WinPE).

---

## Giấy phép & miễn trừ trách nhiệm

Phát hành theo **giấy phép MIT** — xem [`LICENSE`](LICENSE).

Phần mềm được cung cấp **nguyên trạng ("as is"), không kèm bất kỳ bảo đảm nào**, dù
ngụ ý hay rõ ràng, bao gồm nhưng không giới hạn ở các bảo đảm về khả năng thương mại,
phù hợp cho một mục đích cụ thể và không xâm phạm. **Trong mọi trường hợp, tác giả
hoặc chủ sở hữu bản quyền không chịu trách nhiệm** cho bất kỳ khiếu nại, thiệt hại hay
nghĩa vụ nào phát sinh từ hoặc liên quan đến phần mềm hay việc sử dụng phần mềm.
**Bạn toàn quyền sử dụng, chỉnh sửa và phân phối lại mã nguồn theo ý mình và tự chịu
trách nhiệm**; mọi trách nhiệm và rủi ro thuộc về bạn.

Các thành phần bên thứ ba được liệt kê trong
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

---

## Thương hiệu

**Microsoft**, **Windows**, **Windows PE / WinPE** và các tên sản phẩm Microsoft khác là
thương hiệu của tập đoàn Microsoft. Dự án này là sản phẩm **độc lập**, **không liên kết,
không được ủy quyền, bảo trợ hay chứng thực bởi Microsoft Corporation**. Mọi thương hiệu
khác thuộc về chủ sở hữu tương ứng.

---

## Không nên biến WinPE thành desktop

WinPE là **môi trường cài đặt/cứu hộ**, không phải hệ điều hành dùng chung. Việc dùng
launcher này để dựng một trải nghiệm desktop đầy đủ trên WinPE (shell thường trực, dùng
hằng ngày…) **không được khuyến khích** và có thể **vi phạm điều khoản cấp phép/sử dụng
Windows Preinstallation Environment của Microsoft**. Hãy dùng launcher đúng mục đích: cứu
hộ tạm thời, chẩn đoán, triển khai và phục hồi.

---

## Hợp tác — MSP / ISV

![Universal Builder — hợp tác](ref/builder.webp)

Repo này cung cấp **chỉ Launcher**, mã nguồn mở theo MIT.

**Universal Builder** (công cụ trực quan tạo boot media WinPE **ISO / WIM** với ứng dụng,
script, và PowerShell module của riêng bạn) dành cho **chương trình hợp tác với MSP/ISV**
— ví dụ để đóng gói bộ công cụ cứu hộ/hỗ trợ của bạn vào một boot image tùy biến.

Tìm hiểu thêm tại **[https://coresystem.vn](https://coresystem.vn)**.

---

Xem thêm: [`NOTES.md`](NOTES.md) — nhật ký phát triển chi tiết.
