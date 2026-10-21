# Nhật ký thay đổi

## Chưa phát hành

- **Cue âm thanh tinh anh:** tinh anh xuất hiện hoặc được Vua phong có tiếng "ting-ting" hai nốt (trước dùng tiếng mèo ngạc nhiên); Đẩy vào slime đá tinh anh miễn Đẩy có tiếng "cộp" khiên đá (trước dùng tiếng đòn bị chặn). Âm tổng hợp kiểu hoạt hình, theo tắt tiếng/âm lượng/fullscreen như mọi cue chiến đấu; mỗi cue cách nhau ít nhất 1 giây.

## 1.2.0-beta — Quái mạnh hơn, tinh anh nổi bật (2026-10-21)

Bản beta: test tự động và mô phỏng đạt, **chưa chơi thử thật**. Game khó hơn rõ rệt, nhất là Khó và Viễn chinh (người dùng chọn giữ bảng buff và hạ mục tiêu cân bằng).

- **Buff nền mọi quái:** Slime 58 HP/12 sát thương (trước 50/10), slime nhanh 28/7 (25/6), slime đá 188/18 (150/16), slime thảo dược 46 HP và hồi chiêu hồi máu 3,15 s (40, 3,5 s), Vua Slime 720/30 ở Phiêu lưu và 480/30 ở trận tự do (600/25, 400/25).
- **Tinh anh mạnh rõ** (nhân thêm trên buff nền, đặc tính cũ giữ nguyên): slime thường nay cũng có tinh anh (+20% HP, chờ giữa hai đòn −35%, thời gian gồng giữ nguyên); slime nhanh +20% sát thương; slime đá +30% HP và **miễn Đẩy** (không bị đẩy, choáng hay ngắt; kỹ năng vẫn tốn hồi chiêu, có banner và hiệu ứng chặn; đòn combo, búa và thẻ Đẩy mạnh cũng không đẩy lùi nó, nhưng vẫn gây sát thương); slime thảo dược hồi chiêu −15%, tổng lượng hồi +30% (mỗi lần hồi không mạnh hơn). Quân Vua Slime phong tinh anh cũng nhận các chỉ số này. Vẫn tối đa 1 tinh anh, không có ở Dễ.
- **Nhãn tinh anh:** chữ vàng nhỏ trên đầu quái tinh anh nêu đặc tính chính: MIỄN ĐẨY (đá), ĐÁNH NHANH (slime), ĐÒN MẠNH (nhanh), HỒI MẠNH (thảo dược); khi nó gồng, nhãn nhích lên trên dấu "!".
- **Vệt vàng:** slime nhanh tinh anh có vệt gió vàng dài hơn (4 vệt) và hai bóng mờ vàng khi lao.
- **Giáp dày:** slime đá tinh anh cầm khiên to hơn trên tấm lót vàng, thêm một đinh tán.
- **Hào quang:** slime thảo dược tinh anh có quầng xanh lá nhẹ phập phồng sau lưng (tắt khi hết lượng hồi).
- **Kỷ lục và Viễn chinh:** luật màn Phiêu lưu và Viễn chinh được nâng phiên bản: kỷ lục cũ vẫn lưu nhưng tách riêng; chuyến Viễn chinh đang dở từ bản trước không tiếp tục được.
- Từ sau 1.1.0-beta (đã phát hành nội bộ dưới nhãn 1.1.0-beta): Combat Depth, Enemy Director, chiến thuật quái, Vua Slime ba khiên, Viễn chinh với di vật, sự kiện và kỷ vật trưng bày — xem PROGRESS.md.
- Dữ liệu tùy chỉnh: `elite` có thêm `hp`, `damage`, `attackCooldown`, `healCooldown`, `healBudget`, `pushImmune` (docs/BATTLE-DATA.md). File battle.json của người dùng không bị đổi.

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
