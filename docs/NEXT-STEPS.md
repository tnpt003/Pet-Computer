# Bàn giao review hiện tại và kế hoạch đợt tiếp theo

Cập nhật bàn giao: 2026-10-20 (Asia/Saigon). Review kỹ thuật bên dưới ghi nhận ngày 18/10, sau Battle Mode đợt 11.
Workspace lúc review: C:\Project VS\Pet Computer; HEAD e45519d với thay đổi đợt 10–11 chưa commit.

## Bàn giao mới — cứu mèo không làm quái đứng động tác (2026-10-20)

Người dùng thấy slime bị freeze khi cứu mèo và đồng ý hướng bên dưới; yêu cầu ghi để Claude kiểm tra. Mốc này chỉ bàn giao tài liệu, chưa triển khai. Đọc AGENTS.md, kiểm chứng code và giải thích kế hoạch tích hợp trước khi sửa. Review R1/R2 phía dưới đã được Claude xử lý ở mốc A theo PROGRESS.md; không làm lại.

### Hiện trạng và mục tiêu
- Trên HEAD 282a879, PetBattle.cs, nhánh SlimeState.Windup vẫn có `if (catHeld) { slime.StateTime -= dt; break; }`: giữ nguyên tiến độ chuẩn bị đánh khi mèo được cầm. Đây là đầu mối đã thấy trong code, chưa khẳng định mọi hiện tượng đứng hình đều do nhánh này.
- Mục tiêu: cứu mèo giúp né đòn nhưng chiến trường vẫn sống động. Quái không giữ sẵn một đòn để tung ngay khi mèo được thả. Pause/menu/fullscreen vẫn đóng băng trận đúng cơ chế hiện tại.

### Hướng hành vi đã trao đổi
- Quái đang đi: tiếp tục di chuyển/giữ đội hình, không tụ hết dưới con trỏ. Tận dụng quyết định đội hình hiện có; Claude đề xuất điểm tham chiếu cụ thể (ví dụ vị trí mèo trên sàn trước khi nhấc) trước khi triển khai, không tự thêm hệ AI mới.
- Đang chuẩn bị đánh: hoàn tất động tác và đánh hụt tại vị trí/vùng đã báo trước, sau đó Recover/hồi chiêu bình thường. Không bám lại vị trí con trỏ trong lúc ra đòn.
- Slime nhanh đang lao: lao hết đường đã khóa, không quay hướng đuổi theo con trỏ; không gây damage/stun khi mèo được cầm hoặc đang bay.
- Slime hỗ trợ: tiếp tục hồi máu, teleport và tìm chỗ trú theo luật hiện có.
- King: tiếp tục cơ chế đang diễn ra, giữ vị trí đòn đã cảnh báo. Phân biệt helper do cơ chế boss với spawn đợt thường; kiểm tra cap và khả năng lạm dụng, không mở lại tiến độ wave chỉ để boss chạy.
- Khi thả mèo: giữ bảo vệ tiếp đất/cooldown cấp lại hiện có. Không giải phóng đòn giữ sẵn; lần đánh mới phải có cảnh báo đầy đủ. Nếu thả đúng lúc đòn cũ đang diễn ra, bảo đảm không trúng ngay vì chuyển từ held sang grounded; Claude làm rõ quy tắc hết đòn cũ/đánh hụt trước khi code.

### Giới hạn phải giữ
- Dừng tiến độ wave/survival và hồi kỹ năng người chơi khi cầm mèo như hiện tại; không dùng việc cầm để farm thời gian hoặc kỹ năng. Đối chiếu cả trạng thái bay/rescue trong cầu nối PetWindow → BattleSim.
- Quái có thể hồi máu và tái lập đội hình, không thêm hình phạt cứu mèo, không đổi damage/HP/stun hoặc balance ngoài phạm vi.
- Chỉ tách khóa tương tác cứu mèo khỏi pause trận; KHÔNG cho mọi timer chạy vô điều kiện. Giữ upgrade/tip/pause reasons chồng nhau, kết thúc trận và giới hạn spawn.
- Giữ bất biến không nhận damage/stun khi held/airborne, drag/throw/dizzy, sleep và Pet Mode; combat ở C#, Python không tham gia.

### Kiểm tra Claude cần bổ sung khi triển khai
1. Nhấc trước/trong windup: animation hoàn tất, một lần đánh hụt, một lần Recover; không đứng vô hạn, không phát hit/cue lặp.
2. Nhấc/thả liên tục và thả đúng ranh giới impact: không trúng tức thì từ đòn cũ, không reset cooldown hoặc nhân bảo vệ tiếp đất.
3. Dash đang chạy vẫn hết đường; không retarget con trỏ, không damage/stun khi cầm/bay; lần đánh mới có cảnh báo.
4. Support heal/guard/teleport và King retreat/summon/order không kẹt state; giữ các sửa R1/R2 mới và cap quái.
5. Wave/survival/skill timer vẫn dừng theo luật cứu mèo; Pause/menu/fullscreen/upgrade/tip đóng băng đúng, resume không chạy bù đòn.
6. Kết thúc/replay khi đang cứu không sót target/attack/pause; regression Pet Mode và playtest trực tiếp hoạt ảnh.

Báo rõ phần xác nhận, thiết kế còn cần chốt, test tự động và test tay. Không chỉ xóa dòng giữ StateTime: cần kiểm tra vòng đời đòn, vị trí cảnh báo và miễn sát thương cùng nhau. Sau mốc code được giao: test/commit/publish/smoke theo AGENTS.md; lần bàn giao tài liệu này không publish.

## Review mới nhất cho Claude — Enemy Tactics (2026-10-20)

Người dùng yêu cầu lưu review để Claude đọc và kiểm chứng, chưa yêu cầu triển khai mọi đề xuất. Review trên HEAD 7e0d14e (tactics f55a36c). Đọc code thật và AGENTS.md, tái hiện lỗi rồi giải thích kế hoạch trước khi sửa. Các roadmap cũ phía dưới không có nghĩa phải làm lại mốc đã xong.

### Kết quả kiểm tra mới
- scripts/test.ps1 -Ui: 55 Python và 1040 C# qua; WPF 514 qua, 2 lỗi. Giữ nguyên kết quả lịch sử 516/516 của lần publish trước, không dùng nó thay kết quả lần này.
- Hai assertion lỗi trong tests/DesktopPet.UiChecks/UpgradeChecks.cs: “The Defense card says it completes Thorns” và “Taking it switches Thorns on with a banner”. HUD thực tế: Defense 3/3 · ★ Thorns · ◆ Turtle shell.
- Chưa playtest trực tiếp, profile hiệu năng hoặc kiểm tra nhiều màn hình. Review không sửa code/restart/publish pet.

### R1 — Vòng đời miễn sát thương khi hồi máu: cần tái hiện và sửa cục bộ
- src/DesktopPet/PetBattle.cs: StepSlimes (~1242) tính lại Mended đầu bước; FinishHeal (~1579) xóa MendingId nhưng chưa thu hồi ngay Mended của đồng minh. Nhánh chết trong Damage (~912) cũng chưa thu hồi bảo vệ do healer cấp. StepBombs (~736) chạy trước StepSlimes.
- Hệ quả suy ra từ code, CHƯA có test tái hiện riêng: đồng minh có thể dư miễn một nhịp sau heal hoàn tất/healer chết, làm một hit/bom bị bỏ qua.
- Đề xuất gom kết thúc bảo vệ khi heal hoàn tất, bị ngắt, healer chết, trận kết thúc. Nếu nhiều healer, không xóa nhầm nguồn bảo vệ còn hợp lệ. Không đổi sức mạnh/thời lượng ability ngoài phạm vi.
- Test cần thêm: hit đúng nhịp kết thúc heal; healer chết rồi đồng minh nhận damage cùng lượt; bom bao cả healer/mục tiêu với hai thứ tự danh sách; Push hủy ngay; không miễn boss; cooldown chống miễn lại 2 s giữ đúng.

### R2 — Test UI chưa thích ứng với gacha
- UpgradeChecks.cs (~135–139) giả định card Defense phải là big_heart và MaxHP tăng lên 120. Lần này Turtle shell được offer, vẫn đạt Defense 3/3 và Thorns. Chưa có bằng chứng UI synergy hỏng; fixture đang giả định quá hẹp.
- Tách test Big Heart với RNG cố định hoặc RareChance=0 trong fixture, test Turtle shell và test chung card Defense hoàn tất synergy. Không tắt rare trong game thật hoặc bỏ assertion để ép xanh. Chạy lại kiểm tra ổn định.
- scripts/test.ps1:28 luôn gợi ý chạy interactive desktop khi WPF thất bại, kể cả assertion fail. Phân biệt lỗi môi trường và lỗi assertion, giữ exit code lỗi và nguyên nhân thật.

### R3 — Mô phỏng cân bằng chưa đại diện người chơi
- tests/DesktopPet.Tests/AdventureBalanceTests.cs (~53–57): bot Push ngay khi phát hiện healer cast/dasher windup, ít chịu áp lực guarded heal.
- Thêm policy phản ứng chậm/bỏ lỡ, người chơi thường và bot tối ưu; seed cố định, độ trễ theo simulation time, không sleep thật.
- Đo win rate, HP còn, thời gian không gây damage, đổi mục tiêu, chuỗi stun, heal/guard thành công. Giữ test cơ chế riêng với mục tiêu balance; chưa tự chỉnh balance chỉ từ một bot.

### R4 — Backlog chuẩn hóa/tối ưu, không refactor tất cả ngay
- Gom cleanup tactical state khi chết/ngắt/kết thúc (heal guard, flank, retreat, target/guard), giữ state có chủ đích tồn tại.
- PetBattle.cs hơn 2000 dòng: cân nhắc tách tactics, heal/guard, spawn, upgrade theo mốc nhỏ; giữ API/thứ tự update. Không trộn refactor lớn với R1/R2.
- Thêm kiểm tra thông số quan trọng JSON và fallback C# khớp, không tạo nguồn dữ liệu lặp thứ ba.
- Đo CPU/allocation/frame time trận đông quái trước tối ưu. Đã có scratch list/nhịp quyết định riêng; không đổi chỉ vì code dài.
- Không xóa ToArray tùy tiện: damage có thể spawn thêm quái khi đang duyệt danh sách.
- Test start → win/loss → replay → đổi trang bị/độ khó → desktop; xác nhận state/pause/UI không sót, không dùng save thật.
- Chưa đề xuất bỏ ability nào. Giữ C#–Python, sleep/wake, drag/throw/dizzy và Pet Mode.

Thứ tự đề xuất: R1 tái hiện/sửa → R2 ổn định test → R3 bot thực tế → profile/chọn R4. Claude ghi xác nhận hoặc bác bỏ từng phát hiện bằng code/test, file đổi, kết quả thật, phần cần test tay. Triển khai khi được giao theo AGENTS.md; tài liệu-only commit, không publish binary.

## Dành cho AI tiếp nhận

Người dùng yêu cầu lưu review và kế hoạch ở đây để AI khác đọc và tiếp tục công việc. Đọc AGENTS.md, PROGRESS.md và code thật trước khi triển khai; không coi các mục đã sửa bên dưới là lỗi còn mở. Giữ nguyên thay đổi chưa commit của người dùng/Claude.

Lần bàn giao này chỉ cập nhật tài liệu. Hướng chính được chọn ngày 2026-10-20 là Adventure Update — Chương 1; roadmap ở ADVENTURE-UPDATE.md. Các ý tưởng lẻ và review cũ phía dưới là nguồn tham khảo/lịch sử, không còn là thứ tự triển khai độc lập. Đọc AGENTS.md và giải thích kế hoạch trước khi triển khai; giữ nguyên source đang có và không tự publish/commit/restart.

## Ưu tiên mới nhất — Enemy Director, đội hình và gacha (2026-10-20)

Đọc [ENEMY-DIRECTOR.md](ENEMY-DIRECTOR.md) cùng COMBAT-DEPTH.md trước khi code: King xa mèo/hỗ trợ từ xa, chống double stun, support art mới + teleport/vệ sĩ, đá chắn đường, spawn theo màn/threat và gacha đội hình/tinh anh/thẻ có kiểm soát. Phân biệt yêu cầu với lựa chọn posture/offer policy chưa chốt. Giữ build cộng dồn, kháng stun và ngoại lệ damage đã được chốt trong Combat Depth; không làm lại mốc đã xong.

Người dùng yêu cầu ghi để AI khác code. Lần bàn giao chỉ sửa tài liệu, chưa review/test code mới. Theo AGENTS: giải thích tích hợp trước triển khai; hoàn thành mốc code thì test/commit/publish/smoke.

## Ưu tiên bàn giao mới — Combat Depth (2026-10-20)

Xem [COMBAT-DEPTH.md](COMBAT-DEPTH.md): phản hồi người dùng màn 1–4 dễ, thẻ thiếu quyết định; hướng combo nhóm thẻ (3 Defense → Giáp gai), vai trò support/nhanh/đá/King và **stun mèo 1,5 giây với biểu cảm choáng**. Đã phân biệt yêu cầu người dùng với thiết kế đề xuất cần kiểm chứng. Chơi lại được người dùng xác nhận trơn tru, giữ luồng đó.

Trạng thái: tài liệu để AI khác inspect/lập kế hoạch, chưa code/test trong lần bàn giao này. Đọc AGENTS.md và tài liệu chi tiết trước khi sửa. Đây là ưu tiên combat tiếp theo trên Adventure đã có, không làm lại roadmap cũ. Commit/publish theo quy tắc hiện hành khi hoàn thành mốc code.

## Hướng chính đã chọn — Adventure Update, Chương 1 (2026-10-20)

Người dùng đã chọn gom các tính năng thành một bản cập nhật lớn: **Cuộc phiêu lưu của mèo — Cuộc xâm nhập của Slime**. Xem roadmap triển khai chính tại [ADVENTURE-UPDATE.md](ADVENTURE-UPDATE.md).

- Một chương 5 màn: bản đồ → chuẩn bị → combat/nâng cấp giữa đợt → boss → phần thưởng → tiến trình lưu.
- Gộp nâng cấp tạm thời, một quái hỗ trợ và cosmetic milestones thành cùng trải nghiệm. Sổ quái đầy đủ/quái khác/shop/căn cứ chuyển backlog.
- Roadmap có các mốc kỹ thuật và tiêu chí nghiệm thu end-to-end; không phát triển các đề xuất lẻ dưới đây như những yêu cầu độc lập nữa. Nội dung roadmap mới ưu tiên nếu khác phạm vi gợi ý cũ.
- Trạng thái: đã thống nhất hướng và gom tài liệu, chưa code/review/test trong lần này. Đọc code/AGENTS và giải thích tích hợp trước triển khai; không tự publish/commit/restart. Giữ các kết quả lịch sử, không báo chúng thành kết quả Adventure.

## Ý tưởng nguồn — Chiều sâu trận đấu (đã gom vào Adventure Update)

Trạng thái: **đề xuất, chưa triển khai và chưa review code mới trong lần bàn giao này**. Người dùng nói bản hiện tại có vẻ ổn và yêu cầu ghi các hướng mở rộng để AI khác kiểm tra. Không coi đây là yêu cầu làm tất cả ngay. Đọc AGENTS.md, PROGRESS.md, code và kết quả test hiện tại; báo phần đã có, phần thiếu, kế hoạch nhỏ nhất và các quyết định cần thống nhất trước khi code. Không tự publish/commit/đóng hoặc restart pet.

Tài liệu bên dưới ghi đợt 15 đã triển khai trang bị/UI và test đạt; đó là ghi nhận của đợt trước, không phải kết quả kiểm chứng mới của reviewer. Giữ nguyên các trạng thái đã hoàn thành và mọi thay đổi chưa commit.

### Hướng 1 — Chọn nâng cấp giữa đợt (ưu tiên đề xuất cao nhất)

Mục tiêu: mỗi trận có lựa chọn chiến thuật khác nhau, tận dụng hai kiểu vũ khí; không chỉ tăng số quái/nút.

- Dọn xong một đợt, trước đợt tiếp theo: hiện 3 thẻ nâng cấp hợp lệ, chọn 1 rồi xác nhận tiếp tục. Không mở sau đợt cuối khi đã thắng hoặc sau khi thua/end battle.
- Bản đầu khoảng 5–6 nâng cấp: Móng sắc (damage), Chân nhanh (move speed), Nhịp đánh (attack cooldown), Sức nặng (knockback), Cá tiếp sức (hồi HP tức thời). Con số phải được thử, không lấy ví dụ làm balance mặc định.
- Nâng cấp chỉ có hiệu lực trong trận; giới hạn cộng dồn rõ. Thẻ phân biệt hiệu ứng tức thời và modifier kéo dài. Không có reroll/trả phí trong bản đầu.
- Khi chọn, combat đứng yên: không spawn/di chuyển/đánh/hồi cooldown hoặc tính thời gian chọn vào kỷ lục. Tiếp tục chỉ gỡ lý do chờ lựa chọn, không gỡ pause của người dùng/fullscreen/app.
- Dùng dữ liệu definition + runtime modifier riêng, không sửa mẫu vũ khí/quái chung hoặc preferences chỉ số gốc. Chơi lại bắt đầu trận sạch, không giữ nâng cấp trận trước.
- Thẻ không trùng, không vượt stack cap, không chọn upgrade vô tác dụng hoặc không tương thích nếu có lựa chọn hợp lệ khác. Xác định fallback khi còn ít hơn 3 lựa chọn và trường hợp full HP.
- UI dùng phong cách thẻ/preview đã có, tiếng Việt/Anh, không tạo chuỗi dialog gây phiền.
- Xem xét kỷ lục: luật nâng cấp thay đổi sức mạnh và thời gian trận; đề xuất phiên bản luật/nhãn Legacy phù hợp, không xóa dữ liệu cũ hoặc âm thầm so sánh các luật không tương đương.
- Test cần có nếu triển khai: trigger đúng một lần mỗi wave, double click không áp dụng hai lần, timer đóng băng, pause chồng lý do, end/fullscreen/đóng app giữa lựa chọn, modifier reset, selection deterministic bằng seed trong test, không phá combo/hit timing hoặc Pet Mode.

### Hướng 2 — Một loại quái có vai trò chiến thuật mới

Sau khi hướng 1 ổn, chọn MỘT loại để thiết kế/test; chưa triển khai cả ba:
- Quái hỗ trợ: hồi máu đồng đội, tạo lý do ưu tiên mục tiêu.
- Quái bắn xa: cảnh báo đường bắn trước, người chơi né/kéo mèo hoặc áp sát.
- Quái giáp: có điểm yếu hoặc thời điểm dễ bị đánh, tránh chỉ là tăng HP.

Dùng hệ quái chung; capability mới có controller/renderer rõ, JSON chỉ cấu hình khả năng được hỗ trợ. Hình dáng/cảnh báo phải dễ phân biệt với quái hiện có. Kiểm tra hiệu năng, target priority, pause, drag immunity và giới hạn đối tượng; thêm từng loại, không tăng độ phức tạp một lần.

### Hướng 3 — Thành tích mở khóa trang trí

Kết nối Battle Mode với Pet Mode: thắng trận đầu nhận khăn; hạ boss bằng búa nhận huy hiệu; thử thách né đòn nhận phụ kiện. Đây chỉ là ví dụ để chọn phạm vi nhỏ.

- Phần thưởng ngoại hình, không khóa chăm sóc/sinh hoạt bình thường sau yêu cầu đánh quái.
- Không tự thay phụ kiện đang dùng khi nhận thưởng; thông báo nhẹ và cho người dùng chủ động trang bị.
- Điều kiện dựa trên sự kiện trận thật, tính một lần, không nhận trùng khi replay result/AI restart; chỉ tính trận hoàn tất theo luật.
- Lưu tương thích dữ liệu cũ, giữ unlock khi asset tạm thiếu; test bằng dữ liệu riêng. Không thêm shop/currency/inventory lớn trong đợt đầu.

### Hướng 4 — Sổ quái (tùy chọn, chưa chốt thứ tự)

Bảng nhỏ từ registry quái: hình ảnh, đặc tính, cách đối phó, số lần hạ. Có quy tắc rõ khi nào coi là đã gặp; không cộng số liệu trong preview hoặc test. Tận dụng localization/render dữ liệu đã có, không lặp một bản chỉ số riêng dễ lỗi thời.

### Thứ tự và giới hạn

Đề xuất: nâng cấp giữa đợt → một quái vai trò mới → thành tích trang trí; sổ quái là lựa chọn phụ. Đây là thứ tự gợi ý để người dùng quyết định, không yêu cầu tự làm hết. Trước khi chốt hướng 1, AI phải kiểm tra lifecycle/wave/UI/data/records thực tế và chỉ ra tác động tới balance kiếm/búa. Chưa thêm LLM/voice/CV hoặc framework plugin.

Bàn giao sau kiểm tra: tóm tắt hiện trạng, đề xuất phạm vi một đợt, rủi ro và tiêu chí nghiệm thu; phân biệt mô phỏng với playtest thật. Khi có triển khai được yêu cầu, cập nhật trạng thái ở đây cùng PROGRESS.md, MANUAL-TEST.md và BATTLE-DATA.md nếu schema đổi.

## Đợt trước — Trang bị và phong cách chiến đấu (2026-10-19)

Trạng thái: **đã triển khai ở đợt 15 (2026-10-19, commit 5f4d471, đã publish)** — bảng chuẩn bị có preview + thẻ vũ khí + Xem thử, hai phong cách kiếm/búa (hitEffect, hoạt ảnh theo mốc chạm đích, số liệu búa mới), bảng kết quả Chơi lại/Đổi trang bị/Về desktop, kỷ lục v2 theo vũ khí + độ khó với Legacy. Test tự động: 55 Python + 624 C# + 395 WPF đạt. Chưa chơi thử thật; số liệu búa chỉ chốt bằng mô phỏng. Chi tiết trong PROGRESS.md. Ghi chú gốc của yêu cầu: Người dùng cho biết luồng chuẩn bị đã code xong nhưng giao diện còn quá nhiều text/button và kết quả thiếu đường quay lại đổi vũ khí/độ khó. Người dùng muốn gộp cải tiến UI với mở rộng phong cách chiến đấu. Mục này ưu tiên hơn các phạm vi cũ bên dưới, gồm việc cho phép thêm vũ khí thứ hai trong đợt này. Đọc code thật trước khi sửa; không xây lại phần đợt 14 đã làm.

### Mục tiêu và luồng

Chọn cách đánh → xem mèo cầm vũ khí → chơi → xem kết quả → thử lại hoặc đổi trang bị.

- Giữ entry: chuột phải → Battle Mode → Bắt đầu trận → bảng chuẩn bị. Không đưa lựa chọn vũ khí/độ khó trở lại menu chuột phải.
- Bảng chuẩn bị: preview mèo đúng coat/accessory, vũ khí được chọn; thẻ vũ khí có hình/tên/mô tả ngắn; chọn độ khó rõ ràng; hướng dẫn thu gọn; Đóng và Sẵn sàng.
- Quái/timer/kỷ lục chỉ chạy sau Sẵn sàng và kiểm tra lại điều kiện an toàn. Chuẩn bị/preview không được tác động simulation mèo thật.

### 1. Thiết kế lại bảng chuẩn bị

- Ưu tiên hình mèo và trang bị, khoảng cách/bố cục rõ, một nút hành động chính. Tránh biến thành form nhiều dòng chữ hoặc quá nhiều nút ngang hàng.
- Thẻ dùng vector/art style hiện có, không dùng emoji hệ thống làm hình cuối. Có tên, đặc tính và selected state ngoài màu sắc; tương phản tốt nền sáng/tối.
- Thông số so sánh ngắn; chi tiết/hướng dẫn chỉ mở khi cần. Nếu dùng thanh/chấm chỉ số phải có thang nhất quán, không minh họa sai số liệu thật.
- Preview cập nhật ngay khi đổi lựa chọn. Nút Xem thử cho mèo preview thực hiện một đòn; model, timer, hiệu ứng tách khỏi mèo thật, không bắt đầu trận/gây damage. Hủy animation và giải phóng tài nguyên khi đóng; âm thanh preview nếu có phải theo chính sách âm thanh hiện tại.
- Thẻ lấy từ registry dữ liệu, không hard-code một button/handler mỗi weapon ID. Chỉ hiển thị vũ khí engine thực sự hỗ trợ, không có lựa chọn giả.
- Nhớ weapon ID và độ khó hợp lệ; dữ liệu cũ hoặc ID bị xóa fallback an toàn. Tôn trọng vi/en, DPI và focus/click-through.

### 2. Hai phong cách chiến đấu

Inspect vũ khí hiện có trước: nếu đã có thì hoàn thiện khác biệt, không tạo bản trùng. Mục tiêu tối đa hai phong cách cho đợt này:
- Kiếm: nhanh, tầm ngắn, ít đẩy, thuận lợi duy trì combo nhưng cần áp sát.
- Búa: chuẩn bị đòn lâu, đẩy mạnh, tạo khoảng cách nhưng có thể hụt khi quái di chuyển. Animation/vệt đánh thể hiện độ nặng.
- Không biến búa thành bản kiếm mạnh hơn mọi mặt. Chốt con số sau mô phỏng và playtest; không tự khẳng định đã cân bằng tốt.
- Tái sử dụng WeaponDefinition/runtime và schema đợt 14. Khả năng mới cần implementation rõ; JSON không tự tạo hành vi. Mẫu dùng chung không chứa cooldown/swing state của instance.
- Tầm đánh, hit timing, cooldown và hit effect phải áp dụng thật vào combat và khớp thẻ/preview. Snapshot loadout khi bắt đầu trận. Không đổi vũ khí giữa trận trong đợt này.

### 3. Bảng kết quả và điều hướng

- Hiện hình mèo thắng/thua, vũ khí, độ khó, thời gian, số quái hạ và kỷ lục; chỉ số phụ thu gọn.
- Chơi lại: tạo trận mới cùng loadout/độ khó của trận vừa xong, kiểm tra an toàn; nếu chưa thể bắt đầu thì thông báo hoặc về chuẩn bị, không cưỡng ép ngủ/drag/flight.
- Đổi trang bị: mở bảng chuẩn bị với lựa chọn vừa dùng, cho đổi cả vũ khí lẫn độ khó.
- Về desktop: đóng UI kết quả, giữ/trả Pet Mode hợp lệ.
- Bỏ tự đóng theo thời gian của bảng kết quả để người dùng có thời gian chọn. Vẫn giữ xử lý fullscreen/đóng app hiện có, không để UI bật đè fullscreen.
- Chuẩn bị/đang đánh/kết quả có lifecycle rõ; chống double-click, mở chồng cửa sổ, hai trận, timer sót, tính kết quả hai lần. Trước khi mở màn mới phải dọn hoặc chuyển màn cũ đúng cách.

### 4. Kỷ lục công bằng và tương thích

- Kiểm tra cấu trúc kỷ lục hiện tại. Đề xuất tách theo weapon ID + độ khó, hiển thị loadout trên kết quả.
- Dữ liệu cũ không được xóa hoặc tự gán cho một weapon nếu không có cơ sở. Giữ dưới nhãn Legacy/Trước phân loại trang bị hoặc migration có bằng chứng rõ ràng.
- Dùng ID ổn định, không dùng tên dịch làm khóa; vũ khí bị xóa không làm mất dữ liệu hoặc crash. Ghi phiên bản cấu trúc lưu nếu thay schema; giữ ghi file an toàn.
- Không cần leaderboard/network trong đợt này. Các bản custom khác stats nhưng cùng ID chưa đảm bảo so sánh công bằng; ghi giới hạn hoặc phân biệt cấu hình nếu triển khai hợp lý, không tuyên bố kỷ lục cạnh tranh tuyệt đối.

### 5. Test và bàn giao

- Test lựa chọn được nhớ, ID mất/không hợp lệ, thẻ sinh từ dữ liệu, snapshot loadout và stats thực sự tác động hit/cooldown/range.
- Preview không sửa model/HP/position/AI của mèo thật; đóng bảng không còn timer/âm thanh/animation nền.
- Test Chơi lại/Đổi trang bị/Về desktop, double click, bảng đang mở mà mèo ngủ/drag/flight, fullscreen, đóng app. Không tính win/loss thêm khi chỉ đổi trang bị.
- Test đọc/migrate kỷ lục cũ, tách weapon+difficulty, dữ liệu lỗi; dùng dữ liệu test riêng.
- Chạy regression Pet Mode và combat. Xem UI vi/en, text dài, DPI 125–150%, preview kiếm/búa không clipping; chỉ ghi đã test thủ công khi thực sự kiểm tra.
- Giữ các lỗi test phụ thuộc chuột/màn hình đã ghi ở đợt 14 làm hạn chế cần xác minh, không che bằng chạy lại đến xanh hoặc coi kết quả cũ là test mới.
- Cập nhật PROGRESS.md, MANUAL-TEST.md và BATTLE-DATA.md nếu schema đổi; báo rõ file thay đổi, kết quả và phần chưa xác minh.

### Không thuộc đợt này

Nâng cấp giữa đợt, shop/inventory lớn, mở khóa trang bị, quái/kỹ năng mới, đổi vũ khí giữa trận, LLM/voice/CV. Hướng sau khi đợt này ổn: chọn một nâng cấp giữa các đợt quái, chỉ hiệu lực trong trận; chưa triển khai ngay.

Lần bàn giao chỉ ghi tài liệu. AI tiếp nhận đọc AGENTS.md, giải thích kế hoạch và làm theo phạm vi người dùng giao; không tự publish/commit/đóng hoặc restart pet. Giữ nguyên thay đổi chưa commit của người dùng/Claude.

## Yêu cầu trước — luồng chuẩn bị và khung dữ liệu (2026-10-19)

> Trạng thái (đợt 14, 2026-10-19, commit 8a29302, đã publish): đã triển khai A, B, C. Bảng chuẩn bị ở PetWindow.BattlePrep.cs. Dữ liệu tùy chỉnh ở %LOCALAPPDATA%\DesktopPet\battle.json (schema và ví dụ: [BATTLE-DATA.md](BATTLE-DATA.md), [examples/battle.json](examples/battle.json)). Test tự động: 55 Python + 614 C# đạt; WPF 379 đạt, 6 check cũ thất bại do chuột thật ở màn hình phụ (bản HEAD cũng thất bại y hệt). Chưa chơi thử trên desktop thật. Chi tiết trong PROGRESS.md.

Người dùng yêu cầu bàn giao để Claude triển khai luồng vào trận và khung dữ liệu mở rộng dưới đây. Phần này thay thế thứ tự ưu tiên cũ của review 18/10; các mục review phía dưới giữ làm backlog/thông tin lịch sử, không phải điều kiện phải hoàn tất trước luồng mới. Đọc code và giải thích kế hoạch trước khi sửa, giữ phạm vi đã yêu cầu. Không tự publish, commit, đóng/restart pet hoặc ghi đè thay đổi chưa commit.

### A. Luồng vào trận

Chuột phải → Battle Mode → Bắt đầu trận → Bảng chuẩn bị → Sẵn sàng → Chiến đấu → Kết quả.

- Không yêu cầu chọn vũ khí hoặc độ khó trong menu chuột phải trước khi bấm Bắt đầu trận.
- Bắt đầu trận mở phiên chuẩn bị, chưa spawn quái, chưa gây sát thương/chạy timer trận hoặc tính kỷ lục.
- Bảng chuẩn bị nhỏ chứa chọn vũ khí, chọn độ khó, hướng dẫn có thể bỏ qua/xem lại, nút Sẵn sàng và Hủy/Đóng. Nhớ lựa chọn hợp lệ trước đó để lần sau vào nhanh.
- Sẵn sàng mới bắt đầu simulation; kiểm tra lại điều kiện an toàn tại thời điểm bấm, không chỉ khi mở bảng. Chống double-click tạo hai trận/cửa sổ.
- Trong Preparing, quy định rõ quyền sở hữu hành vi: không để mèo tự ngủ/đi chơi hoặc Python ghi đè chuẩn bị. Không cưỡng ép cắt ngủ/drag/flight khi mở; nếu người dùng kéo mèo hoặc fullscreen/pause xuất hiện, chặn Sẵn sàng đến khi an toàn.
- Hủy/đóng chuẩn bị không tính thắng/thua, không cấp thưởng và trả Pet Mode đúng trạng thái; không để timer/window/event handler sót lại.
- Chơi lại từ bảng kết quả quay về chuẩn bị với lựa chọn cũ. Trong trận HUD chỉ tập trung HP/kỹ năng/pause/end; không đổi vũ khí giữa trận trong đợt này.
- Hướng dẫn phải nằm trong trải nghiệm Battle Mode, không thêm tầng menu chuột phải. Tôn trọng localization, DPI, focus và click-through. Chuẩn bị không nhận phím ngoài ý muốn từ app khác.

### B. Chuẩn hóa nội dung theo mẫu và instance

Mục tiêu: slime nhỏ/slime đá/slime vua đều là quái chung. Thêm biến thể cùng khả năng bằng dữ liệu, không viết lớp riêng cho từng biến thể. Phát triển từ battle.json, EnemyType, Slime/BattleSim và hệ render hiện có; tên dưới đây là trách nhiệm khái niệm, không ép đổi tên toàn bộ project.

- EnemyDefinition: ID, family, tên địa phương hóa, ngoại hình, stats, movement, attack, resistances, abilities.
- EnemyInstance: tham chiếu mẫu và trạng thái riêng mỗi con (HP, vị trí, cooldown, target, state). Không sửa mẫu dùng chung trong trận.
- EnemyController: thực thi khả năng được hỗ trợ; EnemyRenderer: vẽ theo renderer/style đã đăng ký. Không đưa UI/physics sang Python.
- WeaponDefinition: ID/tên, damage, range, cooldown/windup/hit timing, animation và hit effect; trạng thái swing/cooldown nằm ở runtime riêng.
- WaveDefinition: nhóm enemy ID, số lượng, lịch spawn/thời gian nghỉ.
- BattleRules: giới hạn quái, độ khó, cứu nguy và điều kiện kết thúc. SkillDefinition có thể theo cùng nguyên tắc nhưng không refactor toàn bộ kỹ năng nếu chưa cần.

Ví dụ khái niệm (không buộc thay nguyên schema đang có): stone_slime có family=slime, renderer=slime, scale=1.35, HP=120, moveSpeed=22, attack=melee, knockbackMultiplier=0.35 và stunDurationMultiplier=0.5. Slime nhỏ thay size/HP/speed; boss dùng khả năng summon/slam đã hỗ trợ. Các con số chỉ minh họa, không tự thay cân bằng hiện tại.

Quy tắc dữ liệu:
- schemaVersion, ID ổn định, validation ID trùng/tham chiếu thiếu/kiểu không hỗ trợ/NaN và giới hạn số liệu; lỗi có thông tin để sửa.
- Tách kích thước vẽ khỏi hitbox và attack range; mặc định tương thích để không âm thầm đổi combat cũ.
- Khả năng mới như bay/bắn đạn vẫn cần code controller/renderer; JSON chỉ chọn khả năng đã hỗ trợ, không chạy code tùy ý.
- Snapshot cấu hình khi bắt đầu trận; không đổi số liệu giữa trận khi người dùng sửa file.
- Cấu hình lỗi fallback an toàn, không crash startup hoặc để chạy dữ liệu nửa hợp lệ. Hỗ trợ dữ liệu/preferences cũ và ID lựa chọn cũ không còn tồn tại.
- Xác định rõ nơi custom: tài liệu schema và ví dụ. Ưu tiên bản built-in làm mặc định + file override JSON ngoài ở thư mục dữ liệu ứng dụng cho người dùng chỉnh, được validate khi tải; không đòi sửa/rebuild exe chỉ để đổi một biến thể. Đường dẫn cụ thể thống nhất với conventions của project và ghi trong docs.
- Không cần framework plugin hoặc editor đồ họa trong đợt này.

### C. Phạm vi triển khai và tiêu chí nghiệm thu

1. Inspect hệ vũ khí/quái hiện tại; giữ mọi nội dung đang có. Nếu chỉ có một vũ khí, vẫn tạo selection từ registry dữ liệu, không bịa thêm cả hệ trang bị. Thêm vũ khí thứ hai là đợt sau nếu người dùng yêu cầu.
2. Triển khai lifecycle chuẩn bị trận và UI, rồi nối lựa chọn vào combat hiện có. Giữ pause reasons, sleep lock, drag/throw/dizzy, AI fallback, kỷ lục và end battle.
3. Chuẩn hóa dữ liệu tăng dần. Chứng minh bằng fixture/ví dụ một biến thể slime mới nạp được mà không sửa controller; không tự chèn biến thể thử vào wave mặc định.
4. Test: mở/hủy/ready/replay, double click, trạng thái mèo đổi khi đang chuẩn bị, pause/fullscreen, resume/end; mỗi enemy có HP độc lập; dữ liệu lỗi/tham chiếu thiếu/phiên bản sai; override hợp lệ và fallback; lựa chọn vũ khí thực sự áp dụng vào trận và giữ snapshot.
5. Chạy regression phù hợp, cập nhật MANUAL-TEST.md, hướng dẫn custom và PROGRESS.md. Phân biệt probe/test với chơi desktop thật. Không dùng kết quả test cũ làm kết quả của đợt mới.

Các đề xuất review cũ (frame gap, playtest, nhận diện beta) vẫn có giá trị nhưng không ép người dùng hoàn tất theo thứ tự cũ trước khi làm nhiệm vụ trên. Shop/inventory, đổi vũ khí giữa đợt, pet pack tổng quát, LLM/voice/CV chưa thuộc phạm vi.

## Đánh giá và bằng chứng hiện tại

Project phù hợp dùng cá nhân/beta nhỏ; chưa đủ kiểm chứng để kết luận ổn định rộng rãi. Combat C# độc lập Python; có quái/boss, 5 kỹ năng, combo, độ khó, kỷ lục. Giữ engine WPF/vector hiện tại, Python bridge và fallback. Không xây lại ứng dụng.

Review độc lập sau đợt 11 đã chạy scripts/test.ps1 -Ui: **55 Python tests, 575 C# checks, 328 WPF checks đều đạt**, exit 0. Không phát hiện lỗi nghiêm trọng mới trong phần đã kiểm tra; không phải bảo đảm không còn lỗi. Lần ghi tài liệu này không chạy lại test và không thay source.

Chưa kiểm tra chơi thật, âm thanh trên loa, máy sạch hoặc mixed-DPI/nhiều màn hình vật lý trong lần review. Các mô phỏng/probe không thay thế những kiểm tra đó. Publish của Claude được ghi riêng trong PROGRESS.md; reviewer không publish/restart.

## Đã xử lý — không sửa lại nếu chưa có bằng chứng hồi quy

| Vấn đề cũ | Trạng thái sau đợt 11 |
| --- | --- |
| Pause ứng dụng vẫn dùng được Shield/chọn mục tiêu | Đã thêm BattlePause.App, đồng bộ SetPaused, chặn kỹ năng/target; có test giữ pause riêng của trận khi resume ứng dụng. |
| Nhấc/thả hủy mọi đòn quái dù grace có cooldown | Đã đổi: Windup giữ tiến độ khi mèo được cầm, tiếp tục khi mèo xuống. Có mô phỏng đo HP/số đòn cho slime và boss ở 3 độ khó. Cảm giác công bằng chưa được chơi thử xác nhận. |
| Test đẩy quái thất bại do spawn gần mép | Đã đặt quái ở vị trí xác định, thêm test biên riêng; suite review đạt. |
| Tài liệu bàn giao còn coi ba lỗi trên là chưa sửa | Đã cập nhật trong lần bàn giao này. |

Lịch sử: review đợt 10 từng đạt 55 Python/559 C#, WPF 322 đạt + 1 lỗi. Đó là kết quả cũ, không phải trạng thái test hiện tại.

## Ưu tiên 1 — Số đo giật khung hình chính xác

> Trạng thái (đợt 12, 2026-10-18, commit cb89351): đã sửa — RecordFrame nhận elapsed thật, simulation giữ dt chặn 0.1 s; có FrameChecks (gap giả 300 ms). Chưa đo thật khi idle/ngủ/battle/boss/pause trên desktop.

Điểm kiểm tra: src/DesktopPet/PetWindow.cs, Tick và RecordFrame; scripts/perf.ps1 nếu liên quan.

Phát hiện từ code: dt đã bị Math.Clamp(..., 0, 0.1) trước khi truyền vào RecordFrame. Khựng 300 ms có thể chỉ được ghi thành 100 ms. Đây là lỗi đo lường, chưa phải bằng chứng ứng dụng đang khựng.

Hướng thực hiện:
- Tách elapsed thực giữa các frame được xử lý khỏi dt giới hạn dùng cho simulation.
- RecordFrame đo elapsed thực; physics/combat vẫn giữ clamp và bước tích phân hiện có.
- Ghi rõ đơn vị, cửa sổ thống kê và ý nghĩa max gap; không thay timestep để làm đẹp số đo.
- Kiểm tra bằng thời gian giả: gap 300 ms phải báo khoảng 300 ms nhưng simulation vẫn không chạy bù vượt giới hạn.
- Đo khi idle/ngủ/battle/boss/pause với cùng điều kiện. Chưa kết luận RAM tăng là leak nếu chưa phân biệt heap sống và bộ nhớ GC giữ lại.

## Ưu tiên 2 — Hoàn thiện trải nghiệm trận đầu

> Trạng thái (đợt 12, commit 18cb227): đã làm hướng dẫn 4 thẻ (tự mở 1 lần, mở lại từ menu, trận dừng khi mở, không lấy focus) và phản hồi giữ đòn/bảo vệ trên sàn + HUD; có TutorialChecks. Chưa xem trực quan trên nền sáng/tối thật, chưa có playtest. Không đổi luật sát thương/cứu nguy.

### Hướng dẫn ngắn

- Hướng dẫn có thể bỏ qua và mở lại: click quái chọn mục tiêu; chọn kỹ năng rồi click quái; kéo mèo để cứu; giải thích cooldown/bảo vệ sau tiếp đất.
- Tôn trọng tiếng Việt/Anh, mute và focus; không bắt bàn phím khi người dùng đang gõ ứng dụng khác.
- Không spam hướng dẫn mỗi trận; nếu thêm preference thì default và load dữ liệu cũ phải an toàn.
- Hướng dẫn không tự bắt đầu trận khi mèo ngủ/đang kéo/bay và không gây trôi timer hoặc đòn đánh nền.

### Phản hồi nguy hiểm và trạng thái kỹ năng

- Làm rõ quái đang giữ tiến độ chuẩn bị đánh khi mèo được cầm. Thả gần quái có thể tiếp tục đòn sắp tới; người chơi cần hiểu qua cảnh báo.
- Phân biệt bảo vệ sau tiếp đất, thời gian chờ cấp bảo vệ lại và kỹ năng đang bị khóa do cầm/bay.
- Dùng HUD/effect hiện có, tương phản trên nền sáng/tối; tránh thêm cửa sổ lớn, hiệu ứng rối hoặc thông báo liên tục.
- Không tự đổi luật sát thương/cứu nguy nếu chưa có bằng chứng playtest và thống nhất thiết kế.

## Ưu tiên 3 — Playtest và kiểm chứng bản beta

> Trạng thái (đợt 12): CHƯA chơi thử thật. Đã thêm bảng Nhật ký chơi thử + câu hỏi vào docs/MANUAL-TEST.md cho người dùng điền.

Thực hiện 3–5 trận mỗi độ khó nếu có khả năng chơi thật. Ghi: phiên bản, độ khó, thời lượng, thắng/thua, HP cuối, số lần cứu, kỹ năng dùng và vấn đề quan sát. Không bịa dữ liệu nếu chỉ chạy simulation; ghi phần cần người dùng kiểm tra.

Câu hỏi cần trả lời:
- Đòn quái sau khi thả mèo có dễ hiểu/công bằng không?
- Năm kỹ năng có vai trò rõ, hay chỉ bấm tất cả khi hết cooldown?
- HUD/click-through/focus có ảnh hưởng ứng dụng bên dưới không?
- Âm thanh chiến đấu lâu có quá dày/to? Cảnh báo có rõ khi nhiều quái/boss?
- Các mức khó có tạo quyết định khác nhau hay chỉ kéo dài trận?

Kiểm tra thêm: pause giữa throw, menu/fullscreen, tắt Python, start/end nhiều lần, đóng app giữa trận, màn hình mất kết nối và mixed-DPI nếu có phần cứng. Dùng dữ liệu thử riêng, không ghi đè preferences/kỷ lục thật.

## Ưu tiên 4 — Nhận diện bản phát hành

> Trạng thái (đợt 12, commit facb54d): build ghi commit + ".dirty" khi có thay đổi chưa commit (About, log); ZIP đặt tên theo version + commit (+ thời gian nếu dirty). Chưa đổi số version, chưa changelog/tag, chưa thử máy sạch (cần người dùng quyết/làm).

Hiện csproj để Version=1.0.0, scripts/publish.ps1 đặt tên ZIP theo version và thay ZIP cùng tên. Có nhiều thay đổi chưa commit. Đây là rủi ro truy vết phiên bản, không phải lỗi combat.

- Khi chuẩn bị phát hành, thống nhất version beta và changelog; đưa version/mã commit vào thông tin build nếu phù hợp.
- Nếu build từ working tree có thay đổi, thể hiện trạng thái đó; mã commit một mình không nhận diện đủ nội dung chưa commit.
- Không ghi đè gói bản cũ chỉ vì cùng tên; có chiến lược tên artifact phân biệt phiên bản.
- Chốt commit/tag theo chỉ dẫn người dùng, không tự commit/publish trong nhiệm vụ review/tài liệu.
- Thử gói đầy đủ trên máy sạch: không cần Visual Studio, có .NET/Python/assets cần thiết; AI lỗi thì local fallback vẫn hoạt động.

## Tương lai — chưa nằm trong đợt triển khai này

> Trạng thái (đợt 13, 2026-10-18): đã làm hai vũ khí theo yêu cầu người dùng — Kiếm (giữ số cũ) và Búa (battle.json "hammer"), chọn trong menu, kỷ lục chung theo độ khó; có BattleWeaponTests + WeaponChecks và số đo mô phỏng trong PROGRESS.md. Chưa chơi thử thật.

Sau khi chốt trải nghiệm trận đầu, cân nhắc hai vũ khí: kiếm nhanh và búa chậm có đẩy lùi. Mục đích tạo lựa chọn chiến thuật mà không thêm nhiều nút. Chưa thêm inventory/shop, nhiều quái/kỹ năng, LLM/voice/CV lúc này.

Giữ logic combat độc lập WPF; khi mở rộng, tách theo trách nhiệm thay vì tiếp tục dồn vào PetWindow. Không refactor diện rộng chỉ vì file dài.

## Tiêu chí bàn giao sau khi thực hiện

- Liệt kê file thay đổi, hành vi mới và rủi ro; chạy kiểm thử phù hợp, giữ regression Pet Mode (sleep/wake/drag/throw/dizzy/emotion/AI fallback).
- Ghi kết quả thật, phân biệt unit test, WPF probe và desktop vật lý.
- Cập nhật PROGRESS.md, docs/MANUAL-TEST.md và trạng thái từng mục ở đây. Nếu chưa làm được playtest/máy sạch, ghi rõ chưa xác minh thay vì đánh dấu hoàn thành.
