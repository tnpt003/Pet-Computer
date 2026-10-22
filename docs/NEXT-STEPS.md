# Trạng thái hiện tại và việc tiếp theo

Cập nhật: 2026-10-22 (Asia/Saigon). Bản đang chạy: 1.3.0-beta.

> **Quy tắc 20/10:** luôn commit và publish sau mỗi mốc code hoàn chỉnh, kiểm thử đạt; chạy smoke và báo artifact. Không cần hỏi lại. Mốc tài liệu-only commit, không publish binary. Xem AGENTS.md. Không tự push/upload.

Đọc AGENTS.md, PROGRESS.md và code thật trước khi làm. Các bàn giao/review cũ (18/10 → 21/10: Battle Mode, Adventure, Combat Depth, Enemy Director, Enemy Tactics R1–R4, buff quái) đã triển khai và nằm ở [archive/NEXT-STEPS-2026-10.md](archive/NEXT-STEPS-2026-10.md); không coi chúng là việc còn mở.

## Đã xong

- Pet Mode đầy đủ (ngủ/thức, kéo/ném/Dizzy, cảm xúc, bóng/laser/cho ăn, đi trên cửa sổ, nói + AI, nhật ký, bộ lông/phụ kiện/ngày lễ).
- Battle Mode: Sảnh, Phiêu lưu Chương 1 (5 màn), Viễn chinh (quay bậc, máy viên nang, di vật, kỷ vật), túi đồ theo chế độ, Phần thưởng.
- Món trưng bày có physics, đệm slime (ngủ, bạt nhún).
- Người dùng đã test tay toàn bộ docs/MANUAL-TEST.md đến bản b5fa2eb (2026-10-22): passed.

## Kế hoạch đang làm (duyệt 2026-10-22)

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1 | Dọn tài liệu: archive lịch sử, NEXT-STEPS này, tick MANUAL-TEST | đang làm |
| M2 | Chuẩn hóa: hình học mèo vào PetSettings; một hàm tạo cửa sổ overlay; gộp cờ "người dùng yêu cầu"; test giả vị trí chuột (6 check trận không phụ thuộc chuột thật); test battle.json khớp fallback C# | chưa |
| M3 | Tách PetBattle.cs thành partial theo nhóm (chỉ di chuyển code); đổi tên shelf/trophy → keepsake (giữ key preferences) | chưa |
| M4 | Gom cỡ chữ lẻ về thang chuẩn, đo chiều cao panel | chưa |
| M5 | Lịch: Tết/Trung thu tới 2050 (thuật toán âm lịch, đối chiếu bảng cũ); thêm 8/3, 20/10, 1/1 | chưa |
| M6 | Viễn chinh: quay lại bậc, 8% HP tối đa, 1 lần mỗi bậc, bốc từ seed | chưa |
| M7 | HUD trận kéo được (nhớ theo màn hình, double-click về chỗ cũ); đo frame time trận đông quái | chưa |
| M8 | Chế độ Vô tận: đợt mạnh dần, 3 đợt chọn 1 thẻ, Vua mỗi 10 đợt, kỷ lục đợt cao nhất theo độ khó, đóng app là hết lượt, túi đồ chế độ Vô tận | chưa |

## Backlog (chưa chọn)

- Mở rộng Viễn chinh: vùng/chương mới, thêm di vật/sự kiện.
- Battle: thu dải sàn chỉ ôm vùng có slime; HUD cập nhật 15 lần/giây — chỉ làm nếu số đo cho thấy cần. Kỹ năng (BattleSkills) chưa thành dữ liệu.
- Phát hành rộng: bỏ nhãn beta, thử máy sạch, installer/MSIX, tự cập nhật, ký code.
- Dài hạn: quan hệ phát triển, nhiều mèo; LLM chỉ khi người dùng đổi quy tắc AGENTS.md.

## Đã bỏ theo người dùng (2026-10-22)

- Cho ăn bỏ cooldown/xếp hàng (phương án 1) và mèo no từ chối ăn.
- Đo lại RAM khi chạy dài.
- Phím tắt toàn cục cho Làm ngay.
- Dạy mèo nói (cách 2): không làm trong đợt này.
