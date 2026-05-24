# Codex UI Reference Breakdown

This pass treats the supplied reference as a component map, not as a text prompt.

## Component Map

| Ref Area | Runtime Asset | Purpose | Current Generation Rule |
| --- | --- | --- | --- |
| A. Outer open book | `codex_book_frame` | Full background shell behind both pages | Thick leather cover, page stacks on sides, metal corners, shadow |
| B. Left/right parchment | `codex_page_left`, `codex_page_right` | Content pages | Warm parchment, inner border, subtle paper grain and faint ruling |
| C. Center binding | `codex_spine` | Page divider | Dark leather strip, gold stitch arcs |
| D. Top title plaque | `codex_header_plaque`, `codex_pumpkin_badge` | Title brand area | Wooden plaque, bolts, pumpkin badge, title text rendered in Unity |
| E. Category tabs | `codex_tab` | Weapons/passives/fusion/monsters filters | Wood tab base, runtime tint per category |
| F. Item cards | `codex_card_weapon`, `codex_card_passive`, `codex_card_locked`, `codex_card_selected` | Grid entries | Dark card body, bronze frame, top icon well, star row, vertical type stripe, selected glow |
| G. Detail hero panel | `codex_detail_panel`, `codex_icon_frame` | Selected entry summary | Dark inset panel with icon frame and parchment-page text area below |
| H. Stats/recommend chips | `codex_stat_slot`, `codex_recommend_frame` | Attribute rows and recommended links | Torn dark stat strip, small bronze square frames |
| I. Close button | `codex_close_button` | Exit button | Red beveled square with hand-painted X |

## Rules

- Do not directly crop the reference image into production assets unless a licensed high-resolution source is provided.
- Keep each generated piece reusable and named exactly as the runtime loader expects under `Assets/Resources/UI/DemoCodexCutout`.
- Preserve `Assets/Resources/UI/DemoCodex` as the fallback style.
- If a visual critique points to a part, update the corresponding component above rather than repainting the whole UI.
