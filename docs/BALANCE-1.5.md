# Cân bằng và hiệu năng bản 1.5 (mốc M29, 2026-10-30)

Báo cáo **mô phỏng** (không phải chơi thật) trên dữ liệu gốc battle.json ở commit 0f01d1b, kèm đo hiệu năng ở cuối file. Người dùng chọn phương án (a) ngày 30/10: chỉ đo và đề xuất, **không đổi số liệu**, không đổi rulesVersion. Mọi ngưỡng kiểm tra trong test đều đạt.

Cách chạy lại: `scripts/test.ps1` (bảng in ra trong đầu ra của DesktopPet.Tests). Mỗi dòng 20 seed (Viễn chinh 40 chuyến). Các loại người chơi:
- **simple:** dùng kỹ năng theo luật đơn giản và phản ứng ngay. Đây là trường hợp tốt nhất, thường dễ hơn chơi thật.
- **casual:** phản ứng chậm 0,6 s và bỏ sót 30%.
- **idle:** để mèo tự đánh.

Người chơi thật còn nhấc mèo né đòn nên thường làm tốt hơn mô phỏng ở các tình huống đó. Bot không tiêu xu ở cửa hàng/Lò rèn (Viễn chinh mô phỏng không có đồ rơi), không dùng Mèo nổi giận, không chọn bộ kỹ năng riêng (chương 2 mang bộ gợi ý của màn, Viễn chinh/Vô tận mang 5 kỹ năng cũ).

## Mô phỏng mới ở M29

- Cuộn len và Găng đấm ở **chương 2**, cùng ngưỡng với chương 1: simple Normal ≥ 80%, cách Kiếm ≤ 30 điểm (màn Slime Nén Hard ≤ 40), trận thắng trung bình ≤ 4 phút.
- Cuộn len và Găng đấm ở **Viễn chinh cả hai tuyến** (Normal và Hard, simple), cùng ngưỡng với Kiếm/Búa.
- **Vô tận** đủ 4 vũ khí (trước chỉ có Kiếm), ngưỡng trung vị ≥ đợt 15.
- Các bảng cũ giữ nguyên từng dòng (346/346 dòng giống lần chạy trước M29).

## Kết quả chính (simple, tỉ lệ thắng)

**Chương 1** (5 màn):

| Vũ khí | Normal | Hard |
|---|---|---|
| Kiếm, Búa | 100% mọi màn | 70–100% (thấp nhất màn 3 Kiếm 70%, màn 5 Búa 75%) |
| Cuộn len, Găng đấm | 100% mọi màn | 60–100% (thấp nhất màn 4 Găng 60%) |

**Chương 2** (5 màn):

| Vũ khí | Normal | Hard |
|---|---|---|
| Kiếm | 95–100% | 75–100%; Slime Nén 70% |
| Búa | 95–100% | 80–100%; Slime Nén 45% |
| Cuộn len | 100% | 100% ở màn 1–4; Slime Nén 60% |
| Găng đấm | 100% | 90–100%; Slime Nén 70% |

Máu còn lại khi thắng Slime Nén ở Normal là 64–79%.

**Viễn chinh** (thắng/40 chuyến, đường an toàn / đường tham):

| Vũ khí | Tuyến 1 Normal | Tuyến 1 Hard | Tuyến 2 Normal | Tuyến 2 Hard |
|---|---|---|---|---|
| Kiếm | 39 / 35 | 15 / 11 | 31 / 28 | 10 / 14 |
| Búa | 38 / 32 | 22 / 10 | 27 / 30 | 8 / 13 |
| Cuộn len | 38 / 35 | 27 / 12 | 39 / 39 | 18 / 25 |
| Găng đấm | 28 / 32 | 12 / 3 | 29 / 31 | 10 / 11 |

**Vô tận** (luật mới, Normal, 8 lượt, giới hạn 20 phút mô phỏng), trung vị đợt:

| Vũ khí | Trung vị | Các lượt |
|---|---|---|
| Kiếm | 31 | 20 20 30 30 31 32 32 37 |
| Búa | 30 | 20 28 28 30 30 31 33 36 |
| Cuộn len | 30 | 26 26 27 28 30 30 32 35 |
| Găng đấm | 30 | 20 20 29 30 30 30 31 35 |

Luật cũ (trước M24) với Kiếm có trung vị đợt 30.

## Điểm lệch và đề xuất (chờ người dùng quyết, chưa làm)

1. **Cuộn len mạnh nhất ở chương 2 và tuyến 2.**
   - Hard chương 2 thắng 100% ở màn 1–4.
   - Tuyến 2 Normal thắng 39/40, trong khi Kiếm 28–31 và Búa 27–30.
   - Đề xuất nếu chơi thật cũng thấy vậy: giảm `sweep` hoặc sát thương Cuộn len khoảng 10%. Việc này đổi kỷ lục theo vũ khí, nên cần tăng rulesVersion của các chế độ có Cuộn len.
2. **Găng đấm yếu ở Viễn chinh.**
   - Tuyến 1 đường an toàn Normal 28/40 (Kiếm 39), Hard đường tham 3/40.
   - Ở Phiêu lưu thì Găng đấm vẫn đạt ngưỡng.
   - Đề xuất: tăng nhẹ `launchStun` hoặc sát thương chuỗi. Cũng nên kiểm lại khi bot biết dùng Mèo nổi giận.
3. **Tuyến 2 khó hơn tuyến 1 rõ** (Normal Kiếm 28–31 so với 35–39/40).
   - Đây là chủ ý vì tuyến 2 là tuyến sau, nhưng máu trước trùm chỉ còn 78–94%, máu khi thắng 50–60%.
   - Bot không mua ở cửa hàng nên mô phỏng có thể khó hơn chơi thật.
   - Đề xuất: giữ nguyên đến khi chơi thật. Nếu thấy quá khó thì giảm `stepThreat` của tuyến 2 trước.
4. **Vô tận: một số lượt chết ngay ở đợt 20** (2/8 với Kiếm và Găng đấm, 1/8 với Búa). Đợt 20 là vòng trùm thứ hai (Slime Nén, `bosses[1]`), ngay cuối chặng Đầm nhầy (đợt 15–20).
   - Trung vị vẫn khoảng 30.
   - Đề xuất: nếu chơi thật thấy đợt 20 gắt, giảm threat của chặng đầu (`eras[0]`) hoặc đổi thứ tự `bosses` để Slime Nén ra ở đợt 30.
5. **Slime Nén Hard với Búa 45%** (Kiếm 70%). Mức chênh này đã được duyệt ở M21 (ngưỡng 40 điểm), xem lại sau khi chơi thật.
6. **Chương 2 Normal gần như luôn thắng** (95–100%) cho bot simple.
   - Kết quả này cùng mức với chương 1, đúng mục tiêu ≥ 80%.
   - Người chơi casual ở chương 2 thắng 93% (simple 95%).
   - Không đề xuất đổi.

## Hiệu năng (perf.ps1 -Battle, 3 phút mỗi lần)

Đo ngày 2026-10-30 trên bản publish 1.4.4+0f01d1b. Mỗi lần là một mèo riêng với dữ liệu riêng; mèo không bao giờ thua. CSV nằm ở artifacts\perf\20261030-*.csv.

| Kịch bản | Nội dung | fps trung vị | fps thấp nhất | Khung dài nhất p95 / max | Quái TB / tối đa | CPU TB | RAM riêng (max) |
|---|---|---|---|---|---|---|---|
| `crowd` | slime thường, tối đa 16 | 61,6 | 37 | 47 / 124 ms | 15,8 / 17 | 1,3% | 218 MB |
| `crowd-new` | quái M11 (hợp thể, bom cưỡi, tách đôi, phun, lò xo) | 62,9 | 47 | 47 / 106 ms | 16,3 / 19 | 1,6% | 219 MB |
| `crowd-late` (mới) | quái M12–M16 (xích, cờ hiệu, slime bay, bóng ma, bong bóng, ảo ảnh, dỗi, đào hang, băng, bám tường, nhím, hẹn giờ, trộm) | **48,9** | 33,7 | 51 / 152 ms | 17,3 / 22 | 2,3% | 287 MB |

Lần đo trước (29/10, bản M11): `crowd` 57,4 / 47,4, `crowd-new` 56 / 39,6. Máy hôm nay nhanh hơn hôm đó.

**`crowd-late` dưới mục tiêu ~57 fps.** Kịch bản này nặng hơn mọi trận thật:
- Có 4 loại quái dùng chuột cùng lúc (trận thật tối đa 2).
- Ảo ảnh có thêm bản giả, hẹn giờ gọi thêm quái, nên có lúc lên tới 22 quái.
- Slime bay có cửa sổ riêng.

Chưa đo được phần nào tốn nhất. Có thể là cửa sổ slime bay, vẽ dây và vòng hào quang, hoặc bóng và bản giả của ảo ảnh.

Đề xuất (chờ người dùng):
1. Đo thêm một kịch bản bằng đúng trận nặng nhất của dữ liệu gốc (màn cuối tuyến 2 hoặc Vô tận đợt 30+).
2. Nếu trận thật cũng dưới ~55 fps, tìm phần vẽ tốn nhất rồi tối ưu trước release 1.5.0.
