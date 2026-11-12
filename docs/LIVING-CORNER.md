# Góc sống và quan hệ (bản 1.6, Đợt 2)

Người dùng duyệt chia mốc 2026-11-11. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 1 và 2.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Trụ cào | Món mới trong giỏ; mèo tới cào rồi vươn vai; lệnh Cào móng có trụ gần thì đi tới trụ | Đã có code, còn test tay |
| M2. Chậu cây, hộp thêm trạng thái | Chậu cây (ngửi, chạm lá, ngồi cạnh, lớn theo số lần chơi, không héo); hộp thò đầu và vồ đồ đi ngang | Đã có code, còn test tay |
| M3. Mốc thân thiết | Bond thành 5 cấp có tên trong Nhật ký; mỗi cấp mở một hành vi (chào theo cấp, ngủ tựa vào đồ, chuỗi sau khi chơi) | Đã có code, còn test tay |
| M4. Món yêu thích, lựa chọn | Python đếm lần chơi từng món; mèo quay lại món thích, rủ chơi bằng món đó; hai món cạnh nhau thì chọn một | Đã có code, còn test tay |
| M5. Gọi mèo, dạy động tác | Lệnh "Gọi mèo"; dạy Xoay vòng, Ngồi xin, Đập tay qua vuốt ve rồi gọi từ "Làm ngay" | Luật nháp 12/11, chờ duyệt |

Quyết định chung (người dùng đồng ý 11/11):

- Đồ góc sống (trụ cào, chậu cây) có giới hạn riêng **2 món** trên desktop; đồ chơi (bóng, hộp, cần câu, chuột) vẫn tối đa 3 món. Đo FPS khi có đủ 5 món.
- Cấp thân thiết và món yêu thích do Python giữ (memory). Python tắt thì không có các hành vi mở khóa của M3, M4; mèo chạy hành vi local như hiện nay.
- Để sang đợt khác: lưu bố trí, chế độ xem góc sống, bộ đồ theo chủ đề (Đợt 6); bảng ảnh, ký ức từ trận thắng (Đợt 4). Để sau: dáng đi theo tâm trạng, nghi thức riêng, mèo tự sắp xếp đồ.

## Luật M1 Trụ cào (duyệt 11/11)

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

### Bổ sung ngoại lệ (đã duyệt 11/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Đồ vật có physics)

Trụ cào là đồ góc sống có physics như thùng carton (giới hạn riêng 2 món đồ góc sống, đồ chơi vẫn 3). Mèo tự đi tới cào mỗi 6–10 phút, chặn như mèo tự vào hộp; lệnh Cào móng sẵn có của Python/local và "Làm ngay" đi tới trụ khi trụ đủ gần, nếu không thì cào tại chỗ như cũ. Cào 2,4 s rồi vươn vai, chung cooldown tự chăm sóc; click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng, không bao giờ khi ngủ/waking. Trụ vẫn không chạm mèo: không Bonk, Dizzy, cảm xúc, không đánh thức. Python không có lệnh mới.

### Cách code M1 (11/11)

- Trụ là món `post` trong giỏ (`DesktopToyRules.Post`, `IsLiving`, `MaxLivingOut` = 2, `CanTake(id, …)` đếm riêng từng nhóm; `Normalize` giữ tối đa 3 đồ chơi và 2 đồ góc sống). Dùng chung khung cầm, ném, lưu, tránh vùng đất của Đợt 1 M1 (`PetWindow.Toys.cs`); physics `ToyBall.StepBox` như hộp. Giỏ có một đường kẻ trước nhóm góc sống; icon menu là glyph Library của Segoe.
- Hình `ToyPostArt` (84×120 DIP): đế gỗ, cột quấn dây thừng, đĩa trên cùng, quả bông treo. Bấm không kéo gọi `Wobble`, quả bông đung đưa theo `Feedback`; lúc mèo cào, dây thừng rung và vài sợi xơ rơi phía mèo đứng.
- Chuyến đi ở `PetWindow.ToyPost.cs`: `PostTrip` Walk → Scratch → Stretch. Walk là tính năng sở hữu chuyển động (như đi tới hộp, nằm trong `FeatureOwnsMovement`); Scratch và Stretch là tư thế "Làm ngay" của `PetBehavior.StartManual`, nên click, kéo, cảm xúc ngắt như mọi cử chỉ. Tick nằm trước hẹn giờ đệm/hộp để không có giấc ngủ chen giữa cào và vươn vai.
- Lệnh Cào móng: `PetBehavior.ScratchElsewhere` (giống `SleepElsewhere`) được gọi khi hành vi local chọn Cào móng hoặc Python gửi `SCRATCH`; `TryStartCommand` gọi `ScratchPostNow` cho "Làm ngay". Giao thức không đổi.
- Tư thế Cào móng vẽ chân trước ở phía đối diện đuôi, nên mèo quay phía đó vào trụ; tâm mèo cách tâm trụ 34 DIP (`PostScratchRules.StandOff`), chân rơi lên mép dây thừng gần mèo.
- Đã soát ảnh render ghép cửa sổ mèo lên cửa sổ trụ lúc cào (nền xanh xám). Chưa xem trên desktop thật, chưa xem nền sáng/tối riêng, chưa kéo/ném chuột thật, chưa thử DPI khác, nhiều màn hình, chưa đo FPS; câu tiếng Nhật chưa ai duyệt.

## Luật M2 Chậu cây và hộp thêm trạng thái (duyệt 11/11)

### Chậu cây là đồ góc sống

- Lấy từ Giỏ, miễn phí, nhóm "Góc sống" cùng trụ cào (tối đa 2 món: có cả trụ và chậu là đủ).
- Chậu đất nung rộng khoảng 50 DIP, cây lá tròn, vẽ vector theo docs/DESIGN-SYSTEM.md, nhìn rõ trên nền sáng/tối.
- Physics như hộp và trụ: kéo quá khoảng 6 DIP là cầm; thả rơi, nảy thấp, không lăn, không xoay. Chậu không vỡ, đất không đổ. Bấm không kéo thì lá rung nhẹ. Lưu, Cất nhanh, Pause, toàn màn hình, trận, rút màn hình, DPI: như mọi món.
- Cây không thuộc nông trại: không tưới, không thu hoạch, không xu, không ảnh hưởng thời tiết.

### Cây lớn dần, không bao giờ héo

- Mỗi lần mèo ghé cây xong trọn vẹn được 1 điểm, tối đa 1 điểm mỗi 10 phút. 4 dáng: mầm (0), cây nhỏ (5), cây sum suê (15), ra hoa (30). Lên dáng mới có hiệu ứng lá lấp lánh ngắn, không âm thanh mới.
- Không bao giờ héo hay nhỏ lại: tắt app, vắng máy, cất vào giỏ đều giữ nguyên dáng. Lưu trong `preferences.json` như vị trí đồ (là trạng thái món đồ, C# giữ), không đụng `ai-memory.json`.
- Giỏ ghi dáng hiện tại cạnh tên, ví dụ "Chậu cây (cây nhỏ)".

### Mèo ghé cây

- Mèo tự đi tới mỗi 8–12 phút, chặn như trụ cào (bận, Quiet, Giờ tập trung, trận, việc khác, wake cooldown, ngủ/waking) và chờ cooldown tự chăm sóc 45 giây. Đi quá 15 giây chưa tới thì thôi.
- Tới nơi, mèo làm một trong ba việc, không lặp lại việc lần trước: ngửi lá (cúi mũi khoảng 1,5 giây, như ngửi đồ mới M5); chạm lá (vỗ một chân, lá rung); ngồi cạnh nhìn cây khoảng 4 giây.
- Không có "Làm ngay" mới, không có lệnh Python mới; Python không tham gia. Ghé cây không đổi needs, bond, learning.
- Ngắt như trụ cào: click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng, nhấc chậu hoặc Cất nhanh thì chuyến hủy và không được điểm.

### Hộp: mèo trốn trong hộp, thò đầu

- Thêm một việc với thùng carton: mèo tự chui vào hộp ngồi thức, chỉ thò đầu lên nhìn quanh khoảng 8–15 giây, rồi nhảy nhẹ ra. Mỗi 8–12 phút, chặn như mèo tự vào hộp ngủ. Không có "Làm ngay" mới.
- Dùng lại cú nhảy vào/ra và mặt trước hộp vẽ trong cửa sổ mèo của M2 Đợt 1, nên mọi click vẫn rơi vào mèo: click trái nhấc thẳng mèo ra và kéo được, click phải mèo ra và mở menu. Cảm xúc ưu tiên cao: mèo nhảy ra.
- Đang thò đầu mà tới lúc mèo tự ngủ (ba dịp của đệm/hộp) thì ngủ luôn trong hộp theo giấc ngủ thường của M2, không cần nhảy ra.
- Người dùng không cầm được hộp khi có mèo bên trong (như M2); Cất nhanh, toàn màn hình, trận: hộp biến mất, mèo đứng tại chỗ.

### Hộp: vồ đồ đi ngang

- Đang thò đầu mà người dùng lên dây chuột chạy ngang trong khoảng 200 DIP quanh hộp, hoặc vẩy cần câu để lông vũ xuống trong khoảng đó: mèo rình 0,5 giây rồi vồ ra khỏi hộp bằng cú nhảy thường, sau đó đuổi theo đúng luật M4 (tính chung lượt và thời gian nghỉ 2 phút của M4).
- Đang trong 2 phút nghỉ của M4 thì mèo chỉ quay đầu nhìn theo, không vồ.
- Bóng len lăn ngang không làm mèo vồ ra (chuyền bóng M5 giữ nguyên luật cũ: mèo ở trong hộp thì không tham gia).
- Trốn tìm để sang Đợt 3 Mini-game.

### Khi code cần kiểm tra

- Trạng thái "trong hộp, thức" là mới: soát sleep lock (không coi là ngủ), wake cooldown, drag/throw từ trong hộp, emotion priority, chuyến M3/M4/M5 khi mèo đang trong hộp, local fallback, Pause.
- Giới hạn nhóm góc sống khi có cả trụ và chậu; `preferences.json` cũ (chưa có chậu) vẫn đọc được.
- Đo FPS với 3 đồ chơi, trụ và chậu nằm yên; soát render chậu 4 dáng, mèo ghé cây, mèo thò đầu trong hộp trên nền sáng/tối.

### Bổ sung ngoại lệ (đã duyệt 11/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Đồ vật có physics)

Chậu cây là đồ góc sống có physics như trụ cào (chung giới hạn 2 món góc sống). Mèo tự ghé cây mỗi 8–12 phút, chặn như trụ cào và chờ cooldown tự chăm sóc; ngửi, chạm lá hoặc ngồi cạnh; mỗi lần ghé trọn vẹn được 1 điểm (tối đa 1 điểm/10 phút), cây lớn qua 4 dáng và không bao giờ héo; lưu ở `preferences.json`, Python không tham gia. Thùng carton thêm việc mèo tự vào ngồi thức, thò đầu 8–15 giây (mỗi 8–12 phút, chặn như vào hộp ngủ); click trái nhấc thẳng ra và kéo được, click phải ra và mở menu, cảm xúc ưu tiên cao thì nhảy ra; đang thò đầu mà tới dịp ngủ thì ngủ trong hộp theo luật ngủ thường. Đang thò đầu, chuột dây cót hoặc lông vũ người dùng đang chơi đi ngang trong ~200 DIP thì mèo vồ ra rồi đuổi theo luật M4 (chung lượt và nghỉ). Đồ vẫn không chạm mèo: không Bonk, Dizzy, cảm xúc, không đánh thức; không âm thanh mới.

### Cách code M2 (11/11)

- Chậu là món `plant` trong nhóm góc sống (`DesktopToyRules.Plant`, rộng 50 DIP, thân chậu cao 36 DIP; lá vẽ phía trên). Chung khung cầm, ném, lưu, tránh vùng đất; physics `StepBox` như hộp. Icon menu là glyph chiếc lá E8BE của Segoe.
- Hình `ToyPlantArt` (84×112 DIP): 4 dáng theo `PlantRules.StageOf` (0/5/15/30 điểm), lá rung theo `Feedback` (bấm, mèo vỗ), lấp lánh 1,2 giây khi lên dáng (đồng hồ chạy theo khung hình, Pause đóng băng).
- Chuyến ghé cây ở `PetWindow.ToyPlant.cs`: `PlantTrip` Walk → Visit, cả chuyến sở hữu chuyển động (`FeatureOwnsMovement`), nên click, kéo, cảm xúc mạnh hủy qua `CancelFeatureActivity`. Mèo đứng cách tâm chậu nửa chậu + 30 DIP, quay về phía chậu (cùng hướng như ngửi đồ mới M5). Ghé xong trọn vẹn mới gọi `PlantRules.Earn`; điểm và giờ điểm cuối lưu `PlantPoints`, `PlantPointAt` trong `preferences.json` (giờ UTC; đồng hồ bị lùi không chặn điểm).
- Thò đầu ở `PetWindow.ToyBox.cs`: `BoxPeek` Walk → In → Stalk. Dùng chung cú nhảy vào/ra, độ nâng và mặt trước hộp của giấc ngủ trong hộp (`BoxOccupied`, `BoxFrontShown` tính cả lúc thò đầu). Tới dịp ngủ (đồng hồ đệm/hộp hoặc gần buồn ngủ vì không ai chạm) thì chuyển thẳng sang giấc ngủ thường trong hộp. Bị ngắt thì nhảy nhẹ ra, trừ khi đang bị kéo hoặc hộp đã mất.
- Vồ ra: `PeekLure` lấy món người dùng đang chơi (`ChaseLure` của M4) trong 200 DIP quanh hộp và đủ thấp để vồ; rình 0,5 giây rồi `PounceFromBox` (ToyChase.cs) mở lượt đuổi M4 bằng cú vồ, tính chung lượt chuột và thời gian nghỉ.
- Sửa kèm (lỗi có sẵn từ M4, test M2 tìm ra): cất chuột rồi lấy ra lại thì lần lên dây đầu có thể bị bỏ qua vì chuột mới đếm số lần lên dây từ 0 mà mèo còn nhớ số của chuột cũ; `NewToy` giờ xóa số nhớ đó.
- Đã soát ảnh render: chậu 4 dáng, mèo ghé cây, mèo thò đầu trong hộp (nền xanh xám). Chưa xem trên desktop thật, chưa xem nền sáng/tối riêng, chưa kéo/ném chuột thật, chưa thử DPI khác, nhiều màn hình, chưa đo FPS; câu tiếng Nhật chưa ai duyệt.

## Luật M3 Mốc thân thiết (duyệt 11/11)

### Năm mốc, chỉ đi lên

- Bond 0–100 hiện có (Python giữ, mỗi ngày tăng tối đa 3, giảm tối đa 2, không giảm khi vắng mặt) chia 5 mốc: **Mới quen** (0), **Quen mặt** (20), **Thân** (40), **Rất thân** (60), **Tri kỷ** (80). Bond mới bắt đầu ở 30, tức Quen mặt; lên Tri kỷ cần ít nhất khoảng 17 ngày chơi.
- Mốc là **mốc cao nhất từng đạt**, không bao giờ tụt: bond có giảm (ném mạnh) thì thanh bond trong Nhật ký giảm, còn mốc và các hành vi đã mở vẫn giữ. Không có áp lực đăng nhập, không phạt khi vắng.
- Python tính và lưu mốc cao nhất trong `ai-memory.json`; file cũ được tính mốc từ bond hiện tại. "Quên điều mèo đã học" không xóa mốc (giống bond hiện nay).

### Mỗi mốc mở một hành vi

| Mốc | Hành vi mở thêm |
|---|---|
| Mới quen | Hành vi hiện có, không đổi |
| Quen mặt | **Ngó chào**: người dùng quay lại sau ≥10 phút vắng máy, mèo quay đầu về phía con trỏ, vẫy đuôi khoảng 1,5 giây |
| Thân | **Chuỗi sau khi chơi**: một trò người dùng bật kết thúc tự nhiên (hết 45 giây bóng, hết chuỗi ném nhặt/chuyền, mèo nghỉ sau cần câu/chuột), mèo liếm chân (tư thế chải lông) → vươn vai → đi về chỗ nghỉ đã nhớ (nếu cùng màn hình) hoặc ngồi tại chỗ khoảng 5 giây |
| Rất thân | **Ra đón**: thay Ngó chào, mèo đi về phía con trỏ trên sàn (tối đa khoảng 8 giây) rồi vẫy chân (tư thế Vẫy chân sẵn có) |
| Tri kỷ | **Ngủ tựa vào đồ**: khi mèo tự chọn ngủ mà có đồ chơi, đồ góc sống hoặc kỷ vật cùng màn hình trong khoảng 400 DIP, mèo đi tới nằm sát cạnh món đó rồi ngủ (giấc ngủ thường; đệm và hộp vẫn theo luật cũ) |

- Các hành vi cũ giữ nguyên ngưỡng: câu "welcome_back" (bond ≥ 50 sau ≥ 1 giờ), lăn ngửa sau khi vuốt ve (bond ≥ 60, affection ≥ 60).
- Python quyết định mốc; C# chỉ thực hiện hành vi theo mốc và tự lo thời điểm, đường đi, physics. **Python tắt hoặc lỗi: C# coi như chưa mở hành vi nào** (mốc biết được trong phiên bị xóa khi AI tắt), mèo chạy hành vi local như hiện nay.

### Khi nào hành vi mở khóa được chạy

- Ngó chào và Ra đón: mỗi lần quay lại tối đa một lần, cách nhau ít nhất 30 phút. Mèo đang ngủ (kể cả away nap) thì không bị đánh thức thêm: chờ mèo thức (away nap tự thức khi người dùng quay lại, như đã duyệt), tối đa 30 giây rồi bỏ.
- Cả bốn hành vi là việc mèo tự làm: chặn khi Quiet, Giờ tập trung, app toàn màn hình, trận và màn hình trận, Pause, mèo bận/bị kéo/đang bay, cảm xúc ưu tiên cao, đang có việc khác (trò, chuyến tới đồ). Chuỗi sau khi chơi tối đa một lần mỗi 5 phút và chung cooldown tự chăm sóc 45 giây.
- Ngắt như các chuyến tới đồ: click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng.
- Không âm thanh mới; câu nói dùng hệ thống câu nói hiện có (có cooldown, im trong Quiet).
- Không phải tương tác của người dùng: needs, bond, learning không đổi vì các hành vi này.

### Lên mốc mới

- Lần đầu Python thấy mốc cao hơn, quyết định gửi kèm báo lên mốc một lần. C# hiện vài trái tim nhỏ bay lên trên đầu mèo khoảng 1,5 giây và một câu mới (tình huống `bond_up` trong `ai/lines.json`, vi/en; tiếng Nhật theo luật hiện có rơi về tiếng Anh nếu thiếu).
- Mèo đang ngủ, bị kéo, đang bay, trong trận hoặc app toàn màn hình: chờ tối đa 10 phút tới lúc mèo rảnh, quá thì chỉ hiện trong Nhật ký. Không phải cảm xúc, không đổi emotion.

### Nhật ký

- Thẻ độ thân thiết hiện tên mốc cao nhất, 5 nấc, khoảng còn lại tới mốc kế, danh sách hành vi đã mở và hành vi kế tiếp (khóa, ghi "Mở ở mốc …").
- Python tắt: Nhật ký như hiện nay (không có dữ liệu Python thì không có thẻ này).

### Giao thức (thêm trường tùy chọn, không đổi trường cũ)

- Decision thêm `bond_level` (số nguyên 1–5, mốc cao nhất) và `bond_up` (1–5, chỉ có trong một decision khi vừa lên mốc). `diary` thêm `bond_level`. C# bỏ qua giá trị sai mà không từ chối cả decision, như các trường tùy chọn khác.
- Không có lệnh mới: C# tự chạy hành vi theo mốc nhận được.

### Khi code cần kiểm tra

- Python: tính mốc, không tụt, file cũ, lên mốc chỉ báo một lần, quên-đã-học không xóa mốc, câu `bond_up` vi/en.
- C#: không mốc khi Python tắt hoặc vừa tắt; sleep lock (không đánh thức), wake, drag, throw, emotion priority, Quiet, Giờ tập trung, fullscreen, trận, Pause; chuỗi sau khi chơi không chạy khi trò bị ngắt; Ngủ tựa vào đồ vẫn là giấc ngủ thường (timer, click thức, kéo được).
- Soát render trái tim lên mốc, thẻ Nhật ký vi/en/ja.

### Bổ sung ngoại lệ (đã duyệt 11/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Mốc thân thiết: Python chia bond thành 5 mốc chỉ đi lên (Mới quen 0, Quen mặt 20, Thân 40, Rất thân 60, Tri kỷ 80) và gửi `bond_level`/`bond_up` (trường tùy chọn); C# mở theo mốc: Ngó chào khi người dùng quay lại sau ≥10 phút (Quen mặt), chuỗi liếm chân → vươn vai → về chỗ nghỉ sau trò người dùng bật kết thúc tự nhiên (Thân, tối đa 1 lần/5 phút), Ra đón đi về phía con trỏ rồi vẫy chân (Rất thân, thay Ngó chào), ngủ nằm sát đồ vật/kỷ vật trong 400 DIP khi mèo tự chọn ngủ (Tri kỷ, giấc ngủ thường). Tất cả là hành vi tự động: chặn khi Quiet, Giờ tập trung, fullscreen, trận, Pause, bận, cảm xúc ưu tiên cao, việc khác; không đánh thức mèo ngủ; click/kéo/cảm xúc ngắt như thường. Python tắt thì không có hành vi mở khóa. Lên mốc: trái tim nhỏ trên đầu ~1,5 s và một câu, không phải cảm xúc, không âm thanh mới.

### Cách code M3 (12/11)

- Python (`ai/service.py`): `BOND_LEVELS`, `bond_level()`, `bond_peak` lưu trong `ai-memory.json` (file cũ tính từ bond hiện tại, không bao giờ giảm), `bond_up` gửi một lần; sự kiện câu nói `bond_up` (`ai/lines.json` vi/en/ja) xếp sau sự kiện nông trại, trước "back". `diary.bond_level`.
- Giao thức C# (`AiProtocol`): `AiDecision.BondLevel`, `BondUp`, `PetDiary.BondLevel`; giá trị ngoài 1–5 bị bỏ, decision vẫn nhận.
- Hành vi ở `PetWindow.Bond.cs`, luật số ở `PetBondRules` (Core): `bondLevel` lấy từ decision, xóa về 0 khi AI tắt hoặc một lần hỏi không có trả lời. Ngó chào/Ra đón: `TickContext` ghi thời gian vắng dài nhất, lúc quay lại ≥10 phút thì chờ tối đa 30 giây cho mèo rảnh (`AutoKind.BondAct`). Chuỗi sau khi chơi: `PlayEnded()` gọi khi hết 45 giây bóng, hết 20 giây chờ ném nhặt, hết chuỗi chuyền vì để yên, và lúc bắt đầu 2 phút nghỉ chung của cần câu/chuột/chuyền. Liếm chân, vươn vai, Vẫy chân là tư thế "Làm ngay" của `PetBehavior`; nhìn, đi, ngồi là phần chuyến sở hữu chuyển động.
- Ngủ tựa vào đồ: `SleepOnCushionInstead` thử `GoNap()` (đệm, hộp) rồi `GoLeanNap()`; chung đường đi của đệm (`cushionNap` Walk, cờ `napLean`), tới nơi là giấc ngủ thường tại chỗ.
- Trái tim: `PetRenderer.DrawBondHearts`, 1,5 giây, chỉ là hình. Tên mốc tiếng Anh dùng "Good friends", "Best friends" vì "Close" đã là khóa dịch của nút Đóng.
- Đã soát ảnh render trái tim trên đầu mèo. Chưa xem thẻ Nhật ký và chưa thử quay lại máy thật sau 10 phút, chưa chạy với Python thật qua nhiều ngày; câu tiếng Nhật chưa ai duyệt.

## Luật M4 Món yêu thích và lựa chọn (duyệt 12/11)

### Món nào được tính

- Chỉ ba món người dùng chơi cùng mèo: **Bóng len, Cần câu lông vũ, Chuột dây cót**. Không tính laser (không phải đồ trên desktop), thùng carton, trụ cào, chậu cây (việc mèo tự làm, tính vào thì mèo tự "nuôi" món yêu thích).
- Một lượt là một lần trò do người dùng bật: một chuỗi ném nhặt, một chuỗi chuyền, một lượt đuổi cần câu, một lượt đuổi chuột. Mèo rủ chơi mà người dùng không chơi tiếp thì không tính.

### Python giữ món yêu thích

- C# gửi thêm trường tùy chọn `toy_plays` trong observation: số đếm cộng dồn theo món (`ball`, `wand`, `mouse`), giống các bộ đếm tương tác hiện có. Bộ đếm `toy` cũ giữ nguyên cách tính.
- Python lưu điểm từng món trong `ai-memory.json`, điểm **giảm một nửa sau mỗi 7 ngày** để món yêu thích có thể đổi.
- Món yêu thích cần ít nhất 5 điểm và hơn món thứ hai 1,3 lần. Đã có món yêu thích thì món khác phải vượt món đó 1,3 lần mới thay; đổi tối đa một lần mỗi ngày.
- "Quên điều mèo đã học" xóa điểm món và món yêu thích (khác mốc thân thiết M3, vì đây là điều mèo học được).
- **Python tắt hoặc lỗi: không có món yêu thích**, mèo chạy đúng như hiện nay (rủ chơi bằng bóng như M3). Món biết được trong phiên bị xóa khi AI tắt, như mốc M3.

### Ba hành vi mở thêm

Chỉ khi có món yêu thích, món đó đang nằm ngoài desktop, cùng màn hình với mèo.

1. **Ghé món thích:** mỗi khoảng 10–15 phút mèo đi tới món, ngửi hoặc vỗ chân rồi ngồi cạnh khoảng 5 giây. Món không dịch chuyển, đồ vẫn không chạm mèo.
2. **Rủ chơi bằng món thích:** dùng chung lịch 8–15 phút của rủ chơi M3.
   - Bóng len: giữ nguyên rủ chơi M3 (ngậm bóng tới chỗ con trỏ).
   - Cần câu hoặc chuột: mèo không tha (tha chuột vẫn để sau). Mèo tới ngồi cạnh món, nhìn về phía con trỏ, vỗ nhẹ món (chỉ là hình, món không động) và chờ 20 giây. Trong 20 giây người dùng vẩy cần câu hoặc lên dây chuột thì vào trò M4 Đợt 1 như thường (tính lượt và 2 phút nghỉ chung), không thì mèo đi việc khác.
3. **Chọn một trong hai:**
   - Mèo tự đi tới đồ (ghé, rủ chơi, ngửi đồ mới) mà có hai món chơi được nằm trong khoảng 250 DIP của nhau: mèo dừng ở giữa, nhìn món này rồi món kia (khoảng 1 giây mỗi bên) rồi tới món yêu thích; không có món yêu thích thì chọn ngẫu nhiên.
   - Người dùng chơi cần câu và chuột cùng lúc: mèo đuổi món yêu thích, thay cho cách chọn hiện có; món kia vẫn chạy bình thường.

### Chặn và ngắt

- Là việc mèo tự làm, chặn như rủ chơi M3 và các chuyến tới đồ: Quiet, Giờ tập trung, app toàn màn hình, trận và màn hình trận, Pause, mèo bận/bị kéo/đang bay, cảm xúc ưu tiên cao, đang có việc khác, wake cooldown, người dùng vắng. Không đánh thức mèo ngủ.
- Ngắt như các chuyến tới đồ: click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng.
- Không âm thanh mới. Lúc món yêu thích mới được chọn có một câu nói một lần (tình huống `favorite_toy` trong `ai/lines.json`, vi/en; hệ thống câu nói hiện có, có cooldown, im trong Quiet).
- Không phải tương tác của người dùng: needs, bond, learning không đổi vì các hành vi này; chỉ lượt người dùng chơi mới được cộng.

### Nhật ký

- Thẻ "Món yêu thích": tên món và số lượt gần đây của ba món. Python tắt thì không có thẻ này.

### Giao thức (thêm trường tùy chọn, không đổi trường cũ)

- Observation thêm `toy_plays` (object, số nguyên ≥ 0 cho `ball`, `wand`, `mouse`). Decision thêm `favorite_toy` (`ball`/`wand`/`mouse`, có thể vắng); `diary` thêm `favorite_toy` và `toy_plays` gần đây. Giá trị sai bị bỏ qua, decision vẫn nhận.
- Không có lệnh mới: C# tự chạy hành vi theo món nhận được.

### Khi code cần kiểm tra

- Python: đếm theo món, giảm một nửa 7 ngày, ngưỡng 5 điểm và 1,3 lần, đổi tối đa 1 lần/ngày, file cũ không có điểm món, quên-đã-học xóa điểm, câu `favorite_toy` vi/en.
- C#: không có món yêu thích khi Python tắt hoặc vừa tắt (rủ chơi bóng M3 như cũ); đếm đúng lượt người dùng bật, không đếm rủ chơi bị bỏ; sleep lock (không đánh thức), wake, drag, throw, emotion priority, Quiet, Giờ tập trung, fullscreen, trận, Pause; rủ chơi cần câu/chuột chuyển sang trò M4 Đợt 1 đúng lượt và nghỉ; chọn một trong hai khi hai món gần nhau và khi chơi cần câu với chuột cùng lúc.
- Soát render mèo vỗ món, nhìn hai bên; thẻ Nhật ký vi/en/ja.

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Món yêu thích: C# gửi `toy_plays` (lượt người dùng chơi Bóng len, Cần câu, Chuột dây cót), Python giữ điểm giảm một nửa mỗi 7 ngày và gửi `favorite_toy` (≥5 điểm, hơn 1,3 lần, đổi tối đa 1 lần/ngày). C# mở: ghé món thích mỗi 10–15 phút (ngửi/vỗ/ngồi cạnh, món không động), rủ chơi bằng món thích chung lịch 8–15 phút của M3 (bóng như cũ; cần câu/chuột thì ngồi cạnh vỗ nhẹ, chờ 20 giây), hai món trong 250 DIP thì nhìn hai bên rồi chọn món thích, chơi cần câu và chuột cùng lúc thì đuổi món thích. Hành vi tự động: chặn như rủ chơi M3, không đánh thức mèo ngủ, click/kéo/cảm xúc ngắt như thường; không âm thanh mới, đồ không chạm mèo. Python tắt thì không có món yêu thích.

### Cách code M4 (12/11)

- Python (`ai/service.py`): `observe_toys()` đọc `toy_plays` (lần đầu mỗi phiên chỉ là mốc), cộng vào `toy_scores`, giảm nửa mỗi 7 ngày theo thời gian thật (`toy_scores_at`), chọn `favorite_toy` (≥5 điểm, hơn 1,3 lần, đổi tối đa một lần mỗi ngày qua `favorite_day`); `forget_toys()` khi "Quên điều mèo đã học". Câu `favorite_toy` (`ai/lines.json` vi/en/ja) xếp ngay sau `bond_up`. `diary.favorite_toy`, `diary.toy_plays` (điểm làm tròn).
- Giao thức C# (`AiProtocol`): `PetObservation.ToyPlays` (`ToyPlayReport`), `AiDecision.FavoriteToy`, `PetDiary.FavoriteToy`/`ToyPlays`; giá trị sai bị bỏ, decision vẫn nhận.
- Đếm lượt (`CountToyPlay`): ném nhặt khi một chuỗi mới bắt đầu hoặc cú ném đầu sau khi mèo rủ (`BallThrown`), chuyền khi chuỗi bắt đầu (`TryStartPass`), cần câu/chuột khi lượt đuổi bắt đầu (`TryStartChase`, `PounceFromBox`). Mèo rủ mà không ai chơi thì không tính.
- Hành vi ở `PetWindow.FavoriteToy.cs`, số ở `FavoriteToyRules` (Core), `AutoKind.FavoriteVisit` chặn như `ToyInvite`. Món thích lấy từ decision, xóa khi AI tắt hoặc một lần hỏi không trả lời (gọi trong `ForgetBond`). Ghé món: hẹn giờ 10–15 phút, đi tới cạnh món (như chậu cây), ngửi hoặc vỗ chân 1,2 giây, ngồi 5 giây. Rủ chơi: hẹn giờ rủ của M3 gọi `InviteToPlay()`; Python tắt thì đúng `InviteToFetch()` cũ; món thích là cần câu/chuột thì ngồi cạnh, nhìn con trỏ, vỗ món mỗi 6 giây, 20 giây. Đang ghé/rủ mà người dùng ném bóng, chuyền, cầm cần câu hoặc lên dây chuột thì trò của người dùng thắng ngay (`FavoriteYields`).
- Chọn một trong hai: khi có Python và một món chơi được khác nằm trong 250 DIP của món định tới (ghé, rủ chơi, hoặc hai món mới chưa ngửi), mèo đi tới giữa hai món, nhìn mỗi bên 1 giây (đầu nghiêng), rồi chọn món thích, không có thì ngẫu nhiên; chọn Bóng len khi rủ thì chuyển sang tha bóng như M3, chọn món mới thì đi ngửi như M5. Cầm cần câu và lên dây chuột cùng lúc: `ChaseLure` ưu tiên món thích (mặc định vẫn cần câu trước).
- Nhật ký: thẻ "Món yêu thích" (tên món, lượt gần đây ba món), chỉ khi decision có `diary.toy_plays`.
- Đã soát ảnh render mèo vỗ cần câu và nghiêng đầu hai bên. Chưa chơi với Python thật qua nhiều ngày (giảm nửa 7 ngày, đổi món), chưa xem thẻ Nhật ký trên máy thật; câu tiếng Nhật chưa ai duyệt.

## Luật M5 Gọi mèo và dạy động tác (nháp 12/11, chờ duyệt)

Người dùng chốt 12/11: dạy 3 động tác mới; lưu ở C#, Python tắt vẫn dạy được; gọi mèo chỉ trong cùng màn hình. Vẫy chân và Lăn ngửa đã có sẵn trong "Làm ngay" từ 13/10 nên giữ nguyên, không khóa lại.

### Gọi mèo

- Menu khay thêm dòng **"Gọi mèo"**. Mèo quay đầu về phía con trỏ khoảng 0,5 giây, rồi đi trên sàn (hoặc dọc mép cửa sổ đang đứng, không tự nhảy xuống) về phía **con trỏ hiện tại**, bám theo con trỏ tối đa khoảng 10 giây, vì lúc bấm khay con trỏ đang ở góc taskbar. Tới gần (khoảng 40 DIP) thì ngồi ngước nhìn con trỏ khoảng 3 giây rồi rảnh.
- Con trỏ ở màn hình khác với mèo: mèo chỉ quay đầu nhìn về phía màn hình đó khoảng 1,5 giây, không đi; menu vẫn bấm được.
- Là lệnh của người dùng như "Làm ngay": chạy cả trong Quiet, Giờ tập trung và wake cooldown; không cắt kéo, đang bay, sleep lock hay waking. Mèo đang ngủ (kể cả away nap) không bị đánh thức: lệnh chờ tới lúc mèo tự thức, tối đa 30 giây rồi bỏ. Không chạy khi Pause, trong trận và màn hình trận, app toàn màn hình (dòng menu mờ).
- Ngắt như mọi việc của mèo: click trái/kéo dừng và kéo như thường, click phải dừng và mở menu, cảm xúc ưu tiên cao nhường, Pause đóng băng. Gọi lần nữa thì bắt đầu lại.
- Không phải tương tác của needs, bond, learning (như "Làm ngay"); không âm thanh mới.

### Ba động tác mới

| Động tác | Khi đã thuộc | Khi đang học (làm thử vụng) |
|---|---|---|
| **Xoay vòng** | Nhún nhẹ rồi xoay một vòng tại chỗ (~1,2 giây) | Xoay nửa vòng, lảo đảo, ngồi phịch xuống |
| **Ngồi xin** | Đứng trên hai chân sau, hai chân trước co trước ngực (~2 giây) | Nhổm lên được một nửa rồi chống chân xuống |
| **Đập tay** | Giơ một chân trước cao về phía con trỏ, giữ ~1,2 giây; con trỏ ở gần bàn chân thì có tia sáng nhỏ (chỉ là hình) | Giơ chân thấp, rụt lại |

- Tư thế vẽ vector/procedural trong PetRenderer, theo docs/DESIGN-SYSTEM.md; không sprite. Là động tác cử chỉ (như Vẫy chân): lệnh AI không thay được giữa chừng, click/kéo hủy về Idle ngay.

### Dạy động tác

- Menu chuột phải thêm submenu **"Dạy động tác"**, mỗi động tác một dòng kèm tiến độ (ví dụ "Xoay vòng ●●○○○", đã thuộc thì "✓ Đã thuộc").
- Chọn một động tác chưa thuộc: mèo ngồi nhìn con trỏ, **làm thử vụng** khoảng 3 giây, rồi ngồi chờ 6 giây. Người dùng **vuốt ve** mèo (cử chỉ vuốt sẵn có) trong 6 giây đó thì tính **một buổi học**: hiện nhãn nhỏ "2/5" trên đầu mèo khoảng 1,5 giây. Không vuốt thì hết buổi, không tính, không phạt.
- Mỗi động tác tối đa **3 buổi mỗi ngày** (ngày theo giờ máy), cách nhau ít nhất 30 giây; đủ **5 buổi** là thuộc, tức ít nhất 2 ngày. Hết lượt hôm nay thì dòng menu mờ, ghi "Mai học tiếp".
- Thuộc rồi: động tác hiện trong "Làm ngay" (nhóm cử chỉ, cạnh Vẫy chân) và mèo làm bản gọn gàng. Lần đầu thuộc có một câu nói (câu nói sẵn có của C#, im trong Quiet) và nhãn "Đã thuộc!".
- Buổi học là lệnh của người dùng như "Làm ngay": chạy cả Quiet và wake cooldown, không cắt kéo/bay/sleep lock/waking (mèo bận thì chờ như hàng đợi của "Làm ngay"). Click/kéo/cảm xúc ưu tiên cao giữa buổi thì hủy buổi, không tính. Pause đóng băng.
- Mèo **không tự làm** các động tác đã học trong M5 (không có hành vi tự động mới).

### Lưu và Python

- Lưu trong preferences của C#: số buổi từng động tác, ngày và số buổi hôm nay. Python không tham gia, không trường giao thức mới; Python tắt vẫn dạy và gọi được. "Quên điều mèo đã học" (dữ liệu học của Python) không xóa động tác đã thuộc.
- Ba hành động mới báo cho Python qua trường `state` sẵn có như mọi PetAction; thêm vào `BUSY_STATES` của `ai/service.py` để Python không gợi ý chen giữa (giữ đồng bộ với `PetActionRules.RejectsDecisions`).
- Vuốt ve trong buổi học vẫn là vuốt ve bình thường cho Python (needs, bond, learning như hiện nay); bản thân buổi học, gọi mèo và động tác không cộng gì thêm.
- Không âm thanh mới.

### Khi code cần kiểm tra

- Gọi mèo: cùng màn hình đi tới con trỏ và bám theo; khác màn hình chỉ quay đầu; trên mép cửa sổ không tự rơi; ngủ không bị đánh thức và tới sau khi thức trong 30 giây; Quiet, wake cooldown vẫn chạy; Pause, trận, fullscreen không chạy; click/kéo/menu ngắt.
- Dạy: vuốt trong 6 giây mới tính; giới hạn 3 buổi/ngày, 30 giây, 5 buổi; lưu qua lần mở lại; hủy khi click/kéo/cảm xúc; thuộc rồi mới có trong "Làm ngay"; sleep lock, wake, drag, throw, emotion priority, local fallback không đổi.
- Soát render 3 động tác (bản thuộc và bản vụng), hai hướng mặt, có phụ kiện; chữ menu vi/en/ja.

### Bổ sung ngoại lệ đề xuất (chờ duyệt, sẽ ghi vào docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Gọi mèo và dạy động tác: "Gọi mèo" trong menu khay đưa mèo đi về phía con trỏ hiện tại trên sàn/mép cửa sổ của nó (tối đa ~10 giây, cùng màn hình; khác màn hình chỉ quay đầu) rồi ngồi ngước nhìn; "Dạy động tác" cho Xoay vòng, Ngồi xin, Đập tay: mèo làm thử vụng, người dùng vuốt ve trong 6 giây thì tính một buổi (tối đa 3 buổi/ngày mỗi động tác, 5 buổi là thuộc), thuộc rồi gọi từ "Làm ngay". Cả hai là lệnh người dùng như "Làm ngay": chạy cả Quiet và wake cooldown, không cắt kéo/bay/sleep lock/waking, mèo ngủ thì chờ tự thức (gọi tối đa 30 giây); không khi Pause, trận, fullscreen; click/kéo/cảm xúc ngắt như thường. Lưu ở C#, Python không tham gia; mèo không tự làm động tác đã học; không âm thanh mới.
