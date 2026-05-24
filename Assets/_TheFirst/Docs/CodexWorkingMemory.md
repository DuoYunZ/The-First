# Codex Working Memory

Last updated: 2026-05-21

## Project

- Unity project: `D:\test\TheFirst`
- Game: `南瓜瓜幸存者`, a top-down cartoon 3D Vampire Survivors-like game.
- Demo scope: keep the demo focused on two playable roles, `南瓜剑士` and `鬼畜米奇/法师`; engineer is prototype-only for now and should not ship in the demo.
- `WaveManager` is deprecated. Prefer `GameTimelineManager` and timeline assets.
- The Unity repo/worktree is very dirty because assets, art, packages, and generated files are present locally. Do not reset or clean broad folders without explicit approval.

## Current Design Direction

- Core demo target: polished 30-minute-ish vertical slice, not a broad content dump.
- Game should increase build depth and replay motivation. Energy/ultimate/fusion-ultimate is not the main hook and may be reduced if it adds too much manual pressure.
- Long-term build direction favors weapon evolution: weapon + passive, or weapon + weapon, creating upgraded weapons. This should replace or reduce fusion-ultimate complexity if needed.
- Evolution design now has two target families: role/element special evolutions such as `斩击 + 风 = 风刃` and `斩击 + 雷 = 雷光刺`, plus broad weapon/passive evolutions where a weapon at Lv.5 and a matching passive at Lv.5 can evolve through treasure/recipe checks.
- Weapon categories should be meaningful build tags, not low-value behavior labels. Prefer categories such as `斩击`, `法术`, `机械工程`, `元素`, etc.
- Codex item details should stay compact: color/tag categories are enough, do not show redundant `W/P` badges, build-flow essays, tactical positioning paragraphs, or build-advice blocks unless explicitly redesigned later.

## Roles

- Swordsman:
  - Initial role.
  - Current skill tree has base nodes, precision branch, agile branch, and talents.
  - `剑圣之魂/剑圣之道` style effects should affect slash weapons and slash-evolved weapons, not only the starting blade.
  - Desired rule: slash count gains one extra blade wave per 2 slash weapon levels.
  - Runtime sword-focus direction: every 60 seconds without actual HP damage grants 1 stack, max 3. Slash area bonus is +30%, +30%, then +50% on the final stack; taking HP damage removes one stack and resets the timer. Shielded hits that do not reduce HP should not remove stacks.
- Mage:
  - Has spell/element themed skill tree.
  - `元素共鸣` should scale trigger chance by owned spell skills and should include evolved spell weapons.
- Engineer:
  - Demo release should not expose engineer.
  - Future fantasy: engineering build can combine into a giant robot.

## Recent Important Code Areas

- Character skill tree UI:
  - `Assets/_TheFirst/Scripts/Gameplay/CharacterSelectManager.cs`
  - `Assets/_TheFirst/Scripts/UI/CharacterSkillNodeUI.cs`
  - `Assets/_TheFirst/Scripts/UI/CharacterSkillTreeGridUI.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Core/PlayerProgressManager.cs`
- Global/gold UI:
  - `Assets/_TheFirst/Scripts/UI/UIManager.cs`
- Codex UI:
  - `Assets/_TheFirst/Scripts/UI/SkillTreeUIManager.cs`
  - generated/cut UI assets under `Assets/Resources/UI/DemoCodex*`
- Current combat/UI fixes:
  - `Assets/_TheFirst/Scripts/UI/SettlementUI.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Enemies/EnemyAI.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Enemies/EnemyProjectileAttack.cs`
  - `Assets/_TheFirst/Scripts/UI/LevelSelectUI.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/DataManager.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/GameTimelineManager.cs`
- Current upgrade/treasure/role mechanics:
  - `Assets/_TheFirst/Scripts/Upgrades/UpgradeManager.cs`
  - `Assets/_TheFirst/Scripts/Upgrades/UpgradeCardUI.cs`
  - `Assets/_TheFirst/Scripts/UI/TreasureSlotMachineUI.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/PlayerRoleRuntimeMechanics.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Core/Health.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/PlayerStats.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Core/PassiveItemData.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Mech/WeaponController.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Mech/MechController.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Parts/WeaponPart.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/CombatSceneInitializer.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/NPCInteraction.cs`
  - `Assets/_TheFirst/Scripts/Upgrades/WeaponFusionRecipeSO.cs`
  - `Assets/_TheFirst/Scripts/Upgrades/FusionCondition.cs`
  - `Assets/_TheFirst/Scripts/Upgrades/WeaponFusionManager.cs`
  - `Assets/_TheFirst/Scripts/Gameplay/Stone/EvolutionRecipeSO.cs`

## Recent Fixes Already Done

- Skill tree connector highlighting was changed so a connector is bright only when both endpoints are unlocked.
- Excluded/locked nodes now have clearer visual states.
- `CharacterSkillNodeUI` no longer depends only on grayscale material for lock visibility; it now uses alpha and icon color as fallback.
- `CharacterSelectManager` rebinds button listeners on enable/open to avoid dead buttons after UI art changes.
- Reset button now appears for unlocked characters and becomes interactable once at least one character skill node is learned.
- Node unlock now refreshes the whole character panel immediately, so reset becomes clickable without reopening the window.
- `UIManager` now tries to auto-bind missing `Gold_Display_Text` and forces gold text above sibling UI.
- Fixed the fallback `MainTimeline 2` 280s event: it no longer has a Boss name pointing at the 100-enemy swarm wave, and now uses a small elite checkpoint wave.
- Elemental Resonance spell-count scaling now counts unique weapon build sources, including evolved weapon inherited sources, instead of only counting occupied weapon slots.
- Added `CameraFollowBounds` for Hub camera limits: Hub can route Cinemachine Follow/LookAt through a clamped proxy target so the player can move past the edge while the camera stops at configured bounds. It preserves normal direct follow unless a Bounds Collider is assigned or manual bounds are enabled; Hub movement uses World X/Z.
- Settlement weapon damage rows now snapshot weapon icon/level data immediately when the panel opens. This fixes failure settlement icons disappearing after player death destroys the weapon controller.
- Ranged projectile enemies now stop NavMeshAgent motion and keep their Rigidbody kinematic/zeroed while attacking, preventing attack-range drift across the floor.
- Level select now has a runtime difficulty selector prototype for Normal/Hard in the confirm panel. Hard is disabled until `Demo_Hard20_Unlocked`; selected difficulty is stored in `DataManager.selectedDemoDifficulty`, and `GameTimelineManager` no longer auto-forces hard just because hard is unlocked.
- Treasure chests now use a separate runtime slot-machine style reward UI instead of reusing normal level-up cards. Chest rewards can multi-level the same owned weapon, trigger an available evolution, or pay jackpot gold as fallback.
- `TreasureSlotMachineUI` was visually restored toward the slot-machine concept: dark wood cabinet, gold frame, bulb chase, three reels, lever, jackpot result plate, and stronger jackpot pulse.
- Treasure slot-machine concept image was generated at `Assets/_TheFirst/Docs/Concepts/TreasureSlotMachine_Concept.png`.
- Treasure slot-machine v2 removes all gold reward presentation, waits for the player to click outside the panel before closing, has stronger entrance juice, and shows per-reel hover details for weapon level transitions/evolution. The updated concept is `Assets/_TheFirst/Docs/Concepts/TreasureSlotMachine_Concept_v2.png`.
- Chest reward logic now directly triggers evolution when weapons have no more upgrade room but an evolution recipe is available, avoiding a bad-feeling gold fallback at the peak moment.
- Treasure slot-machine lever can now be pulled again by spending gold. Reroll cost starts at 60 and rises by 35 per reroll; each paid reroll boosts the odds of 2-reel/3-reel weapon rewards, with the existing luck stat still contributing to reward quality.
- Treasure weapon rewards now try to award concrete weapon skill-tree nodes for the target weapon, not just raw level transitions. Reels can show different node icons/names, and hover details show the exact card/effect that will be granted.
- Character cards are no longer randomly mixed into ordinary upgrade choices. Runtime role milestone cards are forced into the offer at player levels 5 and 10 when a valid role card exists.
- Character-card milestone priority now prefers authored/unlocked skill-tree linked role cards first; runtime role milestone cards are fallback cards, so the skill-tree role cards were not replaced.
- Added runtime role mechanics: Mage energy active cast, Swordsman sword-focus passive area scaling, and Engineer parts collection/weapon modification. The existing ultimate input still tries Mage/Engineer role active skills before falling back to the old demo ultimate path; Swordsman no longer consumes the active key for a blade burst.
- Codex item UI now hides the tiny `W/P` type badge, clears the recommendation row for weapon/passive pages, and removes build-flow/tactical-position/build-advice text from the generated detail copy.
- Evolution recipe conditions now support explicit trigger weapon level (`requiredWeaponLevel`) and direct passive references (`requiredPassiveItem`) in addition to passive IDs. `WeaponFusionManager` can check “weapon Lv.5 + passive Lv.5” recipes while skipping `codexOnly` prototype entries.
- Legacy energy-stone evolution assets now deserialize through `EvolutionRecipeSO` compatibility fields (`baseWeapon`, `requiredStoneType`, `evolvedWeapon`, `evolutionName`) and are shown in the Codex evolution tab as role/element special evolutions.
- Codex evolution tab now hides unknown recipes as black silhouettes. When the main weapon reaches the configured reveal level (default Lv.5), the recipe card turns colored and reveals material/result details. Weapon/passive level milestones are persisted through `PlayerProgressManager` stats (`WeaponLevel_*`, `PassiveLevel_*`), recorded from card upgrades, treasure level grants, and WeaponPart proficiency leveling.
- Codex now auto-creates prototype `codexOnly` weapon+passive evolution entries for demo weapons that do not yet have authored passive-evolution recipes, so every weapon has a visible future pursuit slot. These prototype entries are display-only until bound to real result weapons. Current explicit weapon/passive designs include: Blade+SwordmasterSoul=万刃剑阵, Fireball+ArcaneMastery=爆炎星核, IceShard+ElementalResonance=极寒晶雨, LightningStrike+ThunderWill=天罚雷柱, ChainLightning+ThunderWill=雷暴网络, Hurricane+MoveSpeed=风暴回廊, Grenade+AoeRadius=南瓜重炮, Landmine+MechanicalResonance=自动雷场, Orbit+Armor=磁暴岩盾, SupportAura+KillHeal=生命圣域, FlameDagger+Luck=追魂灵刃, FrostNova+WeaponDuration=永冻结界, Beam+WeaponFireRate=棱镜核心, SuperMech+MechanicalResonance=南瓜巨神兵.
- Unknown/evolution detail view direction: keep it as simple as the existing locked-item view, not a full detail page. Show one large evolution-result icon/silhouette, one condition line in `X Lv.5 + Y Lv.5` form, and a simple `met / total` progress number. When all conditions are met, the silhouette turns bright.
- Codex unknown/evolution locked view now uses icon-based condition rows instead of text conditions: large result icon/silhouette on top, then condition icon + condition icon below. Unmet condition icons stay as silhouettes; met conditions turn bright.
- Swordsman sword-focus interval was reduced from 60s to 30s. Runtime role HUD now prefers editable prefab `Assets/_TheFirst/Prefabs/UI/RoleMechanicHUD.prefab`, with sword-gauge sprites under `Assets/_TheFirst/Art/Textures/UI/SwordFocusGauge_*.png`; if the prefab cannot load, code falls back to the old generated HUD.
- Level select difficulty confirmation now prefers editable prefab `Assets/_TheFirst/Prefabs/UI/LevelDifficultySelector.prefab`. `LevelSelectUI.applyRuntimeConfirmStyling` defaults off so hand-authored confirm panel/button visuals are not overwritten by code unless explicitly enabled.
- Treasure slot-machine UI now uses the authored Halloween machine art from `Assets/_TheFirst/Art/Textures/UI/Chest.png` and card frame art from `Assets/_TheFirst/Art/Textures/UI/Chest_Card.png`. `TreasureSlotMachineUI` overlays the three reward icons/texts on those card frames, keeps hover details/reroll logic, and also loads duplicated runtime copies from `Assets/_TheFirst/Resources/UI/` so builds are not editor-only.
- Treasure slot-machine card reels were adjusted to preserve the card art proportions and now spin by vertically scrolling whole `Chest_Card` views inside masked reel windows, instead of just swapping text/icon content on a static card. The chest/card texture import settings were changed to Sprite/UI-friendly settings: no mipmaps, no NPOT scaling, transparency enabled.
- Treasure slot-machine runtime now prefers editable prefab `Assets/_TheFirst/Prefabs/UI/TreasureSlotMachineUI.prefab`; keep child names intact (`SlotMachinePanel`, `Reel_1..3`, `CardRoller`, `Card_0..4`, `ConfirmButton`, `RerollButton`) and adjust RectTransforms there for final alignment. Spin motion now integrates a high starting speed into a smooth deceleration and stops each reel on the next whole card. The bottom of the UI now has explicit `确认` and `金币重置` buttons, and the result no longer prints the extra summary strings such as weapon-name gain text or “同一武器连续升级”.
- Treasure slot-machine prefab binding was relaxed after prefab edits removed some card text children: each card now only requires `CardArt`; `Icon`, `CenterText`, `Label`, and `SubLabel` are optional, so deleting/hiding detail text in the editable prefab no longer causes a fallback to the generated layout.
- Treasure slot-machine title now preserves the editable prefab title text after reward reveal instead of overwriting it with `宝箱开奖`/`JACKPOT`.
- Treasure slot-machine `Play()` and reveal flow now use null-safe UI writes for optional editable-prefab nodes such as `Subtitle`, `ResultText`, `Footer`, `JackpotGlow`, `HoverDetail`, and `LeverKnobProxy`, preventing prefab layout edits from causing `NullReferenceException`.
- Treasure slot-machine bulb chase is also null-safe. Editable prefabs may omit all `Bulb_0..16` children; `UpdateBulbs` now skips missing bulb images instead of throwing.
- Treasure slot-machine no longer creates or activates `JackpotGlow`; jackpot/evolution still keeps a small panel pulse but no ugly glow plate. Final reel stopping no longer resets the roller back to center after it stops; the winning card is placed in the next visible card slot and the roller continues downward with an `EaseOutQuad` deceleration until that card reaches center. Optional lever animation hook: add a `LeverHandle` child with an Animator containing a `Pull` trigger, and the slot UI will trigger it on initial open and paid reroll.
- Treasure slot-machine stop timing was corrected from sequential reel stopping to concurrent staggered stopping. All three reels keep moving during the stop phase; each reel enters its final deceleration after a small delay (`0s/0.15s/0.30s`) instead of waiting frozen for the previous reel to finish.
- Added generated placeholder treasure slot-machine SFX under `Assets/_TheFirst/Audio/SFX/TreasureSlot/` and duplicated runtime-loadable copies under `Assets/_TheFirst/Resources/Audio/SFX/TreasureSlot/`: lever pull, reel loop, reel tick, reel stop, reward reveal, jackpot, confirm, and reroll. `TreasureSlotMachineUI` now exposes AudioClip fields, auto-loads these defaults from Resources, plays one-shots through `AudioManager` when available, and uses a local looping AudioSource for the reel loop.
- Weapon upgrade card gem-slot progress is no longer tied to the deprecated/disabled ultimate-card switch. `UpgradeManager.GetGemCountForWeapon` now derives progress from owned weapon skill nodes and the explicit gem counter, so normal level-up cards keep previously embedded gems, and treasure weapon-skill rewards count toward the same card gem progress.
- Player `Health` now emits `OnPlayerHealthDamaged` only after shields/armor leave positive damage, so mechanics such as sword-focus loss react to actual HP loss instead of every hit.
- `荆棘护甲` reflection is disabled for the current demo direction. `ThornsDamage` passive effects are converted into armor gain for now, and passive milestone text/upgrade previews describe armor instead of reflect damage.
- Added a guard in `CharacterStatueNPC` so the generated `PlayerControls` instance is recreated before use if Unity lifecycle/order changes leave it null.
- Added the same `PlayerControls` lifecycle guard to `NPCInteraction` after a runtime null reference appeared in Update.
- Boss clear now enters a pending-victory state as soon as the timeline wins, so delayed player damage during the boss death ceremony cannot flip the final settlement to failure. `GameManager.HandleVictory()` owns victory progress recording.
- `SwordmasterSoul` slash-wave bonus now reads the real owned slash weapon level from `WeaponController`/`WeaponPart`, including evolved slash weapons, instead of relying only on the old proficiency level field. Slash/elements/mechanical tag inference also checks the weapon asset name and display name, so evolved assets like `WSB_WindBlade` without `weaponID` are still recognized.
- Passive items are now capped at Lv.3 through `PassiveItemData.EffectiveMaxLevel`, runtime upgrade logic, treasure/card gem progress, Codex evolution condition display, and all assets under `Assets/_TheFirst/Prefabs/Passive Item Data`. Lv.3 is treated as the passive capstone/red-gem milestone.
- Codex collection count text is now stretched inside the footer bar instead of offset from the bar edge. Future weapon Codex entries no longer reveal right-side weapon details; selecting them shows the same silhouette locked view as locked items, with generic `未来解锁` text and no meaningless progress when no threshold is configured.
- Codex/gameplay unlock gating is now wired for the new pursuit rules. Weapon Codex entries with configured `unlockStatKey/unlockThreshold` ignore legacy `unlockedItems` until the stat threshold is met, and `UpgradeManager` uses the matching `WeaponSkillTree` gate before offering locked weapon unlock cards in-run.
- Added/updated unlock gates: Aura = `PassiveLevel_甜蜜磁力` Lv.3, FreezeTouch = `WeaponLevel_IceShard` Lv.3, FrostNova = `Freeze_Count` 300, LightningStrike/LaserCore = first stage clear stat `Demo_Intro10_Clear`, ChainLightning = `WeaponLevel_LightningStrike` Lv.5, Landmine = `Engineer_Unlocked`, SwordmasterSoul = `WeaponLevel_Blade` Lv.5, KillHeal = `Kill_Count` 3000, LifeSteal = `Player_TotalHealing` 3000.
- `PlayerProgressManager` now auto-populates `allSkillTrees` from `Assets/_TheFirst/Prefabs/Skill Tree` in the Unity Editor, so the empty manager prefab no longer prevents stat-gated unlock checks from finding the new skill-tree assets. `Health.Heal` records player cumulative healing into `Player_TotalHealing`.
- `UpgradeManager.cs` had broken mojibake string literals after prior edits; those compile-breaking logs/tooltips/treasure text blocks were sanitized to ASCII, and the swallowed runtime calls for upgrade pause, panel title setup, weapon gem registration, and `OnItemUnlocked` notification were restored. Unity script compile passes with 26 existing warnings.
- `荆棘护甲/Thorns` has now been removed from runtime content instead of being converted to armor: the passive node/data assets were deleted, `UpgradeDatabase` and combat UI passive references were cleaned, and stale `ThornsDamage` effects no longer apply a fallback armor bonus. The enum value is kept only as a deprecated serialization placeholder.
- Fusion/evolution runtime checks now treat missing weapon-level requirements as Lv.5, matching the current design direction. Legacy A+B fusion checks also trigger at Lv.5 instead of full max level, and both legacy/new fusion paths match inherited weapon sources so evolved/branched weapons can still satisfy source-weapon conditions. `WeaponFusionManager.EnsureInstance()` is used by upgrade/branch flows so missing scene managers no longer silently skip `WeaponFusionRecipeSO` checks in the Unity Editor.
- Formal runtime `WeaponFusionRecipeSO` assets were generated under `Assets/_TheFirst/GameData/FusionRecipes/` for the current evolution set. They are `codexOnly = false` and trigger from Lv.5 weapons plus either Lv.5 partner weapons or Lv.3 passive items. Added recipes including Blade+Hurricane=WindBlade, Blade+ChainLightning=LightBlade, Blade+Fireball=FireBlade, Fireball+Hurricane=InfernoStorm, Fireball+Landmine=FlamethrowerTurret, FlameDagger+Fireball/SoulDagger, Orbit+Armor=EarthShield, EarthShield+ChainLightning=MagneticArmor, Orbit+LifeSteal=VampireWheel, Aura+HP=LifeAura, and the broader weapon+passive evolutions. Missing result weapon stat assets were created under `Assets/_TheFirst/Prefabs/Weapon/Evolved/`. Three old root-level test fusion assets with mismatched names/conditions were removed to prevent wrong cards.
- `WeaponFusionManager` now merges all `WeaponFusionRecipeSO` assets found under `Assets/_TheFirst/GameData` every time `EnsureInstance()` is used in the Unity Editor. Existing scene lists no longer block newly generated files under `GameData/FusionRecipes`, so the recipes do not need to be manually assigned to a manager prefab for editor testing.
- Fusion cards are now filtered out after their result weapon is already owned/evolved, covering both `WeaponFusionRecipeSO` and legacy `FusionRecipeSO` paths. Blade evolutions now push their result weapon back into `PlayerBladeAttack`: `WindBlade` switches to wind projectile mode, `LightBlade` to thunder thrust mode, and `FireBlade` to fire slash mode. WindBlade projectile firing falls back to the evolved weapon's `bladeEnergyPrefab`/`projectilePrefab` if the editable blade component field is not assigned.
- Skill/icon cleanup pass for the 60-minute build test: all player-facing skill/weapon/passive/shield icon references under `_TheFirst` now scan clean. Filled LaserCore skill-tree node icons, UnlockAura/UnlockShield/ShieldData icons, Upgrade_MaxHealth name/icon/description, and Boss internal attack names/icons. Empty `weaponID` fields were filled for `SO_Laser_Beam`, `WSB_Aura_light`, and the legacy `SO_FlameTurret` result asset.
- Added temporary 60-minute build-test timeline `Assets/_TheFirst/Prefabs/Enemies/MainTimeline_DEMO_BuildTest60.asset` with 60 fixed-time events over 3600s, reusing existing demo/hard wave configs across six 10-minute chapters. `GameTimelineManager` has an inspector switch `useBuildTest60Timeline`, reference `demoBuildTest60TimelineConfig`, and `demoBuildTest60ExperienceMultiplier`; `CombatArena01` references the new asset but leaves the switch off by default. When enabled, it disables `advanceWaveOnClear` so the fixed-time simulation can run as a long build-flow test.
- Codex layout was simplified toward the Vampire Survivors reference. `SkillTreeUIManager` now uses a single compact runtime panel: top `Collected: X of Y`, one mixed icon grid for weapons/passives/fusions, and a bottom detail strip with only icon, name, one short effect line, and one footer line such as `Ignores`, `Max Level`, `Requires`, or `Progress`. The old two-page detail/stat layout remains in code behind a runtime switch, but the current default is the compact layout.
- Compact Codex follow-up: `LightningStrike_SkillTree` is default unlocked again. Fusion/evolution entries now stay black silhouettes until their result weapon is actually unlocked/known, instead of turning bright just because a source weapon reached the reveal level. The compact Codex footer/weapon descriptions are bilingual: Chinese language shows Chinese descriptions, requirements, progress, and ignore lines; English keeps the Vampire Survivors-style labels.
- Weapon+weapon formal `WeaponFusionRecipeSO` recipes now use `FusionType.Merge` for the current union design. The evolved weapon keeps inherited source tags/levels for mechanics like SwordmasterSoul checks, but partner weapons are consumed and removed from the normal weapon slots.
- After a weapon reaches `WeaponStage.Evolved`, normal/base weapon upgrade cards and treasure raw-level grants no longer target it. Short-term design: evolved weapons are complete until we add a separate super-weapon/limit-break upgrade pool.
- Compact Codex fusion entries now prefer formal `WeaponFusionRecipeSO` recipes and de-duplicate legacy `FusionRecipeSO`/`EvolutionRecipeSO` entries by result weapon, so old elemental entries like the legacy WindBlade recipe should not appear beside the new formal WindBlade recipe.
- `WSB_WindBlade.weaponName` was unified to `风刃`; the old `疾风之刃` name should not appear as a second label for the same effect.
- Boss knockback immunity now also covers direct displacement/pull channels: `StatusEffectReceiver`, `EnemyAI`, `StraightMoverAI`, smooth projectile knockback, hurricane/landmine displacement, black-hole/tornado/magnetic pull, and storm orbiter pull all check `BossUnit.immuneToKnockback`.
- Unity script compile passed on 2026-05-20 after the union/evolved-upgrade/knockback pass. Unity MCP reports 26 existing warnings, no script errors.
- Evolution cards shown in the normal upgrade panel now display the result weapon (`effect.weaponToUnlock`) for name/icon/gem slots instead of the trigger/source weapon. Formal and legacy fusion nodes set `associatedWeapon` to the result weapon so cards like Blade+Hurricane show `风刃`, not `斩击`.
- Normal level-up no longer offers evolution cards by default. `UpgradeManager.offerEvolutionCardsOnLevelUp` exists as an inspector switch for experiments, but the default path is chest-driven evolution.
- Treasure slot-machine reels now always use an icon while spinning. Locked/no-reward reel slots use `Assets/_TheFirst/Art/Textures/UI/Nothing.png`, duplicated at `Assets/_TheFirst/Resources/UI/Nothing.png` for runtime loading.
- Treasure multi-reel weapon rewards now pick skill nodes across different upgradeable owned weapons first, only falling back to repeat the same weapon if there are not enough distinct candidates.
- Treasure slot-machine evolution rewards now occupy only one reel, while the two side reels still use the normal treasure reward odds. If a side reel rolls a weapon reward it grants a real weapon skill node or raw +1 weapon level before the evolution is applied; otherwise it shows the Nothing pumpkin lock. Side rewards exclude the weapons consumed/replaced by that same evolution so the reward does not get wasted.
- Orbit weapon direction update: base `Orbit` is now `环绕蜂刺` and uses the normal non-shield orbit prefab. `大地岩盾` is now a new intermediate evolved weapon asset (`Weapon_EarthShield`) from `环绕蜂刺 Lv.5 + Armor Lv.3`; `大地岩盾 Lv.5 + 闪电链 Lv.5` evolves to `磁暴岩盾`; `环绕蜂刺 Lv.5 + 吸血之牙 Lv.3` evolves to `吸血鬼之轮`. Orbital evolutions swap their `orbitalPrefab`/`baseOrbitalSpeed`, and `WeaponPart.ApplyBranch` respawns orbiters immediately so visual prefab/speed changes take effect.
- Weapon max level tuning: player weapon stat assets under `_TheFirst` are now normalized to `maxLevel = 10`, `WeaponPart` no longer caps Fireball/Lightning/Ice at Lv.5, unlock cards count as Lv.1 for gem display, and Lv.5 lights the red capstone gem. Existing evolved weapons still do not receive normal/base upgrade cards after reaching `WeaponStage.Evolved`.
- Added the first runtime pass for `回旋镖`: `WSB_HuiXuanBiao_01` now has `weaponID = Boomerang`, behavior `Boomerang`, max level 10, a default weapon chain in `UpgradeDatabase`, and projectile return now homes back to the player instead of flying to a fixed overshoot point or instantly auto-rethrowing on catch.
- The demo intro timeline still pulls the late pressure waves earlier (`360/450/525s`) and `GameTimelineManager` applies a late HP multiplier ramp after 240s so the 4:00-remaining section does not collapse into weak trickle spawns.
- Unity script compile passed on 2026-05-20 after the treasure/orbit/boomerang pass using MSBuild; warnings are existing project warnings only.
- Treasure slot-machine evolution reels now get a distinct evolution treatment instead of looking exactly like normal weapon upgrades: the evolution reel uses a gold-tinted card, `进化` label, larger result icon/name treatment, and a reveal pulse. This is still code-driven styling on the existing slot-machine card; if the final art needs hand-authored evolution-frame details, expose a dedicated evolution card skin/prefab next.
- Friendly projectile collision filtering was tightened for orbit/shield weapons. `Orbiter` now forces its colliders to trigger and ignores friendly projectiles; `Projectile` exposes `IsEnemyProjectile`, marks boomerangs as `PlayerProjectile`/player projectile layer, and ignores friendly `Orbiter`/projectile colliders. This prevents EarthShield/MagneticArmor/VampireWheel style orbiters from blocking boomerangs or other player bullets.
- Boomerang skill tree assets were added under `Assets/_TheFirst/Prefabs/Skill Tree/Boomerang/` and registered in `UpgradeDatabase.weaponSkillNodes`. Current tree direction: damage, speed, range/duration, size, miss-cooldown, twin throw, return-edge, and three catch-stack upgrades.
- Boomerang runtime now supports multi-throw via `AddProjectile`/local orbital-count bonus, applies local speed and duration/range bonuses, and waits for all active boomerangs to return or expire before resetting cooldown/out-state.
- Unity script compile passed on 2026-05-21 after the treasure evolution UI, friendly collision, and boomerang skill-tree pass using MSBuild; warnings are existing project warnings only.
- Boomerang follow-up: return state now clears the outbound hit list so returning boomerangs can damage enemies again, with a small return-damage bonus. The catch radius for `WSB_HuiXuanBiao_01` was reduced from a far 5m catch to 0.85m and runtime catch radius is clamped, so the boomerang no longer disappears far away from the player.
- Boomerang `BM_CooldownI` was redesigned into return speed (`WeaponProjectileSpeed +20%`) because auto-return makes miss-cooldown a weak upgrade. Catch-stack node text was renamed toward return-stack language; mechanically it remains a return-loop ramp that buffs the next throw's damage/size.
- Weapon gem display now treats Lv.5 as the center red capstone gem, not as five normal gems plus a sixth red gem. Lv.1-4 fill normal sockets, Lv.5 lights the red capstone, and Lv.6-10 start the second normal-gem round.
- Treasure rewards that raise a weapon to the Lv.5 evolution threshold now flag an evolution check immediately instead of waiting until max level 10. If a treasure roll already contains an evolution reel, paid rerolls preserve that same evolution reel and only reroll the side rewards.
- Treasure slot-machine evolution reel art was split into editable runtime prefab `Assets/_TheFirst/Resources/UI/EvolutionReelCard.prefab`. `TreasureSlotMachineUI` auto-loads it from `Resources/UI/EvolutionReelCard` and overlays it on the evolution reel only when needed; optional child names `Icon`, `Label`, and `SubLabel` are populated if present.
- Unity script compile passed on 2026-05-21 after the boomerang return/catch, Lv.5 gem/evolution threshold, preserved evolution reroll, and `EvolutionReelCard` prefab pass using MSBuild; warnings are existing project warnings only.
- Boomerang catch-stack design was replaced because auto-return made catch rewards meaningless. The three former catch-stack nodes now reward return-path play instead: `BM_CatchStackI` is inbound damage, `BM_CatchStackII` creates a pulse every 4 inbound hits, and `BM_CatchStackIII` creates a catch burst scaled by inbound hit count. `BoomerangStackUpgrade` remains only as an old serialization placeholder; new nodes use `BoomerangReturnDamage`, `BoomerangReturnPulse`, and `BoomerangRecallBurst`.
- Unity script compile passed on 2026-05-21 after the return-path boomerang redesign using MSBuild; warnings are existing project warnings only.
- Treasure evolution reel card is now prefab-authoritative. Runtime hides the normal `Chest_Card` art/text/icon for evolution reels, shows `Resources/UI/EvolutionReelCard.prefab`, preserves that prefab's root RectTransform instead of forcing Stretch, and only fills optional children named `Icon`, `Label`, and `SubLabel` without overriding their colors. Edit `Assets/_TheFirst/Resources/UI/EvolutionReelCard.prefab` for the final red/purple evolution card look.
- Weapon gem display now treats Lv.5 as both the fifth normal gem and the red capstone gem. Lv.6-Lv.10 replace normal sockets with the tier-1 gem sprite from left to right while keeping all five normal gem slots visible once the first round is filled.
- Unity script compile passed on 2026-05-21 after the evolution-reel prefab-authoritative pass and Lv.5 gem display fix using MSBuild; warnings are existing project warnings only.
- Lv.5 weapon gem animation now plays two gem flights: the fifth normal gem flies in without closing the card, then the red capstone gem flies in shortly after and owns the final dismiss/upgrade commit. This keeps the capstone animation while still showing all five normal gems.
- Unity script compile passed on 2026-05-21 after restoring the red capstone gem flight animation using MSBuild; warnings are existing project warnings only.
- Treasure chest evolution is now guaranteed when an evolution recipe is already available at chest open. The old `treasureEvolutionChance` probability gate caused ready evolutions to sometimes miss on the first roll and only appear after paid reroll; `RollTreasureSlotReward` now immediately builds an evolution reward when `TryGetAvailableTreasureEvolution` succeeds, and rerolls still preserve that evolution reel while refreshing side rewards.
- Unity script compile passed on 2026-05-21 after the guaranteed ready-evolution chest fix using MSBuild; warnings are existing project warnings only.
- Compact CodexBook is now editable as a prefab. `SkillTreeUIManager` first uses inspector `codexBookPrefab`, then `Resources/UI/CodexBook`, and only falls back to the old generated layout if prefab binding fails. The default editable prefab was generated at `Assets/_TheFirst/Resources/UI/CodexBook.prefab`; keep runtime binding child names such as `Runtime_CollectionText`, `Runtime_CodexScroll`, `Viewport`, `Content`, `Runtime_SidebarItemPrefab`, `Runtime_DetailRoot`, `Runtime_DetailIcon`, `Runtime_DetailTitle`, `Runtime_DetailBody`, `Runtime_DetailFooter`, and `Runtime_CloseButton`.
- Unity script compile passed on 2026-05-21 after the CodexBook prefab pass using MSBuild; warnings are existing project warnings only.
- Treasure slot-machine Nothing pumpkin reels are no longer pure empty/locked slots. Each pumpkin reel now grants base attack (`treasurePumpkinBaseAttackBonus`, default +2% damage multiplier) and its hover detail shows `南瓜祝福 / 基础攻击力 +2%` instead of inheriting the target weapon's locked tooltip such as Orbit/环绕蜂刺 lock. If all weapons are capped, the treasure result becomes a three-pumpkin base-attack reward instead of doing nothing.
- Unity script compile passed on 2026-05-22 after the pumpkin base-attack treasure reward pass using MSBuild; warnings are existing project warnings only.

- Boomerang catch cooldown old behavior was removed. `WeaponPart.ResetCooldown()` no longer sets `fireCooldown = 0.01f`; both caught and missed boomerangs now call the shared cooldown calculation, including global fire-rate, local weapon fire-rate upgrades, engineer mechanical fire-rate bonus, and current energy-stone fire-rate modifier. This prevents immediate rethrow on catch.
- Unity script compile passed on 2026-05-22 after the boomerang cooldown fix using MSBuild; warnings are existing project warnings only.

## Known State / Notes

- The user reset save data and the previous all-lit skill tree problem disappeared. Likely cause: old save had node IDs from earlier skill-tree data before some node/resource changes.
- Current expectation: after learning one skill, reset button should become clickable immediately.
- If gold display fails again, inspect actual scene hierarchy. The old issue was likely text existing but being covered by the gold bar image or stale scene reference.
- Do not treat old skill tree screenshots as current truth after save reset.
- Unity script compile passed on 2026-05-20 after the compact Codex layout pass. Unity MCP reports 26 existing warnings, no script errors.

## Existing Planning Docs

- `Assets/_TheFirst/Docs/DemoVerticalSlicePlan.md`
- `Assets/_TheFirst/Docs/DemoArtDirectionBrief.md`
- `Assets/_TheFirst/Docs/UI_Codex_ReferenceBreakdown.md`
- Concept art:
  - `Assets/_TheFirst/Docs/Concepts/DemoUI_CodexSkillTree_Concept.png`
  - `Assets/_TheFirst/Docs/Concepts/DemoCombatArena_ConceptSheet_01.png`
  - `Assets/_TheFirst/Docs/Concepts/TreasureSlotMachine_Concept.png`
  - `Assets/_TheFirst/Docs/Concepts/TreasureSlotMachine_Concept_v2.png`

## Suggested New Thread Prompt

Continue work on `D:\test\TheFirst`. First read `Assets/_TheFirst/Docs/CodexWorkingMemory.md`, then continue from the latest project state. Keep responses concise, avoid reloading old UI/art prompt history unless needed, and inspect code before changing it. Current priority is gameplay/design/development for the demo; UI/art work may be handled in separate windows.
