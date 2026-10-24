# Lỗi đã sửa

Sổ ghi các lỗi đã sửa của Desktop Pet. Phần A: lỗi người dùng phát hiện khi dùng thật (đánh số theo thời gian, mục mới ở trên). Phần B: lỗi do test, mô phỏng hoặc review tìm ra trong lúc làm. Chi tiết đầy đủ ở PROGRESS.md / docs/archive/PROGRESS-2026-10.md.

Mỗi mục: triệu chứng, nguyên nhân, cách sửa, kiểm chứng. Thêm lỗi mới: tạo mục số tiếp theo ở đầu phần A (hoặc một dòng ở phần B).

# A. Lỗi người dùng phát hiện

---

## #22 — Vòng loading khi hướng dẫn trông như chưa hiện hết (2026-10-24, commit 5cf8980)
- **Triệu chứng:** ở bước hướng dẫn vòng loading (màn 3), vòng trông chưa hiện đủ.
- **Nguyên nhân:** (1) trước ec27b0a trận đứng hình đúng lúc vòng còn đang lớn dần (0,3 s); (2) chỗ sáng chỉ ôm vòng xoay phía trên, còn vùng làm chậm dưới sàn (hình bầu dục quanh chân mèo) và con mèo nằm trong vùng tối.
- **Sửa:** bước chờ vòng hiện xong (≥ 0,35 s); chiếu sáng cả vòng xoay, vùng dưới sàn (`BattleFieldView.LoadingZoneBounds`) và con mèo.
- **Kiểm chứng:** WPF 847/847 (bước loading bắt đầu khi vòng đã 0,35 s, có 3 chỗ sáng, vùng sàn rộng ≥ 2 × bán kính). Chưa xem trên màn hình thật.

## #21 — Hướng dẫn chiếu sáng virus.exe khi nó chưa hiện hết (2026-10-24, commit ec27b0a)
- **Triệu chứng:** virus.exe còn nhỏ, đang bung ra mà hướng dẫn đã sáng và trận đứng hình.
- **Nguyên nhân:** hướng dẫn đứng hình ngay khung đầu tiên file xuất hiện; thời gian trận dừng nên hoạt ảnh bung (0,35 s) dừng giữa chừng. Quảng cáo (0,25 s), tường cập nhật (0,3 s), icon lúc bị nhiễm (0,6 s) cùng kiểu.
- **Sửa:** các bước app độc chỉ bắt đầu khi thứ đó đã hiện xong (quảng cáo ≥ 0,3 s hoặc đang khóa, vòng/tường ≥ 0,35 s, virus.exe ≥ 0,4 s, icon nhiễm ≥ 0,65 s).
- **Kiểm chứng:** WPF (bước virus.exe bắt đầu khi file đã hiện 0,42 s).

## #20 — Không chọn được thẻ, phải chuyển cửa sổ khác mới chọn được (2026-10-24, commit ec27b0a)
- **Triệu chứng:** màn Cầm cự, bảng chọn thẻ hiện mà bấm không ăn; chuyển sang cửa sổ phía sau rồi quay lại mới chọn được (log: bảng thẻ mở ~22:46, chọn được 22:47:43).
- **Nguyên nhân:** giữ chuột lên vòng loading thì dải chiến trường `CaptureMouse`; vòng tự tắt khi đang giữ (đủ 2 s) → lúc thả, `EndHold` thấy không còn vòng được giữ nên thoát luôn, không `ReleaseMouseCapture`. Mọi click sau đó (cả bảng thẻ) chạy vào dải chiến trường cho tới khi Windows lấy lại chuột (đổi cửa sổ). Lỗi có từ khi thêm "giữ 2 s" (6b), lộ ra vì hướng dẫn màn 3 dạy giữ vòng ngay trước mốc chọn thẻ 20 s.
- **Sửa:** `EndHold` luôn trả chuột; `DropStaleHold()` mỗi khung trả chuột khi không còn vòng nào được giữ.
- **Kiểm chứng:** WPF (vòng tự tắt dưới chuột → khung sau trả chuột; thả chuột sau đó cũng trả). Cần thử tay: giữ vòng tới khi tắt rồi chọn thẻ ngay.

## #19 — Chỗ sáng của hướng dẫn nằm lệch khỏi con mèo (2026-10-24, commit 1d07490)
- **Triệu chứng:** bước "Kéo mèo lên", viền vàng nằm phía trên bên trái con mèo.
- **Nguyên nhân:** cửa sổ mèo có lề `PickupAnimation.Padding` 40 DIP quanh khung mèo 112×112; chỗ sáng đo từ góc cửa sổ nên lệch 40 DIP. Test lúc đó đo cùng gốc sai nên vẫn PASS (xem phần B).
- **Sửa:** cộng lề; test so thêm với `pet.X + CenterX` (độc lập với cách đo).
- **Kiểm chứng:** WPF 843/843 (tâm chỗ sáng 960 = pet.X + 56).

## #18 — Chỗ sáng của hướng dẫn chưa căn giữa (thanh kỹ năng HUD) (2026-10-24, commit 8dab2d4)
- **Triệu chứng:** viền vàng quanh thanh kỹ năng lệch, khoảng hở hai bên không đều.
- **Nguyên nhân:** HUD có Margin 8 mà chỗ sáng tính theo khung bên trong; thanh kỹ năng ôm cả lề StackPanel; slime chưa tính lúc phồng khi gồng; quảng cáo/vòng xoay/tường dùng số ước lượng.
- **Sửa:** đo mọi chỗ sáng bằng `PointToScreen` của chính phần tử (đúng lề, HUD bị kéo, DPI), đệm đều 6 DIP; thanh kỹ năng = hợp 5 ô; sân trận lấy đúng hình chữ nhật đang vẽ.
- **Kiểm chứng:** WPF (ô máu, thanh kỹ năng, ô Đẩy, túi đồ đều đệm đúng 6 DIP; tâm slime < 1 DIP).

## #17 — Âm thanh mèo và ô hướng dẫn chưa tick sẵn ở máy khác (2026-10-24, commit f6ea181)
- **Triệu chứng:** máy khác cài mới, âm thanh mèo tắt; ô "Hướng dẫn trong trận" tắt ở màn đã thắng.
- **Nguyên nhân:** `PetPreferences.Sounds` mặc định false (thiết kế cũ "off by default"); ô hướng dẫn mặc định theo "màn chưa thắng".
- **Sửa:** `Sounds` = true cho preferences mới (file đã lưu giữ giá trị cũ, theo người dùng chọn); `AdventureGuide` = true, ô luôn tick sẵn, bỏ tick lúc Sẵn sàng thì được nhớ. Rà các mặc định khác: giữ nguyên.
- **Kiểm chứng:** C# (preferences mới bật cả hai), WPF 841/841.

## #16 — Hướng dẫn tại chỗ: nhiều kiểu, khó hiểu; cần một kiểu và chỉ ở Phiêu lưu (2026-10-24, commit cb77240)
- **Góp ý:** hướng dẫn nên một kiểu, bỏ những gì nhìn là hiểu, theo từng màn chương 1 (màn 1 kéo mèo 6 lần có slime nhanh, màn 2 HUD/kỹ năng/túi/thẻ/cộng hưởng có làm tối, màn 3 gom app độc + icon nhiễm, màn 4 Đẩy ~4 lần + tinh anh, màn 5 Vua theo cơ chế, Đẩy chặn phong tinh anh 1 lần); đổi Phiêu lưu → Hướng dẫn phiêu lưu; mô tả kỹ từng màn.
- **Làm:** khung "Hướng dẫn phiêu lưu" (đứng hình không phải Pause, làm tối + chỗ sáng + một dòng, Hiểu rồi/Bỏ qua); bỏ hướng dẫn 5 thẻ và dấu tay; màn 2 không app độc, màn 3 lịch app độc cố định + không tinh anh, màn 4 đủ 4 loại slime mỗi loại một tinh anh; goal/tip từng màn viết lại. Ngoại lệ nhấc mèo khi đứng hình ghi vào AGENTS.md.
- **Kiểm chứng:** C# 1533, WPF 839/839 lúc đó.

## #15 — Đẩy hồi 3 giây thay vì 5 giây (2026-10-24, commit cb77240)
- **Triệu chứng:** người dùng nhớ Đẩy hồi 5 s, game đang 3 s.
- **Nguyên nhân:** mốc cân bằng 1 (5c1efcc) hạ 5 → 3 s.
- **Sửa:** trả về 5 s (battle.json `pushCooldown`, mặc định code), cập nhật mô tả/test.

## #14 — Trận "pause bất thường" và chậm (slow down) khi đang chơi (2026-10-24, commit 9fe79b2, rồi thay bằng cb77240)
- **Triệu chứng:** đang đánh thì trận như khựng, chậm hẳn.
- **Bằng chứng:** log 16:10 trận trùm: 8 bài hướng dẫn tại chỗ (target, lift, infected, ads, heal, app, stun, loading) nổ trong 47 s đầu.
- **Nguyên nhân:** mỗi bài làm trận chạy ×0,25 tới 6 s (mèo vẫn chạy tốc độ thường) và các bài nối nhau; bài chạy ở mọi chế độ; hết giờ vẫn tính đã học.
- **Sửa:** 9fe79b2 bỏ làm chậm, chỉ Phiêu lưu, giãn 10 s; sau đó cb77240 thay hẳn bằng Hướng dẫn phiêu lưu (#16).

---

## #13 — Máy đã cài báo "Bạn đang dùng bản mới nhất" dù đã có bản mới (sửa ở 1.4.3, 2026-10-23)

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

---

## #12 — Vòng Tab Lốc Xoáy bị con mèo che hết (2026-10-23, commit f6a35e7)
- **Triệu chứng:** trong trận, cả vòng tab của app "Tab Tornado" nằm sau lưng mèo, nửa phía trước cũng bị che.
- **Nguyên nhân:** dải chiến trường là một cửa sổ riêng, luôn nằm dưới cửa sổ mèo, nên mọi thứ vẽ trên dải đều ở sau mèo.
- **Sửa:** chia vòng làm hai nửa: dải trận vẽ nửa sau, cửa sổ mèo vẽ nửa trước đè lên mèo. Mèo bị nhấc/đang bay thì dải trận vẽ cả vòng như cũ. Bán kính vẽ giới hạn để nửa trước không bị cắt ở mép cửa sổ mèo.
- **Kiểm chứng:** test.ps1 -Ui đạt; đã xem mô phỏng trước khi làm. Chưa xem trên màn hình thật.

## #11 — Thanh âm lượng không có tác dụng; tiếng sau cắt ngang tiếng trước (2026-10-23, commit 62848ae)
- **Triệu chứng:** kéo thanh âm lượng mà tiếng không đổi.
- **Nguyên nhân:** (1) âm lượng chỉ áp dụng khi bấm Lưu (preferences của người dùng vẫn 0.7); (2) thang tuyến tính nên 50% nghe gần như 100%; (3) thời lượng cue tính theo 44100 Hz trong khi file WAV là 22050 Hz → cue sau cắt ngang cue trước.
- **Sửa:** kéo là áp dụng ngay (Hủy thì trả lại, 0% tắt luôn âm đang kêu); gain = âm lượng² theo tai người; thời lượng đọc byte rate từ header WAV.
- **Kiểm chứng:** check C# (thang âm lượng, thời lượng cue) và WPF (kéo áp dụng ngay, Hủy trả lại).

## #10 — Chữ trong trận bị con mèo che (2026-10-23, commit 62848ae)
- **Triệu chứng (ảnh người dùng):** nhãn tinh anh ("ĐÁNH NHANH"), số sát thương, CHẶN/MIỄN của slime đứng sát mèo bị thân mèo che.
- **Nguyên nhân:** như #12 — chữ vẽ trên dải trận, nằm dưới cửa sổ mèo.
- **Sửa:** `ClearOfCat` dời chữ ra sát cạnh thân mèo theo phía của nó.

## #9 — Tạm dừng trận nhưng mèo vẫn cử động và nhấc lên được (2026-10-23, commit 62848ae)
- **Triệu chứng:** Pause trận mà sao choáng, đầu, kiếm của mèo vẫn chạy; vẫn cầm mèo lên được.
- **Sửa:** khi trận bị đóng băng (mọi lý do trừ Away) dừng cả thời gian hoạt ảnh/động tác của mèo và chặn bắt đầu kéo. Thay quy tắc cũ (18/10) "kéo được mèo khi trận dừng" theo yêu cầu mới.
- **Kiểm chứng:** check WPF: pause đóng băng tư thế, không nhấc được, Tiếp tục thì nhấc lại được.

## #8 — Gỡ 1.4.0 để lại mục khởi động cùng Windows trỏ vào file đã xóa (2026-10-23, sửa ở 1.4.1)
- **Triệu chứng:** sau khi gỡ bản cài 1.4.0, Windows vẫn cố mở Desktop Pet lúc khởi động (mục Run còn sót).
- **Sửa:** hook gỡ cài đặt của Velopack xóa mục Run nếu nó trỏ tới chính bản đã cài (mục của bản copy thư mục khác giữ nguyên). Kèm theo: gỡ trong app chạy trình gỡ ở bước cuối khi thoát, để mèo tự đóng gọn (trước đó trình gỡ đóng mạnh mèo, log thiếu dòng "exited") — commit 01ded63.
- **Kiểm chứng:** cài/gỡ thật trên máy dev: gỡ 1.4.1 thì mục Run mất, dữ liệu 8 file còn nguyên. Máy đã cài 1.4.0 phải xóa mục Run bằng tay (Task Manager → Startup apps) hoặc lên 1.4.1 rồi mới gỡ.

## #7 — Cài Setup khi bản copy thư mục đang chạy: trông như không có gì xảy ra (2026-10-23, sửa ở 1.4.1)
- **Triệu chứng:** chạy Setup xong không thấy mèo mới mở.
- **Nguyên nhân:** khóa single-instance — bản copy đang chạy giữ khóa, bản vừa cài mở lên thấy đã có mèo nên tự thoát.
- **Sửa:** `PetHandover.cs`: lần chạy đầu sau khi cài, bản cũ được đóng bình thường (WM_CLOSE như bấm Thoát), chờ tối đa 10 s rồi bản cài giữ khóa.
- **Kiểm chứng:** cài thật: bản dev thoát code 0, bản cài mở sau ~0,05 s.

## #6 — Đang choáng mà rê chuột nhanh qua mèo vẫn hiện mặt Ngạc nhiên (2026-10-22, commit 77f67f3)
- **Nguyên nhân:** bộ cảm xúc không coi trạng thái choáng (Bonk, khi món trưng bày rơi trúng đầu) là "bận".
- **Sửa:** `PetEmotions.Step` coi `Bonk > 0` là bận: không Ngạc nhiên, không cảm xúc khác trong lúc thấy sao.
- **Kiểm chứng:** test C#: đối chứng có Surprised khi không choáng, đang choáng thì không.

## #5 — Thẻ trạng thái mở rất xa mèo và không kéo được (2026-10-21, commit 3a751ea)
- **Triệu chứng (ảnh người dùng):** bấm Trạng thái, thẻ hiện ở chỗ khác hẳn trên màn hình.
- **Nguyên nhân:** thẻ gán thẳng Left/Top của WPF, mà cửa sổ mới hiểu tọa độ theo DPI khác với tọa độ của mèo (DIP theo scale màn hình mèo đang đứng).
- **Sửa:** đo kích thước theo DIP, đặt vị trí bằng pixel theo scale của mèo (như thông báo trận); kéo được bằng chỗ trống trên thẻ.
- **Kiểm chứng:** check WPF: thẻ nằm ngay trên đầu mèo tính bằng pixel ở scale thật và scale giả 150%. Chưa thử trên màn hình phụ thật.

## #4 — Đòn combo vẫn đẩy lùi slime đá tinh anh "miễn Đẩy" (2026-10-21, commit 6d0c666)
- **Nguyên nhân:** "miễn Đẩy" lúc đầu chỉ áp dụng cho kỹ năng Đẩy; đòn đánh thường, búa, thẻ Đẩy mạnh vẫn đẩy lùi.
- **Sửa (người dùng duyệt mở rộng):** mọi đòn trúng không đẩy lùi tinh anh miễn Đẩy, sát thương giữ nguyên.
- **Kiểm chứng:** test C#: combo lệch 0 DIP nhưng vẫn mất máu; tinh anh thường bị đẩy 44 DIP.

## #3 — Banner trận tràn sang thanh HP và ô kỹ năng (2026-10-21, commit c66ba1d)
- **Triệu chứng (ảnh người dùng):** "Expedition · Step 1/6 · Shortcut" quá dài so với cột giữa HUD (230 DIP).
- **Sửa:** banner nằm trong Viewbox chỉ thu nhỏ — dài thì tự nhỏ lại, HUD không đổi kích thước; banner Viễn chinh rút gọn thành "Bậc 1/6 · <tên>".
- **Kiểm chứng:** check WPF banner dài thu vào ≤ 230 DIP; đã xem ảnh render.

## #2 — Tư thế trèo cửa sổ trông như mèo đứng lơ lửng vẫy tay (2026-10-15)
- **Triệu chứng (ảnh người dùng):** mèo leo cạnh cửa sổ không giống đang trèo.
- **Sửa:** vẽ lại tư thế Climbing: thân kéo dài và nghiêng, hai chân trước bám qua mép (có móng), chân sau đạp vào cạnh, đuôi rủ, mắt nhìn lên. Chỉ đổi phần vẽ, physics giữ nguyên. (Ảnh "mèo không có chân trước" lúc đó là bản publish cũ.)
- **Kiểm chứng:** check hình 6 khung × 2 hướng × có/không mũ nằm trong cửa sổ; đã xem ảnh cảnh trèo.

## #1 — Mèo giật hình khi bị ném và khi đi (2026-10-09)
- **Nguyên nhân:** timer chạy không đều, cửa sổ được dời hai bước (Left rồi Top) nên có khung hình lệch.
- **Sửa:** nhịp ~60 FPS đều; nội suy vị trí vẽ giữa hai bước physics 1/120 s; dời cửa sổ bằng một lệnh SetWindowPos. Physics, lực ném, trọng lực, độ nảy, FPS mục tiêu không đổi. Debug view hiện FPS và khoảng hở dài nhất.

---

# B. Lỗi do test, mô phỏng hoặc review tìm ra

| Ngày | Lỗi | Tìm bởi | Sửa |
|---|---|---|---|
| 2026-10-24 | Lịch app độc (`malwarePlan`/`malwareFrom`) không chạy ở màn thường: đếm bằng `FightSeconds`, giá trị chỉ ghi lúc trận kết thúc | Test virus.exe của hướng dẫn (UiChecks) | Màn thường đếm `Time` (màn sống sót vẫn theo đồng hồ) — ec27b0a |
| 2026-10-24 | Test tâm chỗ sáng của mèo PASS dù lệch 40 DIP: test đo cùng gốc sai với code | Người dùng gửi ảnh (#19) | Test so thêm với `pet.X` (độc lập) — 1d07490 |
| 2026-10-24 | Một seed màn 3 không ra "Cập nhật bắt buộc": không có chỗ đặt tường thì game đổi sang vòng loading | GuideStageTests | Lịch chờ tới khi đặt được tường (`ThrowUpdate(orLoading: false)`) — cb77240 |
| 2026-10-24 | battle.json gốc bị từ chối (goal > 160 ký tự) → test cân bằng không thấy chương | Test C# | Rút gọn goal; đo độ dài trước khi sửa dữ liệu — cb77240 |
| 2026-10-23 | Lên bản 1.4.2: `git tag` thường + `--follow-tags` không đẩy tag → GitHub Actions không chạy | Người dùng thấy Release không lên | Tài liệu dùng `git tag -a`; lần đó đẩy riêng `git push origin v1.4.2` |
| 2026-10-23 | Câu nhắc nghỉ dựng sẵn trong C# lệch với ai/lines.json (dòng thứ hai vi/en) | Test đối chiếu mới (S5) | Sửa cho khớp lines.json |
| 2026-10-23 | Thuộc tính `PetWindow.Language` che `FrameworkElement.Language` của WPF | Lúc làm hộp chào mừng | Đổi tên `UiLanguage` |
| 2026-10-23 | PowerShell 5.1 đọc .ps1 không BOM theo ANSI → hướng dẫn cài tiếng Việt trong ghi chú Release hỏng dấu | Đóng gói thử | Tách sang `scripts/release-install.md` đọc bằng UTF-8 |
| 2026-10-22 | `battle-records.json` chứa `null` làm app ném lỗi không bắt | Viết JsonStore (S2) | Đọc `null` → bắt đầu lại, ghi log |
| 2026-10-22 | Tết 2030 ghi 3/2 (đúng 2/2), Trung thu 2034 ghi 27/9 (đúng 26/9) | Nối bảng ngày lễ tới 2050 | Sửa ngày |
| 2026-10-22 | Quả bóng đồ chơi lấy focus và hiện trong Alt+Tab | Rà trước 1.4.0 | Cửa sổ bóng không kích hoạt, ẩn khỏi Alt+Tab |
| 2026-10-22 | Món đồ đáp lên mép cửa sổ không bẹp/nảy | Làm tính năng đệm slime | Chỉ coi "đang đứng" khi VY == 0 và chạm mép |
| 2026-10-22 | Bảng chuẩn bị màn 2 cao 519 DIP, tràn màn hình thấp (480 DIP) | Check đo chiều cao panel | Giới hạn theo màn hình + cuộn, nút luôn ở dưới |
| 2026-10-21 | Bản đồ Viễn chinh cao ~574 DIP, tràn ở 1366×768 125%, 1280×720 150%… | Test phát hành Viễn chinh | Giới hạn theo màn hình + cuộn (người dùng chọn) |
| 2026-10-20 | Vua Slime lùi về phía mèo đang đứng (hướng lui tính sai) | Review chiến thuật quái | Vua nhảy về mép xa mèo |
| 2026-10-20 | Slime đá chặn đường thành bức tường che cho mèo (quái khác không đi qua) | Mô phỏng cân bằng | Quái chắn chỉ chặn mèo, không chặn đồng đội |
| 2026-10-20 | Kế hoạch "đòn trùng lúc nhấc mèo luôn hụt" khiến nhấc/thả liên tục thành bất tử | Test cứu mèo | Chỉ hụt khi mèo đang được nhấc; bảo vệ tiếp đất cần nhấc ≥ 0,3 s |
| 2026-10-15 | Chế độ Quiet không gửi quan sát cho AI → nhật ký/nhu cầu/câu phản ứng bị cũ | Review của Codex | Quiet vẫn gửi quan sát nhưng từ chối mọi lệnh |
| 2026-10-15 | Thức dậy bị kéo dài (1,2 s → 2,6 s) khi click/chuột phải/Quiet/fullscreen giữa lúc đang thức | Review của Codex + test WPF | Reset giữ bộ đếm khi đang Waking |
| 2026-10-15 | Đổi ngôn ngữ xong vẫn hiện câu nói ngôn ngữ cũ (phản hồi AI đến muộn) | Review của Codex | Ghi ngôn ngữ lúc gửi, bỏ câu nếu đã đổi |
| 2026-10-13 | Bấm Làm ngay khi đang thức dậy kéo dài waking 1,2 s → 2 s | Test WPF | Chỉ hủy hoạt động khi lệnh thật sự bắt đầu |
| 2026-10-13 | Cửa sổ cài đặt cao quá màn hình của mèo | Rà soát tối ưu | Giới hạn chiều cao theo màn hình + cuộn |
| 2026-10-09 | Tài liệu/PROGRESS.md hỏng dấu tiếng Việt | Đọc lại tài liệu | Sửa lại UTF-8 |
