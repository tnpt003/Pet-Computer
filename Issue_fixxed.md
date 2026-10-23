# Lỗi đã sửa

Mỗi mục: triệu chứng, nguyên nhân, cách sửa, cách kiểm chứng, việc người dùng cần làm. Mục mới ở trên.

---

## #1 — Máy đã cài báo "Bạn đang dùng bản mới nhất" dù đã có bản mới (sửa ở 1.4.3, 2026-10-23)

**Triệu chứng**
- Đã phát hành 1.4.2 lên GitHub, nhưng máy khác (đã cài 1.4.1, mạng khác máy dev) bấm khay → "Kiểm tra cập nhật" vẫn báo "Bạn đang dùng bản mới nhất." và không tải bản mới.

**Bằng chứng (log máy đó, `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`)**
```
21:32:34 Desktop Pet 1.4.1 (01ded63) started
21:32:36 Update check failed: Response status code does not indicate success: 403 (rate limit exceeded).
23:06:38 Update check failed: ... 403 (rate limit exceeded).
23:07:31 ... (khoảng 20 lần bấm liên tiếp, lần nào cũng 403)
```
Release 1.4.2 trên GitHub lúc đó hoàn toàn bình thường: Actions thành công, đủ 6 file, `releases.win.json` ghi 1.4.2.

**Nguyên nhân — hai lỗi chồng lên nhau**
1. **Hỏi qua API GitHub bị giới hạn lượt.** 1.4.0–1.4.2 dùng `GithubSource` của Velopack, hỏi `api.github.com` để lấy danh sách Release. API cho người không đăng nhập chỉ **60 lượt/giờ cho mỗi địa chỉ IP**, mỗi lần kiểm tra tốn vài lượt. Máy kia dùng mạng có **IP chung với nhiều người** (4G, wifi trường/công ty, nhà mạng gom khách vào một IP — CGNAT): người khác đã dùng hết lượt, nên ngay lần kiểm tra **đầu tiên** lúc mở app đã bị 403. Mỗi lần bấm lại tốn thêm lượt nên càng khó hồi. (Không phải do số máy cài: tải file bằng trình duyệt không tính lượt; mỗi mạng có hạn mức riêng. Sau đó `https://api.github.com/rate_limit` trên máy kia đã hồi 60/60.)
2. **App báo nhầm lỗi thành "mới nhất".** Trong `PetUpdates.Check`, khi kiểm tra lỗi, code gửi cùng tín hiệu với "không có bản mới" (`Changed(null)`), nên khay hiện "Bạn đang dùng bản mới nhất." — người dùng không biết là đang lỗi.

**Lỗi phụ cùng đợt: Actions không chạy khi đẩy 1.4.2.** Hướng dẫn cũ dùng `git tag v1.4.2` (tag thường) rồi `git push origin main --follow-tags`; `--follow-tags` chỉ đẩy tag *annotated*, nên chỉ `main` lên GitHub, workflow release (chạy theo tag `v*`) không chạy. Xử lý lúc đó: `git push origin v1.4.2`.

**Cách sửa (1.4.3, commit 7854009)**
- `src/DesktopPet/PetUpdates.cs`: nguồn cập nhật đổi từ API sang link tải thẳng của Release mới nhất `PetUpdates.FeedUrl` = `https://github.com/tnpt003/Pet-Computer/releases/latest/download/` (Velopack đọc `releases.win.json` rồi tải gói từ cùng chỗ). Đây là tải file thường, không bị giới hạn 60 lượt.
- Lỗi khi người dùng bấm kiểm tra → sự kiện `Failed` → khay (`App.xaml.cs`) báo "Chưa kiểm tra được cập nhật (lỗi mạng hoặc GitHub). Thử lại sau ít phút." (vi/en/ja). Trong 10 phút sau một lần lỗi (`RetryAfterFailure`), bấm lại chỉ báo như vậy, không hỏi GitHub.
- `docs/RELEASE.md`: tag dùng `git tag -a vX -m "..."`; thêm quy tắc giữ nguồn `latest/download` đúng (chỉ workflow tạo Release, Release khác để pre-release, bản vá nhánh cũ không đặt Latest, kiểm tra link `releases.win.json` sau mỗi lần phát hành).

**Kiểm chứng (thật, trên GitHub)** — harness giả lập máy đã cài (`TestVelopackLocator` + `Update.exe` thật):
- 1.4.1 → 1.4.2 qua link mới: tải delta 2,3 MB, ghép thành công.
- 1.4.0 → 1.4.2 qua link mới: delta không khớp → Velopack tự tải gói đầy đủ (~90 MB), vẫn lên được.
- Sau khi 1.4.3 lên GitHub: 1.4.2 cách cũ (API) thấy 1.4.3; 1.4.2 qua link mới tải delta 1.4.3 và ghép thành công; 1.4.3 báo "đã mới nhất".
- `scripts/test.ps1 -Ui`: Python OK, C# 51 suite, WPF 805/805 (check mới: FeedUrl, giữ 10 phút).
- **Chưa thử trọn vòng** trên máy đã cài: 1.4.3 tự cập nhật lên bản kế tiếp (kiểm chứng khi phát hành 1.4.4).

**Việc người dùng cần làm**
- Máy còn 1.4.0–1.4.2 vẫn hỏi qua API cũ cho tới khi lên 1.4.3: bấm "Kiểm tra cập nhật" **một lần** (không bấm liên tục); nếu vẫn báo "mới nhất" thì cài đè bằng Setup (không cần gỡ, dữ liệu giữ nguyên): https://github.com/tnpt003/Pet-Computer/releases/latest/download/PetComputer-win-Setup.exe
- Từ 1.4.3 trở đi: tự cập nhật lúc mở mèo và mỗi 6 giờ (nếu bật "Tự động tải và cài bản mới"); lỗi thì khay nói rõ là lỗi.
