# Chơi cùng sen (bản 1.6, Đợt 1)

Người dùng duyệt kế hoạch 2026-11-09. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền đồ vật | Tối đa 3 món, Giỏ đồ chơi, Cất nhanh, lưu vị trí | Đã có code, còn test tay |
| M2. Thùng carton | Ngủ trong hộp theo đúng luật ngủ thường: click trái thức và vẫn kéo được | Đã có code, còn test tay |
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
- Đồ nằm yên không được di chuyển cửa sổ từng khung. Giữ FPS active ~60, sleeping ~30, paused ~10. Hiện chỉ có một món nên chưa đo được 3 cửa sổ đồ; đo lại khi có đủ món ở M2, M4. Đo 09/11 (bản faca0f0, một màn hình, dữ liệu riêng, Quiet, không ai bấm chuột, 2 vòng, mỗi vòng khoảng 31 giây sau khi bỏ 8 giây đầu), FPS trung vị của vòng 1, vòng 2: không giỏ 57,4 và 57,0; giỏ và bóng nằm yên 56,8 và 57,2. Chưa thấy khác biệt. Chưa đo lúc bóng đang bay, DPI khác hay nhiều màn hình.
- Test dùng dữ liệu riêng, không ghi đè `preferences.json` của người dùng.

## Luật M2 Thùng carton (duyệt 11/11)

### Thùng carton là đồ vật

- Lấy ra từ Giỏ đồ chơi, miễn phí, tính vào giới hạn 3 món.
- Rộng khoảng 100 DIP, nắp mở hai bên, chỉ nằm trên sàn, tự tránh vùng đất như bóng.
- Kéo quá khoảng 6 DIP là cầm; thả thì rơi và nảy thấp, trượt ngắn, không lăn, không xoay. Bấm không kéo thì hộp rung nhẹ.

### Khi nào mèo vào hộp

- Mèo tự đi tới trong đúng ba dịp như đệm slime: mỗi 4–7 phút, khi mèo tự chọn ngủ, và khi sắp buồn ngủ vì khoảng 95 giây không ai chạm.
- Bị chặn khi mèo bận, Quiet, Giờ tập trung, trong trận, đang có việc khác, hoặc trong wake cooldown 30 giây. Mèo phải đứng trên sàn, cùng màn hình với hộp. Đi quá 20 giây chưa tới thì thôi.
- "Làm ngay → Ngủ trong hộp" chỉ khi mèo cách hộp tối đa 200 DIP; như mọi "Làm ngay", chạy cả trong Quiet và wake cooldown.
- Gợi ý ngủ của AI và away nap vẫn ngủ tại chỗ.
- Có cả đệm và hộp thì mèo chọn cái gần hơn.

### Ngủ trong hộp là giấc ngủ thường

- Mèo nhảy nhẹ vào hộp rồi dùng đúng tư thế ngủ, đúng timer khoảng 10 giây; Pause đóng băng timer. Không có âm thanh mới.
- Click trái thức ngay và vẫn kéo được, mèo được nhấc thẳng ra khỏi hộp. Click phải thức ngay và mở menu.
- Thức tự nhiên: mèo ở trong hộp suốt 1,2 giây waking, chỉ thò đầu lên, rồi nhảy nhẹ ra (khoảng 0,3 giây, chỉ là phần hình) và về Idle.
- Mèo bị ném trúng hộp thì không có gì xảy ra; chỉ đệm mới nảy.

### Khi mèo đang trong hộp

- Không cầm được hộp; hàng của nó trong giỏ mờ, ghi "mèo đang ngủ".
- Cất nhanh vẫn chạy: hộp biến mất, mèo ngủ tiếp trên sàn, không thức. App toàn màn hình, vào trận hoặc rút màn hình: hộp ẩn, mèo cũng ngủ tiếp tại chỗ.
- Hộp không tự lăn tránh vùng đất cho tới khi mèo ra.
- Người dùng nhấc hộp lúc mèo đang đi tới thì chuyến đó hủy, không phạt gì.

### Chưa làm ở M2

- Vồ đồ chơi từ trong hộp, trốn tìm.

### Khi code cần kiểm tra

- Mặt trước hộp phải che thân mèo mà cửa sổ mèo luôn nằm trên cửa sổ hộp: vẽ mặt trước trong cửa sổ mèo khi mèo ở trong hộp, để mọi click vẫn rơi vào mèo và luật thức, kéo không cần đường input mới. Soát mọi tư thế ngủ, waking, lúc bị nhấc ra.
- Test lại sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, wake cooldown, và đệm slime khi có cả hai.

### Cách code M2 (11/11)

- Hộp là món `box` trong giỏ, chung khung cầm, ném, lưu, tránh vùng đất của M1 (`PetWindow.Toys.cs`). Physics riêng `ToyBall.StepBox`: trọng lực 1400, chỉ nảy khi chạm sàn nhanh hơn 160 DIP/s và giữ 22 % tốc độ, ma sát sàn mạnh, không xoay. Bấm không kéo gọi `Wobble`: hộp rung tại chỗ.
- Giấc ngủ trong hộp dùng chung trạng thái đi tới và ngủ của đệm slime (`cushionNap` trong `PetWindow.KeepsakePlay.cs`), cờ `napBox` chọn giường. `GoNap` chọn cái gần hơn khi có cả hai. Phần riêng của hộp ở `PetWindow.ToyBox.cs`: chỉ là hình, gồm nhảy vào 0,35 giây cao 32 DIP, nằm cao hơn sàn 3 DIP, nhảy ra 0,3 giây, cùng mặt trước hộp vẽ trong cửa sổ mèo (`ToyBoxArt.DrawFront`, `PetRenderer.BoxFrontDrop`).
- Nhảy ra chỉ khi giấc ngủ kết thúc mà mèo vẫn đứng trên sàn, hộp còn dưới mèo và không ai kéo. Bị kéo thì mặt trước tắt ngay. Hộp mất thì giấc ngủ chỉ mất phần hình, mèo ngủ tiếp tại chỗ.
- Kỷ vật rơi trúng mèo đang ngủ trong hộp vẫn gây Bonk như mọi giấc ngủ (ngoại lệ Kệ kỷ vật có physics). Chỉ đệm bỏ qua Bonk, vì lúc đó mèo nằm trên chính món kỷ vật.
- Đã soát ảnh render (ghép cửa sổ mèo lên cửa sổ hộp) lúc ngủ, waking, giữa cú nhảy ra và sau khi ra: mặt trước hai cửa sổ trùng khít. Chưa xem trên desktop thật, chưa thử DPI khác, nhiều màn hình.
