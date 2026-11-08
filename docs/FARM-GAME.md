# Nông trại như game trồng trọt

Người dùng duyệt 2026-11-08. Mở rộng ruộng của bản 1.5.1 (docs/UPDATE-1.5.1.md, luật Mốc 3–6 vẫn giữ trừ chỗ ghi rõ dưới đây). Các con số là ước tính, chưa qua chơi thử.

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| A. Thao tác nhanh | Cày, gieo, tưới, gặt cả ruộng từ menu | Xong (9f8bd67), còn test tay |
| B. Cấp nông trại | XP, 10 cấp, mở khóa cây và món | Xong (9fcf389), còn test tay |
| C. Đơn hàng | Chim giấy mang đơn, giao lấy xu và XP | Chưa làm |

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

## Luật Mốc C (đã duyệt, chưa làm)

- Chim giấy mang đơn tới, mở bằng mục "Đơn hàng" trên menu ruộng. Biển nhỏ ở đầu trái mảnh đất báo khi có đơn giao được.
- Đơn chỉ đòi cây đã mở khóa, từ 2 đến 6 nông sản. Thưởng 1,5 lần giá bán và XP như lúc thu hoạch.
- Đơn không hết hạn. Giao hoặc bỏ đơn thì ô đó có đơn mới sau 20 phút, tính theo đồng hồ thật cả khi tắt app.
