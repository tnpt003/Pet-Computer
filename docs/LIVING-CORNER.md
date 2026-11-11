# Góc sống và quan hệ (bản 1.6, Đợt 2)

Người dùng duyệt chia mốc 2026-11-11. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 1 và 2.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Trụ cào | Món mới trong giỏ; mèo tới cào rồi vươn vai; lệnh Cào móng có trụ gần thì đi tới trụ | Luật chờ duyệt |
| M2. Chậu cây, hộp thêm trạng thái | Chậu cây (ngửi, chạm lá, ngồi cạnh, lớn theo số lần chơi, không héo); hộp thò đầu và vồ đồ đi ngang | Chưa viết luật |
| M3. Mốc thân thiết | Bond thành 5 cấp có tên trong Nhật ký; mỗi cấp mở một hành vi (chào theo cấp, ngủ tựa vào đồ, chuỗi sau khi chơi) | Chưa viết luật |
| M4. Món yêu thích, lựa chọn | Python đếm lần chơi từng món; mèo quay lại món thích, rủ chơi bằng món đó; hai món cạnh nhau thì chọn một | Chưa viết luật |
| M5. Gọi mèo, dạy động tác | Lệnh "Gọi mèo"; mở vẫy tay, xoay vòng qua tương tác rồi gọi từ "Làm ngay" | Chưa viết luật |

Quyết định chung (người dùng đồng ý 11/11):

- Đồ góc sống (trụ cào, chậu cây) có giới hạn riêng **2 món** trên desktop; đồ chơi (bóng, hộp, cần câu, chuột) vẫn tối đa 3 món. Đo FPS khi có đủ 5 món.
- Cấp thân thiết và món yêu thích do Python giữ (memory). Python tắt thì không có các hành vi mở khóa của M3, M4; mèo chạy hành vi local như hiện nay.
- Để sang đợt khác: lưu bố trí, chế độ xem góc sống, bộ đồ theo chủ đề (Đợt 6); bảng ảnh, ký ức từ trận thắng (Đợt 4). Để sau: dáng đi theo tâm trạng, nghi thức riêng, mèo tự sắp xếp đồ.

## Luật M1 Trụ cào (đề xuất 11/11, chờ duyệt)

### Trụ cào là đồ góc sống

- Lấy ra từ Giỏ đồ chơi, miễn phí. Giỏ chia hai nhóm "Đồ chơi" (tối đa 3) và "Góc sống" (tối đa 2); nhóm nào đủ thì món còn lại trong nhóm đó mờ và ghi lý do.
- Cao khoảng 110 DIP, đế gỗ rộng khoảng 60 DIP, cột quấn dây thừng, đỉnh có đĩa nhỏ treo một quả bông. Vẽ vector theo docs/DESIGN-SYSTEM.md, nhìn rõ trên nền sáng/tối.
- Physics như thùng carton: kéo quá khoảng 6 DIP là cầm; thả thì rơi, nảy thấp, trượt ngắn, không lăn, không xoay; chỉ nằm trên sàn, tự tránh vùng đất và xe hàng. Bấm không kéo thì quả bông đung đưa.
- Lưu, Cất nhanh, Pause, app toàn màn hình, trận, rút màn hình, đổi DPI: giống mọi món của Đợt 1 M1.

### Khi nào mèo cào trụ

- **Tự đi tới** mỗi 6–10 phút. Bị chặn như mèo tự vào hộp: mèo bận, Quiet, Giờ tập trung, trong trận, đang có việc khác (trò bóng/laser, chuyến M2–M5), wake cooldown 30 giây, ngủ hoặc waking. Mèo phải đứng trên sàn, cùng màn hình với trụ. Đi quá 15 giây chưa tới thì thôi.
- **Lệnh Cào móng sẵn có** (Python gửi `SCRATCH` hoặc hành vi local tự chọn): nếu có trụ cùng màn hình cách mèo tối đa 400 DIP thì mèo đi tới trụ cào; xa hơn hoặc không có trụ thì cào tại chỗ như hiện nay. Không đổi giao thức, không có lệnh Python mới.
- **"Làm ngay → Cào móng"**: có trụ cách tối đa 200 DIP thì tới trụ, không thì cào tại chỗ. Như mọi "Làm ngay", chạy cả trong Quiet và wake cooldown.
- Cào trụ dùng chung cooldown tự chăm sóc 45 giây với chải lông, vươn vai và các cử chỉ.

### Cào và vươn vai

- Mèo đứng sát trụ, quay mặt vào trụ, rướn hai chân trước lên cột (tư thế Cào móng có sẵn, tay đặt lên cột) khoảng 2,4 giây; dây thừng rung nhẹ, vài sợi xơ bay. Sau đó vươn vai 2,4 giây rồi về Idle.
- Không có âm thanh mới.
- Trụ không chạm mèo và không bị mèo đẩy; đồ rơi trúng mèo vẫn đi xuyên, không Bonk, không Dizzy, không cảm xúc.
- Cào trụ không phải tương tác của người dùng: needs, bond và learning không đổi (như "Làm ngay").

### Khi bị ngắt

- Click trái hoặc kéo mèo: dừng ngay, kéo như thường. Click phải: dừng và mở menu.
- Cảm xúc ưu tiên cao (Angry, Surprised, Dizzy...) đến thì dừng cào. Pause đóng băng cả chuyến đi lẫn lúc cào.
- Người dùng nhấc trụ lúc mèo đang đi tới hoặc đang cào: chuyến đó hủy, mèo về Idle, không phạt gì. Cất nhanh cũng vậy.
- Không bao giờ bắt đầu khi mèo ngủ; mèo đang cào thì không tự chuyển sang ngủ.

### Khi code cần kiểm tra

- Test lại sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, wake cooldown, Quiet, Giờ tập trung, lệnh Cào móng khi không có trụ (giữ hành vi cũ).
- Giới hạn nhóm trong giỏ, cùng dữ liệu `preferences.json` cũ (chưa có trụ) vẫn đọc được.
- Đo FPS với 3 đồ chơi và trụ nằm yên; soát render trụ, mèo cào trên nền sáng/tối.

### Bổ sung ngoại lệ đề xuất (chờ duyệt, ghi vào docs/APPROVED-EXCEPTIONS.md → Đồ vật có physics khi duyệt)

Trụ cào là đồ góc sống có physics như thùng carton (giới hạn riêng 2 món đồ góc sống, đồ chơi vẫn 3). Mèo tự đi tới cào mỗi 6–10 phút, chặn như mèo tự vào hộp; lệnh Cào móng sẵn có của Python/local và "Làm ngay" đi tới trụ khi trụ đủ gần, nếu không thì cào tại chỗ như cũ. Cào 2,4 s rồi vươn vai, chung cooldown tự chăm sóc; click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng, không bao giờ khi ngủ/waking. Trụ vẫn không chạm mèo: không Bonk, Dizzy, cảm xúc, không đánh thức. Python không có lệnh mới.
