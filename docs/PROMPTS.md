# Mẫu prompt cho project

Mẫu để copy khi làm việc với AI agent. Không cần viết dài, chỉ cần đủ các ý chính. Quy trình mỗi bản:

0. Chốt mục tiêu bản (danh sách tính năng)
1. Nền (đã có)
2. Ý tưởng kèm tiêu chí "xong"
3. Chuẩn hóa vào nền và duyệt thiết kế
4. Làm và chạy test tự động
5. Test tay theo smoke ngắn và mục mới
6. Release, rồi dọn PROGRESS/archive
7. Lặp lại

## Bước 0: Chốt mục tiêu bản (đầu mỗi đợt, một lần)

```
Mục tiêu bản 1.5:
1. Mèo nói tiếng người (cách A, líu lo hoạt hình)
2. Slime nhún khi đáp/đi (việc E)
3. Smoke test ngắn trong MANUAL-TEST
Ngoài 3 việc này thì ý tưởng mới đưa vào backlog, không làm trong 1.5.
Hãy chia thành các mốc, mỗi mốc ghi tiêu chí xong.
```

Chốt mục tiêu giúp đợt làm có điểm dừng. Đợt 1.4.4 không có danh sách chốt, nên cứ thêm việc mãi (cân bằng, hướng dẫn, 4 đợt visual, icon…).

## Bước 2: Ý tưởng kèm tiêu chí "xong"

Thay vì:

> slime nhìn cứng quá, làm cho nó sống động hơn

Nên viết:

```
Ý tưởng: slime nhún khi đáp đất và khi đi (squash & stretch).
Vì sao: slime đứng cứng, trông như ảnh dán.
Không đổi: hitbox, sát thương, timing, rulesVersion.
Xong khi:
- Slime đáp đất thì bẹp xuống rồi bật lại, dưới 0,3 s
- Trận 20 slime vẫn ~57 fps (perf.ps1 -Battle crowd)
- Tôi xem ảnh battle-juice thấy rõ
```

- **Vì sao:** giúp AI chọn cách làm đúng ý bạn.
- **Không đổi:** chặn phạm vi lan sang chỗ khác.
- **Xong khi:** để AI biết lúc nào dừng, và bạn biết phải test cái gì.

## Báo lỗi

```
Lỗi: [ảnh] bóng mèo lệch khỏi chân trong trận.
Lúc nào: màn 2 Phiêu lưu, hình nền tối, DPI 150%.
Mong muốn: bóng nằm đúng dưới chân, hoặc bỏ luôn cũng được.
```

- **Lúc nào:** giúp AI tái hiện lỗi nhanh, thay vì phải đoán.
- **Mong muốn:** cho AI biết những cách sửa nào bạn chấp nhận.

## Duyệt kế hoạch

"ok làm đi" là đủ khi đồng ý toàn bộ. Nếu chỉ đồng ý một phần thì nói rõ:

```
ok mốc 1 và 2. Mốc 3 bỏ. Mốc 4 đổi: chỉ làm cho Vô tận, không đụng Viễn chinh.
```

## Một phiên chat một mốc

Phiên càng dài, mỗi tin nhắn càng phải gửi kèm toàn bộ lịch sử trước đó: tốn token và AI dễ nhầm chi tiết cũ với mới.

Mở phiên mới (PROGRESS.md nhỏ nên nạp lại ngữ cảnh rất rẻ):

```
Đọc PROGRESS.md. Làm mốc 2 của bản 1.5 (slime nhún).
```

Kết thúc phiên:

```
Ghi PROGRESS.md: đã xong gì, còn dở gì, tôi cần test tay gì. Rồi tôi mở phiên mới.
```

Lỗi nhỏ phát sinh giữa mốc: sửa luôn nếu liên quan đến mốc; không liên quan thì nói "ghi vào việc dở" để phiên khác làm.

## Release

```
Release 1.4.4. Nếu test hoặc pack lỗi thì dừng và báo, không đẩy tag.
```

## Tóm tắt

| Loại prompt | Phải có |
|---|---|
| Mục tiêu bản | Danh sách việc + "ngoài danh sách thì vào backlog" |
| Ý tưởng | Vì sao, Không đổi, Xong khi |
| Lỗi | Ảnh, Lúc nào, Mong muốn |
| Duyệt | Đồng ý hết, hoặc nói rõ phần nào đổi |
| Phiên | Một mốc; cuối phiên ghi PROGRESS |
