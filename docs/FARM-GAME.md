# Nông trại như game trồng trọt

Người dùng duyệt 2026-11-08. Mở rộng ruộng của bản 1.5.1 (docs/UPDATE-1.5.1.md, luật Mốc 3–6 vẫn giữ trừ chỗ ghi rõ dưới đây). Các con số là ước tính, chưa qua chơi thử.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| A. Thao tác nhanh | Cày, gieo, tưới, gặt cả ruộng từ menu | Xong (9f8bd67), còn test tay |
| B. Cấp nông trại | XP, 10 cấp, mở khóa cây và món | Xong (9fcf389), còn test tay |
| C. Đơn hàng | Chim giấy mang đơn, giao lấy xu và XP | Đã thay bằng Gian hàng lưu động (Giai đoạn 2, Mốc 4) |

## Giai đoạn 2: nông trại sống động (duyệt 08/11)

Mục tiêu: mèo không còn chỉ đi qua đi lại mà có việc liên tục như một nông trại nhỏ.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| 1. Nhịp làm việc | Mèo làm liên tục có nghỉ, tự cày và gieo lại | Xong (acfb3ee), còn test tay |
| 2. Bếp lửa | Nồi, que khuấy, lửa; mèo đổ nguyên liệu, khuấy, ăn | Xong (5bdea20), còn test tay |
| 3. Hồ nước | Mèo câu cá, 3 loại cá, món cá | Xong (6a0c93e), còn test tay |
| 4. Gian hàng lưu động | Tự xuất hiện như cổng, thay cho Đơn hàng | Xong (4b9178c), còn test tay |
| 5. Thời tiết gắn nông trại | Mỗi loại có tác dụng, chỉ có lợi | Xong (6e225b5), còn test tay |

Người dùng đã chốt: thời tiết lấy wallpaper làm khí hậu chính và xen kẽ loại khác mỗi 10–20 phút (thay luật "chỉ đổi khi đổi nền", áp dụng ở Mốc 5); mèo tự cày và gieo lại. Hồ và bếp lúc đầu gộp chung mảnh đất, đã tách thành vùng riêng ngày 09/11 (xem Luật ba vùng đất). Luật từng mốc viết và duyệt trước khi code.

### Luật Mốc 1 (thay nhịp và luật "không cày, không gieo" của 1.5.1 Mốc 4)

- Làm xong một chuyến thì nghỉ 15–40 giây rồi tìm việc tiếp; chờ tính từ lúc chuyến kết thúc. Hết việc hoặc sai thời điểm thì 45 giây xem lại. Sau 6 chuyến liên tiếp nghỉ dài 2–3 phút; một lần không có việc thì đếm lại. Lần đầu sau khi mở app khoảng 30 giây.
- Mỗi chuyến một ô, ô gần nhất. Ưu tiên: gặt, tưới, cày, gieo. Chim giấy giữ nguyên lịch.
- Mỗi ô nhớ cây vừa gặt (kể cả tự tay gặt). Mèo chỉ cày và gieo lại trên ô đó, đúng cây đó, trả xu như thường. Ô chưa từng trồng không đụng tới. Bạn tự tay cày thì ô thành việc của bạn, mèo không gieo. Thiếu xu hoặc cây bị khóa thì mèo bỏ qua ô.
- Công tắc "Mèo tự gieo lại" trong Cài đặt → Thói quen, mặc định bật; tắt thì mèo chỉ gặt và tưới. Tắt "Mèo làm vườn cùng" thì tắt cả nhịp này.
- Tiếng khi mèo tự làm cách nhau tối thiểu 60 giây; bấm tay vẫn có tiếng. Điều kiện chặn và hủy chuyến như 1.5.1 Mốc 4.

### Luật ba vùng đất (duyệt 09/11, thay các câu "nhấc và di chuyển cùng mảnh đất" của Mốc 2, Mốc 3)

- Ruộng, Bếp, Hồ là ba vùng đất riêng, mỗi vùng có vị trí, màn hình và lần nhấc riêng. Bếp và Hồ có bờ đất và viền cỏ như ruộng. Vị trí lưu trong `farm.json` (`HearthFractionX`, `PondFractionX`), vẫn phiên bản 3; lần đầu đứng ngay bên phải ruộng theo thứ tự cũ.
- Chuột phải: Bếp có "Nấu ăn", "Di chuyển bếp"; Hồ có "Câu cá", "Di chuyển hồ"; menu Ruộng giữ nguyên và chỉ nhấc ruộng. Chuyển động, hiệu ứng và tiếng y như ruộng: rung, đất vụn, bay lên, kéo ngang theo sàn, "Thả xuống đây" hoặc "Hủy", đáp có đất bắn.
- Mỗi lần chỉ một vùng ở trên không; vùng khác đang bay thì mục di chuyển mờ. Thả trúng vùng khác thì vùng đó trượt trên không tới khoảng trống gần nhất rồi mới đáp, cách nhau 6 DIP.
- Đang nhấc vùng nào thì chỉ hủy chuyến tới vùng đó. Nhấc bếp: nồi vẫn giữ món, không nấu được cho tới khi đáp. Nhấc hồ: không câu được. Nhấc ruộng: mèo vẫn nấu và câu.
- Xe hàng đứng ở khoảng trống gần bên trái ruộng nhất, trên màn hình của ruộng, không đứng lên vùng đất nào. Xe không đến khi có vùng đang nhấc.
- Sang màn hình khác (duyệt 09/11): vùng đang bay đi theo con trỏ qua ranh giới, tự lấy sàn và DPI của màn mới. "Hủy" sau khi đã sang màn khác thì vùng hiện lại trên không ở chỗ cũ rồi rơi xuống. Tránh chồng nhau chỉ xét các vùng cùng màn hình. Màn hình của Bếp và Hồ lưu trong `farm.json` (`HearthMonitor`, `PondMonitor`), dữ liệu cũ coi như cùng màn với Ruộng. Rút màn hình thì vùng về màn hình chính, giữ vị trí tương đối, không chồng lên vùng khác.
- Vùng khác màn hình với mèo: mèo không tự đi sang. Ruộng vẫn bấm tay được, mèo không tới làm. Bếp: "Nấu" thì mèo ăn tại chỗ, không tự nấu, món trong nồi chờ tới khi cùng màn. Hồ: "Câu cá" mờ, mèo không tự câu. Thời tiết chỉ tác dụng lên vùng cùng màn với mèo.

### Luật Mốc 2 (duyệt 08/11, thay luật "mèo ăn ngay" của 1.5.1 Mốc 6 khi mèo tới được bếp)

- Bếp có đá kê, củi, nồi và que khuấy, đứng bên phải bù nhìn, nhấc và di chuyển cùng mảnh đất; khu rộng thêm 100 DIP nên ruộng sát mép phải dịch sang trái một lần. Lúc không nấu chỉ có than vẽ tĩnh. Bếp không nhận bấm trái, chuột phải vẫn ra menu ruộng.
- Bấm "Nấu": mèo đi tới bếp (giới hạn 30 giây), đổ nguyên liệu khoảng 1,2 giây (kho bị trừ lúc này), khuấy khoảng 6 giây với lửa bùng và hơi nóng, rồi ăn cạnh bếp. Phần ăn giữ luật cũ: Happy 6 giây, chung hồi 20 giây, nhấc mèo lúc đang ăn thì mất bữa.
- Mỗi lần một món: nồi có món hoặc mèo đang trên đường nấu thì nút "Nấu" mờ. Mèo đang ở màn hình khác hoặc trên cửa sổ thì ăn ngay tại chỗ như trước.
- Điều kiện khi bạn bấm giống "Cho ăn": làm được trong Quiet; mèo ngủ, bận hoặc Pause thì nút mờ, không trừ đồ, không đánh thức. Bị ngắt trước khi đổ thì không mất gì, phải bấm lại.
- Bị ngắt sau khi đổ: nồi giữ món (lưu `PotDish` trong `farm.json`, vẫn phiên bản 3), mèo quay lại khuấy ở lần tìm việc sau, kể cả sau khi mở lại app và khi đã tắt "Mèo làm vườn cùng". Lúc Quiet, Away hoặc chưa ăn được thì mèo chưa quay lại. Bữa này tính là bữa mèo tự nấu.
- Mèo tự nấu: tối đa một lần mỗi 10–15 phút, sau mọi việc ngoài ruộng, không nấu trong Quiet. Chọn món cấp cao nhất mà kho còn ít nhất 4 mỗi nguyên liệu sau khi trừ. Công tắc "Mèo tự nấu ăn" trong Cài đặt → Thói quen, mặc định bật; tắt "Mèo làm vườn cùng" thì cũng thôi tự nấu. Bữa mèo tự nấu không báo cho Python, không tính là bạn cho ăn, không đổi đồng hồ buồn ngủ; mèo vẫn Happy 6 giây.
- Tiếng: "pop" lúc đổ nguyên liệu, lần mèo tự nấu theo khoảng cách 60 giây của Mốc 1. Khuấy im lặng, không thêm asset.

### Luật Mốc 3 (duyệt 09/11)

- Hồ có bờ đất, đá và lau sậy, đứng bên phải bếp, nhấc và di chuyển cùng mảnh đất; khu rộng thêm 140 DIP nên ruộng sát mép phải dịch sang trái một lần. Lúc không câu vẽ tĩnh. Hồ không nhận bấm trái, chuột phải vẫn ra menu ruộng.
- Một lần câu: mèo đi tới bờ xa (bờ gần nếu sát mép màn hình, giới hạn 30 giây), vung cần 0,8 giây, chờ phao 4–10 giây, kéo 0,8 giây, cá nhảy lên rồi vào kho với nhãn "+1 Tên cá". Lần nào câu cũng được cá; bị ngắt thì không mất gì.
- Bạn gọi: mục "Câu cá" trong menu chuột phải của ruộng, hồi 60 giây giữa hai lần xong. Điều kiện giống "Cho ăn": làm được trong Quiet; mèo ngủ, bận, đang ăn, đang nấu hoặc Pause thì mục mờ, không đánh thức. Mèo trên cửa sổ hoặc màn hình khác thì không câu.
- Mèo tự câu: mỗi 8–12 phút, sau việc ruộng và trước tự nấu, không câu trong Quiet. Công tắc "Mèo tự câu cá" trong Cài đặt → Thói quen, mặc định bật; tắt "Mèo làm vườn cùng" thì cũng thôi. Câu cá không tính là chăm sóc mèo, không đổi đồng hồ buồn ngủ, không báo Python.
- Cá: Cá nhỏ 70%, 5 xu, 3 XP. Cá chép 25%, 15 xu, 8 XP. Cá vàng quý 5%, 60 xu, 25 XP, mở ở cấp 6 (trước đó phần 5% ra cá chép). Cá nằm trong kho riêng, bán từng con hoặc qua "Bán hết". `farm.json` vẫn phiên bản 3, thêm mục cá.
- Món cá nấu ở bếp như món khác: Cá nướng (1 Cá nhỏ, cấp 1), Cá chép kho cà chua (1 Cá chép + 1 Cà chua, cấp 4), Lẩu cá vàng (1 Cá vàng quý + 1 Bí ngô, cấp 7). Mèo tự nấu được dùng cá nhỏ và cá chép, chừa 4 con mỗi loại; không đụng Cá vàng quý.
- Tiếng: "pop" lúc cá vào kho, lần mèo tự câu theo khoảng cách 60 giây của Mốc 1. Không thêm asset.

### Luật Mốc 4 (duyệt 09/11, thay Luật Mốc C)

- Xe hàng là cửa sổ riêng, đứng sát bên trái ruộng; hết chỗ thì sang bên phải hồ; đi theo khi ruộng bị di chuyển. Lần đầu khoảng 10 phút sau khi mở app, sau đó mỗi 20–40 phút tính từ lúc xe đi. Xe lăn vào, ở lại 5 phút, lăn đi. Pause đóng băng thời gian. Không lưu qua lần tắt app.
- Không đến khi Pause, Quiet, app toàn màn hình, đang trận hoặc bảng trận, bạn vắng máy, hoặc mảnh đất đang nhấc; 45 giây sau xem lại. Mèo ngủ xe vẫn đến và không đánh thức. App toàn màn hình hoặc vào trận thì xe đi ngay.
- Xe tới có một tiếng "sparkle" theo cài đặt âm thanh tự động. Bấm trái vào xe hoặc mục "Gian hàng" trên menu ruộng (mờ khi xe chưa đến) để mở bảng. Bảng mở thay chỗ Kho và Nấu ăn.
- Mỗi lần đến có 1–3 lời mua theo cấp 1 / 3 / 5. Mỗi lời mua 2 đến `cấp + 2` món (tối đa 6), một hoặc hai loại, từ các cây đã mở khóa, Cá nhỏ và Cá chép; tối đa 2 Bí ngô, 3 Cà chua, 3 Cá chép; không đòi Cá vàng quý. Thưởng 1,5 lần giá bán và XP như khi thu hoạch hoặc câu được.
- Giao làm được khi mèo ngủ và không đánh thức; Pause thì không. Không có nút bỏ qua, xe đi là lời mua hết, không phạt gì. Xe đi khi bảng đang mở thì bảng vẫn giao được tới khi đóng. Xe có dấu "!" vàng khi có lời mua giao được.
- Bỏ biển gỗ, chim mang thư và "Đơn hàng"; đơn cũ trong `farm.json` bị bỏ, file vẫn phiên bản 3. Chim giấy mèo đuổi giữ nguyên. Xe chỉ mua, chưa bán gì.

### Luật Mốc 5 (duyệt 09/11, thay luật "chỉ đổi khi đổi nền" và "tuyết, hoàng hôn, phấn hoa, đêm không có tác dụng" của 1.5.1)

- Wallpaper là khí hậu chính. Cứ 10–20 phút có một đợt loại khác, ngẫu nhiên trong 5 loại còn lại, dài 3–5 phút rồi trở về khí hậu chính, chuyển bằng hiệu ứng mờ có sẵn. Đồng hồ chỉ chạy khi thời tiết đang hiện, không lưu qua lần tắt app. Đổi nền giữa đợt thì khí hậu cập nhật ngay, đợt đang diễn vẫn chạy hết.
- Nắng: một giai đoạn còn 80% thời gian. Mưa: đủ 15 giây thì tưới mọi ô chờ nước. Hai loại này giữ như 1.5.1 Mốc 6.
- Tuyết: Cá chép 50% thay vì 25%. Hoàng hôn: mỗi lần gặt 25% được thêm 1 nông sản vào kho, không thêm XP. Phấn hoa: XP khi gặt nhân 1,5, làm tròn lên. Đêm sao: phao cắn sau 2–5 giây, Cá vàng quý 10% thay vì 5%, vẫn cần cấp 6.
- Chỉ có tác dụng khi thời tiết đang hiện quanh mèo và ruộng cùng màn hình với mèo; loại cá và thời gian chờ tính lúc vung cần. Không chạm vào mèo, không đánh thức, không phát tiếng.
- Mỗi lần trời đổi có nhãn 3 giây trên ruộng nói tác dụng, lần đầu sau khi mở app thì không. Bảng "Kho & cửa hàng" có một dòng cho biết thời tiết đang có.

## Giai đoạn 1

Luôn giữ: cây không héo, chim không gây hại, farm không cộng sức mạnh trong trận, không chạm vào mèo. Các gói chưa chọn: thêm cây và mùa vụ, công cụ và phân bón, vật nuôi, mở rộng và trang trí, chất lượng nông sản.

## Luật Mốc A

- Chuột phải vào mảnh đất đang nằm dưới đất có thêm "Gặt tất cả", "Tưới tất cả", "Cày tất cả", "Gieo tất cả ▸ loại cây", kèm số ô. Chỉ hiện việc đang có; ô khóa không tính.
- Gieo từ trái sang, hết xu thì dừng. Mỗi loại cây ghi số ô thực gieo được và tổng giá; không đủ xu cho ô nào thì mờ.
- Kết quả giống bấm từng ô, lưu một lần, một tiếng, một nhãn (một ô thì "+1 Tên cây", nhiều ô thì "+N nông sản").
- Như bấm ô: làm được khi mèo ngủ và không đánh thức. Pause và lúc mảnh đất đang nhấc thì không làm được. Khi Sảnh, bảng chuẩn bị, bản đồ đang mở thì menu không có các mục này.

## Luật Mốc B

- Thu hoạch tay hay mèo gặt đều được XP: Củ cải 2, Cà rốt 5, Cà chua 12, Bí ngô 30. Tối đa cấp 10; XP cộng dồn để lên cấp 2 đến 10: 25, 70, 150, 280, 450, 700, 1000, 1400, 2000.
- Mở khóa: cấp 1 Củ cải, Cà rốt, Súp củ cải, 1 ô đơn hàng; cấp 2 Cà chua, Cà rốt hầm; cấp 3 Cà chua nhồi, ô đơn thứ 2; cấp 4 Bí ngô; cấp 5 Bánh bí ngô, ô đơn thứ 3. Mỗi lần lên cấp thưởng `cấp × 20` xu.
- Thay luật Mốc 5: Cà chua và Bí ngô không còn mở sẵn. Hạt và món chưa mở thì mờ, có ghi cấp cần. Ruộng cũ được tính XP từ số đã thu hoạch, cây đang trồng vẫn lớn bình thường. `farm.json` lên phiên bản 3; bản app cũ chỉ đọc, không ghi đè.
- Bảng "Kho & cửa hàng" hiện cấp, XP, thanh tiến độ và thứ cấp sau mở. Lên cấp thì ruộng lấp lánh và hiện nhãn "Lên cấp N! +X xu" 3 giây. Ô đơn hàng ghi ở trên có tác dụng từ Mốc C.

## Luật Mốc C (đã thay bằng Luật Mốc 4)

- Chim giấy mang đơn tới, mở bằng mục "Đơn hàng" trên menu ruộng. Biển nhỏ ở đầu trái mảnh đất báo khi có đơn giao được.
- Đơn chỉ đòi cây đã mở khóa, từ 2 đến 6 nông sản. Thưởng 1,5 lần giá bán và XP như lúc thu hoạch.
- Đơn không hết hạn. Giao hoặc bỏ đơn thì ô đó có đơn mới sau 20 phút, tính theo đồng hồ thật cả khi tắt app.
- Số nông sản một đơn tối đa là `cấp + 2` (không quá 6), một hoặc hai loại; một đơn đòi tối đa 3 Cà chua, 2 Bí ngô. Ô mới mở khi lên cấp có đơn ngay.
- Có 3 hàng trong bảng; ô chưa mở ghi cấp cần, ô đang chờ đếm ngược. Bỏ đơn một lần bấm, không tốn gì. Đơn mới đến lúc Pause thì chờ bỏ Pause.
- Chim mang đơn bay vào đậu trên biển 5 giây rồi đi, không có tiếng, không chạm vào mèo. Biển có dấu "!" vàng khi có đơn giao được; biển không nhận bấm.
- Bảng đóng cùng các điều kiện như "Kho & cửa hàng"; ba bảng Kho, Nấu ăn, Đơn hàng mở thay chỗ nhau. Giao đơn làm được khi mèo ngủ và không đánh thức. `farm.json` vẫn phiên bản 3, thêm mục đơn hàng.
