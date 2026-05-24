# 南瓜瓜幸存者 Demo Art Direction Brief

更新日期：2026-05-07

## 局内场景总原则

局内战斗图不是局外岛屿展示图，目标是让玩家、敌人、技能特效永远清楚。

- 中心 70% 画面保留低矮空地，装饰集中在边缘。
- 边界用低栅栏、南瓜藤、巧克力槽、矮货架，不用高墙、高树冠、高屋顶。
- 地表用大色块、稀疏贴花、路径引导，避免纯色草地/木地板单调，也避免糖果碎片铺太满影响读怪。
- 所有可碰撞组件最高不超过角色 1.2 倍，地标放在边缘外圈。

## 首批落地优先级

| 优先级 | 组件组 | 具体组件 | 目的 |
|---|---|---|---|
| P0 | `Arena01_ReadableGroundKit` | `Arena01_Ground_GrassBase_8x8`、`Arena01_Ground_StonePath_DecalSet`、`Arena01_Decal_LeavesFlowers_Set`、`Arena01_GrassTuft_Low_Set` | 先解决局内草地单调，同时不影响读怪和技能特效。 |
| P0 | `Arena01_LowBoundaryKit` | `Arena01_Boundary_LowFence_Straight`、`Arena01_Boundary_LowFence_Corner`、`Arena01_Boundary_PumpkinVine`、`Arena01_PumpkinPatch_A`、`Arena01_RoundTree_Edge_A` | 替换或补强战斗边界，形成南瓜林地主题并避免镜头遮挡。 |
| P1 | `Hub_CoreReplacementKit` | `Hub_CentralPortal_Oven`、`Hub_CharacterStage_Swordsman`、`Hub_CharacterStage_Mage`、`Hub_CodexDesk`、`Hub_SkillWorkbench`、`Hub_MapBoard` | 替换两个金色雕像和不搭的糖果屋，把 HUB 统一成南瓜糖果工坊。 |

首批 AI 3D 短提示词：

```text
Arena01_ReadableGroundKit: stylized 3D top-down grass arena ground kit, soft green handpainted grass tile, sparse candy stone path decals, tiny leaves and flowers, low saturation, no tall grass, survivor-like gameplay readability, Unity URP

Arena01_LowBoundaryKit: cute Halloween pumpkin forest low boundary kit, low wooden fences, pumpkin vines, pumpkin patch clusters, short round edge trees, no camera occlusion, modular stylized 3D props for top-down combat arena

Hub_CoreReplacementKit: top-down stylized 3D pumpkin candy workshop hub kit, jack-o-lantern oven portal, two character puppet stages, open codex desk, skill workbench, map board, warm wood floor, cozy toy-like Unity URP
```

## CombatArena01 南瓜林地

定位：Demo 正式主战斗图。风格是明亮、可爱、清晰的南瓜林地，不做阴暗恐怖森林。

构图：

| 区域 | 设计 |
|---|---|
| 中央 | 24m x 24m 左右的清晰草地空地，只有轻微草纹、石子、落叶贴花。 |
| 上边缘 | 短圆树 + 南瓜拱门，作为 Boss 入场方向。 |
| 左上 | 南瓜小屋地标，放在边缘外圈，不侵入战斗区。 |
| 右上 | 糖果水井/小灯柱，提供方向识别。 |
| 左下 | 破木车 + 南瓜箱，形成记忆点。 |
| 右下 | 发光蘑菇簇/南瓜藤，提供夜光色点缀。 |
| 边界 | 低矮木栅栏 + 南瓜藤 + 南瓜田。 |

组件拆分：

| 组件名 | 用途 | AI 3D 提示词 |
|---|---|---|
| `Arena01_Ground_GrassBase_8x8` | 基础草地块 | `stylized 3D grass ground tile, top-down game asset, soft green handpainted texture, subtle grass noise, low saturation, no tall grass, seamless 8x8 modular tile, Unity URP` |
| `Arena01_Ground_StonePath_DecalSet` | 石子路贴花 | `cute candy stone path decal set, irregular flat stones embedded in grass, top-down readable, stylized 3D low profile, modular path pieces, no height obstruction` |
| `Arena01_Decal_LeavesFlowers_Set` | 地表变化 | `small autumn leaves and tiny yellow flowers decal set, pumpkin forest theme, flat ground decoration, sparse, top-down readable, cartoon 3D` |
| `Arena01_Boundary_LowFence_Straight` | 外圈边界 | `low wooden fence with candy-like rounded posts, pumpkin vines wrapped around, stylized 3D, knee-high boundary, top-down combat safe, modular straight piece` |
| `Arena01_Boundary_LowFence_Corner` | 边界转角 | `curved low wooden fence corner, pumpkin vines and small pumpkins, cute Halloween style, low occlusion, modular 3D game prop` |
| `Arena01_PumpkinPatch_A` | 南瓜田 | `pumpkin patch cluster, cute stylized pumpkins, low vines, small leaves, top-down 3D game prop, placed on arena edge, bright but not noisy` |
| `Arena01_RoundTree_Edge_A` | 边缘树 | `short round toy-like tree, stylized 3D, chunky green canopy, trunk visible, designed for arena edge only, low camera occlusion, cute pumpkin forest` |
| `Arena01_Landmark_PumpkinHut` | 主地标 | `small pumpkin hut landmark, jack-o-lantern window glow, rounded wooden door, candy fantasy Halloween style, top-down 3D, edge landmark, not too tall` |
| `Arena01_Landmark_CandyWell` | 副地标 | `small candy cane water well, round stone base, cute fantasy prop, top-down 3D, low height, readable silhouette, pumpkin forest arena landmark` |
| `Arena01_Landmark_BrokenCart` | 角落装饰 | `broken wooden cart with pumpkins, stylized 3D, low profile, placed on arena corner, cute Halloween farm prop, top-down readable` |
| `Arena01_GlowMushroom_Set` | 色彩点缀 | `glowing purple mushroom cluster, cute fantasy style, small low prop, soft emission, top-down 3D game asset, not horror` |
| `Arena01_BossGate_PumpkinArch` | Boss 方向 | `pumpkin vine arch gate, jack-o-lantern lanterns, low wide silhouette, boss entrance landmark, stylized 3D, top-down arena edge prop` |

整图概念提示词：

```text
16:9 top-down in-game combat screenshot, cute stylized 3D pumpkin forest arena, wide readable grass clearing in center, low wooden fences and pumpkin vines on edges, pumpkin hut and candy well landmarks, short round trees only at border, sparse stone path and leaf decals, Unity URP, survivor-like gameplay readability, bright Halloween not horror
```

## CombatArena02 糖果工坊

定位：非首版 Demo 正式图，作为第二张战斗图资产储备。它应该从糖果商店/HUB 转成糖果工坊战斗场，保留木地板和糖果元素，但中心要更像竞技场。

构图：

| 区域 | 设计 |
|---|---|
| 中央 | 大片木地板或糖果砖广场，保留清晰战斗空间。 |
| 上边缘 | 巧克力锅、糖霜管道、糖果搅拌机，形成工坊背景。 |
| 左右边缘 | 巧克力槽、传送带、低矮货架作为边界。 |
| 下边缘 | 糖果街区入口，棒棒糖路标、糖果箱、矮围栏。 |
| 地面 | 木板主色 + 糖霜裂纹 + 少量彩色糖粒 + 巧克力污渍。 |
| 动态装饰 | 传送带慢速动、蒸汽口喷气、搅拌锅旋转，先做视觉，不做伤害。 |

组件拆分：

| 组件名 | 用途 | AI 3D 提示词 |
|---|---|---|
| `Arena02_Ground_WoodPlank_8x8` | 主地板 | `stylized 3D wooden plank floor tile, warm candy workshop color, subtle scratches and icing cracks, seamless 8x8 modular tile, top-down combat readable, Unity URP` |
| `Arena02_Ground_CandyBrick_8x8` | 街区地面 | `pastel candy brick ground tile, low saturation, top-down 3D game floor, modular seamless, not too colorful, readable for combat VFX` |
| `Arena02_Decal_IcingCrack_Set` | 地表贴花 | `white icing crack decal set, flat ground decoration, candy factory theme, top-down readable, modular 3D decal, sparse use` |
| `Arena02_Decal_CandySprinkle_Set` | 糖粒点缀 | `small colorful candy sprinkles decal set, flat low profile, sparse ground decoration, top-down stylized game asset, not noisy` |
| `Arena02_Boundary_ChocolateVat` | 外圈边界 | `large chocolate vat boundary prop, round low edge, stylized 3D candy factory, glossy chocolate surface, top-down arena border, no tall occlusion` |
| `Arena02_Boundary_SugarPipe_LowRail` | 管线边界 | `low sugar pipe rail, candy cane stripes, modular straight piece, stylized 3D, top-down combat boundary, low height` |
| `Arena02_Conveyor_Straight` | 传送带 | `candy conveyor belt straight module, toy-like stylized 3D, chocolate rubber belt, pastel metal frame, low height, arena edge landmark` |
| `Arena02_Conveyor_Corner` | 传送带转角 | `candy conveyor belt corner module, modular 90 degree turn, stylized 3D, low profile, top-down readable` |
| `Arena02_CandyMixer_Landmark` | 大地标 | `large candy mixer machine, rounded toy-like shape, pink icing bowl, chocolate stirring arm, stylized 3D, edge landmark, not blocking center combat` |
| `Arena02_LowShelf_CandyJar` | 低货架 | `low candy jar shelf, short display rack, colorful jars, stylized 3D, top-down low occlusion, candy workshop edge prop` |
| `Arena02_CandyCrate_Set` | 箱子组 | `wooden candy crates with wrapped sweets, modular prop set, low profile, stylized 3D, top-down arena corner decoration` |
| `Arena02_Lollipop_Signpost` | 路标 | `giant lollipop signpost, short pole, cute candy street landmark, stylized 3D, readable silhouette, low enough for top-down camera` |
| `Arena02_SteamVent_Popcorn` | 动态装饰 | `popcorn steam vent, small floor vent with soft white steam, candy factory prop, stylized 3D, low base, VFX friendly` |
| `Arena02_BossGate_FactoryDoor` | Boss 入口 | `candy factory door boss gate, chocolate frame, icing lights, wide low silhouette, stylized 3D, top-down arena edge landmark` |

整图概念提示词：

```text
16:9 top-down in-game combat screenshot, stylized 3D candy workshop battle arena, wide clean wooden floor center, icing cracks and candy sprinkles as sparse decals, chocolate vats and sugar pipes as low boundaries, conveyor belts and candy mixer on edges, Unity URP, survivor-like readability, warm wood and pastel candy accents, not cluttered
```

## HUB 替换方案

推荐优先使用方案 A。它和现有糖果店资产最接近，改动成本最低，也能解释图鉴、技能树、战斗入口。

| 方案 | 核心概念 | 替换雕像 | 替换糖果屋/传送器 | 优点 |
|---|---|---|---|---|
| A. 南瓜糖果工坊 | 玩家在南瓜糖果工坊里整备出战 | 两个角色木偶展示台或糖霜玻璃展示罩 | 中央南瓜烤炉传送门，炉门打开进入战斗 | 最稳，和糖果货架/木地板兼容，Demo 成本低 |
| B. 南瓜剧场后台 | 玩家像进入一场南瓜冒险演出 | 两个小舞台，未解锁角色用幕布遮住 | 舞台中央幕布/旋转布景变成战斗入口 | 收藏感强，角色选择更有仪式感 |
| C. 糖果车站 | HUB 是去各关卡的糖果列车站 | 两个候车站台，角色站在站牌旁 | 糖果列车/南瓜车厢作为传送门 | 地图解锁表现好，但需要新增车站资产 |

方案 A 布局：

| 区域 | 设计 |
|---|---|
| 中央 | `Hub_CentralPortal_Oven`，南瓜脸炉门，顶部糖霜烟囱，炉内旋转 CombatArena01 缩影。 |
| 左侧 | `Hub_CharacterStage_Swordsman`，木偶小舞台，剑痕装饰，展示小剑士。 |
| 右侧 | `Hub_CharacterStage_Mage`，糖霜玻璃罩/星星挂饰，展示小法师；未解锁时玻璃罩微暗。 |
| 后墙 | `Hub_MapBoard`，软木地图板，只钉 CombatArena01，Hard20 用小锁链显示。 |
| 侧边 | `Hub_CodexDesk`，打开的大书、羽毛笔、糖果墨水瓶。 |
| 另一侧 | `Hub_SkillWorkbench`，角色手账、糖纸徽章、升级硬币。 |
| 工程师 | 只留 `Hub_LockedWorkshopDoor`，门牌“维修中”，不显示角色，不进 Demo 交互。 |

HUB 概念提示词：

```text
top-down stylized 3D pumpkin candy workshop hub, central jack-o-lantern oven portal, two character puppet stages for pumpkin swordsman and pumpkin mage, codex desk with open book, skill workbench, map board, warm wood floor, candy shelves integrated, readable interactable layout, Unity URP
```

```text
cute Halloween candy workshop hub, no golden statues, characters displayed on small wooden theater stages, magical pumpkin oven as battle portal, cozy toy-like materials, warm lights, clear paths made of icing lines, top-down game hub screenshot
```

## 图鉴与角色技能树 UI

目标：从调试界面改成南瓜糖果工坊里的收藏与成长界面。不只是换皮，还要让玩家知道为什么要选、如何搭配、解锁后会改变什么。

图鉴方向：Build 手册。

| 模块 | 设计 |
|---|---|
| 外观 | 保留大书，但减少空白，做成冒险图鉴 + 武器贴纸册。 |
| 左页 | 分类书签：武器 / 被动 / 融合 / 怪物。图标网格按锁定、已解锁、推荐排序。 |
| 右页顶部 | 大图标 + 名称 + 类型徽章，例如“落雷爆发 / 雷系 / 单点”。 |
| 右页中部 | 标签 chips：燃烧、冻结、雷击、穿透、召唤、可融合。 |
| 右页下部 | 推荐搭配、成长节点、解锁进度、适合角色。 |
| 锁定态 | 低饱和剪影 + 解锁条件 + 进度条 + “解锁后进入卡池”。 |

图鉴信息模板：

```text
定位：落雷爆发
触发：每 X 秒锁定敌人，从空中落雷
强势：精英/Boss、冻结目标、雷系 build
弱点：清小怪不如闪电链
推荐：冰锥 + 落雷 + 元素共鸣
成长：连续落雷 / 磁暴 / 电磁场
```

技能树方向：角色手账。

| 模块 | 设计 |
|---|---|
| 背景 | 黑板改成角色手账、木桌、羊皮纸、糖纸边框。 |
| 节点 | 基础小节点、分支中节点、终极大节点，大小区分成长价值。 |
| 连线 | 绳子改成糖霜管线、丝带、发光糖浆。 |
| 解锁态 | 已解锁是亮色贴纸/宝石；可解锁是金边脉冲；锁定是低饱和但保留图案；互斥是红色封蜡。 |
| 详情面板 | 点击节点显示数值 + 局内表现变化，例如“多一道背后刀光”。 |
| 收藏感 | 每个已解锁大节点在角色左侧生成一枚徽章，形成成长陈列。 |

UI 概念提示词：

```text
pumpkin survivor codex UI, open adventure book, weapon stickers, passive badges, build recommendation panel, locked silhouettes with progress bars, warm parchment, candy tabs, polished Unity game interface
```

```text
pumpkin swordsman skill tree UI, character notebook on wooden candy workshop desk, slash-shaped nodes, glowing syrup connectors, collectible upgrade stamps, clear branch choices, not a debug panel
```

```text
pumpkin mage skill tree UI, fire branch and ice branch separated, arcane candy gem nodes, parchment notebook, large capstone badges, readable Chinese game UI, cozy Halloween candy workshop style
```

## 主线程落地顺序

1. 先把 `CombatArena01` 按组件清单落成局内可战斗 blocking：中心空地、低边界、四角地标、地表贴花。
2. HUB 采用方案 A：南瓜糖果工坊，先替换两个金色雕像和中心传送器的主题。
3. 图鉴右页先改信息结构：定位、标签、推荐搭配、成长节点、锁定进度。
4. 技能树先换视觉框架：黑板改角色手账，节点和连线换主题材质。
5. `CombatArena02` 先只做概念图和模块资产，不进首版 Demo 正式构建。
