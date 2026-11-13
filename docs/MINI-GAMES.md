# Mini-game và sự kiện (bản 1.6, Đợt 3)

Người dùng duyệt chia mốc 2026-11-12. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 4 và 5.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền mini-game, Úp cốc tìm cá | Mục "Chơi cùng mèo" ở menu chuột phải và menu khay; luật chung (người dùng mở, có giới hạn thời gian, Esc/click ra ngoài thoát, kỷ lục trong Nhật ký). Úp cốc: 3 cốc tráo nhanh dần 3 vòng, người dùng chọn, mèo ngửi, lật cốc, phản ứng | Đã có code, còn test tay |
| M2. Trốn tìm | Mèo nấp sau đồ đang có trên desktop (thùng carton, chậu cây, trụ cào), không có thì app vẽ tạm bụi cây/rèm; lộ tai hoặc đuôi; tìm đúng thì mèo bật ra. Không đọc nội dung cửa sổ, icon hay file thật | Đã có code, còn test tay |
| M3. Bắt đom đóm | Khoảng 30 giây, đốm sáng bay quanh mèo, người dùng chạm thì tan thành hạt sáng, mèo cũng vồ vài con; đếm số đốm | Đã có code, còn test tay |
| M4. Khách ghé thăm | Bướm giấy hoặc slime hiền thỉnh thoảng bay ngang; mèo nhìn theo hoặc đuổi một đoạn rồi khách đi. Sự kiện tự xảy ra, cần ngoại lệ mới | Đã có code, còn test tay |
| M5. Săn kho báu | Mèo thỉnh thoảng tìm thấy mẩu bản đồ; người dùng tìm 3 dấu chân ẩn rồi mở rương cùng mèo; trong rương là một mẩu chuyện lưu vào Nhật ký, không có vật phẩm | Đã có code, còn test tay |

Quyết định chung (người dùng đồng ý 12/11):

- **Mini-game chỉ do người dùng mở**, không bao giờ tự bật. Không mở khi mèo ngủ (kể cả away nap) hoặc waking: lệnh người dùng không đánh thức mèo. Pause đóng băng; app toàn màn hình, vào trận hoặc kéo mèo thì kết thúc ngay. Quiet và Giờ tập trung không chặn trò người dùng mở (như bóng, laser).
- **Phản ứng thắng/thua là hoạt cảnh ngắn**, không đi qua hệ cảm xúc (Happy, Sad...), nên không đụng ưu tiên/cooldown cảm xúc.
- **Python**: mỗi ván gửi một lần sự kiện `toy_started` sẵn có để mood/bond tính là đã chơi. Không giao thức mới; Python tắt thì trò vẫn chạy. Kỷ lục lưu ở C#.
- **Sự kiện tự xảy ra (M4, M5)**: tần suất thấp (khoảng 40–90 phút một lần), không giành focus, bỏ qua thì tự đi, chặn khi ngủ/Quiet/Giờ tập trung/app toàn màn hình/trong trận, có công tắc tắt riêng. Không thưởng đăng nhập, không mất gì khi bỏ lỡ.
- Không vật phẩm hay chiến lợi phẩm trong đợt này (để Đợt 4).
- Để sau: đường bóng, đường đua, dẫn đường bằng cần câu, dọn đồ cùng nhau, chọn đúng món, bật nhảy bắt đồ, chơi theo nhịp, mèo thám tử, vali, đường hầm, giao bưu kiện, tìm bạn trốn. Đã có ở đợt trước: ném nhặt, chuyền bóng (Đợt 1), đập tay (Đợt 2 M5), vồ đồ từ hộp (Đợt 2 M2).

## Luật M1 Nền mini-game và Úp cốc tìm cá (duyệt 12/11)

### Mở và thoát trò chơi

- Menu chuột phải và menu khay thêm submenu **"Chơi cùng mèo"**; M1 có một dòng "Úp cốc tìm cá", các mốc sau thêm dòng. Kỷ lục hiện ngay trên dòng (ví dụ "Úp cốc tìm cá · kỷ lục 4"). Từ 12/11 ở menu chuột phải các dòng mini-game nằm trong "Chơi ›" dưới một đường kẻ (không còn submenu riêng); menu khay giữ "Chơi cùng mèo".
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

## Luật M2 Trốn tìm (duyệt 12/11)

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

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Trốn tìm dùng ngoại lệ mini-game của M1, thêm: trong ván, mèo được "biến mất trong làn khói" rồi hiện ở chỗ nấp (một lần dịch chuyển trong cùng sàn màn hình); app được vẽ tạm bụi cây không physics chỉ trong lúc chơi; cửa sổ hộp, chậu, trụ được nâng lên trên mèo trong lúc nấp. Click vào mèo đang nấp là lượt đoán đúng; kéo quá ~6 DIP vẫn nhấc mèo như thường.

### Cách code M2 (12/11)

- **Người dùng chọn 12/11 khi bắt đầu code:** chỗ nấp chỉ là thùng carton (khi đang bày trên sàn màn hình đó) và bụi cây; chậu cây (rộng 50 DIP) và trụ cào (cột ~24 DIP) hẹp hơn đầu mèo (~64 DIP) nên không dùng.
- Core `PetHideSeek.cs`: `HideSeekRules` (số liệu, `Place` chọn 3 chỗ cách nhau ≥150 DIP trong tầm 700 DIP quanh mèo, tránh vùng đất/xe hàng/đồ chơi khác, hộp là một chỗ khi có; `Sink` độ hạ mèo theo chiều cao chỗ nấp; `RecordFound`). `preferences.json` thêm `HideSeekFound`, `HideSeekFirstTry`.
- Phần dùng chung `PetWindow.MiniGames.cs`: một mini-game một lúc (dòng kia mờ), `MiniGameBlocked`, Esc/click ngoài (`MiniGameKeyPresses`, Úp cốc cũng dùng), nhãn đặt tự do `ShowMiniLabel` (không đặt trên đầu mèo đang nấp).
- `PetWindow.HideSeek.cs`: Đếm 3 giây (nhãn tại chỗ cũ của mèo), mèo biến bằng hiệu ứng khói của "Gọi mèo về màn hình chính" (`TeleportPop`/`TeleportPuff`) rồi đặt thẳng sau chỗ nấp trên sàn. Mèo nấp: `PetRenderer.HideSink` hạ mèo, cắt ở sàn và theo bề ngang chỗ nấp (`HideHalf`, nên đuôi/ria không lộ), `HideCover` vẽ bụi cây hoặc mặt trước hộp đè lên mèo ngay trong cửa sổ mèo: click vào đó là click mèo = đoán đúng (`HideSeekPressCat` trong `BeginDrag`, cú nhấn vẫn đi tiếp, kéo quá ~6 DIP thì trò kết thúc và nhấc mèo như thường; `SeekRidesPress` giữ trò trong lúc nhấn chưa thành kéo). Bụi cây khác là cửa sổ riêng (`PetBushArt.cs`) giữ dưới mèo; click hộp gọi `HideSeekClickThing` trong `EndToyDrag`, nhấc hộp (`StartToyDrag`) kết thúc trò. Câu nói và bong bóng suy nghĩ bị chặn khi mèo đang nấp (`CatHiding`).
- Với 3 chỗ và chỗ sai mờ đi, lượt đoán thứ 3 luôn trúng; "hết 3 lượt" chỉ là giới hạn trên. Muốn khó hơn có thể giảm còn 2 lượt (chưa đổi).
- Test: `HideSeekTests` (Core), `HideSeekChecks` (UiChecks: đếm, khói, nấp, ló tai, đoán sai/đúng, nhấn mèo, 60 giây, Esc, click ngoài/trên chỗ nấp, ngủ, Pause, sàn hẹp). Đã soát ảnh render bụi cây, mèo nấp (không lộ gì), tai ló lên trên nền sáng/tối, mèo bật ra; **chưa soát ảnh mèo nấp trong thùng carton**, chưa xem desktop thật, chưa bấm chuột thật, chưa thử DPI khác, nhiều màn hình, FPS; tiếng Nhật chưa ai duyệt.

## Luật M3 Bắt đom đóm (duyệt 12/11)

Dùng chung toàn bộ luật nền mini-game của M1 (mở từ "Chơi cùng mèo", lệnh người dùng như "Làm ngay", không đánh thức mèo ngủ, chờ tối đa 30 giây, chặn hành vi/cảm xúc tự động khi chơi, Pause đóng băng, Esc/click ra ngoài/chọn lại dòng menu/click hoặc kéo mèo thoát, toàn màn hình/trận/rút màn hình/đổi DPI kết thúc không tính ván, `toy_started` một lần mỗi ván, trạng thái `MiniGame` cho Python). Dưới đây chỉ ghi phần riêng.

### Vùng chơi

- Vùng khoảng 360×220 DIP quanh mèo, phía trên sàn hoặc mép cửa sổ mèo đang đứng, cùng màn hình; không đè vùng đất nông trại, xe hàng, đồ chơi. Không đủ chỗ thì mèo đi ra chỗ rộng trước (tối đa 5 giây); vẫn không đủ thì báo "Không đủ chỗ" và không bắt đầu.
- Đom đóm vẽ trong cửa sổ riêng, đặt dưới cửa sổ mèo. Chỉ đốm sáng nhận click (vùng bấm khoảng 28 DIP cho dễ trúng), chỗ trống click xuyên qua. Đom đóm không bay vào khung thân mèo, nên click mèo và click đom đóm không lẫn nhau.
- Hình đom đóm dựa trên đom đóm của overlay thời tiết nhưng to và ấm màu hơn để phân biệt; vector theo docs/DESIGN-SYSTEM.md, rõ trên nền sáng/tối.

### Một ván

1. Nhãn "Bắt đom đóm nào!", đếm 3… 2… 1 (khoảng 2 giây) rồi đom đóm bay ra.
2. Ván dài **30 giây**, đồng hồ hiện trên nhãn. Luôn có 3–5 đốm bay lượn chậm theo đường cong; mỗi đốm sống 4–6 giây, nhấp nháy rồi tự tắt nếu không ai bắt. Từ giây 15 đốm bay nhanh hơn một chút.
3. **Người dùng click trúng:** đốm tan thành hạt sáng, nhãn "+1".
4. **Mèo cũng bắt:** khoảng mỗi 6–8 giây mèo nhắm một đốm bay thấp gần mình và vồ một cú ngắn tại chỗ (nhảy lên không quá ~60 DIP, đáp lại đúng sàn cũ); tối đa 4 đốm mỗi ván, có thể vồ hụt. Điểm là **điểm chung** "Cùng bắt được n đốm": đốm mèo bắt cũng tính.
5. Hết 30 giây: đốm còn lại bay đi, mèo ngồi nhìn theo; nhãn tổng kết số người dùng bắt, số mèo bắt, và "Kỷ lục mới!" nếu vượt kỷ lục.
- Không có đốm vàng hay đốm đặc biệt (người dùng chọn 12/11).

### Click, kéo và các luật bất biến

- Click hoặc kéo mèo: trò kết thúc; kéo quá ~6 DIP vẫn nhấc mèo như thường (pickup mượt, dangling, ném có velocity). Click phải: trò kết thúc, mở menu.
- Cú vồ là hoạt cảnh của trò, không phải hành vi tự động; không Dizzy, Bonk hay cảm xúc. Vồ trên mép cửa sổ không rơi khỏi mép.
- Đom đóm không physics, không chạm mèo, không lưu, hết ván là mất.

### Lưu và Python

- `preferences.json` thêm kỷ lục Bắt đom đóm (tổng số đốm cao nhất một ván) và số ván đã chơi; file cũ đọc được. Thẻ "Mini-game" trong Nhật ký thêm một dòng; dòng menu hiện "Bắt đom đóm · kỷ lục n".
- Python như M1: `toy_started` một lần, trạng thái `MiniGame`, không câu nói mới. Không âm thanh mới (dùng lại cue có sẵn, có cooldown).

### Khi code cần kiểm tra

- Luật thuần có unit test: sinh đốm (3–5 đốm, tránh khung mèo), tuổi đốm, tốc độ sau giây 15, nhịp mèo vồ và tối đa 4, kỷ lục.
- UiChecks: 30 giây, click trúng/trượt, Esc, click ra ngoài, click/kéo mèo, ngủ, Pause, Quiet, sàn hẹp; sleep lock, wake, drag, throw, emotion priority, local fallback.
- Soát render đốm, hạt tan, mèo vồ hai hướng mặt, trên sàn và trên mép cửa sổ, nền sáng/tối; chữ vi/en/ja; đo FPS khi có 5 đốm và hạt tan.

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Bắt đom đóm dùng ngoại lệ mini-game của M1, thêm: trong ván, mèo được vồ ngắn tại chỗ theo đom đóm (không quá ~60 DIP, đáp lại đúng sàn cũ, tối đa 4 đốm); app được vẽ tạm đom đóm không physics, không chạm mèo, chỉ có trong lúc chơi.

### Cách code M3 (12/11)

- Core `PetFireflies.cs`: `FireflyRules` (số liệu, `Place` chọn chỗ mèo đứng để vùng 360×220 nằm trong màn hình và tránh vùng đất/xe hàng/đồ chơi, trong tầm đi 5 giây; `Height` thu vùng thấp lại khi mép cửa sổ gần đỉnh màn hình, dưới 160 DIP thì không chơi; `RaiseBest`, `CountPlay`), `FireflySwarm` (đốm bay theo đường cong, quay về giữa khi chạm mép hoặc khung thân mèo, sống 4–6 s, luôn 3–5 đốm, nhanh ×1,35 từ giây 15, `At` vùng bấm 14 DIP, `PounceTarget` đốm thấp gần mèo) và `FireflyCat` (vồ lần đầu sau 3–6 s rồi mỗi 6–8 s, hụt 30 %, tối đa 4). `preferences.json` thêm `FireflyBest`, `FireflyPlays`.
- `PetWindow.Fireflies.cs`: bước Room (đi ra chỗ rộng như Úp cốc), Count 2 s, Play 30 s, Away 1,4 s (đốm bay lên rồi mất). Cú vồ của mèo chỉ là hình: cúi đầu (`ReactDip`), nhấc hình mèo lên tối đa 34 DIP bằng `renderer.TeleportDrop` và vung chân (`BatRemaining`); `pet.Y` không đổi nên mèo luôn đáp đúng sàn hoặc mép cũ, không physics, không Dizzy. Kết thúc, Esc, click ngoài, mèo ngủ chờ 30 s, Pause dùng chung phần nền của M1/M2; click trong vùng chơi (cả click trượt) không kết thúc.
- Vẽ `PetFireflyArt.cs`: thân mực, cánh vỗ, đuôi và quầng sáng hổ phách, nhấp nhanh trước khi tắt; mỗi đốm có đĩa gần trong suốt bán kính 14 DIP để nhận click, chỗ khác click xuyên qua. Cửa sổ đom đóm giữ dưới cửa sổ mèo (kiểm lại mỗi 0,5 s). Đốm tan thành hạt sáng, "+1" khi người dùng bắt. Nhãn đồng hồ và tổng kết dùng `ShowMiniLabel` trên vùng chơi.
- Test: `FireflyTests` (Core), `FireflyChecks` (UiChecks: đếm, 3–5 đốm, vùng chơi, bấm trúng/trượt, mèo vồ tại chỗ, tối đa 4, 30 giây, tổng kết, kỷ lục, Esc, click ngoài/trong vùng, chọn lại dòng menu, nhấc mèo, Quiet, ngủ, Pause, màn hình hẹp); menu "Chơi cùng mèo" nay 3 dòng.

## Luật M4 Khách ghé thăm (duyệt 12/11)

Sự kiện tự xảy ra, không phải mini-game: không mở từ "Chơi cùng mèo", không có ván, không `toy_started`. Người dùng chọn 12/11: hai khách, click khách có phản ứng nhỏ, không có dòng menu gọi khách (chỉ hook cho UiChecks).

### Khách

- **Bướm giấy:** bay lượn từ một mép màn hình sang mép kia, đi ngang gần mèo.
- **Slime hiền:** slime nhỏ màu pastel bò trên sàn màn hình (không lên mép cửa sổ), dừng cách mèo khoảng 80 DIP, nhún "chào" khoảng 1,5 giây rồi bò tiếp. Không máu, không đòn, không liên quan Battle Mode; hình khác slime trong trận.
- Vẽ vector theo docs/DESIGN-SYSTEM.md, rõ trên nền sáng/tối; app chưa có hình bướm nên vẽ mới.

### Khi nào đến

- Khoảng 40–90 phút một lần, mỗi lần chọn ngẫu nhiên một khách; lần đầu tính từ lúc mở app, hẹn giờ chỉ trong RAM. Bị chặn thì 2 phút sau thử lại.
- Là hành vi tự động: chặn khi mèo ngủ (kể cả away nap) hoặc waking, Quiet, Giờ tập trung, app toàn màn hình, trận hoặc bảng chiến đấu, Pause, người dùng vắng, đang chơi mini-game, mèo bận (bị kéo, bay, phản ứng), cảm xúc ưu tiên cao, tính năng khác đang giữ mèo, wake cooldown.
- Công tắc "Khách ghé thăm" trong Cài đặt → Thói quen, mặc định bật.

### Một lượt (khoảng 8–15 giây)

1. Khách hiện từ mép màn hình, cùng màn hình với mèo.
2. Mèo quay đầu nhìn theo. Khoảng 50 % số lần mèo đuổi một đoạn: đi tối đa ~150 DIP hoặc 3 giây trên đúng sàn/mép cửa sổ đang đứng, không nhảy khỏi mép; với bướm thì vồ một cú tại chỗ như M3 (chỉ là hình, `pet.Y` không đổi).
3. Khách đi ra mép bên kia rồi mất. Không lưu lại gì trên desktop.

### Click và các luật bất biến

- Khách vẽ trong cửa sổ riêng, không chiếm focus; chỉ hình khách nhận click, chỗ trống click xuyên qua. Click bướm: bướm bay vụt đi kèm vài hạt sáng. Click slime: slime nhún một cái rồi đi tiếp.
- Khách không physics, không chạm mèo, không bay vào khung thân mèo, không đánh thức mèo.
- Phản ứng của mèo là hoạt cảnh, không phải cảm xúc: không Startle/Surprised, không Dizzy, Bonk.
- Click hoặc kéo mèo: mèo thôi theo dõi, khách vẫn đi tiếp; kéo quá ~6 DIP vẫn nhấc mèo như thường. Click phải mở menu như thường.
- Mèo ngủ, app toàn màn hình, vào trận, đổi DPI, rút màn hình: khách mờ rồi mất ngay. Pause đóng băng khách.
- Không thưởng, bỏ lỡ không mất gì. Không âm thanh mới.

### Lưu và Python

- `preferences.json` thêm công tắc và số lần khách đã ghé; file cũ đọc được. Thẻ Nhật ký thêm dòng "Khách đã ghé: n".
- Python không biết về khách, không giao thức mới; trong lúc mèo theo dõi, lệnh Python bị chặn như hành vi tự động khác. Python tắt thì khách vẫn đến.

### Khi code cần kiểm tra

- Luật thuần có unit test: hẹn giờ 40–90 phút và thử lại 2 phút, điều kiện chặn, đường bay/bò, chỗ slime dừng, nhịp đuổi tối đa 150 DIP/3 giây, đếm số lần.
- UiChecks: một lượt mỗi khách, click khách, click/kéo mèo giữa lượt, ngủ, Pause, Quiet, Giờ tập trung, fullscreen, công tắc tắt, mép cửa sổ; sleep lock, wake, drag, throw, emotion priority, local fallback.
- Soát render bướm, slime, hạt sáng, mèo nhìn theo/vồ hai hướng, nền sáng/tối; chữ vi/en/ja; đo FPS khi khách bay.

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

App được tự hiện khách (bướm giấy, slime hiền) không physics, không chạm mèo, cửa sổ riêng click xuyên qua; mèo được tự quay nhìn, đuổi ngắn và vồ tại chỗ. Sự kiện là hành vi tự động, có công tắc tắt.

### Cách code M4 (12/11)

- Core `PetGuests.cs`: `GuestRules` (hẹn 40–90 phút, thử lại 120 s, cỡ khách, tốc độ chọn sao cho một lượt 8–15 s, `ChaseTarget` giữ mèo trên đúng sàn/mép và cách slime ≥ 50 DIP), `Guest` (đường bay/bò, dừng chào, mờ dần khi bị đuổi đi, click, tối đa 30 s) và `GuestWatcher` (50 % số lượt mèo đi theo, ≤ 150 DIP hoặc 3 s, vồ bướm một lần). `AutoKind.GuestVisit` chặn như `ToyInvite`. `preferences.json` thêm `Guests` (mặc định bật), `GuestVisits`.
- `PetWindow.Guests.cs`: `TickGuests` chạy mỗi frame từ `Tick` (Pause truyền dt 0 nên khách đứng yên): hẹn giờ chỉ trong RAM; khách mờ rồi mất khi mèo ngủ, toàn màn hình, trận/bảng trận, tắt công tắc; mất ngay khi đổi màn hình/vùng làm việc/DPI. `TickGuestWatch` trong `TickFeatures` sau mini-game: mèo nhìn theo (`Looking`, nghiêng đầu theo độ cao khách), đi theo, vồ bằng `renderer.TeleportDrop` như M3 (`pet.Y` không đổi). Lúc mèo nhìn, `FeatureOwnsMovement` đúng nên PetBehavior và lệnh Python chờ; `CancelFeatureActivity` (click/kéo mèo, cảm xúc chiếm mèo, bắt đầu việc khác) chỉ dừng việc nhìn, khách vẫn đi. Hook UiChecks: `TryGuestVisit(kind, follows)`.
- **Quyết định khi code (chờ người dùng xem):** luật ghi slime "nhún chào rồi bò tiếp… ra mép bên kia", nhưng khi mèo đứng dưới sàn thì bò tiếp sẽ xuyên qua thân mèo, trái luật "không chạm mèo". Code cho slime quay lại đi ra mép nó đến khi mèo ở sàn; mèo ở trên mép cửa sổ thì slime bò tiếp sang mép bên kia. Mèo bị thả/ném chắn đường thì slime dừng chào hoặc quay đi, không bao giờ bò vào mèo.
- Vẽ `PetGuestArt.cs`: bướm giấy nhìn chính diện (cánh trên Rose nhạt, cánh dưới Lilac nhạt, nếp gấp giấy, thân và râu ArtInk), slime vòm bạc hà có mầm lá, má hồng, mắt cười khi chào; nét ArtInk 1,8. Mỗi khách một cửa sổ nhỏ di chuyển theo, giữ dưới cửa sổ mèo; chỉ đĩa gần trong suốt quanh khách nhận click.
- Test: `GuestTests` (Core), `GuestChecks` (UiChecks: hẹn giờ và thử lại khi ngủ, một lượt bướm có đi theo và vồ, một lượt slime không đi theo, click khách, kéo mèo, cảm xúc, ngủ, Pause, Quiet, Giờ tập trung, toàn màn hình, mini-game, công tắc, mép cửa sổ); `AutonomyTests` thêm `GuestVisit`.

## Luật M5 Săn kho báu (duyệt 12/11)

Hai phần: **mèo tìm thấy bản đồ** là sự kiện tự xảy ra (như M4); **đi săn** là lệnh người dùng (như mini-game M1–M3). Mèo không bao giờ tự bắt đầu một ván. Người dùng chọn 12/11: hẹn giờ riêng 60–120 phút, tối đa 3 bản đồ/ngày; ván không thua; mẩu chuyện viết lúc code rồi người dùng duyệt sau.

### Mèo tìm thấy bản đồ (tự động)

- Hẹn giờ riêng 60–120 phút, chỉ trong RAM, lần đầu tính từ lúc mở app; tối đa 3 bản đồ mỗi ngày (theo ngày giờ máy, đếm lưu ở C#). Bị chặn thì 2 phút sau thử lại.
- Chặn như khách M4: mèo ngủ (kể cả away nap) hoặc waking, Quiet, Giờ tập trung, app toàn màn hình, trận hoặc bảng chiến đấu, Pause, người dùng vắng, đang chơi mini-game, khách đang ghé, mèo bận (bị kéo, bay, phản ứng), cảm xúc ưu tiên cao, tính năng khác đang giữ mèo, wake cooldown.
- Mèo ngồi xuống, ngậm một mẩu giấy cuộn có dấu X, ngước nhìn khoảng 2 giây (hoạt cảnh, không phải cảm xúc), rồi làm việc như thường; trên đầu mèo có biểu tượng bản đồ nhỏ.
- Bản đồ giữ 10 phút (Pause đóng băng thời gian này); bỏ qua thì mờ dần rồi mất, không mất gì. Thoát app thì bản đồ mất (không lưu).
- Công tắc "Săn kho báu" trong Cài đặt → Thói quen, mặc định bật; tắt thì bản đồ đang có mất ngay.

### Bắt đầu và thoát (lệnh người dùng)

- Bắt đầu bằng click biểu tượng bản đồ, hoặc dòng "Săn kho báu" trong "Chơi cùng mèo" (menu chuột phải và menu khay). Chưa có bản đồ thì dòng mờ kèm "chưa có bản đồ"; các lý do mờ khác như M1. Click biểu tượng không tính là click mèo.
- Luật mở/thoát chung của M1: chạy cả Quiet, Giờ tập trung, wake cooldown; không đánh thức mèo (chờ tự thức tối đa 30 giây); Esc, chọn lại dòng menu, click trái/kéo mèo thoát; fullscreen, vào trận, rút màn hình, đổi DPI kết thúc ngay. Click ra chỗ trống không thoát (vùng chơi rộng, dễ bấm nhầm).
- Một ván tối đa khoảng 3 phút. Thoát giữa chừng thì bản đồ còn tới hết 10 phút; săn lại thì dấu chân đặt lại từ đầu.
- Trong ván, hành vi tự động, lệnh Python, cảm xúc tự động và khách M4 không chen vào. Mỗi ván gửi `toy_started` sẵn có một lần.

### Ba dấu chân ẩn

- Ba dấu chân mèo đặt ngẫu nhiên cùng màn hình với mèo, trong khoảng ±400 DIP ngang quanh mèo, từ sàn lên tối đa 300 DIP, cách nhau ≥ 80 DIP, trong vùng làm việc; không đè lên mèo, vùng đất nông trại, xe hàng, đồ chơi, đồ góc sống.
- Dấu chân rất mờ (khoảng 20 % độ đậm); con trỏ tới gần khoảng 50 DIP thì sáng dần. Click trúng thì dấu sáng hẳn, nhãn "1/3", "2/3" trên đầu mèo; mèo đi tới dưới dấu đó (trên đúng sàn/mép đang đứng, không nhảy khỏi mép) và đánh hơi khoảng 1 giây. Tìm theo thứ tự bất kỳ. Thời gian đi tối đa ít nhất 10 s, xa hơn thì theo quãng đường cộng 2 s (`TreasureRules.WalkLimit`, sửa 14/11: hai dấu có thể cách ~800 DIP nên trước đó mèo đôi khi dừng thiếu).
- Gợi ý: 20 giây chưa tìm thêm dấu nào thì mèo quay đầu về dấu gần nhất; 40 giây thì mèo đi tới gần dấu đó và dấu sáng nhẹ hơn mức thường.
- **Không thua**: hết 3 phút mà chưa đủ thì các dấu còn lại tự sáng lên, mèo dẫn tới rương như đã tìm đủ.
- Dấu chân vẽ trong cửa sổ trong suốt riêng không chiếm focus; chỉ hình dấu chân (và rương) nhận click, chỗ trống click xuyên qua như M3/M4.

### Mở rương

- Đủ 3 dấu: rương nhỏ hiện kèm khói nhẹ trên sàn/mép mèo đang đứng, gần dấu cuối; mèo đi tới đứng cạnh (không chạm rương).
- Click rương: mèo vỗ nắp một cái, nắp bật mở kèm hạt sáng, một tờ giấy bay lên hiện **một mẩu chuyện** 1–2 câu (vi/en/ja) trong nhãn PetUi khoảng 8 giây; click tờ giấy thì tắt sớm. Không click rương trong 30 giây thì mèo tự vỗ mở.
- Khoảng 12 mẩu chuyện viết sẵn về những chuyến phiêu lưu nhỏ của mèo trên bàn làm việc, không lặp tới khi đã mở hết, hết thì xoay vòng lại. Mẩu chuyện viết lúc code, người dùng duyệt sau.
- Không vật phẩm, xu, thưởng (để Đợt 4); không âm thanh mới. Phản ứng của mèo là hoạt cảnh, không phải cảm xúc.

### Click, kéo và các luật bất biến

- Dấu chân, rương, tờ giấy không physics, không chạm mèo, không đánh thức mèo, chỉ có trong ván, không lưu trên desktop.
- Click/kéo mèo kết thúc ván; kéo quá ~6 DIP vẫn nhấc mèo như thường. Click phải mở menu như thường.
- Pause đóng băng cả ván (đồng hồ 3 phút, gợi ý, rương). Mèo ngủ không thể xảy ra trong ván (hành vi tự động bị chặn); fullscreen, trận, đổi DPI, rút màn hình kết thúc không tính rương.

### Lưu và Python

- `preferences.json` thêm công tắc, số rương đã mở, danh sách id mẩu chuyện đã mở, số bản đồ hôm nay (kèm ngày); file cũ đọc được.
- Nhật ký thêm thẻ "Kho báu": số rương đã mở và các mẩu chuyện đã mở, đọc lại được.
- Python không biết về bản đồ, không giao thức mới; Python tắt thì vẫn có bản đồ và săn được.

### Khi code cần kiểm tra

- Luật thuần có unit test: hẹn giờ 60–120 phút, thử lại 2 phút, giới hạn 3/ngày và qua ngày, 10 phút giữ bản đồ (Pause đóng băng), điều kiện chặn, đặt 3 dấu (khoảng cách, vùng làm việc, tránh đồ/nông trại), mốc gợi ý 20/40 giây, hết 3 phút tự hoàn thành, chọn truyện không lặp, đọc file cũ.
- UiChecks: tìm bản đồ và bị chặn khi ngủ/Quiet/fullscreen; bắt đầu từ biểu tượng và menu; một ván đủ, ván hết giờ; click/kéo mèo giữa ván; Esc; Pause; fullscreen; công tắc tắt; mép cửa sổ; rương tự mở; sleep lock, wake, drag, throw, emotion priority, local fallback.
- Soát render mèo ngậm bản đồ, biểu tượng, dấu chân mờ/sáng, rương đóng/mở, tờ giấy, thẻ Nhật ký; nền sáng/tối; chữ vi/en/ja; đo FPS trong ván.

### Bổ sung ngoại lệ (đã duyệt 12/11, bản đầy đủ ở docs/APPROVED-EXCEPTIONS.md → Hành vi mèo)

Mèo được tự "tìm thấy bản đồ" (hoạt cảnh ngồi ngậm giấy ~2 s, biểu tượng trên đầu 10 phút), là hành vi tự động có công tắc; đi săn là lệnh người dùng theo ngoại lệ mini-game M1, dấu chân và rương vẽ trong cửa sổ riêng click xuyên qua, mèo đi tới dấu đã tìm và vỗ mở rương.

### Cách code M5 (12/11)

- Core `PetTreasure.cs`: `TreasureRules` (hẹn 60–120 phút, thử lại 120 s, tối đa 3 bản đồ/ngày theo ngày máy, `Place` đặt 3 dấu chân ±400 DIP ngang và tới 300 DIP trên mặt đất, cách nhau ≥ 80, trong vùng làm việc, tránh thân mèo và đồ trên sàn; `Glow` 20 % → sáng dần trong 50 DIP quanh con trỏ, 40 % khi là gợi ý 40 s, 100 % khi tìm thấy; `Under`, `ChestSpot`, `StandSpot` giữ mèo và rương trên đúng sàn/mép; `PickStory`/`OpenChest` chọn truyện chưa mở, mở hết thì quay vòng theo thứ tự cũ), `TreasureMap` (10 phút, dt 0 khi Pause, mờ 20 s cuối), `TreasureHunt` (thứ tự bất kỳ, gợi ý 20/40 s tính từ lần tìm gần nhất, 3 phút tự sáng phần còn lại). `AutoKind.TreasureFind` chặn như `GuestVisit`. `preferences.json` thêm `Treasure` (mặc định bật), `TreasureChests`, `TreasureStories` (id đã mở, mới nhất cuối), `TreasureMapsToday`, `TreasureMapDay`; file cũ đọc được.
- `PetWindow.Treasure.cs`: `TickTreasure` chạy mỗi frame từ `Tick` (Pause truyền dt 0): hẹn giờ chỉ trong RAM và đứng yên khi đang có bản đồ; công tắc tắt thì bản đồ (và ván) mất ngay. Cảnh tìm thấy (`TickTreasureFind`, 2 s ngồi, `renderer.CarryMap`, ngước nhìn bằng `ReactTilt`) là việc chiếm mèo như khách M4, bị click/kéo/cảm xúc cắt thì bản đồ vẫn còn. Biểu tượng bản đồ là cửa sổ nhỏ riêng trên đầu mèo, giữ trên cửa sổ mèo để click không bao giờ rơi vào mèo; ẩn khi kéo mèo, toàn màn hình, trong trận.
- Ván săn theo khung M1: chờ mèo tự thức tối đa 30 s, `toy_started` một lần, cảm xúc tự động tạm dừng, `FeatureOwnsMovement` đúng nên PetBehavior, Python và khách M4 không chen vào; Python thấy trạng thái "MiniGame". Khác M1 theo luật: Pause đóng băng (TickFeatures không chạy), click chỗ trống không thoát, chỉ Esc/chọn lại dòng/click hoặc kéo mèo/toàn màn hình/trận/đổi màn hình-DPI/mặt đất đổi kết thúc. Click phải mèo cũng kết thúc như mọi mini-game (qua `CancelFeatureActivity`) rồi mở menu như thường.
- Dấu chân và rương vẽ trong một cửa sổ dưới cửa sổ mèo (`PetTreasureArt.cs`), chỉ đĩa gần trong suốt sau dấu chưa tìm và sau rương nhận click. Mẩu chuyện là thẻ PetUi riêng (minh họa Scroll), click tắt sớm. Không thêm âm thanh: dùng `sparkle` và `ball_tap` sẵn có. 12 mẩu chuyện vi/en/ja nằm trong `TreasureStoryText` (viết lúc code, **chờ người dùng duyệt nội dung**).
- **Quyết định khi code (chờ người dùng xem):** đồng hồ 10 phút của bản đồ vẫn chạy trong ván (ván không bị cắt khi hết giờ, rương vẫn mở được); hẹn giờ tìm bản đồ mới không chạy khi đang giữ bản đồ; mép cửa sổ quá ngắn không đủ chỗ cho rương cạnh mèo thì không vẽ rương, mèo vỗ mở tại chỗ và mẩu chuyện vẫn hiện; hết 3 phút thì rương đặt gần dấu cuối cùng tự sáng.
- Test: `TreasureTests` (Core), `TreasureChecks` (UiChecks), `AutonomyTests` thêm `TreasureFind`; ảnh soát `treasure-*.png` khi đặt `PET_RENDER_REVIEW`.
