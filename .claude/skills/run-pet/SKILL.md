---
name: run-pet
description: Chạy Desktop Pet để xem thay đổi hoạt động thật, tái hiện lỗi, đọc log, chụp màn hình hoặc đo hiệu năng mà không đụng dữ liệu người dùng. Dùng khi cần mở app, thử một trận, xem log sau khi người dùng báo lỗi, chụp ảnh bảng hoặc trận, đo fps, hoặc khi `/run` được gọi trong dự án này.
---

# Chạy Desktop Pet

## Chọn cách rẻ nhất đủ trả lời
1. **Xem hình một bảng hay một quái:** chạy UiChecks với `PET_RENDER_REVIEW=<thư mục>` (thêm `PET_REVIEW_LANGUAGE` nếu cần xem ngôn ngữ khác). Không cần mở app, không ảnh hưởng mèo đang chạy.
2. **Xem lỗi người dùng gặp:** đọc log trước, `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log` và `.old.log`, chỉ lọc dòng liên quan. Trận có ghi seed ở dòng `Battle started`.
3. **Cần app chạy thật:** làm theo mục dưới.
4. **Đo fps:** `scripts/perf.ps1 -Battle run|paused|expedition|crowd|crowd-new|crowd-late`. Script tự dùng dữ liệu riêng, nhưng mèo của người dùng phải đóng trước.

## Mở app an toàn
App chỉ cho một bản chạy, nên phải đóng mèo đang chạy trước:

1. Ghi nhớ mèo có đang chạy không, rồi thoát bằng WM_CLOSE. Không kill Python hay app khác.
2. Đặt `PET_DATA_DIRECTORY=<dự án>\artifacts\run-data\<guid>` (đường dẫn tuyệt đối). Nếu thiếu bước này, app sẽ ghi vào preferences thật của người dùng, nên không được bỏ qua.
3. Thử trận tùy chỉnh: chép `docs/examples/battle-m*.json` thành `battle.json` trong thư mục đó.
4. Chạy `scripts/run.ps1` (thêm `-NoAi` để tắt Python). Script này build rồi mở bản Release từ `bin`.
5. Log của lần chạy nằm ở `<thư mục dữ liệu>\logs\desktoppet.log`.

`PET_BATTLE_AUTOSTART` mở sẵn một trận với các giá trị như trên, nhưng chỉ dành cho đo đạc. Bản đã publish ở `artifacts\win-x64\DesktopPet.exe`.

## Chụp màn hình
Mèo là cửa sổ trong suốt luôn nằm trên cùng, nên phải chụp vùng màn hình thay vì chụp cửa sổ. Chỉ chụp màn hình có mèo và lưu vào `artifacts/`, vì desktop có thể lộ thông tin riêng tư của người dùng; không đưa ảnh này vào commit nếu chưa được đồng ý.

## Sau khi xong
Thoát bản thử bằng WM_CLOSE, xóa biến môi trường đã đặt, và nếu lúc đầu mèo đang chạy thì mở lại `artifacts\win-x64\DesktopPet.exe`.

## Báo đúng những gì đã thấy
Agent không thay được thao tác kéo, ném, chơi cả trận hay đổi DPI thật. Mở được app chỉ chứng minh app chạy, nên ghi rõ phần nào đã xem, phần nào người dùng vẫn phải test tay.
