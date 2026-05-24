# 南瓜瓜幸存者 DEMO Vertical Slice 计划

更新日期：2026-05-07

## 当前落地状态

- Demo 门控：正式 Demo 只开放 `Role01`、`Role02` 和 `CombatArena01`，工程师相关内容保留为内部原型。
- 时间线：`CombatArena01` 会自动选择 `MainTimeline_DEMO_Intro10` 或 `MainTimeline_DEMO_Hard20`；首通 Intro 后解锁 Hard20。
- 解锁：Intro 通关解锁小法师、Hard20、落雷、经验磁铁；Hard20 通关解锁闪电链、冰霜新星、灵能飞刀、奥术精通、元素共鸣。
- 经验：困难时间线使用额外经验倍率，目标是让 20 分钟局达到更完整的 build 成型节奏。
- 稳定性：敌人材质受击闪烁已改为 `MaterialPropertyBlock`，避免运行中大量复制材质；时间线末段会校准存活敌人数。
- 落雷：`LightningStrike` 已从旧光环借壳改为独立落雷逻辑，按冷却锁定敌人，播放竖向闪电，造成主目标伤害、小范围溅射、短麻痹，并支持连续落雷/磁暴/电磁场分支。
- 元素共鸣：已从固定命中计数触发改为“概率 + 保底”触发，每拥有一个元素/法术武器都会提高触发概率、范围和伤害。
- 剑圣之魂：不再只是固定 +1 刀光；拥有该被动后，斩击武器每提升 2 级获得 1 道额外刀光，超过原 5 段配置时会动态补侧向刀光。
- 概念图：局内南瓜林地/糖果工坊战斗场景概念图保存在 `Assets/_TheFirst/Docs/Concepts/DemoCombatArena_ConceptSheet_01.png`。
- 美术方向：局内场景、HUB 替换、图鉴和技能树 UI 的组件拆分与提示词已整理到 `Assets/_TheFirst/Docs/DemoArtDirectionBrief.md`。
- 波次调优：`Wave_07_Pressure_1`、`Wave_07_Pressure_2`、`Wave_08_MidGame`、`Wave_09_Hard`、`Wave_09_Hard_1`、`Wave_10_Insane` 已从大堆同类怪改成 staggered 混合波，降低 Intro 结尾和 Hard20 后半段同屏峰值与单调感。
- 计数稳定性：奔袭怪和弹球怪超时离场时会主动通知时间线，减少“日志还有敌人但场上只剩 Boss”的计数偏移。

## 目标边界

DEMO 版本先只展示两个角色和一张正式战斗地图：

- 角色：南瓜小剑士、南瓜鬼畜米奇/小法师。
- 地图：CombatArena01。
- 不上线工程师。工程师、巨大机器人和喷火塔可以继续保留为内部原型，但不要进入 DEMO 的角色选择、HUB 展示和正式 Build。
- 正式测试建议采用两段式：第一局使用 `MainTimeline_DEMO_Intro10`，通关后解锁 `MainTimeline_DEMO_Hard20`。
- 目标体验时长：首玩 25-30 分钟内能形成完整闭环，包含 2-3 局短局、一次角色/武器解锁、一次局外升级决策。

## 下一轮测试重点

1. `Intro10` 从 07:55 到 10:00：观察 475s、545s、599s 三段是否仍然堆怪过猛；预期是 Boss 前仍有压力，但不再是纯同类怪墙。
2. `Hard20` 从 11:00 到 16:00：观察 660s、735s、825s、915s 四段是否比之前更有层次；预期是小怪、蜘蛛、近战和精英错峰进入。
3. `Hard20` 从 17:00 到 20:00：观察 1020s、1120s、1199s 三段同屏数量和 Boss 入场稳定性；若仍卡顿，下一步优先再削 `Wave_10_Insane` 的 650 主组。
4. 奔袭/弹球怪离场：关注时间线日志中的 `Enemy removed without kill`，确认“场上只剩 Boss 但日志仍剩几十只”的偏差明显减少。
5. 落雷：确认 `LightningStrike` 不再表现为旧光环，而是周期性竖向落雷，并且连续落雷、磁暴、电磁场分支能正常触发。

## 30 分钟试玩流程

### 第 0-2 分钟：HUB 初见

玩家进入糖果工坊式 HUB，只看到两个角色入口和一个战斗入口。图鉴/技能树可以打开，但锁定项显示“轮廓 + 解锁方向”，不要把全部内容平铺出来。

初始状态建议：

- 初始角色：南瓜小剑士。
- 小法师：锁定，条件为“通关第 1 局或累计获得 300 糖果币”。
- 初始武器池：刀光、火焰球、冰锥、疾风刃、环绕护盾、榴弹。
- 初始被动池：幸运四叶草、双简跑鞋、西瓜装甲、生命蛋糕、甜甜铁拳、经验磁铁、时光曲奇、范围/冷却类基础被动各 1 个。
- 图鉴锁定展示：落雷、闪电链、冰霜新星、灵能飞刀、地雷、激光核心、光环类被动等只露 silhouette 和解锁条件。

### 第 2-12 分钟：第 1 局剑士教学局

目标是让玩家明确“武器 + 被动 + 角色技能树”的组合方向，而不是只看数值上涨。

推荐事件：

- 00:00-01:30：低压敌群，快速升到 4-5 级，确保至少拿到 2 把武器。
- 02:00：第一只小精英，掉落宝箱或一次稀有升级。
- 03:30：横向冲锋怪预警，让玩家第一次移动躲避。
- 05:00：固定刷出一组高密度小怪，用来展示 AOE/穿透 build 的爽感。
- 06:30：远程敌或炸弹怪进入，让玩家感受到走位压力。
- 08:30：第二只精英，给一次高品质选择，鼓励形成流派。
- 09:45-10:00：Boss 入场。Boss 不一定复杂，但需要清晰的血条、攻击预警和胜利反馈。

第一局结束奖励：

- 通关：解锁小法师。
- 未通关但坚持 6 分钟：解锁 1 个新武器或 1 个主动被动，避免失败后空手离场。
- 结算页展示“本局核心流派”：例如“近战斩击 + 攻速 + 吸血”。

### 第 12-20 分钟：局外成长

玩家回 HUB 后做一次明确选择：

- 小剑士技能树开放 2-3 个便宜节点。
- 图鉴新增“推荐搭配”而不是只显示四个属性数字。
- 小法师解锁后在角色树里展示完全不同的 build 方向：元素扩散、爆发、控制。

### 第 20-30 分钟：第 2 局小法师或剑士重玩

第二局需要让玩家感到“换角色/换武器池后，打法真的不一样”。

推荐：

- 小法师开局提高火球/冰锥/落雷/闪电链出现权重。落雷是单点天罚和后续磁暴/连续雷击核心，闪电链是弹射清怪，两者要分开定位。
- 剑士重玩提高刀光、飞刀、吸血、冲刺爆炸的出现权重。
- 第二局结束后解锁 1-2 个更有表现力的内容：落雷、冰霜新星、闪电链、灵能飞刀、触发型被动。

## 图鉴优化

当前图鉴主要问题是“信息值低”：图标列表 + 四个数字只能回答“它是什么”，不能回答“我为什么要选它”和“它适合什么 build”。

建议改成三层信息：

1. 定位：近身斩击、直线弹道、连锁清怪、控场爆发、布置陷阱、持续光束。
2. 标签：燃烧、冻结、雷击、穿透、召唤、暴击、可进化、需要特定被动。
3. 推荐搭配：例如“刀光 + 甜甜铁拳 + 吸血之牙”，“冰锥 + 冰霜之触 + 范围提升”。

已经在代码中先补了“定位/触发节奏/标签/被动成长节点”的文本生成。后续 UI 上可以把右页改成：

- 顶部：大图标 + 名称 + 稀有度/类型角标。
- 中部：标签 chips。
- 下部：成长曲线、里程碑、推荐搭配。
- 锁定态：显示剪影、解锁条件、当前进度、解锁后会进入哪个池子。

## 角色技能树优化

当前角色树的问题不是功能不够，而是“像调试界面”：黑底、绳子连接和灰色节点弱化了角色主题。

小剑士树建议：

- 左路：斩击数量、攻速、剑气扩散，视觉节点用刀痕/刃光。
- 中路：生存、吸血、护甲，视觉节点用盾牌/红心/南瓜铠。
- 右路：冲刺爆炸、斩杀、短时间爆发，视觉节点用鞋印/爆裂南瓜。
- 里程碑节点要改变表现：额外刀光、刀光颜色、攻击节奏变化，而不只是数值。

小法师树建议：

- 火路：燃烧、爆炸、地面火焰。
- 冰路：冻结、冰环、碎裂。
- 雷/奥术路：连锁、落雷、奥术爆点。
- 里程碑节点要触发明显 VFX：空中符文、身边旋转法球、命中后小爆点。

UI 视觉建议：

- 用“糖果工坊桌面上的角色手账”替代大黑板。
- 节点连接用糖霜管线、丝带或发光糖浆。
- 未解锁节点不要全灰，应保留材质轮廓和低饱和颜色，玩家才会想解锁。

## HUB 场景重构方向

当前 HUB 是糖果商店货架 + 两个金色雕像 + 门/传送器，主题不统一。建议把 HUB 定为“南瓜糖果工坊”，让所有交互点都是同一种世界观里的设施。

核心构图：

- 中央：糖果烤炉/魔法锅炉，作为战斗传送门。传送时炉门打开，里面是旋转南瓜灯和地图缩影。
- 左侧：角色展示台。不要用金雕像，改成“南瓜木偶舞台”或“糖霜玻璃罩里的角色模型”。
- 右侧：图鉴书桌。打开图鉴时镜头靠近一本大书，而不是纯 UI 覆盖。
- 后墙：武器陈列柜。已解锁武器以小模型或图标贴纸出现，未解锁为剪影。
- 地面：糖果砖路/木地板保留，但用南瓜藤、糖霜线、发光小灯把路径引到交互点。

AI 3D 生成组件拆分：

- `Hub_CentralPortal_Oven`：圆形糖果烤炉，南瓜灯炉门，顶部糖霜烟囱。
- `Hub_CharacterStage`：两个小展示台，木偶剧场风格，预留角色站位。
- `Hub_CodexDesk`：打开的大书、羽毛笔、糖果墨水瓶。
- `Hub_WeaponShelf`：半圆陈列柜，格子足够放 12 个武器小图标/模型。
- `Hub_ProgressBoard`：软木板/糖纸公告栏，用于显示解锁目标。

概念图提示词：

```text
top-down 3D stylized pumpkin candy workshop hub, warm toy-like materials, central magical candy oven portal with jack-o-lantern door, two small character puppet stages, open codex desk, weapon display shelf, readable game interactable layout, cozy but not cluttered, Unity mobile game style, high saturation accents, soft shadows
```

## 第 1 张关卡：南瓜林地

定位：DEMO 主地图，明亮、清晰、怪物可读性高。

场景结构：

- 中央 24m x 24m 清晰战斗空地。
- 外圈是南瓜田、矮木栅栏、圆树和糖果石头，不遮挡角色。
- 地面用草地 + 糖果石子路 + 南瓜藤纹理做区域变化。
- 四角放可辨识地标：南瓜屋、糖果水井、破木车、发光蘑菇簇。
- 边界尽量低矮，不要用太高的树冠挡视角。

AI 3D 生成组件：

- `Arena01_GroundTile_GrassCandyPath`
- `Arena01_PumpkinPatch_A`
- `Arena01_Fence_CurvedLow`
- `Arena01_RoundTree_ShortCanopy`
- `Arena01_CandyStoneCluster`
- `Arena01_Landmark_PumpkinHouse`
- `Arena01_Landmark_CandyWell`
- `Arena01_GrassTuft_Set`
- `Arena01_Boundary_PumpkinVine`

概念图提示词：

```text
top-down stylized 3D pumpkin glade battle arena, circular readable grass clearing, pumpkin patches around edge, low candy fences, round toy trees, candy stone path, Halloween cute not horror, bright saturated Unity game environment, clear enemy readability, soft shadows
```

## 第 2 张关卡：糖果工坊外场

定位：不进首版 DEMO build，但可以作为下一个 20-30 分钟扩展目标。

场景结构：

- 地面是木板 + 糖霜裂缝 + 巧克力输送带。
- 场景里有动态但不致命的装饰：糖果转盘、缓慢移动的传送带、爆米花蒸汽口。
- 视觉上从绿色林地切到粉/黄/巧克力，但需要保留战斗可读性，不要铺满高饱和糖果。

AI 3D 生成组件：

- `Arena02_WoodFloor_CandyCrack`
- `Arena02_Conveyor_Straight`
- `Arena02_CandyMixer_Landmark`
- `Arena02_SugarPipe_Arch`
- `Arena02_ChocolateVat_Boundary`
- `Arena02_PopcornSteamVent_Deco`
- `Arena02_CandyCrate_Set`

概念图提示词：

```text
top-down stylized 3D candy workshop battle arena, wooden floor with icing cracks, chocolate vats on edges, candy conveyor belts as landmarks, pumpkin candy factory theme, readable central combat space, toy-like props, bright but controlled palette, Unity mobile roguelite
```

## 怪物设计建议

现有敌人已经覆盖基础追逐、直线冲锋、弹球、远程、炸弹、精英和 Boss，但外观/行为的“记忆点”还不足。DEMO 建议补 4 个可读敌人，先复用现有 AI 或小改代码。

### 1. 糖浆史莱姆

- 行为：慢速追逐，死亡后短时间留下减速糖浆。
- 作用：让玩家理解地面区域和走位。
- 可先复用 Chasing AI，死亡触发一个低伤害/减速地面 prefab。

提示词：

```text
small cute caramel slime enemy, pumpkin candy roguelite, translucent amber body, simple angry face, top-down readable 3D model, low poly stylized
```

### 2. 糖果蝙蝠

- 行为：小体型高速追逐，血低，成群出现。
- 作用：测试环绕、刀光、AOE 的清杂能力。
- 可复用现有 Chasing AI，仅调 speed/HP/scale。

提示词：

```text
small candy bat enemy, lollipop wings, cute spooky Halloween style, top-down 3D low poly, bright readable silhouette
```

### 3. 爆米花炸弹怪

- 行为：接近玩家后自爆，死亡前有明显膨胀/闪烁预警。
- 作用：制造局内意外和走位压力。
- 可复用现有 Spider suicide bomber。

提示词：

```text
popcorn bomb monster, round popcorn bucket body, fuse on top, cute angry face, stylized 3D game enemy, clear red warning color accents
```

### 4. 南瓜盾兵

- 行为：中速追逐，正面减伤或高护甲，侧后方正常受伤。
- 作用：让穿透/范围/绕位有意义。
- 代码可分两阶段：DEMO 先做高血量精英，后续再加方向减伤。

提示词：

```text
pumpkin shield soldier enemy, tiny wooden shield, pumpkin helmet, toy-like stylized 3D, top-down readable, cute Halloween fantasy
```

## 波次与经验曲线

当前 `MainTimeline 2` 总敌人数约 2284，只靠 2 点经验宝石，理论总经验约 4568。旧曲线 `baseXp=10, linear=10, power=1.5` 会让 10 分钟局结束等级偏低，build 成型太慢。

已先把默认曲线调成：

- `baseXp = 6`
- `linearFactor = 3`
- `powerFactor = 0.35`

预期：清完同等经验量时大约能到 29-31 级。这样 10 分钟局能有足够升级选择，又不会接近 40 级导致暂停过多。

时长建议：

- 首局保持 10 分钟。它负责教学、第一次通关和解锁小法师，不要求完整毕业 build。
- 通关首局后开放 20 分钟困难局。它负责让武器/被动组合真正展开，目标等级约 40-43。
- 20 分钟后再接无尽模式更合理，但当前 `GameTimelineManager` 的胜利逻辑是“全部波次触发并清空敌人后胜利”，还没有无尽循环逻辑。无尽建议作为下一步代码功能，不要混进本轮时间线资产。

新增可测试时间线：

- `Assets/_TheFirst/Prefabs/Enemies/MainTimeline_DEMO_Intro10.asset`：10 分钟首局时间线，修正 280 秒假 Boss 的节奏问题，保留最终 Boss。按当前波次粗算约 1738 只怪、3476 基础经验、约 27 级。
- `Assets/_TheFirst/Prefabs/Enemies/MainTimeline_DEMO_Hard20.asset`：20 分钟困难时间线，10 分钟处有中段 Boss，20 分钟处有最终 Boss，后半段敌量更高。按当前波次粗算约 4224 只怪，叠加 1.35x 时间线经验后约 11405 经验、约 42 级。

当前波次资产扫描结论：

- `MainTimeline_DEMO_Intro10` 已经是完整 10 分钟流程：开局、奔袭预警、混合基础怪、远程压力、精英、虫群、自爆压力、Boss 都有。
- `MainTimeline_DEMO_Hard20` 已经是完整 20 分钟流程：10 分钟中段 Boss、20 分钟最终 Boss 都有，适合作为 Demo 的“build 展开局”。
- 现有怪物资产已覆盖追逐、直线奔袭、弹球、远程、投弹、自爆、精英、Boss。短期不需要先写复杂新 AI，应该优先做外观区分和波次组合。
- 已调优后段 `Wave_07_Pressure_1`、`Wave_07_Pressure_2`、`Wave_08_MidGame`、`Wave_09_Hard`、`Wave_09_Hard_1`、`Wave_10_Insane`：最大同类小怪组从 800/1000 降到 520/650，并加入远程、投弹、特殊/精英组与延迟刷出。
- `Wave_08_MidGame` 已从单一中段压力改为 100 个小兵肉盾、45 只蜘蛛穿插和 1 个大骑士错峰进入，避免 660s 之后只是在清同类怪。
- `Wave_07_Pressure_2` 已从 530 只同开场改为 374 只错峰混合，重点缓解 Intro 545s 到 599s Boss 入场前的堆怪。
- `Wave_09_Hard` 已从 580 只同开场改为 532 只错峰混合，缓解 Hard20 的 735s/915s 重复波。
- `Wave_12_BOSS` 当前只有 1 个 Boss，适合保留；Boss 战期间不建议再叠太多杂兵，先保证 Boss 读招和胜利稳定性。

波次建议：

- 00:00-02:00：低血追逐怪，保证升级速度。
- 02:00：第一精英 + 宝箱。
- 03:30：冲锋怪横向/斜向穿场，提前给地面预警。
- 05:00：小怪潮，验证清屏爽感。
- 06:30：炸弹怪 + 远程怪混合，增加走位压力。
- 08:30：第二精英，掉高品质奖励。
- 09:45：Boss 入场，停止继续刷大量杂兵或只保留轻量压力怪。

需要修的配置点：

- `MainTimeline 2` 在 280 秒有一个事件名字像 Boss，但引用的是 `Wave_05_Swarm`，这会造成中段体验不清晰。建议改成小精英/宝箱事件，而不是再刷 100 个小兵。
- 后 480 秒和 540 秒的怪量很大，建议降低纯数量，改为更清晰的混合构成，否则只是堆怪，不一定更爽。

## 被动道具深度

被动分三类更清楚：

1. 基础数值：伤害、冷却、范围、移速、经验、拾取。
2. 流派放大器：剑圣之魂、奥术精通、元素共鸣、机械共鸣。
3. 表现型触发：燃烧轨迹、冰霜之触、雷霆意志、冲刺余烬、吸血之牙、荆棘护甲。

DEMO 初期不要给太多纯数值。每局最好能出现 2-3 个“看得见”的被动，让玩家觉得 build 改变了战斗画面。

建议删减/暂缓：

- 纯数值且没有 UI 反馈的重复项，暂时不要全进池。
- 机械共鸣在工程师不上线前可以保留锁定图鉴，但不要进正式局内卡池，避免误导玩家。

建议新增/强化：

- 火焰类被动：移动留下火焰，满级变成周期性爆燃。
- 冰霜类被动：首次命中冻结，满级冻结碎裂造成小范围伤害。
- 剑士类被动：剑圣之魂让斩击武器每 2 级追加 1 道刀光，玩家能直接看到刀光数量成长。
- 法师类被动：元素共鸣按法术/元素武器数量提高触发概率，并保留命中次数保底，避免脸黑时完全不触发。

## 下一步执行拆分

1. 编译与基础验证：确认当前图鉴脚本、DEMO 门控、经验曲线无编译错误。
2. UI 预制体绑定：给 `CodexStatSlot` 的 prefab 补 `labelText`，或接受现在的兼容模式显示“标签 + 数值”两行。
3. 时间线配置：把 280 秒错误事件改成小精英/宝箱波次；降低后段纯数量堆叠。
4. 资源生成：先生成 HUB 概念图、Arena01 概念图、4 个新怪物概念图，再拆 3D 模型组件。
5. 局内事件：补精英宝箱、冲锋预警、糖果雨/经验簇这三个小事件即可，不要先做复杂随机事件系统。
