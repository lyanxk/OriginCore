# OriginCore Legacy UI Asset Migration

迁移日期：2026-07-15

本目录保存从现有项目旧版目录复制到 `_OriginCore` 隔离边界内的 UI 资源。复制操作保留旧目录及其引用，不让新系统继续依赖 `Assets/Resources` 中的旧路径；每个副本由 Unity 分配新的 GUID。

## Commands

| 新路径 | 旧路径 | 当前用途 |
|---|---|---|
| `Commands/command_move.png` | `Assets/Resources/CommandIcons/command_move.png` | `90_SystemTest` 的 Move 指令按钮 |
| `Commands/command_attack.png` | `Assets/Resources/CommandIcons/command_attack.png` | `90_SystemTest` 的 Attack 指令按钮 |
| `Commands/command_stop.png` | `Assets/Resources/CommandIcons/command_stop.png` | `90_SystemTest` 的 Stop 指令按钮 |

## Abilities

| 新路径 | 旧路径 | 当前用途 |
|---|---|---|
| `Abilities/rts_blink.png` | `Assets/Resources/AbilityIcons/Rts/rts_blink.png` | 仅迁移，未接入按钮或 Runtime 能力 |
| `Abilities/rts_celestial_sentence.png` | `Assets/Resources/AbilityIcons/Rts/rts_celestial_sentence.png` | 仅迁移，未接入按钮或 Runtime 能力 |
| `Abilities/rts_support_aura.png` | `Assets/Resources/AbilityIcons/Rts/rts_support_aura.png` | 仅迁移，未接入按钮或 Runtime 能力 |
| `Abilities/rts_void_implosion.png` | `Assets/Resources/AbilityIcons/Rts/rts_void_implosion.png` | 仅迁移，未接入按钮或 Runtime 能力 |

这些文件来自用户现有工程，不是本轮生成资源。现有工程未提供额外来源或许可说明，因此正式发布前仍需由项目所有者确认其来源与授权。能力图标保持未引用状态，避免把旧 `Assembly-CSharp` 技能实现误表示为新的 `OriginCore.Runtime` 功能。
