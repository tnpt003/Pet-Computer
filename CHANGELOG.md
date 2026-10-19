# Nhật ký thay đổi

## 1.1.0-beta — Adventure Update, Chương 1 (2026-10-20)

Bản beta: đã có test tự động và mô phỏng cân bằng, **chưa được chơi thử thật**, chưa thử DPI 125–150% và máy sạch. Xem docs/MANUAL-TEST.md (mục "Phiêu lưu").

- **Phiêu lưu** (chuột phải → Chế độ chiến đấu → Phiêu lưu): bản đồ chương "Cuộc xâm nhập của Slime" với 5 màn mở theo thứ tự; bảng chuẩn bị cho từng màn; bảng kết quả với Màn tiếp / Chơi lại / Bản đồ / Về desktop. Kết thúc giữa trận màn quay về bản đồ.
- **Tiến trình lưu** ở adventure-progress.json: màn đã thắng, kỷ lục theo màn + vũ khí + độ khó, phần thưởng, mẹo đã xem. Mỗi lượt chỉ tính một lần; trận đang đánh không được lưu; lưu an toàn (.tmp, .bak); báo trên bản đồ/bảng kết quả khi không lưu được hoặc file là của bản mới hơn.
- **Thẻ nâng cấp giữa các đợt** (màn 2–5): chọn 1 trong 3 thẻ (Sắc bén, Nhanh chân, Ra đòn nhanh, Đẩy mạnh, Cá nướng, Lông dày), chỉ hiệu lực trong lượt chơi.
- **Màn sinh tồn** "Cầm cự": sống sót 75 giây, đồng hồ dừng khi bế mèo.
- **Slime thảo dược** hồi máu cho đồng đội (có cảnh báo, có giới hạn; Đẩy để cắt) và **Vua Slime theo pha** ở màn cuối.
- **Phần thưởng**: Mũ slime (màn 3) và Vương miện Vua Slime (màn 5), đội trong Cài đặt → Mèo sau khi nhận.
- **Mẹo theo màn** (màn 2–5) hiện một lần, xem lại từ bảng chuẩn bị; HUD ghi số màn.
- Không đổi: trận tự do, kỷ lục trận tự do (battle-records.json), Pet Mode, Python AI.

## 1.0.0

Bản trước Adventure (Pet Mode, Battle Mode với trận tự do, trang bị kiếm/búa, dữ liệu trận tùy chỉnh).
