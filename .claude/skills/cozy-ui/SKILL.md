---
name: cozy-ui
description: Giữ giao diện Desktop Pet đúng theme Cozy Desktop Adventure khi thêm hoặc sửa UI, bảng, nút, HUD, màu, chữ, icon, tranh minh họa, art vẽ bằng code, animation hay âm thanh. Dùng cả khi người dùng chỉ góp ý về hình như "nhìn xấu", "chữ khó đọc", "không giống cái kéo", "lệch", "tràn", và với mọi thay đổi file WPF hoặc PetRenderer dù họ không nhắc đến design system.
---

# Sửa giao diện Desktop Pet

Theme không được thay đổi gameplay: sleep, input, combat, pause, physics và các ngoại lệ đã duyệt giữ nguyên. Thiết kế được chuẩn hóa dần, nên chỉ sửa đúng thành phần được giao, không viết lại màn hình bên cạnh.

## 1. Đọc đúng mục của docs/DESIGN-SYSTEM.md
Luôn đọc mục "Checklist cho người/AI phát triển" và làm theo. Sau đó chỉ đọc mục khớp việc:

| Việc | Mục |
|---|---|
| Màu, độ tương phản | "Palette" (phần nền đang đứng: kem hay game tối) |
| Cỡ chữ, khoảng cách, bo góc | "Typography, khoảng cách và hình khối" |
| Nút, bảng, thẻ | "Mẫu thành phần" |
| Vẽ mèo, quái, đồ vật, icon, banner | "Art và minh họa" |
| Animation, âm thanh | "Motion và âm thanh" |
| Màn của một chế độ chơi | "Một thế giới, nhiều chế độ" |

## 2. Tái dùng trước khi tạo mới
- Token: `PetUi` cho nền kem, `PetUi.Game` cho nền game tối. Màu chữ của nền này thường không đọc được trên nền kia.
- Control và menu: `PetControlStyle`, `PetMenuStyle`, `PetTrayMenuStyle`.
- Hình dùng chung: `PetIllustration` (motif nhỏ), `PetScene` (banner).
- Thiếu token thì thêm vào `PetUi` và cập nhật DESIGN-SYSTEM.md trong cùng mốc. Không dùng màu có sẵn của WPF như `Brushes.Red`, không copy bảng màu riêng cho từng cửa sổ.

## 3. Giữ đúng ràng buộc của dự án
- Art là vector/procedural, không dùng sprite hay ảnh ngoài.
- Không dùng tên, logo hay màu của app thật cho icon trong trận.
- Chữ mới viết bằng `L("vi", "en")`, rồi chạy `.tools/python/python.exe scripts/i18n.py ja` và bổ sung bản dịch trong `src/DesktopPet/Assets/i18n/`, nếu không `tests/ai/test_i18n.py` sẽ fail. Chừa chỗ cho nhãn en/ja dài hơn tiếng Việt.
- Góp ý "không giống X" thì bám hình dạng nhận ra được của vật thật trước, chi tiết sau.

## 4. Xem ảnh trước khi báo xong
Chạy UiChecks với `PET_RENDER_REVIEW=<thư mục>` và mở ảnh render, xem cả trước và sau nếu sửa thứ đang có. Với bảng có cuộn hoặc kích thước đổi, thêm check đo được trong `tests/DesktopPet.UiChecks/` thay vì chỉ nhìn một lần.

Báo cáo tách rõ đã xem gì: ảnh render khác với xem trong trận thật hay DPI thật. Không nói đã kiểm tra thứ chưa làm; phần còn lại ghi vào điểm cần test tay.

Khi mốc xong, dùng skill `ship-milestone`.
