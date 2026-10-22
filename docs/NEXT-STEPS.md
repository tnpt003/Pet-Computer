# Trạng thái hiện tại và việc tiếp theo

Cập nhật: 2026-10-23 (Asia/Saigon). Bản đang chạy: 1.4.0 (installer + tự cập nhật, xem docs/RELEASE.md).

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
- Phát hành rộng: 1.4.0 bỏ beta, installer + tự cập nhật Velopack/GitHub Releases (N5). Còn: người dùng push tag + thử máy Win 10; ký code (SignPath) khi cần; đồng hồ cát có cát chảy.
- Dài hạn: quan hệ phát triển, nhiều mèo; LLM chỉ khi người dùng đổi quy tắc AGENTS.md.

## Đã bỏ theo người dùng (2026-10-22)

- Cho ăn bỏ cooldown/xếp hàng (phương án 1) và mèo no từ chối ăn.
- Đo lại RAM khi chạy dài.
- Phím tắt toàn cục cho Làm ngay.
- Dạy mèo nói (cách 2): không làm trong đợt này.
