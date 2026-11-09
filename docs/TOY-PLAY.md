# Chơi cùng sen (bản 1.6, Đợt 1)

Người dùng duyệt kế hoạch 2026-11-09. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền đồ vật | Tối đa 3 món, Giỏ đồ chơi, Cất nhanh, lưu vị trí | Đã có code, còn test tay |
| M2. Thùng carton | Ngủ trong hộp theo đúng luật ngủ thường: click trái thức và vẫn kéo được | Chưa viết luật |
| M3. Tha đồ, ném nhặt | Mèo tha đồ và chơi ném nhặt | Chưa viết luật |
| M4. Cần câu lông vũ, chuột dây cót | Hai đồ chơi mới | Chưa viết luật |
| M5. Chuyền bóng, phản ứng | Chuyền bóng và các phản ứng | Chưa viết luật |

Ngoại lệ physics: docs/APPROVED-EXCEPTIONS.md → Đồ vật có physics.

## Luật M1 Nền đồ vật (duyệt 09/11)

### Giỏ và giới hạn

- Mặc định giỏ chưa hiện. Mục "Giỏ đồ chơi" trong nhóm "Chơi" của menu chuột phải và trên khay lấy giỏ ra; trạng thái hiện/ẩn được nhớ. Giỏ đang hiện thì chính mục đó đổi thành "Cất nhanh". Pause, app toàn màn hình và trong trận thì mục này mờ.
- Giỏ đứng trên sàn, kéo ngang được, không ném được, không tính vào giới hạn 3 món.
- Bấm giỏ mở danh sách đồ. M1 chỉ có Bóng len, miễn phí; các món khác thêm ở M2 và M4.
- Tối đa 3 món trên desktop. Đủ 3 thì món còn lại mờ và ghi lý do.
- Thả đồ vào giỏ là cất.

### Đồ trên desktop

- Kéo quá khoảng 6 DIP là cầm; thả có vận tốc, trọng lực và nảy. Bấm không kéo thì bóng lăn nhẹ.
- Đồ chỉ nằm trên sàn màn hình, không đứng trên mép cửa sổ.
- Đồ ở lại màn hình được thả. Vị trí lưu theo tỉ lệ màn hình; màn hình bị rút thì về màn hình chính. Đổi DPI thì đồng bộ vị trí, vận tốc, độ lệch khi cầm và bỏ mẫu ném cũ.
- Thứ tự lớp: mèo, kỷ vật, đồ vật, vùng đất.
- Đồ nằm yên đè lên vùng đất hoặc xe hàng thì tự lăn sang chỗ trống gần nhất.
- Lưu trong `preferences.json`, như kệ kỷ vật. Chỉ lưu khi thả xong, cất và thoát app, không lưu từng khung.

### Giữ trò "Chơi bóng"

- Bóng đang trong giỏ: trò chạy như hiện nay, hết 45 giây bóng về giỏ.
- Bóng đang nằm ngoài: trò bắt đầu từ chỗ đó, hết giờ bóng nằm lại. Bóng nằm ở màn hình khác với mèo thì trò bắt đầu cạnh mèo như lấy từ giỏ, xong nằm lại ở màn hình của mèo.
- Trong lúc chơi, bấm vẫn là đẩy bóng và không cầm được, giữ đúng cảm giác cũ.
- Mèo ngủ, bị kéo hoặc đổi việc thì trò dừng như cũ, nhưng bóng đang nằm ngoài vẫn nằm lại. "Cất bóng" và Cất nhanh thì đưa bóng về giỏ.
- Bóng đang trong trò thì hàng của nó trong giỏ mờ và ghi "đang chơi".

### Cất nhanh

- Một lệnh trên menu chuột phải và khay: cất mọi đồ, ẩn giỏ, dừng trò đang chơi; mèo vẫn ở lại.
- Không đụng kỷ vật và nông trại, hai phần này đã có công tắc và biển riêng.

### Khi code cần kiểm tra

- Cửa sổ của trò 45 giây vẫn riêng; bóng nằm ngoài là vật khác, chỉ giao vị trí cho trò và nhận lại khi cửa sổ trò đóng (`PetWindow.Toys.cs`), nên `CancelFeatureActivity` giữ nguyên. Sửa vùng này thì test lại sleep lock, wake, drag, throw, emotion priority, local fallback.
- Đồ nằm yên không được di chuyển cửa sổ từng khung. Giữ FPS active ~60, sleeping ~30, paused ~10. Hiện chỉ có một món nên chưa đo được 3 cửa sổ đồ; đo lại khi có đủ món ở M2, M4.
- Test dùng dữ liệu riêng, không ghi đè `preferences.json` của người dùng.
