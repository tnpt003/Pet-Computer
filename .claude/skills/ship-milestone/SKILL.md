---
name: ship-milestone
description: Chốt một mốc của Desktop Pet theo đúng quy trình dự án - test đầy đủ một lần, ghi Issue_fixxed và PROGRESS, commit, publish, smoke, mở lại mèo. Dùng khi code của một mốc đã xong, khi người dùng nói "commit", "publish", "chốt mốc", "xong rồi", hoặc sau khi sửa xong lỗi họ báo, kể cả khi họ không nhắc đến từng bước.
---

# Chốt mốc Desktop Pet

Người dùng test trên bản đã publish, nên một mốc chỉ xong khi bản sạch đang chạy và kết quả được ghi lại. Quy tắc gốc ở mục "Quy tắc commit và publish" của AGENTS.md; skill này là thứ tự thực hiện.

Mốc chỉ đổi tài liệu: làm bước 1, 3, 4 rồi dừng, không publish.

## 1. Xác định phạm vi
Chạy `git status --short`, liệt kê file thuộc mốc. File không thuộc mốc có thể là việc đang dở của người dùng: giữ nguyên, không stage, không reset.

## 2. Test đầy đủ một lần
Chạy `scripts/test.ps1 -Ui` đúng một lần, ghi ra `artifacts\<mốc>-tests.log`, rồi chỉ đọc dòng khớp `passed|FAIL|Ran |OK$|exit`. Lần chạy này đồng thời là lưới an toàn cho sleep, drag, emotion và battle, nên không bỏ qua dù thay đổi nhỏ.

- Cần ảnh thì đặt `PET_RENDER_REVIEW=<thư mục>` ngay từ đầu, không chạy lại chỉ để lấy ảnh.
- Có FAIL thì mở chi tiết và sửa. Chỉ coi là lỗi có sẵn khi đã tái hiện trên HEAD sạch; vẫn ghi rõ trong báo cáo, không viết "đạt hết".
- Nếu đụng behavior/input, tự rà sleep lock, wake, drag, throw, emotion priority, local fallback.

## 3. Ghi lại
- `Issue_fixxed.md`: lỗi người dùng báo thành mục `## #N — …` mới ở đầu phần A (triệu chứng, nguyên nhân, cách sửa, commit, kết quả test thật, phần cần xem tay). Lỗi do test/review tìm ra thành một dòng ở bảng phần B.
- `PROGRESS.md`: sửa mục đang dở cho khớp, mỗi ý ngắn; chi tiết nằm ở Issue_fixxed. Giữ file dưới ~20 KB.
- `docs/MANUAL-TEST.md`: thêm mục `[ ]` ở cuối cho điểm cần test tay. Sửa docs khác nếu hành vi đổi; duyệt ngoại lệ mới thì ghi `docs/APPROVED-EXCEPTIONS.md`, AGENTS.md chỉ một dòng.

## 4. Commit
Stage từng file theo tên, không `git add .`. Commit trước publish để artifact truy vết được source. Thông điệp có dấu `"` thì ghi ra file rồi `git commit -F`, vì PowerShell 5.1 làm hỏng dấu ngoặc kép khi truyền vào git.

## 5. Publish và smoke
1. Thoát đúng process pet của project bằng WM_CLOSE. Không kill Python hay app khác.
2. Chạy `scripts/publish.ps1`, rồi `scripts/check-publish.ps1`. Chỉ đọc vài dòng cuối.
3. Mở lại `artifacts\win-x64\DesktopPet.exe`. Máy này chạy bản copy, không cài Setup.

Nếu chỉ còn file chưa commit của người dùng làm bản bị `.dirty`: sao lưu chúng, `git stash push -- <file>`, publish, rồi khôi phục đúng từng byte từ bản sao lưu, vì `stash pop` có thể đổi xuống dòng.

Test hoặc smoke lỗi thì sửa trong phạm vi hoặc báo rõ bước còn thiếu; không báo đã publish thành công.

## 6. Khép mốc
Điền commit hash và kết quả smoke vào Issue_fixxed và PROGRESS, commit riêng dạng `docs: record … publish`. Chạy `graphify update .`. Không `git push`, không tạo tag hay release nếu người dùng chưa yêu cầu.

## 7. Báo cáo
Nêu commit hash, số check thật (Python, i18n, C#, WPF), kết quả smoke, đường dẫn ZIP, và điểm người dùng cần test tay. Phân biệt rõ "đã xem qua render" với "đã thử trong trận thật/DPI thật".
