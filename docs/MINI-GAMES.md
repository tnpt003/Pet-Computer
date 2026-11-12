# Mini-game và sự kiện (bản 1.6, Đợt 3)

Người dùng duyệt chia mốc 2026-11-12. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 4 và 5.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền mini-game, Úp cốc tìm cá | Mục "Chơi cùng mèo" ở menu chuột phải và menu khay; luật chung (người dùng mở, có giới hạn thời gian, Esc/click ra ngoài thoát, kỷ lục trong Nhật ký). Úp cốc: 3 cốc tráo nhanh dần 3 vòng, người dùng chọn, mèo ngửi, lật cốc, phản ứng | Đã có code, còn test tay |
| M2. Trốn tìm | Mèo nấp sau đồ đang có trên desktop (thùng carton, chậu cây, trụ cào), không có thì app vẽ tạm bụi cây/rèm; lộ tai hoặc đuôi; tìm đúng thì mèo bật ra. Không đọc nội dung cửa sổ, icon hay file thật | Bản nháp luật, chờ duyệt |
| M3. Bắt đom đóm | Khoảng 30 giây, đốm sáng bay quanh mèo, người dùng chạm thì tan thành hạt sáng, mèo cũng vồ vài con; đếm số đốm | Chưa viết luật |
| M4. Khách ghé thăm | Bướm giấy hoặc slime hiền thỉnh thoảng bay ngang; mèo nhìn theo hoặc đuổi một đoạn rồi khách đi. Sự kiện tự xảy ra, cần ngoại lệ mới | Chưa viết luật |
| M5. Săn kho báu | Mèo thỉnh thoảng tìm thấy mẩu bản đồ; người dùng tìm 3 dấu chân ẩn rồi mở rương cùng mèo; trong rương là một mẩu chuyện lưu vào Nhật ký, không có vật phẩm | Chưa viết luật |

Quyết định chung (người dùng đồng ý 12/11):

- **Mini-game chỉ do người dùng mở**, không bao giờ tự bật. Không mở khi mèo ngủ (kể cả away nap) hoặc waking: lệnh người dùng không đánh thức mèo. Pause đóng băng; app toàn màn hình, vào trận hoặc kéo mèo thì kết thúc ngay. Quiet và Giờ tập trung không chặn trò người dùng mở (như bóng, laser).
- **Phản ứng thắng/thua là hoạt cảnh ngắn**, không đi qua hệ cảm xúc (Happy, Sad...), nên không đụng ưu tiên/cooldown cảm xúc.
- **Python**: mỗi ván gửi một lần sự kiện `toy_started` sẵn có để mood/bond tính là đã chơi. Không giao thức mới; Python tắt thì trò vẫn chạy. Kỷ lục lưu ở C#.
- **Sự kiện tự xảy ra (M4, M5)**: tần suất thấp (khoảng 40–90 phút một lần), không giành focus, bỏ qua thì tự đi, chặn khi ngủ/Quiet/Giờ tập trung/app toàn màn hình/trong trận, có công tắc tắt riêng. Không thưởng đăng nhập, không mất gì khi bỏ lỡ.
- Không vật phẩm hay chiến lợi phẩm trong đợt này (để Đợt 4).
- Để sau: đường bóng, đường đua, dẫn đường bằng cần câu, dọn đồ cùng nhau, chọn đúng món, bật nhảy bắt đồ, chơi theo nhịp, mèo thám tử, vali, đường hầm, giao bưu kiện, tìm bạn trốn. Đã có ở đợt trước: ném nhặt, chuyền bóng (Đợt 1), đập tay (Đợt 2 M5), vồ đồ từ hộp (Đợt 2 M2).

## Luật M1 Nền mini-game và Úp cốc tìm cá (duyệt 12/11)

### Mở và thoát trò chơi

- Menu chuột phải và menu khay thêm submenu **"Chơi cùng mèo"**; M1 có một dòng "Úp cốc tìm cá", các mốc sau thêm dòng. Kỷ lục hiện ngay trên dòng (ví dụ "Úp cốc tìm cá · kỷ lục 4").
- Dòng mờ kèm lý do khi: Pause, trong trận hoặc màn hình trận, app toàn màn hình, đang có một mini-game khác. Mèo ngủ/waking: dòng vẫn bấm được, trò chờ mèo tự thức tối đa 30 giây rồi bỏ (như Gọi mèo), không đánh thức.
- Mèo đang bận việc ngắn (đi tới đồ, cử chỉ, "Làm ngay"): dừng việc đó rồi bắt đầu; đang kéo, đang bay, sleep lock thì chờ như hàng đợi của "Làm ngay". Đang chơi bóng/laser/cần câu/chuột hoặc chuyền bóng thì trò đó kết thúc trước.
- Chạy cả trong Quiet, Giờ tập trung và wake cooldown. Trong lúc chơi, hành vi tự động, lệnh Python và cảm xúc tự động không chen vào (như rủ chơi).
- **Thoát**: Esc, click ra ngoài vùng chơi, chọn lại dòng menu, click trái/kéo mèo, hoặc hết giờ (tối đa khoảng 2 phút một ván). App toàn màn hình, vào trận, rút màn hình đang chơi, đổi DPI: kết thúc ngay, không tính ván. Pause đóng băng cả tráo cốc lẫn đồng hồ.
- Mini-game không lưu giữa chừng: thoát app khi đang chơi thì lần sau không có ván dở. Kỷ lục nâng ngay khi đạt nên không mất.

### Vùng chơi

- Đồ của trò chơi vẽ trong cửa sổ trong suốt riêng cạnh mèo, cùng màn hình, trên sàn hoặc trên mép cửa sổ mèo đang đứng; không đè lên vùng đất nông trại, xe hàng, đồ chơi; không đủ chỗ thì mèo đi ra chỗ rộng trước (tối đa 5 giây) rồi mới bày cốc.
- Chỉ đồ của trò chơi nhận click; vùng trống click xuyên qua như overlay thời tiết. Click lên cốc không bao giờ tính là click mèo, và ngược lại.
- Vẽ vector theo docs/DESIGN-SYSTEM.md, rõ trên nền sáng/tối. Chữ (lượt, kỷ lục, hướng dẫn một dòng) dùng nhãn PetUi, vi/en/ja.

### Úp cốc tìm cá

- Mèo ngồi cạnh, 3 cốc gốm cao khoảng 44 DIP xếp hàng cách nhau khoảng 70 DIP. Một con cá khô nằm trước cốc giữa; mèo nhìn cá, cốc úp xuống che cá.
- **Tráo**: vòng 1 tráo 3 lần chậm (khoảng 0,6 s mỗi lần), vòng 2 tráo 5 lần (0,45 s), vòng 3 trở đi tráo 7 lần (0,35 s, không nhanh hơn). Cốc đi theo vòng cung, không nhảy cóc, mắt theo dõi được; mèo đảo đầu nhìn theo.
- Tráo xong hiện dòng "Cá ở cốc nào?". Người dùng click một cốc (không giới hạn thời gian chọn ngoài giới hạn ván). Mèo đi tới, ngửi, lấy chân hất cốc lên:
  - **Đúng**: mèo ngậm cá, lắc đuôi, nhãn "+1"; vòng tiếp theo bắt đầu sau khoảng 1 giây.
  - **Sai**: cốc rỗng, mèo nghiêng đầu, rồi lật cốc có cá cho xem; ván kết thúc, hiện số vòng đúng liên tiếp và "Kỷ lục mới!" nếu từ 3 vòng.
- Một ván tối đa 10 vòng đúng (khi đó mèo ăn cá, ván kết thúc).
- Không âm thanh mới: dùng lại cue có sẵn (chạm đồ, vui), có cooldown.

### Lưu và Python

- `preferences.json` thêm kỷ lục Úp cốc (số vòng đúng liên tiếp cao nhất) và số ván đã chơi; file cũ không có trường vẫn đọc được. Thẻ Nhật ký thêm dòng "Mini-game" hiện kỷ lục.
- Mỗi ván gửi `toy_started` một lần lúc bắt đầu. Bản thân thắng/thua không cộng needs, bond, learning thêm. Không câu nói Python mới.
- Hành động mini-game báo cho Python qua trường `state` sẵn có và thêm vào `BUSY_STATES` để Python không gợi ý chen giữa (đồng bộ `PetActionRules.RejectsDecisions`).

### Khi code cần kiểm tra

- Sleep lock, wake, drag, throw, emotion priority, local fallback không đổi; mèo ngủ không bị đánh thức, trò chờ tối đa 30 s; Pause đóng băng; fullscreen/trận/rút màn hình/đổi DPI kết thúc; Esc, click ra ngoài, click/kéo mèo thoát.
- Click cốc không lẫn click mèo; vùng trống click xuyên qua; cốc không chồng lên vùng đất, xe hàng, đồ chơi.
- Tráo giữ đúng vị trí cá (luật thuần có unit test), tốc độ đúng từng vòng, tối đa 10 vòng; kỷ lục lưu qua lần mở lại.
- Soát render cốc, cá, mèo ngửi/hất cốc/ngậm cá, hai hướng mặt, nền sáng/tối; chữ menu, nhãn vi/en/ja. Đo FPS khi tráo nhanh nhất.

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Mini-game do người dùng mở từ "Chơi cùng mèo" (menu chuột phải và khay) là lệnh người dùng như "Làm ngay": chạy cả Quiet, Giờ tập trung và wake cooldown; không đánh thức mèo ngủ (chờ tự thức tối đa 30 s); trong lúc chơi chặn hành vi tự động, lệnh Python và cảm xúc tự động; phản ứng thắng/thua là hoạt cảnh, không phải cảm xúc; đồ trò chơi vẽ trong cửa sổ riêng, vùng trống click xuyên qua, không chạm mèo; Pause đóng băng, fullscreen/trận/kéo mèo kết thúc.

### Cách code M1 (12/11)

- Core `PetCupGame.cs`: `CupGameRules` (số liệu, `Place` chọn chỗ đứng của mèo và bên bày cốc theo vùng đất/xe hàng/đồ chơi, `SlotAt`, `RaiseBest`/`CountPlay`/`TellBest`) và `CupShuffle` (cốc giữ id, cá đi theo cốc, mỗi lần tráo một cốc nhảy vòng cung phía trước, cốc kia trượt phía sau). `preferences.json` thêm `CupGameBest`, `CupGamePlays` (kẹp khi đọc).
- Cửa sổ `PetWindow.CupGame.cs`: `ToggleCupGame()` từ submenu "Chơi cùng mèo" (menu chuột phải, ngay sau "Chơi"/"Cất bóng"; menu khay dưới "Giỏ đồ chơi"). Bị chặn (`CupGameBlocked`) khi Pause, app toàn màn hình, trận và màn hình trận; mèo bận/ngủ thì chờ (`Wait`, tối đa 30 s) như Gọi mèo. `TickCupClock` chạy mỗi khung trước CanPlay: kết thúc không tính khi bị chặn, đổi màn hình, đổi DPI hoặc mặt đất dịch đi; quá 2 phút; Esc và click ra ngoài đọc bằng `GetAsyncKeyState` (chỉ phím Esc, nút chuột; nhấn lúc chọn menu không tính). `TickCupGame` sở hữu chuyển động sau `TickLesson`; `CupOwnsMovement` nằm trong `FeatureOwnsMovement` nên hành vi tự động, lệnh Python, việc khác bị chặn; cảm xúc tự động tắt bằng `Emotions.SetSuspended` (trả lại khi xong, không đụng khi trận đang giữ).
- Vẽ `PetCupGameArt.cs` (cửa sổ riêng, chỉ hình cốc nhận click, chỗ trong suốt click xuyên qua), cá dùng `PetFarmArt.DrawFish`; mèo ngậm cá qua `PetRenderer.CarryFish`. Nhãn "Cá ở cốc nào?", "+1", "n vòng" dùng nhãn trên đầu mèo của M5 (`ShowTrickLabel`).
- **Lệch so với luật, người dùng duyệt 12/11:** cốc cao 36 DIP, rộng 40 (luật ghi khoảng 44) vì mèo vẽ mặt trước, đứng sau hàng cốc 44 DIP bị che mặt; cốc được hất thì trượt sang bên và nghiêng thay vì nhấc thẳng lên, để lộ cá mà không che mặt mèo. Khoảng cách 70 DIP giữ nguyên.
- Python: trạng thái `MiniGame` gửi thay tên hành động khi trò sở hữu mèo, nằm trong `BUSY_STATES` (tách khỏi tập hành động C# để test đồng bộ `RejectsDecisions` vẫn đúng). Mỗi ván gửi `toy_started` một lần.
- Test: `CupGameTests` (Core), `CupGameChecks` (UiChecks: thắng, thua, kỷ lục, Quiet, ngủ, Esc, click ngoài/trên cốc, click mèo, Pause, 2 phút); menu chuột phải nay 15 mục. Đã soát ảnh render (bày cốc, tráo, chọn, hất, ngậm cá, lật cốc có cá) nền sáng/tối; chưa xem trên desktop thật, chưa bấm chuột thật, chưa thử mép cửa sổ, DPI khác, nhiều màn hình, chưa đo FPS; tiếng Nhật chưa ai duyệt.

## Luật M2 Trốn tìm (bản nháp 12/11, chờ duyệt)

Dùng chung toàn bộ luật nền mini-game của M1 (mở từ "Chơi cùng mèo", lệnh người dùng như "Làm ngay", không đánh thức mèo ngủ, chặn hành vi/cảm xúc tự động khi chơi, Pause đóng băng, Esc/click ra ngoài/chọn lại dòng menu/hết giờ thoát, toàn màn hình/trận/rút màn hình/đổi DPI kết thúc không tính ván, `toy_started` một lần mỗi ván, trạng thái `MiniGame` cho Python). Dưới đây chỉ ghi phần riêng.

### Chỗ nấp

- Luôn có **3 chỗ nấp** trên sàn màn hình của mèo, cách nhau ít nhất khoảng 150 DIP, không đè vùng đất nông trại, xe hàng.
- Ưu tiên đồ đang bày trên sàn màn hình đó: **thùng carton** (mèo ngồi trong hộp, chỉ thò chóp tai), **chậu cây** và **trụ cào** (mèo nằm thấp phía sau, lộ chóp đuôi hoặc tai). Thiếu chỗ thì app vẽ thêm **bụi cây tròn** (khoảng 70 DIP rộng, 56 DIP cao, vector theo docs/DESIGN-SYSTEM.md); bụi cây chỉ có trong lúc chơi, không lưu, không physics, không vào giỏ.
- Đồ chơi khác (bóng, cần câu, chuột) không phải chỗ nấp. Mèo đứng trên mép cửa sổ: trò bày trên sàn, mèo nhảy xuống như một cú nhảy thường lúc bắt đầu trốn.
- Không đọc nội dung cửa sổ, icon hay file thật của người dùng.

### Một ván

1. **Đếm:** nhãn trên đầu mèo "Nhắm mắt nhé! 3… 2… 1…" (khoảng 3 giây). Trong lúc đếm, bụi cây (nếu cần) mọc lên, mèo biến mất trong một làn khói nhỏ rồi xuất hiện sẵn sau chỗ nấp đã chọn ngẫu nhiên, nên không lộ đường chạy.
2. **Tìm:** nhãn "Mèo trốn ở đâu?" ở giữa ba chỗ. Mỗi 3–5 giây, chỗ có mèo lộ một dấu hiệu nhỏ khoảng 0,6 giây (tai giật, chóp đuôi vẫy, lá bụi cây rung). Hai chỗ kia thỉnh thoảng cũng rung lá/lay nhẹ để đánh lừa, nhưng ít hơn.
3. **Đoán:** người dùng click vào chỗ nấp (hoặc vào chóp tai/đuôi đang lộ). Tối đa **3 lượt**:
   - **Đúng:** mèo bật ra khỏi chỗ nấp bằng cú nhảy nhỏ, nhãn "Tìm thấy rồi!" (lượt 1 thêm "Ngay lượt đầu!"), bụi cây tàn đi.
   - **Sai:** chỗ đó lắc nhẹ, nhãn "Không có ở đây" khoảng 1 giây; chỗ sai đó mờ đi, không bấm lại được.
   - Hết 3 lượt hoặc quá **60 giây**: mèo tự bật ra, nhãn "Ở đây nè!".
4. Mèo ra rồi đứng chơi nhún nhảy khoảng 1,5 giây, ván kết thúc. Muốn chơi lại thì chọn menu lần nữa.

### Click, kéo và các luật bất biến

- Click chuột vào mèo đang nấp (phần tai/đuôi lộ ra) tính là **tìm đúng**. Kéo quá ~6 DIP thì như mọi lúc: trò kết thúc, mèo được nhấc và kéo bình thường (pickup mượt, dangling, ném có velocity).
- Click phải vào mèo: trò kết thúc, mở menu.
- Người dùng cầm hộp, chậu, trụ khi đang chơi: trò kết thúc không tính ván, mèo hiện lại tại chỗ.
- Mèo đang trốn không ngủ, không chuyển cảm xúc tự động; Pause đóng băng cả lúc đếm lẫn lúc tìm.
- Trò không bắt đầu khi mèo ngủ (chờ tự thức tối đa 30 giây như M1). Thùng carton đang có mèo ngủ thì đã không thể có (mèo phải thức mới chơi).

### Lưu và Python

- `preferences.json` thêm số ván tìm thấy và số lần tìm thấy ngay lượt đầu; file cũ đọc được. Thẻ "Mini-game" trong Nhật ký thêm dòng "Trốn tìm: tìm thấy n ván · m lần ngay lượt đầu".
- Python như M1: `toy_started` một lần, trạng thái `MiniGame`, không câu nói mới. Không âm thanh mới (dùng lại cue có sẵn, có cooldown).

### Khi code cần kiểm tra

- Chọn chỗ nấp: có đủ 3 chỗ cách nhau, tránh vùng đất/xe hàng, dùng đồ có sẵn trước, bụi cây chỉ khi thiếu; màn hình hẹp không đủ chỗ thì báo "Không đủ chỗ để trốn" và không bắt đầu.
- Click đúng/sai/tai đuôi, 3 lượt, 60 giây; kéo mèo đang nấp; cầm hộp/chậu/trụ; Esc, click ra ngoài; sleep lock, wake, drag, throw, emotion priority, local fallback, Pause, Quiet.
- Thứ tự lớp: đồ nấp phải che mèo (mèo vẽ thấp, chỉ lộ tai/đuôi); mèo trong hộp dùng mặt trước hộp trong cửa sổ mèo như M2 Đợt 2.
- Soát render bụi cây, mèo nấp sau từng loại chỗ, dấu hiệu lộ, nền sáng/tối; chữ vi/en/ja; đo FPS.

### Bổ sung ngoại lệ (chờ duyệt cùng luật)

Trốn tìm dùng ngoại lệ mini-game của M1, thêm: trong ván, mèo được "biến mất trong làn khói" rồi hiện ở chỗ nấp (một lần dịch chuyển trong cùng sàn màn hình); app được vẽ tạm bụi cây không physics chỉ trong lúc chơi; cửa sổ hộp, chậu, trụ được nâng lên trên mèo trong lúc nấp. Click vào mèo đang nấp là lượt đoán đúng; kéo quá ~6 DIP vẫn nhấc mèo như thường.
