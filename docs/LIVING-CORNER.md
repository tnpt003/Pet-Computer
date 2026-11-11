# Góc sống và quan hệ (bản 1.6, Đợt 2)

Người dùng duyệt chia mốc 2026-11-11. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 1 và 2.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Trụ cào | Món mới trong giỏ; mèo tới cào rồi vươn vai; lệnh Cào móng có trụ gần thì đi tới trụ | Đã có code, còn test tay |
| M2. Chậu cây, hộp thêm trạng thái | Chậu cây (ngửi, chạm lá, ngồi cạnh, lớn theo số lần chơi, không héo); hộp thò đầu và vồ đồ đi ngang | Đã có code, còn test tay |
| M3. Mốc thân thiết | Bond thành 5 cấp có tên trong Nhật ký; mỗi cấp mở một hành vi (chào theo cấp, ngủ tựa vào đồ, chuỗi sau khi chơi) | Luật chờ duyệt |
| M4. Món yêu thích, lựa chọn | Python đếm lần chơi từng món; mèo quay lại món thích, rủ chơi bằng món đó; hai món cạnh nhau thì chọn một | Chưa viết luật |
| M5. Gọi mèo, dạy động tác | Lệnh "Gọi mèo"; mở vẫy tay, xoay vòng qua tương tác rồi gọi từ "Làm ngay" | Chưa viết luật |

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

## Luật M3 Mốc thân thiết (đề xuất 11/11, chờ duyệt)

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

### Bổ sung ngoại lệ đề xuất (chờ duyệt, ghi vào docs/APPROVED-EXCEPTIONS.md → Hành vi mèo khi duyệt)

Mốc thân thiết: Python chia bond thành 5 mốc chỉ đi lên (Mới quen 0, Quen mặt 20, Thân 40, Rất thân 60, Tri kỷ 80) và gửi `bond_level`/`bond_up` (trường tùy chọn); C# mở theo mốc: Ngó chào khi người dùng quay lại sau ≥10 phút (Quen mặt), chuỗi liếm chân → vươn vai → về chỗ nghỉ sau trò người dùng bật kết thúc tự nhiên (Thân, tối đa 1 lần/5 phút), Ra đón đi về phía con trỏ rồi vẫy chân (Rất thân, thay Ngó chào), ngủ nằm sát đồ vật/kỷ vật trong 400 DIP khi mèo tự chọn ngủ (Tri kỷ, giấc ngủ thường). Tất cả là hành vi tự động: chặn khi Quiet, Giờ tập trung, fullscreen, trận, Pause, bận, cảm xúc ưu tiên cao, việc khác; không đánh thức mèo ngủ; click/kéo/cảm xúc ngắt như thường. Python tắt thì không có hành vi mở khóa. Lên mốc: trái tim nhỏ trên đầu ~1,5 s và một câu, không phải cảm xúc, không âm thanh mới.
