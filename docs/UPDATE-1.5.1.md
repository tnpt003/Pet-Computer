# Bản 1.5.1 — Đời sống ngoài trận

## Phạm vi đã chốt (người dùng duyệt 2026-11-08)

Mục tiêu: mèo ngoài trận đa dạng và thú vị hơn. Ba tính năng chủ lực; kho ý tưởng cũ ở phần sau vẫn chỉ là ứng viên.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| 1. Portal | Cổng tự xuất hiện 20–40 phút/lần, mèo bất ngờ; menu "Niệm phép cổng"; bấm cổng mới vào Sảnh | Xong (07abbba), còn test tay |
| 2. Weather | Thời tiết theo màu wallpaper Windows, hiện vùng quanh mèo | Xong (f7f406e), còn test tay |
| 3. Ruộng cơ bản | Dải 6 ô, cày → gieo → tưới → lớn → thu hoạch, 4 cây, lưu file riêng, di chuyển mảnh đất | Xong (9fc249b), còn test tay |
| 4. Mèo làm cùng | Mèo tự tưới, gặt, đuổi chim; khóa khi ngủ/kéo/Quiet | Xong (caeb0f9), còn test tay |
| 5. Kinh tế farm | Kho, bán lấy xu nông trại riêng, mua hạt, mở ô, bù nhìn | Xong (4d4330e), còn test tay |
| 6. Nối các phần | Nấu món cho mèo ăn, mưa tự tưới, nắng cho cây lớn nhanh hơn một chút | Xong (cadeb1c), còn test tay |

### Portal (mốc 1)

- Tự xuất hiện trên sàn cạnh mèo mỗi 20–40 phút; chờ bấm 30 giây rồi tự đóng, không mất gì. Mèo Surprised và nhìn về phía cổng.
- Không tự hiện khi: mèo ngủ/thức dậy/bị kéo/bay/đứng trên cửa sổ, đang chơi hoặc ăn, Pause, Quiet, app toàn màn hình, người dùng vắng, đang có trận hoặc bảng chiến đấu. Sai thời điểm thì thử lại sau 45 giây, không dồn lượt.
- Menu chuột phải: ngoài trận là "Niệm phép cổng" (mèo ngồi giơ hai chân 1,6 giây, sao xoay quanh, rồi cổng hiện, chờ bấm 45 giây). Có bảng chiến đấu đang mở thì mục về lại "Chế độ chiến đấu" và mở Sảnh. Trong trận vẫn là "Chế độ chiến đấu" với Tạm dừng/Kết thúc.
- Niệm phép do người dùng bật nên chạy cả trong Quiet (như bóng/laser); app toàn màn hình vẫn chặn. Click/kéo/ném mèo hủy phép; phản ứng cảm xúc thật làm phép chờ.
- Bấm cổng mới mở Sảnh; cổng không tự bắt đầu trận và không giành focus. Cổng đóng khi mèo ngủ (không đánh thức), khi mở bảng/trận, tức thì khi có app toàn màn hình hoặc vùng làm việc đổi.

### Weather (mốc 2)

- "Background image" là wallpaper Windows của màn hình có mèo. Chỉ lấy màu chủ đạo từ ảnh thu nhỏ, không lưu ảnh, không gửi đi đâu; cần ghi ngoại lệ khi làm.
- Loại thời tiết chỉ đổi khi đổi nền (đã thay 09/11: wallpaper là khí hậu chính, có đợt xen kẽ, xem docs/FARM-GAME.md Mốc 5): xanh dương sáng = nắng/mây, xám = mưa, trắng/xanh lạnh = tuyết, cam/hồng = hoàng hôn/lá bay, xanh lá = phấn hoa/gió, tối = đêm sao/đom đóm. Không đọc được thì coi là trời quang.
- Cường độ thay đổi theo thời gian (mưa to, nhỏ, tạnh một lúc) để không đơn điệu.
- Chỉ hiện vùng quanh mèo, không phủ cả màn hình. Ẩn trong trận, Quiet, toàn màn hình; không đánh thức mèo; đứng yên khi Pause. Đo FPS trước khi mở rộng.
- Người dùng duyệt thêm 08/11: công tắc "Thời tiết quanh mèo" trong Cài đặt → Thói quen (mặc định bật); ẩn cả khi Sảnh, bảng chuẩn bị hoặc bản đồ đang mở.
- Hình: nắng là tia lấp lánh và mây (càng mạnh càng nhiều mây); mưa; tuyết viền xanh để thấy được trên nền trắng; lá bay; phấn hoa và vệt gió; sao và đom đóm. Vùng rộng khoảng 3 lần mèo (320–520 DIP), mờ dần ra rìa, cắt tự nhiên ở mép vùng làm việc. Hạt tính theo tọa độ màn hình nên không bị kéo theo mèo.
- Đọc lại wallpaper khi Windows báo đổi nền, khi mèo sang màn hình khác và mỗi 60 giây (bắt slideshow); file không đổi thì không giải mã lại. Mỗi đợt cường độ dài 40–140 giây: mạnh, vừa, nhẹ hoặc tạnh.
- Đo 08/11 trên bản Release, mưa ở cường độ khởi đầu 0,6, cửa sổ 358×304 DIP, 2 vòng 30 giây: CPU 0,88/0,83% so với 0,68/0,74% khi tắt (% của toàn bộ nhân), FPS trung vị 57,2/58,0 so với 58,9/56,8. Chênh FPS nằm trong sai số giữa hai vòng.
- Đo hoặc xem một loại bất kỳ không cần đổi nền: đặt `PET_WEATHER_PROBE=Rain` (Sunny, Rain, Snow, Sunset, Pollen, Night), log có thêm dòng Frame stats.

### Farm (mốc 3–6)

- Dải 6 ô sát mép trên taskbar. Cây không héo khi vắng mặt, chín rồi thì chờ mãi. Không cộng sức mạnh trong trận.
- **Di chuyển mảnh đất (người dùng yêu cầu 08/11):** chuột phải vào mảnh đất → "Di chuyển mảnh đất farm" → mảnh đất rung lên như bị lực vô hình nhấc, đất vụn văng và rụng, rồi cả mảnh và 6 ô bay lơ lửng. Người dùng nhấn giữ để kéo mảnh đất sang trái/phải (chỉ theo chiều ngang). Đến nơi muốn đặt, chuột phải → chọn điểm rơi → mảnh đất khựng lại rồi rơi nhanh, đáp "đùng" có đất bắn ra (đổi theo góp ý 08/11, thay cho "rơi từ từ") và nằm đúng vị trí đã chỉ định. Lưu vị trí theo monitor-relative như vị trí mèo.

#### Luật Mốc 3 (người dùng duyệt 08/11)

- Bấm trái vào ô làm bước kế tiếp: cày → chọn hạt → tưới → chờ → thu hoạch. Thu hoạch xong ô về đất hoang. Rê chuột hiện nhãn, thanh tiến độ và thời gian còn lại.
- Hạt → mầm → cây non → chín. Mỗi giai đoạn trước khi chín cần tưới một lần mới chạy; chưa tưới thì đứng chờ (có giọt nước trên ô), chín thì có sao vàng. Cây lớn theo đồng hồ thật, kể cả khi tắt app hoặc Pause, nhưng chỉ qua giai đoạn đã tưới. Đặt lùi đồng hồ không kéo dài quá một giai đoạn.
- Tổng thời gian khi tưới đúng lúc: Củ cải 5 phút, Cà rốt 15 phút, Cà chua 40 phút, Bí ngô 2 giờ, chia đều 3 giai đoạn.
- Từ Mốc 5 hạt có giá, ruộng mới có 3 ô mở và thu hoạch vào kho (xem Luật Mốc 5).
- Lưu ở `farm.json` trong thư mục dữ liệu (có `.bak`): các ô, số đã thu hoạch, màn hình và vị trí theo tỉ lệ. Tệp của bản mới hơn thì không ghi đè.
- Mặc định đặt ở 25% bề ngang màn có mèo; công tắc "Ruộng trên desktop" ở Cài đặt → Thói quen (mặc định bật), tắt thì ẩn, cây giữ nguyên.
- Ẩn khi app toàn màn hình hoặc trong trận; mảnh đất đang nhấc thì về chỗ cũ. Pause không nhận bấm hay di chuyển. Khi nhấc, chuột phải có "Thả xuống đây" và "Hủy" (trượt trên không về chỗ cũ tối đa 0,6 s rồi mới rơi). Kéo không vượt sang màn hình khác; màn bị rút thì về màn chính.
- Bấm ruộng không chạm vào mèo: bấm khi mèo ngủ không đánh thức, không đổi sleep, drag, emotion. Mèo đi phía trước ruộng và không coi ruộng là bục đứng.

#### Luật Mốc 4 (người dùng duyệt 08/11)

- Mèo tự đi làm vườn bằng logic C# local (không qua Python, tắt Python vẫn chạy) và không tính là tương tác của người dùng. Công tắc "Mèo làm vườn cùng" ở Cài đặt → Thói quen, mặc định bật.
- (Nhịp và việc cày, gieo đã thay bằng luật Mốc 1 trong docs/FARM-GAME.md.) Mỗi 2–4 phút một chuyến, một ô: ô chín gần nhất, không có thì ô cần tưới gần nhất. Mèo không cày, không gieo. Không có việc hoặc sai thời điểm thì thử lại sau 45 giây, không dồn lượt.
- Mèo đứng cạnh ô, cách tâm ô 58 DIP. Tưới 2 giây cầm bình, gặt khều chân; kết quả giống hệt bấm tay (lưu file, "+1 Tên cây"). Tới nơi mà ô đã được làm thì thôi. Sau 20 giây đi bộ chưa tới thì bỏ chuyến.
- Chim giấy khoảng 6–12 phút một lần, đậu cạnh một ô có cây, phía xa mèo. Mèo nhìn 0,7 giây, rón rén (60% tốc độ), thu người rồi vồ, chim bay đi. Chim tự bay sau 30 giây, khi ô hết cây hoặc khi mảnh đất bị nhấc. Chim không gây thiệt hại: không mất cây, không chậm lớn; bù nhìn ở Mốc 5 sẽ làm chim đến ít hơn.
- Không đi và không có chim khi: mèo ngủ/thức dậy/bị kéo/có cú nhấn có thể thành kéo/bay/ăn/có cảm xúc chiếm mèo, đang bận hoạt động khác, Pause, Quiet, app toàn màn hình, người dùng vắng, trận, Sảnh/bảng chuẩn bị/bản đồ, mèo đứng trên cửa sổ, ruộng khác màn hình mèo, ruộng ẩn hoặc đang nhấc. Điều kiện nào xuất hiện giữa chuyến cũng hủy chuyến ngay và không đổi tư thế của mèo đang bị giữ, đang bay hay đang ngủ.

#### Luật Mốc 5 (người dùng duyệt 08/11)

- Thu hoạch, bằng tay hay do mèo làm, đều vào kho và vẫn hiện "+1 Tên cây". `Harvested` vẫn là tổng đã thu hoạch, bán không làm giảm.
- Xu nông trại tách hẳn xu trong trận, không cộng sức mạnh chiến đấu. Vốn đầu 20 xu, tối đa 999.999 xu.
- Trả xu ngay lúc gieo, không có kho hạt. Menu gieo hiện giá và làm mờ loại chưa đủ xu. Giá hạt/giá bán: Củ cải miễn phí/3, Cà rốt 4/13, Cà chua 10/30, Bí ngô 25/85. Củ cải miễn phí nên không bao giờ bị kẹt vốn.
- Ruộng mới mở sẵn 3 ô từ bên trái. Ô 4, 5, 6 giá 40, 120, 300 xu và phải mở theo thứ tự. Ô khóa có rào và ổ khóa, rê chuột hiện giá, bấm thì mở bảng; mèo và chim không đụng vào ô khóa.
- Bù nhìn 150 xu, mua một lần, đứng ở đầu phải mảnh đất. Khoảng chờ giữa hai lần chim đến gấp đôi, thành 12–24 phút; lần thử lại khi sai thời điểm vẫn 45 giây.
- Chuột phải vào mảnh đất đang nằm dưới đất có "Kho & cửa hàng": xem xu, bán từng cái hoặc bán hết, mở ô, mua bù nhìn.
- Bảng không chạm vào mèo và không đánh thức mèo. Pause làm mờ mọi nút. Bảng tự đóng khi ruộng ẩn (app toàn màn hình, trận, tắt công tắc) hoặc khi Sảnh, bảng chuẩn bị, bản đồ mở; lúc đó menu cũng không có mục này.
- `farm.json` phiên bản 2. File phiên bản 1 khi mở: nhận 20 xu, số đã thu hoạch chuyển vào kho, ô có cây hoặc đã cày vẫn mở, chỉ khóa các ô đất hoang phía phải, luôn còn ít nhất 3 ô. Bản app cũ thấy file phiên bản 2 thì chỉ đọc, không ghi đè.
- Giá hiện là ước tính, chưa qua chơi thử: mở hết ô và mua bù nhìn tốn 610 xu.

#### Luật Mốc 6 (người dùng duyệt 08/11)

- Chỉ thời tiết đang thật sự hiện quanh mèo mới có tác dụng, và chỉ khi ruộng cùng màn hình với mèo (wallpaper đọc theo màn có mèo). Tắt thời tiết, Quiet, Pause, app toàn màn hình, trận thì không có. Tắt app thì cây lớn tốc độ thường, không tính mưa hay nắng.
- **Mưa:** mưa đang rơi (cường độ từ 0,1, lúc tạnh không tính) đủ 15 giây liên tục thì mọi ô đã mở đang chờ nước được tưới, cả ruộng dù vùng mưa chỉ quanh mèo. Có hiệu ứng nước, không phát tiếng, không chạm vào mèo. Mưa tạnh thì đếm lại từ đầu. Mảnh đất đang nhấc thì chờ đáp rồi mới tưới. Mèo đang đi tưới ô đó thì bỏ chuyến.
- **Nắng:** khi đang hiện trời nắng, mỗi giây nắng đưa mốc chín lại gần thêm 0,25 giây; nắng suốt thì một giai đoạn còn 80% thời gian. Không phụ thuộc cường độ. Wallpaper không đọc được bị coi là trời quang nên vẫn có nắng. Lưu `farm.json` khoảng 30 giây một lần và khi đóng app. Tuyết, hoàng hôn, phấn hoa, đêm không có tác dụng (đã thay 09/11, xem docs/FARM-GAME.md Mốc 5).
- **Nấu ăn:** chuột phải vào mảnh đất → "Nấu ăn", mở thay chỗ "Kho & cửa hàng". Nấu trừ nguyên liệu trong kho, không tốn xu, không giảm số đã thu hoạch, mèo ăn ngay, không có kho món. Công thức: Súp củ cải 2 Củ cải; Cà rốt hầm 2 Cà rốt; Cà chua nhồi 1 Cà chua + 1 Củ cải; Bánh bí ngô 1 Bí ngô + 1 Cà rốt.
- Món nấu đi chung luồng "Cho ăn": chung hồi 20 giây, được làm trong Quiet; mèo ngủ, bận, đang ăn hoặc Pause thì nút mờ, không trừ đồ và không đánh thức mèo. Python giữ nguyên, mỗi món tính là một lần cho ăn. Khác với "Cho ăn" thường: món được vẽ riêng có hơi nóng và mèo Happy 6 giây thay vì 3 giây (qua hệ cảm xúc có sẵn, không đè giận). Nhấc mèo khi đang ăn thì bữa dừng như "Cho ăn" thường và món đã nấu mất.
- Không cộng sức mạnh trong trận. `farm.json` giữ phiên bản 2. Các con số là ước tính, chưa qua chơi thử.

---

# Kho ý tưởng mở rộng

Cập nhật: 2026-10-27 (Asia/Saigon).

Người dùng yêu cầu lưu toàn bộ ý tưởng đã trao đổi cho bản 1.5.1. Đây là **kho ý tưởng ứng viên**, chưa chốt thiết kế, chưa duyệt triển khai tất cả và chưa đặt tên chính thức cho bản cập nhật. Các ý gần nhau được gom chung nhưng giữ lại những cách chơi đã nêu.

Theo yêu cầu người dùng, các thảo luận này chỉ lưu tại đây, **không đưa vào PROGRESS.md**, để không lẫn với công việc 1.5 đang được thực hiện. Không thay kế hoạch trong [UPDATE-1.5.md](UPDATE-1.5.md).

## Hướng sản phẩm

- Vòng trải nghiệm đề xuất: cùng mèo phiêu lưu → mang chiến lợi phẩm về → trang trí góc sống → mèo sử dụng đồ và tạo kỷ niệm mới.
- Sau bản 1.5 tập trung combat, có thể ưu tiên đời sống desktop, quan hệ và hoạt động cùng người dùng.
- Mỗi đồ vật nên tạo hành vi hoặc cách chơi riêng, không chỉ thêm hình trang trí.
- Không tạo áp lực đăng nhập hằng ngày, mất tiến trình hoặc trừng phạt khi người dùng vắng mặt.

## 1. Góc sống và đồ vật

| Ý tưởng | Nội dung đã thảo luận |
|---|---|
| Góc sống của mèo | Giường, thùng carton, trụ cào, đồ chơi và kỷ vật; bản đầu có thể giới hạn khoảng ba món cùng hiện |
| Thùng carton | Chui vào, thò đầu, nằm ngủ, vồ đồ chơi; hỗ trợ trốn tìm |
| Trụ cào | Mèo tới cào rồi vươn vai |
| Giỏ đồ chơi | Cất đồ không dùng; mèo lấy một món ra hoặc mang đồ về cất |
| Đồ vật yêu thích | Mèo thường quay lại món thích nhất dựa trên lịch sử chơi |
| Vị trí đồ có ý nghĩa | Bóng cạnh hộp tạo chuỗi chơi rồi chui hộp; đặt xa thì mèo mang tới |
| Bộ đồ theo chủ đề | Góc thám hiểm, tiệm bánh, trại nhỏ; đủ bộ mở hoạt cảnh, không cộng sức mạnh |
| Đồ vật nhiều trạng thái | Hộp mở/đóng, đèn sáng/tắt, giường có chăn; mèo phản ứng với thay đổi |
| Mèo sắp xếp đồ | Kéo đồ chơi về gần giường trong giới hạn người dùng cho phép |
| Lưu bố trí | Các bộ Góc ngủ, Góc chơi, Góc chiến lợi phẩm; chuyển nhanh giữa chúng |
| Chế độ xem góc sống | Tạm gom mèo và đồ vào vùng gọn để chơi rồi trả về bố trí đã lưu |
| Cất nhanh | Cất đồ trang trí bằng một thao tác, giữ mèo trên desktop |
| Chậu cây | Mèo ngửi, chạm lá, ngồi cạnh; cây đổi hình qua mốc chơi, không héo khi vắng mặt |
| Bảng ảnh | Ghim khoảnh khắc do app dựng từ nhân vật và đồ vật |

## 2. Quan hệ, cá tính và hành vi chủ động

| Ý tưởng | Nội dung đã thảo luận |
|---|---|
| Quan hệ phát triển | Biến bond thành hành vi mở khóa, không chỉ thanh điểm; chơi bóng nhiều thì chủ động rủ chơi bóng |
| Khoảnh khắc tin tưởng | Mở nằm ngửa, tựa vào đồ vật, ngủ gần điểm hẹn khi đủ thân thiết |
| Mèo đề nghị hoạt động | Mang bóng tới hoặc hiện món muốn chơi; người dùng chấp nhận hoặc bỏ qua |
| Mèo tha đồ | Ngậm bóng/đồ chơi nhỏ, mang tới rủ chơi hoặc mang về chỗ cất |
| Chuỗi hành động tự nhiên | Chơi xong liếm chân, vươn vai rồi đi nghỉ |
| Tâm trạng qua dáng đi | Bước vui hoặc cúi thấp tò mò; đề xuất chỉ thay cách thể hiện, giữ luật physics |
| Nghi thức riêng | Cách chào, chuẩn bị ngủ, ăn mừng đặc trưng cho từng mèo |
| Chào lúc quay lại | Hoạt cảnh ngắn khi người dùng trở lại máy, tùy quan hệ |
| Học tín hiệu gọi | Bấm nút gọi, mèo quay đầu rồi tới điểm hẹn khi rảnh |
| Dạy động tác | Mở vẫy tay, xoay vòng, nằm ngửa qua tương tác chủ động; gọi từ Làm ngay |
| Lựa chọn của mèo | Đặt hai đồ chơi, mèo chọn một; sở thích thay đổi nhẹ theo lịch sử |
| Tò mò với đồ mới | Lần đầu tiếp cận chậm, ngửi, chạm thử rồi sử dụng |
| Phản ứng khi chơi hụt | Bắt hụt thì ngơ ngác, nhìn quanh hoặc thử lại |
| Phản ứng phụ kiện | Đội mũ mới thì soi mình, lắc đầu hoặc chỉnh mũ |
| Ký ức có biểu hiện | Trận thắng hoặc tương tác quan trọng tạo kỷ niệm và hành vi riêng; học có giới hạn, cho phép xóa dữ liệu học |

## 3. Đồ chơi mới

| Ý tưởng | Cách chơi |
|---|---|
| Cần câu lông vũ | Điều khiển bằng chuột; mèo rình, chụp hụt, bật nhảy rồi bắt |
| Chuột dây cót | Lên dây rồi thả; mèo rình và đuổi, chuột dừng sau một đoạn |
| Bóng lật đật | Đập thì nghiêng và bật lại; phản ứng bất ngờ khi bật gần mặt |
| Ống chui | Chui một đầu, ló đầu hoặc đuôi ở đầu còn lại |
| Túi giấy | Sột soạt khi chạm, mèo có thể núp bên trong |
| Đồ chơi giấu cá | Đẩy đúng chỗ thì lộ phần thưởng trong hoạt cảnh |
| Bong bóng xà phòng | Thổi một đợt ngắn để mèo chạm vỡ |
| Lông vũ rơi | Mèo nhìn và canh chụp gần sàn |
| Xe đồ chơi | Điều khiển trái/phải trong vùng nhỏ, mèo đuổi hoặc chặn đầu |
| Thảm sột soạt | Bước lên nhún nhẹ và phát tiếng, mèo quay lại kiểm tra |
| Đồ chơi ghép đôi | Bóng qua ống, chuột dây cót chạy tới hộp; hai món tạo cách chơi mới |

## 4. Mini-game và hoạt động phối hợp

| Ý tưởng | Cách chơi |
|---|---|
| Ném và nhặt đồ | Ném món đồ nhẹ, mèo chạy lấy rồi mang về; dùng chung nền với tha đồ |
| Úp cốc tìm cá | Ba cốc tự vẽ tráo chỗ, người dùng chọn, mèo kiểm tra và phản ứng |
| Đường bóng nhỏ | Đặt cọc/vòng và phối hợp đưa bóng tới đích |
| Trốn tìm với mèo | Mèo nấp sau đồ của app, lộ tai/đuôi, tìm đúng thì bật ra |
| Bắt đom đóm | Chạm các đốm sáng trong hoạt động ngắn, chúng tan thành hạt sáng |
| Đập tay | Click đúng nhịp tại bàn chân đang giơ |
| Chuyền bóng | Đẩy bóng tới mèo, mèo đánh trả, đếm lượt liên tiếp |
| Dẫn đường bằng đồ chơi | Dùng cần câu hướng mèo qua vài điểm |
| Dọn đồ cùng nhau | Chỉ món đồ để mèo mang về giỏ; người dùng chủ động bắt đầu |
| Chọn đúng món | Mèo nghĩ tới đồ chơi, đưa đúng thì có hoạt cảnh đáp lại |
| Bật nhảy bắt đồ | Thả đồ chơi để mèo canh nhảy, phần thưởng là hoạt cảnh đẹp |
| Chơi theo nhịp | Click trống theo nhịp, mèo gõ chân hoặc lắc đuôi |
| Đường đua đồ chơi | Đặt vật cản do app vẽ, mèo hoàn thành một lượt ngắn |

## 5. Sự kiện desktop và phiêu lưu không chiến đấu

| Ý tưởng | Nội dung |
|---|---|
| Hộp bí ẩn / bưu kiện | Mở cùng mèo để tìm đồ chơi hoặc mẩu chuyện; không biến thành quà đăng nhập |
| Khách ghé thăm | Chim giấy, bướm, slime hiền tương tác ngắn với mèo rồi rời đi |
| Bướm giấy bay qua | Mèo nhìn theo hoặc đuổi một đoạn |
| Mẩu bản đồ | Mèo tìm thấy bản đồ dẫn tới hoạt cảnh ngắn |
| Sự kiện có lựa chọn | Mở hộp cùng mèo hoặc để mèo tự khám phá, tạo hai hoạt cảnh khác nhau |
| Săn kho báu | Tìm ba dấu chân trong vùng chơi để mở rương cùng mèo |
| Mèo làm thám tử | Một món bị đổi chỗ, quan sát dấu vết và chọn nơi tìm |
| Giao bưu kiện | Mèo mang gói nhỏ tới nhân vật khách |
| Cứu đồ chơi mắc kẹt | Phối hợp bóng, cần câu hoặc công cụ mini-game để lấy đồ xuống |
| Khám phá chiếc vali | Mỗi ngăn mở một cảnh nhỏ và phản ứng của mèo |
| Chuyến đi qua đường hầm | Chọn lối trên bản đồ ngắn, gặp sự kiện, mang về lưu niệm |
| Tìm bạn trốn | Khách nấp trong đồ của app, mèo giúp chỉ dấu |
| Bí mật trong kỷ vật | Khám phá tương tác ẩn qua vài lần chơi |

Sự kiện đề xuất kéo dài ngắn, bỏ qua được và không giành focus. Có thể dùng màn chơi nhỏ thiết kế sẵn; không cần đọc nội dung ứng dụng hay file/icon thật của người dùng.

## 6. Nối Battle Mode với đời sống desktop

| Ý tưởng | Nội dung |
|---|---|
| Mô hình slime tương tác | Quái đã gặp có bản đồ chơi để trưng bày; chạm diễn động tác riêng, không mở trận |
| Kỷ vật có hoạt cảnh | Mèo ngắm hoặc diễn lại động tác liên quan trận thắng; đập thú bông Slime Nén làm nó bung rồi co lại |
| Album phiêu lưu | Lưu lần đầu thắng boss, build, di vật nổi bật bằng artwork của app; không chụp desktop |
| Sổ kỷ niệm | Các lần đầu có ý nghĩa bằng câu ngắn và hình, bổ sung nhật ký số liệu |
| Danh hiệu trang trí | Huy hiệu cạnh tên/hồ sơ, không tăng chỉ số |
| Xem lại / chạy lại build | Lưu cấu hình lượt thắng để xem và dựng trận thử khi thành phần tương ứng đã mở khóa |
| Thành tích cá nhân | So với lần trước về sát thương nhận, hiệu quả kỹ năng, thời gian phá liên kết |

## 7. Mở rộng combat từ nội dung 1.5

| Ý tưởng | Nội dung |
|---|---|
| Thử thách có seed | Cố định seed, vũ khí, bộ kỹ năng và luật; khởi đầu local, không cần tài khoản/bảng xếp hạng online |
| Bộ đồ cố định | Giải bài toán chiến thuật bằng kỹ năng và thẻ cho sẵn |
| Luyện đối phó quái | Từ Sổ tay vào tập cắt dây, né đạn, phá cơ chế; không mất tiến trình |
| Bài thi kỹ năng | Tình huống ngắn riêng cho Đẩy, Kẹp gắp, Kéo cắt… |
| Boss luyện theo pha | Tập một pha đã gặp thay vì đánh lại từ đầu |
| Mục tiêu phụ | Vừa thắng vừa bảo vệ đồ hoặc phá đủ liên kết |
| Thử thách cứu nguy | Dựng tình huống bất lợi để tìm cách xoay chuyển; cần khảo sát khả năng dựng trạng thái trận |
| Hồ sơ phong cách chơi | Tổng kết xu hướng giải cứu, dùng kỹ năng, ưu tiên mục tiêu |
| Gợi ý sau trận | Chỉ ra tình huống cụ thể, ví dụ dây xích tồn tại lâu, kèm bài tập phù hợp |
| Mã dựng trận | Xuất/nhập cấu hình Trận tự chọn bằng văn bản có kiểm tra hợp lệ |
| Chuỗi thử thách ngắn | Ba trận với lựa chọn giữa trận, ngắn hơn Viễn chinh |
| Huy chương cách thắng | Ghi nhận chiến thuật đa dạng thay vì chỉ tốc độ |

## 8. Cá nhân hóa, chia sẻ và tiện ích

| Ý tưởng | Nội dung |
|---|---|
| Tủ phối đồ | Lưu nhiều bộ lông/phụ kiện để đổi nhanh |
| Xem trước phần thưởng | Xem món đồ và hoạt cảnh trước khi chọn mục tiêu mở khóa |
| Hồ sơ mèo dạng thiệp | Tên, tính cách, món thích, ngày nhận nuôi, thành tích; xuất ảnh trên nền do app tạo |
| Hoạt động yêu thích | Ghim vài thao tác vào menu ngắn |
| Chế độ biểu diễn | Chọn chuỗi hoạt cảnh để giới thiệu mèo |
| Bộ sưu tập tư thế | Xem lại động tác đã mở khóa |
| Mục tiêu tự chọn | Ghim mục tiêu món đồ/bài tập, không hạn chót |
| Xuất/nhập dữ liệu | Sao lưu và khôi phục tiến trình có xem trước, không ghi đè âm thầm |

## 9. Hướng dài hạn đã nhắc, chưa chốt vào 1.5.1

- **Nhiều mèo:** đuổi bắt, ngủ cạnh nhau, tranh đồ chơi. Quy mô rất lớn, liên quan input, physics, AI, lưu dữ liệu, hiệu năng và Battle Mode. Đề xuất làm sau khi một mèo tương tác tốt với nhiều đồ vật.
- **Voice:** đọc bong bóng hoặc líu lo hoạt hình; là lớp thể hiện bổ sung. Thiết kế trước đó ở [NEXT-STEPS.md](NEXT-STEPS.md), vẫn cần duyệt thay đổi quy tắc chưa tích hợp voice; việc lưu ý tưởng này không cấp quyền triển khai voice/LLM.

## Các gói chủ đề từng đề xuất (chưa chọn)

| Gói | Nội dung chủ lực |
|---|---|
| Mang chiến lợi phẩm về nhà | Kỷ vật/mô hình slime tương tác, album phiêu lưu, danh hiệu |
| Chơi cùng sen | Tha đồ + ném và nhặt, hộp carton, phản ứng phụ kiện |
| Hiểu đối thủ / Học viện chống slime | Phòng luyện quái, bài thi kỹ năng, so thành tích, xem lại build |
| Mèo mở tiệm đồ chơi | Giỏ đồ, lấy đồ rủ chơi, chuyền bóng/nhặt đồ rồi cất về |
| Thám tử mèo | Dấu vết, đồ thất lạc, khám phá ngắn và sự kiện có lựa chọn |
| Góc nhà của Mochi | Góc sống, trang trí có tương tác và phát triển quan hệ |

Ưu tiên tư vấn đã nêu: góc sống → quan hệ → sự kiện desktop → thử thách combat → nhiều mèo. Với riêng 1.5.1, gói Chơi cùng sen/Mèo mở tiệm đồ chơi là lựa chọn được đề xuất sau bản 1.5 nặng combat; nếu tiếp tục khai thác quái mới thì chọn Mang chiến lợi phẩm về nhà. Đây chưa phải lựa chọn của người dùng.

## Trước khi chuyển thành kế hoạch triển khai

- Người dùng chọn tính năng chủ lực và phần đi kèm; danh sách dài này không đồng nghĩa cam kết làm hết trong một bản.
- Khảo sát code tại thời điểm bắt đầu, chia mốc và ghi tiêu chí hoàn thành/test cho tính năng được chọn.
- Giữ Cozy Desktop Adventure, artwork vector/procedural, style PetUi và chữ vi/en/ja.
- Giữ trách nhiệm: C# thực thi window/render/input/physics/âm thanh; Python quyết định hành vi cấp cao, mood, tính cách, memory; Python lỗi vẫn có fallback local.
- Mọi hành vi mới phải xử lý sleep lock, wake, drag/throw, emotion priority, Pause, Quiet và fullscreen theo quy tắc đã duyệt. Hoạt động người dùng bật cần kết thúc rõ ràng; không tự mở mini-game khi đang làm việc.
- Nhiều đồ vật làm tăng chi phí physics, lưu vị trí và điều phối quyền hành vi; cần đo và test thực tế trước khi tăng số lượng.
- Không sửa PROGRESS.md cho kho ý tưởng này theo yêu cầu người dùng. Khi bắt đầu làm một mốc, thống nhất cách ghi tiến độ để không xung đột phiên đang triển khai 1.5.

## Kết quả lưu tài liệu

- Đã gom các đề xuất trong cuộc trao đổi vào một file riêng; chưa triển khai tính năng.
- Chỉ thay đổi Markdown; kiểm tra diff/whitespace, không chạy build/test hoặc publish binary cho mốc tài liệu.
