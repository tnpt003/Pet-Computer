# Phát hành bản mới (installer + tự cập nhật)

Từ 1.4.0 (2026-10-22). Dùng Velopack 1.2.158: thư viện trong app (`PetUpdates.cs`, `App()`), công cụ `vpk` (local tool trong `dotnet-tools.json`).

## Cách hoạt động

- **Người dùng:** tải `PetComputer-win-Setup.exe` ở trang Releases của repo, chạy là cài (không cần admin). App cài vào `%LOCALAPPDATA%\PetComputer`, tạo shortcut Desktop + Start menu, mở mèo luôn.
- **Dữ liệu người dùng** nằm riêng ở `%LOCALAPPDATA%\DesktopPet` (preferences, nhật ký, tiến trình, kỷ lục). Cập nhật hay gỡ cài đặt không đụng tới thư mục này. Tên gói `PetComputer` cố ý khác tên thư mục dữ liệu (`scripts/pack.ps1` chặn nếu trùng).
- **Tự cập nhật:** chỉ bản đã cài. Lúc mở app và mỗi 6 giờ, app đọc `releases.win.json` của Release mới nhất qua link tải thẳng `https://github.com/tnpt003/Pet-Computer/releases/latest/download/` (repo phải public). Có bản mới thì tải ngầm, rồi khay hệ thống báo bằng một thông báo. Mục khay "Cập nhật lên bản …" đóng mèo, cập nhật, mở lại; nếu không bấm thì bản đã tải được áp dụng lần mở mèo sau. Mục khay "Kiểm tra cập nhật" để hỏi ngay. Lỗi (mạng/GitHub): ghi log; nếu người dùng bấm kiểm tra thì khay báo "Chưa kiểm tra được cập nhật…", và trong 10 phút sau một lần lỗi, bấm lại chỉ báo như vậy mà không hỏi GitHub.
- **Vì sao không dùng API GitHub (từ 1.4.3):** 1.4.0–1.4.2 dùng `GithubSource` (API `api.github.com`), chỉ 60 lượt/giờ cho mỗi IP khi chưa đăng nhập; trên mạng dùng chung IP lượt bị người khác dùng hết → 403, mà bản cũ còn báo nhầm "bạn đang dùng bản mới nhất". Link `latest/download` là tải file thường, không bị giới hạn đó. Link chỉ chứa file của Release mới nhất: máy ở bản liền trước dùng gói delta (đã thử thật 1.4.1 → 1.4.2: tải 2,3 MB); máy cũ hơn thì delta không khớp và Velopack tự tải gói đầy đủ (đã thử 1.4.0 → 1.4.2). Máy còn 1.4.0–1.4.2 vẫn hỏi qua API cho tới khi lên được 1.4.3 (hoặc chạy Setup.exe mới để cài đè; dữ liệu giữ nguyên).
- **Lần mở đầu sau khi cài** (hook `OnFirstRun`, từ 1.4.1): hộp chào mừng (`PetWelcome.cs`) với mẹo, ngôn ngữ (người mới: theo ngôn ngữ Windows; đã có preferences: giữ) và ô "Khởi động cùng Windows" tích sẵn; lựa chọn ghi vào `HKCU\...\Run` (giá trị `DesktopPet`) trỏ tới bản đã cài, nên mục cũ của bản copy thư mục được chuyển sang. Cập nhật không hiện lại hộp này. Velopack không có giao diện chọn thư mục hay checkbox trong Setup (cài một click) nên phần này nằm trong app.
- **Bản khác đang chạy lúc cài** (`PetHandover.cs`, chỉ lần chạy đầu): bản đó (thường là bản copy thư mục) nhận WM_CLOSE vào cửa sổ mèo như khi bấm Thoát, chờ tối đa 10 s rồi bản cài giữ khóa single-instance. Không đóng được thì như cũ (mở bảng điều khiển của bản đang chạy).
- **Cài đặt → Chung → Cập nhật:** phiên bản, "Tự động tải và cài bản mới" (preferences `AutoUpdate`, mặc định bật; tắt thì không kiểm tra lúc mở/6 giờ, chỉ khi bấm), nút "Kiểm tra cập nhật". Bản không cài: mờ + dòng giải thích.
- **Có gì mới:** preferences `LastVersion` ghi bản đã cài chạy lần trước; bản khác (hoặc `OnRestarted` sau khi cập nhật qua khay) → thông báo khay, bấm mở trang Release của bản đó. 1.4.0 chưa ghi `LastVersion` nên lên 1.4.1 chỉ báo khi cập nhật qua khay (`OnRestarted`).
- **Trang Release:** `pack.ps1` đặt hướng dẫn cài (vi/en, gồm SmartScreen) lên đầu ghi chú mọi bản. README có link cố định `releases/latest/download/PetComputer-win-Setup.exe`.
- **Gỡ trong app** (từ 1.4.1): Cài đặt → Chung → Ứng dụng → "Gỡ cài đặt Desktop Pet…" (ẩn ở bản không cài). Hộp xác nhận có ô "Xóa cả dữ liệu của mèo" (không tích sẵn). Đồng ý: `PetUpdates.StartUninstall` chuẩn bị, mèo thoát bình thường, rồi bước cuối của `App.OnExit` (`RunPendingUninstall`) mới chạy `%LOCALAPPDATA%\PetComputer\Update.exe --uninstall` (như Settings → Apps) — trước đây chạy ngay nên trình gỡ đóng mạnh mèo (log thiếu dòng "exited"). Tích ô: ghi file đánh dấu `remove-data-on-uninstall` trong thư mục cài; hook gỡ thấy file thì xóa `%LOCALAPPDATA%\DesktopPet` (thử lại tới ~5 s nếu file log còn bị giữ; không bao giờ xóa ổ gốc hay thư mục cài). Mỗi lần mèo mở bình thường xóa file đánh dấu, nên gỡ bị hủy không để lại "lệnh xóa dữ liệu" cho lần gỡ sau.
- **Gỡ cài đặt** (hook `OnBeforeUninstallFastCallback` → `PetUpdates.OnUninstall`, từ 1.4.1): xóa mục khởi động nếu nó trỏ tới chính bản đã cài; mục của bản copy khác giữ nguyên. Bản 1.4.0 chưa có hook này: gỡ 1.4.0 mà đang bật khởi động cùng Windows thì mục Run còn sót (xóa tay trong Task Manager → Startup apps, hoặc cập nhật lên 1.4.1 trước khi gỡ). Rà 2026-10-23 trên máy đã cài 1.4.0: mục Apps "Desktop Pet" 1.4.0, nhà phát hành tnpt003, `Update.exe --uninstall`, NoModify/NoRepair; shortcut Desktop + Start menu trỏ `current\DesktopPet.exe`; ứng dụng chỉ ghi registry ở `HKCU\...\Run`, dữ liệu chỉ ở `%LOCALAPPDATA%\DesktopPet` (cố ý giữ). Shortcut người dùng tự tạo (vd. tới `artifacts\win-x64`) không bị gỡ.
- **Splash và ghi chú:** `pack.ps1` dùng `Assets\Splash.gif` (GIF động 360×250, ~4 s lặp, ~1,2 MB; tạo lại bằng `dotnet run --project tools/SplashBuilder -- <thư mục repo>`, vẽ mèo bằng PetRenderer, Happy) làm hình khi Setup giải nén, thanh tiến trình màu `#C2561F`; chỉ Setup dùng, không nằm trong app và lấy mục `## <version>` trong CHANGELOG.md làm release notes (không có mục thì bỏ qua, chỉ báo).
- **Bản copy thư mục** (`artifacts\win-x64`, zip portable): không tự cập nhật, chạy như trước.
- **Chưa ký code:** SmartScreen có thể cảnh báo khi chạy Setup lần đầu ("More info → Run anyway"). Ký miễn phí sau này: nộp đơn SignPath Foundation (repo đã public, có LICENSE MIT, build bằng GitHub Actions); `vpk pack` có `--signTemplate`.

## Phát hành một bản

1. Nâng `<Version>` trong `src/DesktopPet/DesktopPet.csproj` (SemVer, ví dụ `1.4.1`). Bản mới phải lớn hơn bản đang có thì máy người dùng mới nhận. Thêm mục `## 1.4.1 — …` vào CHANGELOG.md (thành release notes).
2. Trên máy: `scripts\test.ps1 -Ui`, `scripts\publish.ps1`, `scripts\check-publish.ps1` đạt; commit (không còn thay đổi chưa commit).
3. Tùy chọn, thử đóng gói trên máy: `scripts\pack.ps1` → `artifacts\release` (không upload).
4. Gắn tag và đẩy lên (người dùng làm, hoặc cho phép agent làm):
   ```powershell
   git tag -a v1.4.1 -m "Desktop Pet 1.4.1"
   git push origin main --follow-tags
   ```
   Phải có `-a` (tag annotated): `--follow-tags` không đẩy tag thường, khi đó chỉ `main` lên GitHub và Actions không chạy (gặp ở 1.4.2). Lỡ tạo tag thường thì đẩy riêng: `git push origin v1.4.1`.
5. GitHub Actions (`.github/workflows/release.yml`) chạy test Python + C#, publish (kèm .NET và Python), `pack.ps1 -Upload`: tải bản trước để làm gói delta, đóng gói, tạo Release `v1.4.1` "Desktop Pet 1.4.1" kèm mọi file. Xem tiến trình ở tab Actions của repo; lỗi thì Release không được tạo.
6. Trong vòng 6 giờ (hoặc khi mở lại / bấm Kiểm tra cập nhật) các mèo đã cài tải bản mới.

Tag phải đúng `v` + version trong csproj, nếu không pack.ps1 dừng.

## Quy tắc giữ cho nguồn `latest/download` chạy đúng (từ 1.4.3)

Mọi máy đã cài đọc `releases.win.json` của Release GitHub đánh dấu **Latest**. Vì vậy:

- **Chỉ workflow release tạo Release.** Không tạo Release bằng tay để đăng thông báo/tài liệu: Release đó thành "Latest" mà không có `releases.win.json`, mọi máy kiểm tra sẽ báo "Chưa kiểm tra được cập nhật" tới khi có bản app mới. Nếu thật sự cần Release không phải bản app thì đánh dấu **pre-release** (GitHub không coi pre-release là Latest).
- **Bản vá cho nhánh cũ** (ví dụ 1.3.x sau khi đã có 1.4.x) không được thành Latest: tạo với "Set as the latest release" bỏ chọn (API/CLI: `make_latest=false`). Nếu lỡ, Velopack không hạ version nên không hỏng máy nào, nhưng máy 1.4.x không thấy bản 1.4 tiếp theo cho tới khi Latest trỏ về đúng. Kiểm tra sau mỗi lần phát hành: link `https://github.com/tnpt003/Pet-Computer/releases/latest/download/releases.win.json` trả về đúng version mới.
- **Kênh beta** (nếu làm sau này) không đi qua Latest: cần đánh dấu pre-release và một nguồn riêng cho máy dùng beta. Không có phát hành theo tỷ lệ (mọi máy đọc cùng file).
- Máy bỏ qua từ hai bản trở lên tải gói đầy đủ (~90 MB) thay vì delta; bình thường, không cần làm gì.
- Repo phải public. Không chuyển khỏi GitHub (người dùng quyết 2026-10-23), nên nguồn để cố định trong `PetUpdates.FeedUrl`.

## Checklist máy Windows 10 sạch (người dùng)

- [ ] Tải `PetComputer-win-Setup.exe` từ Releases, chạy: nếu SmartScreen chặn → More info → Run anyway. Cài xong mèo hiện ra, có shortcut "Desktop Pet" trên Desktop và Start menu.
- [ ] Mèo chạy đủ: kéo/ném, ngủ, menu chuột phải, khay; bật "Mèo thông minh (AI)" → AI hoạt động (Python đi kèm, máy không cần cài Python/.NET).
- [ ] Đổi vài cài đặt (tên mèo, bộ lông), thoát, mở lại từ shortcut: vẫn giữ.
- [ ] Khay → "Kiểm tra cập nhật": báo "đang dùng bản mới nhất".
- [ ] Sau khi phát hành bản kế tiếp (ví dụ 1.4.1): mở mèo, chờ thông báo "Bản 1.4.1 đã tải xong" (hoặc bấm Kiểm tra cập nhật) → khay "Cập nhật lên bản 1.4.1" → mèo đóng rồi mở lại; Cài đặt/nhật ký vẫn còn. Thử thêm: không bấm, thoát mèo rồi mở lại → đã là bản mới.
- [ ] Cài lần đầu (từ 1.4.1): Setup hiện hình con mèo khi giải nén; mèo mở ra kèm hộp chào mừng (đổi ngôn ngữ thì chữ đổi ngay), ô "Khởi động cùng Windows" tích sẵn. Bấm Bắt đầu → Cài đặt → Chung thấy ô này đang bật; Task Manager → Startup apps có Desktop Pet. Khởi động lại máy: mèo tự mở.
- [ ] Gỡ cài đặt (Settings → Apps → Desktop Pet → Uninstall): shortcut và `%LOCALAPPDATA%\PetComputer` mất; `%LOCALAPPDATA%\DesktopPet` vẫn còn (cài lại thì mèo nhớ mọi thứ). Từ 1.4.1: Task Manager → Startup apps không còn Desktop Pet.
- [ ] Ghi lại lỗi/thông báo lạ; log ở `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log` (dòng "Update check …").
