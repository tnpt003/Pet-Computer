# Trạng thái hiện tại và việc tiếp theo

Cập nhật: 2026-10-23 (Asia/Saigon). Bản đang chạy: 1.4.1 (installer + tự cập nhật + chào mừng sau khi cài, xem docs/RELEASE.md). Release 1.4.0 và 1.4.1 đã có trên GitHub; bản sau nâng lên 1.4.2. Máy dev chạy bản copy `artifacts\win-x64` (không cài Setup).

> **Quy tắc 20/10:** luôn commit và publish sau mỗi mốc code hoàn chỉnh, kiểm thử đạt; chạy smoke và báo artifact. Không cần hỏi lại. Mốc tài liệu-only commit, không publish binary. Xem AGENTS.md. Không tự push/upload.

Đọc AGENTS.md, PROGRESS.md và code thật trước khi làm. Các bàn giao/review cũ (18/10 → 21/10: Battle Mode, Adventure, Combat Depth, Enemy Director, Enemy Tactics R1–R4, buff quái) đã triển khai và nằm ở [archive/NEXT-STEPS-2026-10.md](archive/NEXT-STEPS-2026-10.md); không coi chúng là việc còn mở.

## Đã xong

- Pet Mode đầy đủ (ngủ/thức, kéo/ném/Dizzy, cảm xúc, bóng/laser/cho ăn, đi trên cửa sổ, nói + AI, nhật ký, bộ lông/phụ kiện/ngày lễ).
- Battle Mode: Sảnh, Phiêu lưu Chương 1 (5 màn), Viễn chinh (quay bậc, máy viên nang, di vật, kỷ vật), túi đồ theo chế độ, Phần thưởng.
- Món trưng bày có physics, đệm slime (ngủ, bạt nhún).
- Người dùng đã test tay toàn bộ docs/MANUAL-TEST.md đến bản b5fa2eb (2026-10-22): passed.

## Kế hoạch 2026-10-22 (đã xong M1–M8)

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1 | Dọn tài liệu: archive lịch sử, NEXT-STEPS này, tick MANUAL-TEST | xong (265990e) |
| M2 | Chuẩn hóa: hình học mèo vào PetSettings; một hàm tạo cửa sổ overlay; gộp cờ "người dùng yêu cầu"; test giả vị trí chuột (6 check trận không phụ thuộc chuột thật); test battle.json khớp fallback C# | xong (587d460) |
| M3 | Tách PetBattle.cs thành partial theo nhóm (chỉ di chuyển code); đổi tên shelf/trophy → keepsake (giữ key preferences) | xong (7f1f1cc) |
| M4 | Gom cỡ chữ lẻ về thang chuẩn, đo chiều cao panel | xong (e1c7879) |
| M5 | Lịch: Tết/Trung thu tới 2050 (thuật toán âm lịch, đối chiếu bảng cũ); thêm 8/3, 20/10, 1/1 | xong (c2624f2) |
| M6 | Viễn chinh: quay lại bậc, 8% HP tối đa, 1 lần mỗi bậc, bốc từ seed | xong (2387f2d) |
| M7 | HUD trận kéo được (nhớ theo màn hình, double-click về chỗ cũ); đo frame time trận đông quái | xong (a35c582; đông quái 61 fps, p95 33 ms — chưa cần tối ưu) |
| M8 | Chế độ Vô tận: đợt mạnh dần, 3 đợt chọn 1 thẻ, Vua mỗi 10 đợt, kỷ lục đợt cao nhất theo độ khó, đóng app là hết lượt, túi đồ chế độ Vô tận | xong (88da9f7) |

## Backlog (chưa chọn)

- Vô tận: chỉnh số liệu sau khi chơi thật; mô phỏng cân bằng (phần thưởng mốc đợt: xong 22/10, N3).

- Mở rộng Viễn chinh: vùng/chương mới, thêm di vật/sự kiện.
- Battle: thu dải sàn chỉ ôm vùng có slime; HUD cập nhật 15 lần/giây — số đo 22/10 (16 quái: 61 fps) cho thấy chưa cần. Kỹ năng: tên/mô tả đã thành dữ liệu (N2).
- Phát hành rộng: 1.4.0 bỏ beta, installer + tự cập nhật Velopack/GitHub Releases (N5). 1.4.0 đã phát hành qua Actions; 1.4.1 thêm hộp chào mừng (ngôn ngữ + ô Khởi động cùng Windows), đóng bản copy đang chạy khi vừa cài, Cài đặt → Chung → Cập nhật, thông báo "có gì mới", hướng dẫn cài trên trang Release, link tải cố định trong README, gỡ cài đặt xóa mục khởi động. Còn: người dùng push tag v1.4.1 để thử tự cập nhật + thử máy Win 10; MSI chọn thư mục (nếu muốn); ký code (SignPath) khi cần; đồng hồ cát có cát chảy (xong 24/10).
- **Xong (23/10):** chuẩn hóa S1, S2, S3a, S3b (xong), i18n J1 khung, J2 dịch UI C#, J3 dữ liệu, J4 câu thoại + hiện 日本語 (xong; tiếng Nhật: câu tiếng Anh làm khóa, Claude soạn bản dịch; người dùng duyệt 23/10), S4 cổng `MayAct(AutoKind)` (xong, e6dc357; xem PROGRESS.md).
- Dài hạn: quan hệ phát triển, nhiều mèo; LLM chỉ khi người dùng đổi quy tắc AGENTS.md.

## Hướng mở rộng mới: mèo nói tiếng người (người dùng đề xuất 2026-10-23, chưa duyệt thiết kế)

Ý tưởng: mèo **đọc to thành tiếng** câu đang hiện trong bong bóng chữ (vi/en/ja), giọng kiểu nhân vật hoạt hình. Chưa làm; trước khi code cần người dùng duyệt và **đổi quy tắc AGENTS.md** ("Chưa tích hợp voice"). Không gồm nghe người dùng nói (nhận dạng giọng): đó là hướng riêng nếu muốn.

**Giữ nguyên:**
- Phân vai: Python vẫn chỉ chọn câu (đã có: lines.json, speech.py, `PetLine`). C# phát giọng, như mọi âm thanh khác.
- Khi Python tắt: câu local (PetBreaks…) cũng đọc được.
- Không chặn luồng UI.
- Cue âm thanh hoạt hình hiện có giữ nguyên; giọng là lớp thêm vào, không thay cue.

**Các cách làm (từ rẻ đến tốn):**

| Cách | Mô tả | Ưu | Nhược |
|---|---|---|---|
| A. Tiếng "líu lo" hoạt hình | Ghép âm tiết ngắn theo nhịp chữ trong bong bóng, kiểu Animal Crossing; không phải lời thật | Hợp phong cách, không phụ thuộc máy, nhẹ (vài file wav CC0 hoặc tự tổng hợp), mọi ngôn ngữ như nhau | Không nghe ra chữ |
| B. Giọng Windows có sẵn | `Windows.Media.SpeechSynthesis` / System.Speech, SSML chỉnh cao độ/tốc độ cho "dễ thương" | Nói ra chữ thật, offline, không thêm dependency lớn | Giọng hơi "robot"; máy thiếu giọng vi/ja nếu chưa cài gói ngôn ngữ (khi đó dùng A) |
| C. Thu/tổng hợp sẵn lúc build | Tạo file giọng cho ~165 câu × 3 ngôn ngữ bằng TTS offline, đóng gói vào app | Giọng đồng nhất, chọn được giọng đẹp | Tăng dung lượng; câu có `{name}` và câu học được phải xử lý riêng; sửa câu là phải tạo lại |
| D. Giọng neural offline lúc chạy | Piper/sherpa-onnx, model ~20–60 MB mỗi ngôn ngữ | Tự nhiên, đọc được mọi câu kể cả tên mèo | Dependency + dung lượng lớn, tốn CPU; trái nguyên tắc hạn chế dependency |

**Quy tắc hành vi đề xuất:**
- Chỉ nói khi bong bóng hiện.
- Theo công tắc Âm thanh và âm lượng chung.
- Im khi:
  - Quiet;
  - app fullscreen;
  - mèo đang ngủ (trừ câu chào khi thức);
  - trong trận;
  - Tạm dừng.
- Không chồng lên cue âm thanh khác và có cooldown.
- Bị cầm hoặc ném thì ngắt giọng.
- Đổi ngôn ngữ thì bỏ câu đang đọc.
- Có công tắc riêng "Mèo nói thành tiếng", **mặc định tắt**.
- Trong Tùy chỉnh có thêm mục giọng: cao/thấp, nhanh/chậm, nút nghe thử. Nghe thử vẫn phát kể cả khi tắt âm thanh, giống nghe thử cue.

**Mốc gợi ý (khi duyệt):**
- **V1:** công tắc + cách A, để có cảm giác "mèo nói" mà không phụ thuộc máy.
- **V2:** cách B. Đọc câu thật nếu máy có giọng của ngôn ngữ đang dùng, thiếu thì quay về A. Mục giọng trong Tùy chỉnh.
- **V3 (tùy chọn):** C hoặc D nếu giọng Windows chưa đủ đẹp.

**Cần người dùng chọn:**
1. Muốn nghe ra chữ thật (B/C/D) hay chỉ "líu lo" hoạt hình (A)?
2. Ngôn ngữ nào trước (vi/en/ja)?
3. Có chấp nhận tăng dung lượng hoặc thêm dependency (C/D) không?
4. Duyệt đổi quy tắc "chưa tích hợp voice" trong AGENTS.md.

## Đã bỏ theo người dùng (2026-10-22)

- Cho ăn bỏ cooldown/xếp hàng (phương án 1) và mèo no từ chối ăn.
- Đo lại RAM khi chạy dài.
- Phím tắt toàn cục cho Làm ngay.
- Dạy mèo nói (cách 2): không làm trong đợt này.
