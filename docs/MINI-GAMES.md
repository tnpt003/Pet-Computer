# Mini-game và sự kiện (bản 1.6, Đợt 3)

Người dùng duyệt chia mốc 2026-11-12. Luật từng mốc viết và duyệt trước khi code. Các con số là ước tính, chưa qua chơi thử. Ý tưởng gốc: docs/UPDATE-1.5.1.md → Kho ý tưởng mở rộng, mục 4 và 5.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| M1. Nền mini-game, Úp cốc tìm cá | Mục "Chơi cùng mèo" ở menu chuột phải và menu khay; luật chung (người dùng mở, có giới hạn thời gian, Esc/click ra ngoài thoát, kỷ lục trong Nhật ký). Úp cốc: 3 cốc tráo nhanh dần 3 vòng, người dùng chọn, mèo ngửi, lật cốc, phản ứng | Luật đã duyệt 12/11, đang code |
| M2. Trốn tìm | Mèo nấp sau đồ đang có trên desktop (thùng carton, chậu cây, trụ cào), không có thì app vẽ tạm bụi cây/rèm; lộ tai hoặc đuôi; tìm đúng thì mèo bật ra. Không đọc nội dung cửa sổ, icon hay file thật | Chưa viết luật |
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
