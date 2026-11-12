# Chơi cùng sen (bản 1.6, Đợt 1)

Người dùng duyệt kế hoạch 2026-11-09. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền đồ vật | Tối đa 3 món, Giỏ đồ chơi, Cất nhanh, lưu vị trí | Đã có code, còn test tay |
| M2. Thùng carton | Ngủ trong hộp theo đúng luật ngủ thường: click trái thức và vẫn kéo được | Đã có code, còn test tay |
| M3. Tha đồ, ném nhặt | Mèo tha đồ và chơi ném nhặt | Đã có code, còn test tay |
| M4. Cần câu lông vũ, chuột dây cót | Hai đồ chơi mới | Đã có code, còn test tay |
| M5. Chuyền bóng, phản ứng | Chuyền bóng và các phản ứng | Đã có code, còn test tay |

Ngoại lệ physics: docs/APPROVED-EXCEPTIONS.md → Đồ vật có physics.

## Luật M1 Nền đồ vật (duyệt 09/11)

### Giỏ và giới hạn

- Mặc định giỏ chưa hiện. Mục "Giỏ đồ chơi" trong nhóm "Chơi" của menu chuột phải và trên khay lấy giỏ ra; trạng thái hiện/ẩn được nhớ. Giỏ đang hiện thì chính mục đó đổi thành "Cất nhanh". Pause, app toàn màn hình và trong trận thì mục này mờ.
- Giỏ đứng trên sàn, kéo ngang được, không ném được, không tính vào giới hạn 3 món.
- Bấm giỏ mở danh sách đồ. M1 chỉ có Bóng len, miễn phí; các món khác thêm ở M2 và M4.
- Tối đa 3 món trên desktop. Đủ 3 thì món còn lại mờ và ghi lý do. Đồ góc sống của Đợt 2 (trụ cào) có giới hạn riêng 2 món (docs/LIVING-CORNER.md).
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
- Mèo ngủ, bị kéo hoặc đổi việc thì trò dừng như cũ, nhưng bóng đang nằm ngoài vẫn nằm lại. "Cất bóng" (trong Chơi ›, hoặc chuột phải lên quả bóng từ 12/11) và Cất nhanh thì đưa bóng về giỏ. Từ 12/11 mỗi đồ có menu chuột phải riêng kết thúc bằng "Cất vào giỏ" (docs/WINDOWS.md → Menu and settings layout).
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
- "Ngủ trong hộp" (từ 12/11 ở menu chuột phải của hộp, không còn trong Làm ngay; mèo ngủ/bận thì dòng mờ, mở menu hộp không đánh thức mèo) chỉ khi mèo cách hộp tối đa 200 DIP; như mọi "Làm ngay", chạy cả trong Quiet và wake cooldown.
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
- Đo 11/11 (bản 1e1fa40, cùng cách đo M1), FPS trung vị vòng 1, vòng 2: không giỏ 59,8 và 60,1; giỏ và bóng nằm yên 59,9 và 59,8; giỏ, bóng và hộp nằm yên 59,2 và 59,2. Hộp thêm một cửa sổ, chênh dưới 1 fps. Chưa đo lúc mèo ngủ trong hộp hay hộp đang bay, chưa đủ 3 món.

## Luật M3 Tha đồ và ném nhặt (duyệt 11/11)

### Món tha được

- Chỉ Bóng len. Thùng carton không tha được. Cần câu lông vũ và Chuột dây cót (M4) cũng không tha được.

### Ném nhặt (người dùng bắt đầu)

- Người dùng kéo bóng rồi thả với tốc độ trên khoảng 400 DIP/s. Ném nhẹ, đặt xuống hoặc bấm cho lăn thì không tính.
- Mèo chỉ đi nhặt khi đang thức, rảnh (Idle hoặc đang đi), đứng trên sàn, cùng màn hình với bóng. Mèo nhìn theo bóng; khi bóng chạm sàn và chậm lại thì chạy tới, ngậm bóng, mang về chỗ con trỏ trên sàn (theo trục ngang của con trỏ), nhả bóng rồi ngồi ngước lên chờ ném tiếp.
- Do người dùng bật nên chạy cả trong Quiet và wake cooldown, như bóng/laser. Mèo đang ngủ thì ngủ tiếp (luật M1). Mèo bận, bị kéo, đang bay, đang phản ứng hoặc có việc khác thì bỏ qua cú ném.
- Nhặt liên tiếp tối đa 5 lần, rồi nghỉ 2 phút: chỉ nhìn theo bóng, không chạy.
- Đi quá 20 giây chưa tới bóng thì thôi.

### Tha đồ (mèo tự làm)

- Rủ chơi: mỗi khoảng 8–15 phút, nếu bóng đang nằm ngoài và người dùng đang dùng máy, mèo ngậm bóng mang tới chỗ con trỏ trên sàn rồi ngồi chờ. Trong 20 giây người dùng ném thì thành ném nhặt, không thì mèo đi việc khác.
- Bị chặn như mèo tự vào hộp: mèo bận, Quiet, Giờ tập trung, trong trận, đang có việc khác, wake cooldown, người dùng vắng. Mèo phải đứng trên sàn, cùng màn hình với bóng. Đi quá 20 giây chưa tới thì thôi.
- Mèo không tự cất bóng vào giỏ và không tự tha bóng về cạnh giỏ.

### Khi mèo đang ngậm bóng

- Bóng vẽ ở miệng trong cửa sổ mèo (như mặt trước hộp ở M2), cửa sổ bóng ẩn; mọi click vẫn rơi vào mèo.
- Bấm trái hoặc kéo mèo: mèo nhả bóng, bóng rơi từ miệng theo physics, việc kéo như thường. Bấm phải: nhả bóng và mở menu.
- Cảm xúc tự động chờ như lúc đi tới hộp. Pause đóng băng tất cả.
- App toàn màn hình, vào trận hoặc rút màn hình: mèo nhả bóng tại chỗ, rồi bóng ẩn theo luật M1. Cất nhanh hoặc "Cất bóng": bóng về giỏ, chuyến tha hủy.
- Người dùng cầm bóng khi mèo đang chạy tới thì chuyến đó hủy, không phạt gì.
- Hàng bóng trong giỏ mờ, ghi "mèo đang ngậm".
- Không có âm thanh mới: nhặt và nhả dùng lại `ball_tap`, có cooldown.
- Trò "Chơi bóng" 45 giây giữ nguyên; trong lúc trò chạy không có ném nhặt hay tha đồ.
- Python không tham gia, không có sự kiện IPC mới; mọi phần ở C#.

### Khi code cần kiểm tra

- Soát hình bóng ở miệng qua mọi tư thế đi, chạy, ngồi, quay mặt, lúc nhả và lúc bị nhấc.
- Bóng nhả ra nhận vị trí miệng, đồng bộ DPI như M1; chỉ lưu vị trí khi bóng nằm yên sau khi nhả.
- Test lại sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, wake cooldown, và chuyến đi tới hộp/đệm khi có cả hai.

### Cách code M3 (11/11)

Commit/publish 12d60f3, smoke 3/3 PASS.

- Trạng thái chuyến ở `PetWindow.ToyFetch.cs`: `Watch` (nhìn bóng bay) → `Run` (tới bóng) → `Carry` (ngậm, về dưới con trỏ) → `Wait` (nhả, ngồi chờ 20 giây). Rủ chơi bắt đầu thẳng ở `Run`, cờ `fetchInvite`, đi bộ; ném nhặt chạy nhanh gấp 1,6. Luật thuần (ngưỡng 400 DIP/s, 5 lần rồi nghỉ 120 giây, hẹn 480–900 giây) ở `FetchStreak` (`PetDesktopToys.cs`).
- Cổng: `AutoKind.ToyFetch` chặn như trò bóng (bận, ngủ, bị kéo, đang bay, app toàn màn hình, trận, việc khác) nhưng không chặn Quiet và wake cooldown; ném lại khi mèo đang chờ thì bỏ điều kiện "việc khác" vì việc đó là chính chuyến này. `AutoKind.ToyInvite` chặn như ngủ trong hộp, thêm người dùng vắng. Lúc có chuyến, `FeatureOwnsMovement` đúng nên cảm xúc tự động, ngủ đệm/hộp, leo cửa sổ đều chờ.
- Bắt đầu từ `EndToyDrag` (`BallThrown`); bóng tới trong 10 DIP của miệng và thấp dưới 24 DIP thì ngậm. Lúc ngậm, món bóng có cờ `Carried`: cửa sổ ẩn, physics và lưu bỏ qua; `PetRenderer.CarryBall` vẽ bóng bán kính 8,5 ở miệng trong `DrawHead`, theo mọi transform của đầu và hướng mặt. Nhấc mèo thì không vẽ.
- Nhả (`DropCarriedBall`): bóng nhận monitor và DIP của mèo như sau trò chơi bóng, đặt tại miệng (trước tâm mèo 4 DIP, trên sàn 44 DIP), rơi theo physics, chỉ lưu khi nằm yên. Cửa sổ hiện ngay trong khung đó.
- Mọi lối hủy đều qua `StopToyFetch`: `CancelFeatureActivity` (click trái/phải mèo, kéo, đổi việc, !CanPlay) thì nhả; `GuardToyFetch` mỗi khung trong `TickDesktopToys` nhả khi app toàn màn hình, vào trận, bóng sang màn khác, trò chơi bóng; Cất nhanh và cất bóng thì bóng về giỏ. Rủ chơi gặp Quiet/Giờ tập trung thì dừng và nhả. Không có âm thanh mới (`ball_tap`) và không có sự kiện IPC.
- Đã soát ảnh render bóng ở miệng khi đi trái/phải, ngồi, nhìn, đứng và lúc nhả (PET_RENDER_REVIEW). Chưa xem trên desktop thật, chưa thử DPI khác, nhiều màn hình; chưa đo FPS riêng (M3 không thêm cửa sổ).

## Luật M4 Cần câu lông vũ và Chuột dây cót (duyệt 11/11)

### Chung cho hai món

- Lấy từ Giỏ đồ chơi, miễn phí, mỗi món chỉ một cái, tính vào giới hạn 3 món. Giỏ có 4 món: Bóng len, Thùng carton, Cần câu lông vũ, Chuột dây cót.
- Kéo quá khoảng 6 DIP là cầm; thả có vận tốc và trọng lực. Chỉ nằm trên sàn, tự tránh vùng đất như hộp.
- Mèo chỉ đuổi khi người dùng đang chơi: đang cầm cần câu, hoặc chuột đang chạy vì người dùng lên dây. Do người dùng bật nên chạy cả trong Quiet và wake cooldown, như bóng/laser.
- Mèo chỉ tham gia khi thức, rảnh (Idle, đang đi, hoặc đang ngồi chờ ở bước cuối của ném nhặt), đứng trên sàn, cùng màn hình với món đồ. Mèo đang ngủ thì ngủ tiếp. Mèo bận, bị kéo, đang bay, đang phản ứng, đang trong trò bóng hoặc laser, hoặc đang có chuyến M2/M3 thì không tham gia.
- Mèo vồ bằng cú nhảy thường. Đồ vẫn không chạm mèo: không Bonk, Dizzy hay cảm xúc. "Bắt được" chỉ là hành động của mèo.
- Trong lúc đuổi, cảm xúc tự động chờ. Bấm trái hoặc kéo mèo: dừng đuổi, kéo như thường. Bấm phải: dừng và mở menu. Pause đóng băng tất cả.
- App toàn màn hình, vào trận, rút màn hình: dừng đuổi, đồ ẩn theo luật M1. Cất nhanh: dừng đuổi, đồ về giỏ.
- Mèo không tha hai món này và không tự lên dây chuột.
- Không có âm thanh mới: dùng lại cue sẵn có, có cooldown. Không có sự kiện IPC mới; mỗi lượt chơi gửi `toy_started` sẵn có một lần như laser, để Python tính là đã chơi.
- Đuổi cần câu liên tục tối đa khoảng 90 giây, hoặc đuổi chuột 5 lượt liên tiếp, rồi mèo nghỉ 2 phút: chỉ nhìn theo, không chạy.

### Cần câu lông vũ

- Nằm yên: cán dài khoảng 90 DIP trên sàn, lông vũ ở đầu dây; không lăn, không xoay.
- Người dùng cầm cán: lông vũ treo ở dây khoảng 90 DIP, đung đưa như con lắc (physics ở C#). Mèo nhìn theo, rình, chạy theo trên sàn; lông xuống trong tầm khoảng 120 DIP trên đầu mèo (như tầm vồ laser) thì mèo nhảy vồ.
- Vồ trúng: mèo giữ lông khoảng 0,8 giây rồi buông; mèo không giật cần khỏi tay người dùng.
- Người dùng giữ yên quá 8 giây thì mèo chán, ngồi nhìn; lông động lại thì đuổi tiếp.
- Thả cần thì mèo thôi đuổi. Cần rơi, trượt ngắn, không nảy cao.

### Chuột dây cót

- Bấm không kéo là lên dây: chuột chạy dọc sàn khoảng 5 giây, khoảng 180 DIP/s, gặp mép màn hình hoặc vùng đất thì quay đầu, rồi chậm dần và dừng. Bấm khi chuột đang chạy thì không có tác dụng.
- Ném: chuột rơi; chạm sàn mạnh thì lật ngửa, bánh quay khoảng 1 giây rồi tự lật lại.
- Chuột đang chạy thì mèo đuổi; tới gần thì vồ. Trúng: chuột dừng, mèo đè chuột khoảng 1 giây rồi ngồi. Trượt (chuột quay đầu): mèo đuổi tiếp tới khi chuột dừng. Mỗi lần lên dây là một lượt.
- Đi quá 20 giây chưa tới thì thôi.

### Khi code cần kiểm tra

- Cần câu là món đầu tiên mèo phản ứng trong lúc người dùng đang cầm đồ: giữ nhánh kéo đồ tách hẳn khỏi nhánh kéo mèo.
- Cửa sổ chỉ di chuyển từng khung khi đồ đang được cầm hoặc đang chạy. Đo lại FPS với đủ 3 món nằm yên và khi chuột đang chạy.
- Test lại sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, wake cooldown, chuyến tới hộp/đệm và ném nhặt.

### Cách code M4 (11/11)

Commit/publish ba2afa7, smoke 3/3 PASS.

- Luật thuần ở `PetToyChase.cs` (Core), có unit test: `ToyChaseStreak` (90 giây cần câu hoặc 5 lượt chuột rồi nghỉ 120 giây, dùng chung cho hai món; 20 giây không chơi thì đếm lại), `FeatherString` (lông vũ là một điểm trên dây 90 DIP, tính kiểu Verlet 120 bước/giây, trễ sau đầu cần, không xa quá dây, quét trên sàn; `Pin` giữ lông dưới chân mèo tới khi dây căng quá 12 DIP), `WindUpMouse` (lên dây 5 giây, 180 DIP/s, giây cuối chậm dần, quay đầu ở mép màn hình và vùng đất, chạm sàn nhanh hơn 380 DIP/s thì lật 1 giây). Món `wand` và `mouse` trong `DesktopToyRules`.
- Hai món chung khung cầm, ném, lưu, tránh vùng đất của M1 (`PetWindow.Toys.cs`). Cần câu rơi bằng `StepBox` như hộp. Cửa sổ cần câu 300×240 DIP, tâm cán luôn ở giữa để có chỗ cho dây khi cầm; chỉ phần vẽ nhận click (`ToyWandArt`). Chuột 56×36 (`ToyMouseArt`), bấm không kéo gọi `WindUpMouse.Wind`. Cửa sổ chỉ di chuyển từng khung khi đồ đang được cầm, đang bay, đang chạy hoặc đang lật.
- Mèo đuổi ở `PetWindow.ToyChase.cs`: `Look` (nghỉ, chỉ nhìn) / `Wand` (nhịp của laser: nhìn, chạy, rình, vồ) / `Run` (chạy nhanh gấp 2,2 theo chuột, vồ khi cách 25–85 DIP) → `Pounce` (cú nhảy thường) → `Hold` (0,8 giây giữ lông cách tâm mèo 42 DIP, hoặc 1 giây đè chuột). Tick ngay sau laser trong `TickFeatures` vì lúc nhảy `CanPlay` sai; ngoài lúc nhảy, `!CanPlay` thì thôi đuổi.
- Cổng `AutoKind.ToyChase` chặn như `ToyFetch` (bận, ngủ, bị kéo, app toàn màn hình, trận, việc khác) nhưng không chặn Quiet và wake cooldown; mèo đang ngồi chờ ném nhặt thì bỏ điều kiện "việc khác" và thôi chờ. Mỗi lần lên dây chỉ đuổi một lần (`Winds`), nên click mèo giữa chừng không làm mèo đuổi lại chính lượt đó. Lúc đuổi `FeatureOwnsMovement` đúng. `CancelFeatureActivity`, Cất nhanh, cất món đó, `GuardToyChase` (app toàn màn hình, trận, đồ ẩn, đồ sang màn khác) đều qua `StopToyChase`.
- Mỗi lượt gửi `toy_started` sẵn có một lần. Âm thanh dùng lại `ball_tap` (lên dây, vồ trúng) và `swoosh` (cầm đồ).
- Hàng giỏ không mờ khi mèo đang đuổi; cất món đang bị đuổi thì mèo thôi đuổi.
- Đã soát ảnh render (PET_RENDER_REVIEW) cần câu nằm, đang cầm, đang vẩy, lúc mèo giữ lông, chuột đứng và đang chạy. Chưa xem trên desktop thật, chưa kéo chuột thật, chưa thử DPI khác, nhiều màn hình.
- Đo 11/11 (bản ba2afa7, cùng cách đo M1, thêm tắt AI, 2 vòng): lúc đo máy chỉ chạy khoảng 31 fps cho cả bản M3 12d60f3 không giỏ, nên chỉ so tương đối. FPS trung vị vòng 1, vòng 2: không giỏ 26,6 và 26,1; giỏ, bóng, cần câu, chuột nằm yên 26,9 và 26,9. Ba món nằm yên không làm chậm thêm. Chưa đo được lúc chuột đang chạy hay đang cầm cần câu: click gửi bằng PostMessage không lên dây được chuột. Đo lại khi máy về ~60 fps.

## Luật M5 Chuyền bóng và phản ứng (duyệt 11/11)

### Chuyền bóng (người dùng bắt đầu)

- Chỉ Bóng len đang nằm ngoài. Người dùng bấm bóng (lăn nhẹ ra xa chỗ bấm) hoặc ném nhẹ dưới khoảng 400 DIP/s cho bóng lăn về phía mèo. Ném mạnh hơn vẫn là ném nhặt M3.
- Mèo tham gia như M4: thức, rảnh (Idle, đang đi, hoặc đang ngồi chờ ở bước cuối của ném nhặt), đứng trên sàn, cùng màn hình với bóng; bóng đang lăn về phía mèo, cách trong khoảng 400 DIP. Mèo bận, bị kéo, đang bay, đang phản ứng, trong trò bóng/laser hoặc đang có chuyến M2/M3/M4 khác thì không tham gia.
- Mèo quay về phía bóng, hạ người chờ, có thể bước tới tối đa khoảng 150 DIP. Bóng tới chân thì mèo vỗ trả về phía con trỏ, lực tính để bóng dừng gần chỗ con trỏ trên sàn. Con trỏ ở ngay trên mèo (trong khoảng 60 DIP theo chiều ngang) thì trả về hướng bóng vừa tới. Bóng dừng ngắn trước mặt mèo (dưới 120 DIP) thì mèo đi tới vỗ.
- Mỗi lần mèo vỗ trả sau khi người dùng vừa chạm bóng là một lượt. Từ lượt 2, một nhãn số nhỏ hiện cạnh bóng khoảng 1,5 giây. Kỷ lục lưu trong `preferences.json` và chỉ hiện ở hàng Bóng len trong giỏ ("Kỷ lục chuyền: 12"); không vào Nhật ký.
- Chuỗi kết thúc khi bóng nằm yên quá 6 giây mà người dùng không chạm, bóng lăn qua mèo, hoặc người dùng cầm bóng. Kỷ lục mới từ 5 lượt trở lên thì nhãn "Kỷ lục mới!".
- Chuyền liên tục tối đa khoảng 90 giây hoặc 20 lượt, rồi mèo nghỉ 2 phút: chỉ nhìn theo. Thời gian nghỉ dùng chung với M4: mèo mệt vì chơi thì nghỉ, dù vừa chơi món nào.
- Do người dùng bật nên chạy cả trong Quiet và wake cooldown. Mèo ngủ thì ngủ tiếp. Bấm trái hoặc kéo mèo: dừng, kéo như thường; bấm phải: dừng và mở menu. Cảm xúc tự động chờ. Pause đóng băng. App toàn màn hình, vào trận, rút màn hình, Cất nhanh hoặc cất bóng: dừng.
- Trò "Chơi bóng" 45 giây giữ nguyên; trong lúc trò chạy không có chuyền.
- Không có âm thanh mới (dùng `ball_tap`, có cooldown). Không có sự kiện IPC mới; mỗi chuỗi gửi `toy_started` sẵn có một lần.

### Phản ứng

Chung: chỉ là hành động ngắn (tối đa khoảng 2 giây), dùng lại tư thế sẵn có (Looking, Sitting, Grooming), không phải cảm xúc: không vào hàng cảm xúc, không đổi mood, không có IPC mới. Không bao giờ khi mèo ngủ, waking, bị kéo hoặc đang bay. Bấm hoặc kéo mèo hủy ngay; Pause đóng băng. Cảm xúc tự động chờ trong lúc phản ứng; cảm xúc ưu tiên cao thì phản ứng nhường.

1. **Hụt:** vồ hụt lông vũ hoặc chuột, hoặc để bóng chuyền lăn qua, mèo ngơ ngác khoảng 1 giây (nghiêng đầu, nhìn quanh) rồi đuổi tiếp nếu món đó còn đang chơi. Cách nhau ít nhất 10 giây; trong khoảng đó mèo đuổi tiếp luôn.
2. **Tò mò với đồ mới:** lần đầu mỗi món nằm ngoài desktop, mèo tự đi tới chậm, cúi ngửi khoảng 1,5 giây, chạm thử một chân rồi thôi. Mỗi món một lần, lưu lại. Mèo tự làm nên bị chặn như rủ chơi M3: mèo bận, Quiet, Giờ tập trung, trong trận, đang có việc khác, wake cooldown, người dùng vắng; phải đứng trên sàn, cùng màn hình; đi quá 20 giây chưa tới thì thôi (lần sau thử lại). Nếu lần đầu chính là người dùng rủ chơi món đó thì mèo ngửi nhanh 0,5 giây rồi chơi.
3. **Phụ kiện mới:** Lưu cài đặt với phụ kiện khác cái cũ thì mèo rửa mặt, lắc đầu, chỉnh mũ khoảng 2 giây. Người dùng đổi nên chạy cả trong Quiet. Mèo đang ngủ thì không phản ứng, cả sau khi thức; mèo bận hoặc trong trận thì bỏ qua.

### Chưa làm ở M5

- Mèo tha chuột sau khi bắt, ăn mừng bằng cảm xúc Happy, chuyền bằng món khác.

### Khi code cần kiểm tra

- Phân biệt bấm bóng để chuyền với bấm bóng trong trò 45 giây và với ném nhặt M3 (ngưỡng 400 DIP/s).
- Ngửi đồ mới là hành vi tự động mới: giữ đúng cổng như rủ chơi, không đánh thức mèo ngủ.
- Test lại sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, wake cooldown, ném nhặt M3, đuổi M4 và chuyến tới hộp/đệm.

### Cách code M5 (11/11)

Commit/publish 361d893, smoke 3/3 PASS.

- Luật thuần ở `PetToyPass.cs` (Core), có unit test: `ToyPassRules` (tầm 400 DIP, bước tới 150, bóng dừng ngắn 120, 6 giây, nhãn 1,5 giây; lực vỗ = 1,25 × khoảng tới con trỏ, trong 120–300 DIP/s, đúng với ma sát sàn của `ToyBall.Step`; con trỏ trong 60 DIP trên đầu mèo thì trả về hướng bóng tới), `ToyPassCount` (lượt chỉ tính khi người dùng vừa chạm; kỷ lục nâng ngay mỗi lượt nên thoát app không mất; báo "Kỷ lục mới!" khi từ 5 lượt), `ToyReactRules`. `ToyChaseStreak` thêm `Pass`/`Passed` (90 giây hoặc 20 lượt) dùng chung thời gian nghỉ với M4. `preferences.json` thêm `ToyPassBest`, `ToysSniffed`.
- Chuyền ở `PetWindow.ToyPass.cs`: `Ready` (hạ người, bước tới tối đa 150 DIP gặp bóng sẽ dừng ngắn, đi tới bóng đã dừng trong 120 DIP, vỗ khi bóng trong 49 DIP và thấp dưới 24 DIP) → `Wait` (ngồi nhìn; người dùng chạm và bóng lăn tới thì `Ready` lại; bóng nằm yên 6 giây thì hết) / `Look` (đang nghỉ, chỉ nhìn). Bấm bóng (`Nudge`, lăn như cũ) và thả bóng chậm hơn ngưỡng ném nhặt đều là "người dùng chạm" (`PassTouched` trong `EndToyDrag`). Cổng `AutoKind.ToyPass` như `ToyChase`; đang ngồi chờ ném nhặt thì chuyền giành quyền. Nhãn đếm là cửa sổ phủ click xuyên qua kiểu nhãn Giờ tập trung, chỉ tạo khi cần, ẩn theo đồ.
- Khác bản luật: người dùng cầm bóng giữa chuỗi thì chuỗi chờ (mèo ngồi nhìn), thả nhẹ về phía mèo là chuyền tiếp; ném mạnh (ném nhặt) mới kết thúc chuỗi. Lý do: ném nhẹ là một cách chuyền đã duyệt, nếu cầm là hết chuỗi thì không chuyền bằng tay được. "Kỷ lục mới!" chỉ hiện khi chuỗi tự hết (6 giây hoặc bóng lăn qua), không hiện khi bị click mèo, Cất nhanh.
- Phản ứng ở `PetWindow.ToyReact.cs`, tick trước đuổi M4 và chuyền nên lúc ngơ ngác hai trò đó chờ. Chỉ dùng tư thế sẵn có (Looking, Sitting, Grooming) cùng hai số chỉ phần hình `PetModel.ReactTilt` (nghiêng đầu, độ) và `ReactDip` (hạ đầu, DIP) cộng vào phép đặt đầu trong `PetRenderer`; không vào hàng cảm xúc, không IPC. `!CanPlay` (ngủ, waking, bị kéo, bay, cảm xúc giữ mèo) kết thúc ngay. Hụt: vồ hụt M4 và bóng lăn qua, cách nhau 10 giây. Ngửi: mỗi 2 giây xem có món chưa ngửi, cổng `AutoKind.ToySniff` bằng đúng `ToyInvite`, đi chậm (0,6 tốc độ đi), tới nơi ghi `ToysSniffed` rồi cúi ngửi 1,5 giây và vỗ chân; lần đầu người dùng rủ chơi món mới thì ngửi nhanh 0,5 giây rồi vào trò (`SniffFirst`). Phụ kiện: Lưu trong Tùy chỉnh với phụ kiện khác cái cũ (không tính "Không"), cổng `AutoKind.ToyReact` (không chặn Quiet).
- Không âm thanh mới (`ball_tap`), không sự kiện IPC mới (`toy_started` mỗi chuỗi).
- Đã soát ảnh render (PET_RENDER_REVIEW): mèo chờ bóng, ngơ ngác khi bóng lăn qua, cúi ngửi chuột, rửa mặt sau khi đổi phụ kiện. Chưa xem trên desktop thật, chưa bấm chuột thật, chưa thử DPI khác, nhiều màn hình. UiChecks gọi thẳng `ReactToAccessory`, chưa bấm Lưu thật trong Tùy chỉnh. Lực vỗ, tầm, thời gian là ước tính.
