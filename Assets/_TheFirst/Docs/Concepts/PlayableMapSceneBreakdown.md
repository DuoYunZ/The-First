# 局内地图场景拆分方案

目标：把世界小地图上的区域，拆成真正可玩的俯视角局内场景。参考方式是“南瓜庭院”图：上半部分是可玩场景，下半部分是可复用资产套件。

## 通用规则

- 视角：正交俯视 3D，镜头固定 55 到 60 度俯角。
- 可玩区：画面中心 70% 到 80% 保持低障碍、清晰地面，适合类吸血鬼幸存者移动和刷怪。
- 装饰区：高密度道具放在外圈和边界，不要堆在玩家主移动区。
- 碰撞原则：围栏、墙、悬崖、建筑是硬边界；小花、叶子、糖果碎、石子多做无碰撞 decal。
- 地面层级：基础 tile、路径 tile、污渍/叶子/裂缝 decal、边缘过渡、少量发光危险区。
- 每张图都需要一套 asset strip：地面、路径、decal、边界、角落件、主题地标、灯/提示物、Boss 门。
- 怪物读图：小怪颜色和地图主题一致，但精英怪/危险怪要有高对比色，方便战斗中识别。

## 01 南瓜庭院 Pumpkin Garden

这是图 1 已经成立的模板，后续地图都按这个密度和结构拆。

局内场景：
- 中心是大草坪战斗区，外圈用木栅栏、南瓜田、树丛围出边界。
- 左上南瓜屋、上方 Boss 南瓜门、右上糖果井、左下南瓜车都是外圈地标。
- 石子路只做引导和装饰，不把主战斗区切碎。

资产条：
- Grass Tile
- Stone Path
- Leaf Decals
- Low Fence
- Fence Corner
- Pumpkin Patch
- Edge Tree
- Pumpkin Hut
- Candy Well
- Boss Arch

怪物方向：
- 南瓜仔：基础近战，慢速包围。
- 藤蔓爬虫：地面贴近移动，死亡留下短暂减速藤蔓。
- 南瓜车灵：直线冲撞，适合边界外刷入。
- 稻草灯魂：远程小弹，橙色发光高可读。
- Boss：巨型南瓜门守卫，从 Boss Arch 出场。

## 02 糖果工坊 Candyworks

小地图来源：左侧粉紫糖果工厂岛。

局内场景：
- 中心是淡紫糖霜广场，四周是糖果传送带、糖浆池、糖罐机器。
- 外圈可以有弯曲糖果管道和圆形糖果轨道，但主区域必须留空。
- 地图边界用糖霜墙、棒棒糖栏杆、软糖灌木形成。
- 可玩危险物是糖浆池和短段传送带，不做太多硬障碍。

资产条：
- Frosting Floor Tile
- Candy Stone Path
- Sugar Sprinkle Decals
- Lollipop Fence
- Candy Fence Corner
- Conveyor Segment
- Syrup Puddle
- Gumdrop Bush
- Candy Vat
- Candy Factory Arch

怪物方向：
- 软糖团：基础近战，受击有弹性缩放。
- 棒棒糖旋风：旋转移动，近身造成持续伤害。
- 糖豆喷射机：小型远程怪，喷散射糖豆。
- 焦糖女巫：精英，放一滩黏糖浆减速玩家。
- 糖霜巨人：大体型慢速，死亡分裂成两个软糖团。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween candy factory zone, large clear purple frosting combat floor in the center, candy conveyor belts and syrup puddles only near the edges, lollipop fences, gumdrop bushes, candy vats, curved candy pipes, warm stylized 3D mobile game art, bottom asset strip showing modular pieces: frosting floor tile, candy stone path, sprinkle decals, lollipop fence, corner fence, conveyor segment, syrup puddle, gumdrop bush, candy vat, factory arch, no UI, no characters, clean readable gameplay space.
```

## 03 熔炉工厂 Furnace Foundry

小地图来源：左上棕橙熔炉工业岛。

局内场景：
- 中心是冷却后的黑褐石地，外圈是熔炉、管道、铆钉钢梁、岩浆沟。
- 岩浆裂缝可以做周期发光危险区，但不要切断主要移动路线。
- Boss 门是大型铁炉门，周围有橙色炉光和烟囱。
- 边界用矮石崖、铁管栏杆、熔渣堆组合。

资产条：
- Basalt Floor Tile
- Brass Path Tile
- Lava Crack Decal
- Pipe Fence
- Pipe Corner
- Coal Pile
- Boiler Stack
- Molten Vat
- Furnace Door
- Smelter Boss Arch

怪物方向：
- 煤渣小鬼：基础近战，死亡有小火星。
- 熔岩蛞蝓：慢速，留下短火痕。
- 锅炉工：远程怪，抛弧线火球。
- 管道蜘蛛：从边界管道钻出，短距离突进。
- 熔炉监工：精英，周期性给附近怪物加速。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween furnace foundry zone, large open dark basalt combat floor, orange lava cracks and glowing smelter details near the edges, chunky pipe fences, coal piles, boiler stacks, molten vats, big furnace boss door, warm stylized 3D mobile game art with readable center, bottom asset strip showing modular basalt tile, brass path, lava crack decal, pipe fence, pipe corner, coal pile, boiler stack, molten vat, furnace door, smelter boss arch, no characters, no UI.
```

## 04 冰霜糖堡 Frost Candy Keep

小地图来源：右上冰雪城堡岛。

局内场景：
- 中心是浅蓝冰面广场，边缘用雪堆、冰晶、糖果冰塔围起来。
- 可玩区可加入少量冰滑区域，但必须视觉清晰，不能满地都是。
- 场景需要比南瓜庭院更冷、更亮，怪物轮廓用深蓝和紫色拉开。
- Boss 门是冰晶城堡门或巨大糖果冰拱。

资产条：
- Snow Ice Tile
- Peppermint Path
- Frost Crack Decal
- Low Ice Wall
- Ice Wall Corner
- Blue Pine
- Crystal Cluster
- Frozen Candy Well
- Snow Hut
- Ice Castle Arch

怪物方向：
- 雪球仔：基础近战，圆形弹跳步伐。
- 冰淇淋锥怪：沿用现有怪物形象，作为区域常规怪。
- 水晶蝙蝠：高速小怪，蓝色半透明翅。
- 薄荷雪人：远程，丢雪球弹。
- 冰堡巨像：精英，短时间生成冰墙或减速圈。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween frost candy castle zone, large clear pale blue ice combat plaza in the center, snow banks and crystal clusters around the border, peppermint paths, low ice walls, blue pine trees, frozen candy well, igloo-like snow huts, giant ice castle boss arch, stylized 3D mobile game art, bright cold palette with readable gameplay space, bottom asset strip showing snow ice tile, peppermint path, frost crack decal, low ice wall, corner, blue pine, crystal cluster, frozen well, snow hut, ice castle arch, no UI, no characters.
```

## 05 锈铁工坊 Rust Workshop

小地图来源：右侧棕色机械工坊岛。

局内场景：
- 中心是石砖和金属板混合地面，外圈堆齿轮、木箱、管道、小熔炉。
- 适合做“工业废料但卡通可爱”的风格，不要做成写实垃圾场。
- 可玩危险物是蒸汽喷口和磁力线圈，频率低、范围清楚。
- Boss 门可以是齿轮升降门或矿洞铁门。

资产条：
- Rust Stone Tile
- Metal Plate Tile
- Oil Stain Decal
- Low Pipe Rail
- Pipe Rail Corner
- Gear Pile
- Scrap Crate
- Steam Vent
- Magnet Coil
- Gear Boss Door

怪物方向：
- 齿轮鼠：小型快速近战。
- 废铁矿工：中速近战，头盔灯发光。
- 磁铁无人机：远程，短暂吸引玩家或吸引掉落物。
- 发条图腾：站桩召唤小怪，适合精英目标。
- 锈铁巨人：重型精英，慢速砸地震波。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween rust workshop zone, open center made of stone bricks and metal plates, dense border decoration with chunky gears, pipes, crates, steam vents, small furnaces, magnet coils, low pipe rails, warm brown and brass stylized 3D mobile game art, bottom asset strip showing rust stone tile, metal plate tile, oil stain decal, pipe rail, pipe corner, gear pile, scrap crate, steam vent, magnet coil, gear boss door, no UI, no characters, clean playable center.
```

## 06 污水地窟 Slime Sewer

小地图来源：下方暗绿色地窟/下水道岛。

局内场景：
- 中心是湿石地面，外圈是绿色黏液渠、破管道、蘑菇、铁栅。
- 地图要暗，但主移动区不能黑。用绿色边光和紫色毒泡泡做主题。
- 可玩危险物是毒液 puddle、喷气孔、短桥，不要做复杂迷宫。
- Boss 门是大排水口或怪物嘴形地窟入口。

资产条：
- Wet Stone Tile
- Slime Channel Edge
- Moss Decal
- Broken Pipe Fence
- Pipe Corner
- Toxic Puddle
- Sewer Lamp
- Slime Barrel
- Pipe Bridge
- Drain Boss Mouth

怪物方向：
- 黏液蝌蚪：基础近战，死亡留下小毒点。
- 管道小偷：从管口出现，短突进后撤退。
- 毒蜗牛：慢速，持续产毒液。
- 下水道眼球：远程，发射绿色小弹。
- 排水口巨口：精英或 Boss，周期性吸附并吐出小怪。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween slime sewer zone, open wet stone combat floor in the center, green slime channels and broken pipes around the border, moss decals, toxic puddles, sewer lamps, pipe bridges, glowing green bubbles, big drain boss mouth at the edge, stylized 3D mobile game art, readable center with dark charming palette, bottom asset strip showing wet stone tile, slime channel edge, moss decal, broken pipe fence, pipe corner, toxic puddle, sewer lamp, slime barrel, pipe bridge, drain boss mouth, no UI, no characters.
```

## 07 中央暗塔 Void Candy Tower

小地图来源：中心紫黑高塔。建议作为章节 Boss 图或后期高难地图，不做普通第一关。

局内场景：
- 中心是环形石路和暗紫祭坛，外圈是黑石墙、紫色裂隙、水晶塔。
- 比普通地图更规整，适合 Boss 战和精英潮。
- 场地可以有 4 个边缘水晶柱，周期点亮或作为刷怪口。
- Boss 门是塔门或传送阶梯，画面焦点必须明确。

资产条：
- Obsidian Floor Tile
- Pale Stone Ring Path
- Void Crack Decal
- Dark Low Wall
- Dark Wall Corner
- Rift Crystal
- Void Lantern
- Ritual Sigil
- Portal Stair
- Tower Boss Gate

怪物方向：
- 虚空灯魂：漂浮近战，紫色火焰尾迹。
- 裂隙骑士：中速精英，短冲锋。
- 魔眼天使：可沿用图 3 大翅膀眼球作为区域 Boss 或精英。
- 影子傀儡：复制玩家方向移动，形成包围压力。
- 高塔核心守卫：Boss，分阶段点亮四个水晶柱。

图1式生成提示词：
```text
Top-down orthographic playable arena for a cute Halloween void candy tower boss zone, large readable circular obsidian combat arena, pale stone ring paths, purple void cracks, dark low walls, rift crystals, glowing void lanterns, ritual sigil, portal stairs, tall tower boss gate at the edge, stylized 3D mobile game art, dark purple but readable, bottom asset strip showing obsidian tile, ring path, void crack decal, dark wall, wall corner, rift crystal, void lantern, ritual sigil, portal stair, tower boss gate, no UI, no characters.
```

## 怪物复用和变体建议

- 图 3 的灰色机械怪：优先放到熔炉工厂和锈铁工坊，做火焰版和锈铁版材质变体。
- 图 3 的冰淇淋怪：优先放到冰霜糖堡，也可以在糖果工坊做粉色糖霜变体。
- 图 3 的大眼翅膀 Boss：放到中央暗塔最合适，也可做糖果工坊隐藏 Boss 的低阶版本。
- 图 3 的小粉色法师/小丑：适合糖果工坊，换色后也能做冰霜糖堡远程怪。
- 图 3 的石头/铁块怪：可做冰堡巨像、锈铁巨人、熔炉监工的基础骨架。

## 制作优先级

1. 先做南瓜庭院，可作为第一张完整局内图。
2. 第二张建议做糖果工坊，因为和南瓜庭院共享“可爱糖果万圣节”语汇，资产复用高。
3. 第三张做冰霜糖堡，用冷色调验证项目是否能支持明显不同的地图主题。
4. 第四张做熔炉工厂或锈铁工坊，这两张可以共享管道、齿轮、金属材质。
5. 污水地窟和中央暗塔放后面，前者需要更强的暗色可读性控制，后者更适合作为 Boss 战地图。

## Unity 落地检查

- 每张地图先做一个 `MapName_Blockout` 场景，只放地形、边界和 3 个地标。
- 主战斗区用一个大 `Nav/Walkable` 平面验证移动空间，不要先堆道具。
- 所有边界装饰统一归到 `Environment_Border`，默认有碰撞。
- 地面 decal 统一归到 `Environment_Decals`，默认无碰撞。
- 主题危险物归到 `Environment_Hazards`，等战斗系统接入前只做视觉和占位碰撞。
- 怪物只先做概念和 prefab 名称，不直接改现有刷怪主线。
