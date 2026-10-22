# Phát hành bản mới (installer + tự cập nhật)

Từ 1.4.0 (2026-10-22). Dùng Velopack 1.2.158: thư viện trong app (`PetUpdates.cs`, `App()`), công cụ `vpk` (local tool trong `dotnet-tools.json`).

## Cách hoạt động

- **Người dùng:** tải `PetComputer-win-Setup.exe` ở trang Releases của repo, chạy là cài (không cần admin). App cài vào `%LOCALAPPDATA%\PetComputer`, tạo shortcut Desktop + Start menu, mở mèo luôn.
- **Dữ liệu người dùng** nằm riêng ở `%LOCALAPPDATA%\DesktopPet` (preferences, nhật ký, tiến trình, kỷ lục). Cập nhật hay gỡ cài đặt không đụng tới thư mục này. Tên gói `PetComputer` cố ý khác tên thư mục dữ liệu (`scripts/pack.ps1` chặn nếu trùng).
- **Tự cập nhật:** chỉ bản đã cài. Lúc mở app và mỗi 6 giờ, app hỏi GitHub Releases của `tnpt003/Pet-Computer` (repo phải public). Có bản mới thì tải ngầm, rồi khay hệ thống báo bằng một thông báo. Mục khay "Cập nhật lên bản …" đóng mèo, cập nhật, mở lại; nếu không bấm thì bản đã tải được áp dụng lần mở mèo sau. Mục khay "Kiểm tra cập nhật" để hỏi ngay. Không có mạng/lỗi: chỉ ghi log.
- **Bản copy thư mục** (`artifacts\win-x64`, zip portable): không tự cập nhật, chạy như trước.
- **Chưa ký code:** SmartScreen có thể cảnh báo khi chạy Setup lần đầu ("More info → Run anyway"). Ký miễn phí sau này: nộp đơn SignPath Foundation (repo đã public, có LICENSE MIT, build bằng GitHub Actions); `vpk pack` có `--signTemplate`.

## Phát hành một bản

1. Nâng `<Version>` trong `src/DesktopPet/DesktopPet.csproj` (SemVer, ví dụ `1.4.1`). Bản mới phải lớn hơn bản đang có thì máy người dùng mới nhận.
2. Trên máy: `scripts\test.ps1 -Ui`, `scripts\publish.ps1`, `scripts\check-publish.ps1` đạt; commit (không còn thay đổi chưa commit).
3. Tùy chọn, thử đóng gói trên máy: `scripts\pack.ps1` → `artifacts\release` (không upload).
4. Gắn tag và đẩy lên (người dùng làm, hoặc cho phép agent làm):
   ```powershell
   git tag v1.4.1
   git push origin main --follow-tags
   ```
5. GitHub Actions (`.github/workflows/release.yml`) chạy test Python + C#, publish (kèm .NET và Python), `pack.ps1 -Upload`: tải bản trước để làm gói delta, đóng gói, tạo Release `v1.4.1` "Desktop Pet 1.4.1" kèm mọi file. Xem tiến trình ở tab Actions của repo; lỗi thì Release không được tạo.
6. Trong vòng 6 giờ (hoặc khi mở lại / bấm Kiểm tra cập nhật) các mèo đã cài tải bản mới.

Tag phải đúng `v` + version trong csproj, nếu không pack.ps1 dừng.

## Checklist máy Windows 10 sạch (người dùng)

- [ ] Tải `PetComputer-win-Setup.exe` từ Releases, chạy: nếu SmartScreen chặn → More info → Run anyway. Cài xong mèo hiện ra, có shortcut "Desktop Pet" trên Desktop và Start menu.
- [ ] Mèo chạy đủ: kéo/ném, ngủ, menu chuột phải, khay; bật "Mèo thông minh (AI)" → AI hoạt động (Python đi kèm, máy không cần cài Python/.NET).
- [ ] Đổi vài cài đặt (tên mèo, bộ lông), thoát, mở lại từ shortcut: vẫn giữ.
- [ ] Khay → "Kiểm tra cập nhật": báo "đang dùng bản mới nhất".
- [ ] Sau khi phát hành bản kế tiếp (ví dụ 1.4.1): mở mèo, chờ thông báo "Bản 1.4.1 đã tải xong" (hoặc bấm Kiểm tra cập nhật) → khay "Cập nhật lên bản 1.4.1" → mèo đóng rồi mở lại; Cài đặt/nhật ký vẫn còn. Thử thêm: không bấm, thoát mèo rồi mở lại → đã là bản mới.
- [ ] Gỡ cài đặt (Settings → Apps → Desktop Pet → Uninstall): shortcut và `%LOCALAPPDATA%\PetComputer` mất; `%LOCALAPPDATA%\DesktopPet` vẫn còn (cài lại thì mèo nhớ mọi thứ).
- [ ] Ghi lại lỗi/thông báo lạ; log ở `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log` (dòng "Update check …").
