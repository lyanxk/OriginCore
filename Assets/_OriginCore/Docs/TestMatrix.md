# OriginCore 最终集成与交接测试矩阵

- Unity：2022.3.62f3
- Render Pipeline：URP 14.0.12
- 基线平台：Windows x86_64 / Mono / Development Build
- 更新日期：2026-07-15
- 需求来源：实施指导文档第 9 章及 `RequirementsAddendum.md`
- 当前状态：P00～P17 的历史自动化、连续手工链路、Profiler、Windows Development Build 与 Player 主路径均已通过；阶段 Apply/Validator/Build 工具现已退役。
- 执行约束：后续需求仍需实际落地并同步文档；默认只检查程序集编译与 Unity Console Error/Warning，Test Runner、Play Mode、Profiler 和 Player 构建仅在用户明确要求时执行。

## 1. 自动化 EditMode

当前源码包含 56 个 `[Test]` / `[TestCase]` 声明；参数化用例的实际 Test Runner case 数以 Unity 结果为准。

| 测试组 | 主要测试文件 | 核心断言 | P00～P15 证据 | P16 Run All |
|---|---|---|---|---|
| CommandQueue | `UnitCommandQueueTests.cs` | 非 Shift 替换、Shift 追加、等待上限 5、第 6 条拒绝、Stop 清空、完成推进 | 已通过 | 通过（用户确认） |
| Resources / Production | `ResourceAndProductionTests.cs` | 三资源原子事务、Influence 上限/释放、失败生产不扣款 | 已通过 | 通过（用户确认） |
| Faction / Vitals | `GameplayFoundationTests.cs` | 阵营关系、友军过滤、Shield 优先、死亡一次、注册表清理 | 已通过 | 通过（用户确认） |
| Input / Rebind | `InputRebindServiceTests.cs` | 覆盖 JSON 往返、重复键允许、恢复默认、Action Map 与抑制帧 | 已通过 | 通过（用户确认） |
| Save | `SaveFoundationTests.cs` | schema/UTF-8 往返、未知字段兼容、坏 JSON、未知版本、原子替换失败安全 | 已通过旧用例；未知字段为 P16 新增 | 通过（用户确认） |
| Visibility | `VisibilityFoundationTests.cs` | Hidden/Explored/Visible、圆形覆盖、Visible 清除、Explored 持久化 | 已通过 | 通过（用户确认） |
| Minimap Mapping | `VisibilityFoundationTests.cs` | UI 归一化坐标与世界 XZ 双向映射，无轴交换 | 已通过 | 通过（用户确认） |
| Scene / Prefab Contracts | 各阶段 Foundation 测试 | 配置、Prefab、Scene、NavMesh、序列化引用和组件所有权 | 已通过 | 通过（用户确认） |

## 2. 自动化 PlayMode

当前源码包含 36 个 `[UnityTest]` / `[Test]` / `[TestCase]` 声明；参数化用例的实际 Test Runner case 数以 Unity 结果为准。

| 测试组 | 主要测试文件 | 核心场景 | P00～P15 证据 | P16 Run All |
|---|---|---|---|---|
| Bootstrap / Scene | `BootstrapSceneFlowTests.cs` | Bootstrap、直接启动、场景往返、唯一 AppRoot、SceneContext 注销 | 已通过 | 通过（用户确认） |
| ModeSwitch / Pending Override | `GameModePlayModeTests.cs`、`RtsMoveCommandTests.cs` | F1/F2/F3、相机/HUD/光标、切换帧、Look 不接管、首次有效输入只接管一次 | 已通过 | 通过（用户确认） |
| RTS Selection | `RtsSelectionPlayModeTests.cs` | 点击、空地清空、Shift 追加、框选、敌方检查、快捷选择 | 已通过 | 通过（用户确认） |
| RTS Commands | `RtsMoveCommandTests.cs`、`AttackMovePlayModeTests.cs` | Move/Stop、Shift 队列、失败路径、AttackMove 交战后续行 | 已通过 | 通过（用户确认） |
| Production | `ProductionPlayModeTests.cs` | 扣款、暂停冻结、生成、集结、Influence 释放一次 | 已通过 | 通过（用户确认） |
| ACT | `ActMovementPlayModeTests.cs` | 相机相对移动、二段跳、下蹲低顶、借力跳、输入缓冲 | 已通过 | 通过（用户确认） |
| FPS | `FpsMovementPlayModeTests.cs` | 行走、阈值锁存疾跑、滑铲、ADS FOV、准星、Renderer 恢复 | 已通过 | 通过（用户确认） |
| Pause / UI | `MenuSettingsPausePlayModeTests.cs` | Esc 优先级、Gameplay 冻结、UI 可用、HUD 互斥、页面路由 | 已通过 | 通过（用户确认） |
| SaveLoad | `SaveLoadPlayModeTests.cs` | 保存后改变状态再恢复、坏档安全、未知 archetype、失败退出不破坏旧档 | 已通过 | 通过（用户确认） |
| Visibility | `VisibilityPlayModeTests.cs` | 敌人隐藏/不可选、建筑 Ghost、重新可见、小地图仅 RTS 生效 | 已通过 | 通过（用户确认） |
| P16 Repetition | `IntegrationRegressionPlayModeTests.cs` | 三轮 RTS→ACT→RTS→FPS→RTS 与 Pause/Resume；事件次数、AppRoot、SceneContext、EventSystem、AudioListener、相机不倍增 | P16 新增 | 通过（用户确认） |

## 3. P16 手工全链路

“历史结果”表示对应功能已在分阶段验收中通过；“P16 连续回归”必须从干净启动连续执行，不能用历史结果替代。

| # | 操作 | 期望 | 历史结果 | P16 连续回归 |
|---:|---|---|---|---|
| 1 | 从 `00_Bootstrap` 启动；另一次直接播放 `90_SystemTest` | 自动进入主菜单；直接播放也只有一个 AppRoot | 通过 | 通过（用户确认） |
| 2 | 主菜单打开/返回 Start、Story、Map、Options；Quit 选 No | 页面与模态互斥，No 返回 | 通过 | 通过（用户确认） |
| 3 | 修改分辨率、窗口模式、亮度、音量、准星 RGB 后重新进入 Options | 值持久化且表现同步 | 通过 | 通过（用户确认） |
| 4 | 两个动作绑定同一按键，然后 Restore Defaults | 重复键允许；默认绑定恢复 | 通过 | 通过（用户确认） |
| 5 | 进入 SystemTest，检查 RTS 边缘移动、滚轮缩放与边界 | 默认 RTS；只直接平移；固定 60° 俯角；缩放不转动；相机受边界约束 | 通过（旧规则） | 等待本轮用户确认 |
| 6 | 点击友军、Shift 追加、框选、空地清空、敌方检查、快捷选择 | 指挥选择只含友军；检查与预览正确 | 通过 | 通过（用户确认） |
| 7 | 用 Move 按钮左键释放目标；再连续 Shift 下达 5 条移动 | 左键确认；白色虚线路线顺序一致；第 6 条提示队列已满 | 通过 | 通过（用户确认） |
| 8 | 非 Shift 发布新移动；按 Stop | 当前与队列被替换；Stop 立即清空，虚线路线同步 | 通过 | 通过（用户确认） |
| 9 | Attack 指定敌人；AttackMove 左键指定地面 | 发现/交战；脱离 `attackRange + 2` 后继续原路线 | 通过 | 通过（用户确认） |
| 10 | 选择生产建筑；右键地面直接设集结点；再用 Set Rally 左键确认；生产 Worker/Combat | 两种集结入口均正确；资源/Influence 更新；新单位前往集结点 | 通过 | 通过（用户确认） |
| 11 | 让单位死亡 | 血条/受击反馈正确；选择移除；Influence 只释放一次 | 通过 | 通过（用户确认） |
| 12 | Hero 长距离 RTS 移动后按 F2，不输入并只移动鼠标 | 相机进入 ACT，Hero 继续自动移动，Look 不接管 | 通过 | 通过（用户确认） |
| 13 | ACT 使用 WASD/Jump；测试跑、二段跳、下蹲、低顶、借力跳 | 首次有效输入同帧接管且队列只清空一次 | 通过 | 通过（用户确认） |
| 14 | F3 后测试行走、疾跑、下蹲、滑铲、跳跃、ADS、准星 | Shift 松开仅在最低疾跑速度以上锁存；停止后重新从行走开始；无武器/射击 | 通过 | 通过（用户确认） |
| 15 | F1 返回 RTS 后下达移动 | 位置不跳变；NavMeshAgent 恢复并接受命令 | 通过 | 通过（用户确认） |
| 16 | Esc 暂停；在暂停中操作 Options 与 Save UI | 移动/生产/攻击/迷雾调度冻结；UI 可用；恢复原模式 | 通过 | 通过（用户确认） |
| 17 | 保存命名槽；改变资源/位置/生命后读取；重命名、删除并检查坏档 | 状态恢复；瞬态选择/队列清空；槽位操作失败安全 | 通过 | 通过（用户确认） |
| 18 | RTS 点击小地图多个位置，再在 ACT/FPS 点击 | RTS 相机即时跳到目标且不播放转向；中心误差不超过一个网格；非 RTS 不移动 | 通过（旧规则） | 等待本轮用户确认 |
| 19 | 敌人/建筑移出视野并重新取得视野 | 敌人不可见不可选；建筑显示静态 Ghost；重获视野刷新/清理 | 通过 | 通过（用户确认） |
| 20 | 连续重复模式切换、暂停、存读档和场景往返至少 3 次 | 无回调倍增、重复 AppRoot、MissingReference 或 Console Error | 分项通过 | 通过（用户确认） |

## 4. Profiler / Stats

| 检查项 | 方法 | 通过标准 | 结果 |
|---|---|---|---|
| Selection steady state | CPU Profiler，静止 RTS 选择后观察 10 秒 | Selection 路径无持续每帧 GC Alloc | 通过（用户确认） |
| Visibility steady state | 观察 `VisibilitySystem.Update/TickNow`，保持单位静止 | 仅按配置 5 Hz 更新；稳态无托管数组/List 分配 | 通过（用户确认） |
| Target scanner | 让敌我处于扫描范围并观察 10 秒 | 使用固定缓冲；非每帧扫描；稳态无持续 GC Alloc | 通过（用户确认） |
| Subscription stability | 完成手工步骤 20 前后比较事件/表现 | 每次输入只触发一次；HUD/路线/模式事件不倍增 | 通过（用户确认） |

## 5. Windows Development Build

| 检查项 | 期望 | 当前结果 |
|---|---|---|
| Build Settings | 仅 3 个场景，顺序为 Bootstrap、MainMenu、SystemTest | MCP 只读检查通过 |
| Build Target | `StandaloneWindows64` / x86_64 | MCP 只读检查通过 |
| Scripting Backend | Mono（原型基线） | MCP 只读检查通过 |
| Build | Development + StrictMode + LZ4，输出 `Builds/Windows64Development/OriginCore.exe` | 通过；`[OriginCore P16] BUILD PASS`，0 Error / 0 Warning，300 个文件共 122,625,346 bytes |
| Player 主路径 | Bootstrap → Menu → Options → SystemTest → 三模式 → Pause → Save/Load → MainMenu/Exit | 通过（用户确认） |
| Quit | Player 中 `Application.Quit` 生效；Editor 安全退出 Play Mode | 通过（用户确认） |
| Persistence | settings/save 写入 `persistentDataPath`，不写入 Assets | 通过；`settings.json` 与 schema v1 save 均位于 `LocalLow/TMC/OriginCore/OriginCore`，JSON 有效 |
| Font / Text | 英文占位无方框；不依赖许可未确认字体 | 通过（用户确认） |
| Runtime assembly | 无 Editor assembly 引用 | 通过；Player Managed 目录含 `OriginCore.Runtime.dll`，不含测试程序集；项目内 `OriginCore.Editor` 后续已删除 |

## 6. Console 与缺陷清单

- 项目目标：最终 0 Error。
- 项目 Warning：必须消除或记录原因。
- 已知非项目警告：Unity MCP 在脚本域重载时偶发 `WebSocket is not initialised`，来源为 `com.coplaydev.unity-mcp` Editor 包；它不进入 Player，不视为 Gameplay Warning，但最终截图/结果中仍需注明是否出现。
- 当前未登记 P16 代码阻塞缺陷；完整 Test Runner、连续手工矩阵、Profiler、Development Build 与 Player 主路径均已通过。
- 最终 `Player.log` 扫描未发现 Error、Warning、Exception、NullReference、MissingReference、Assertion 或 Crash 文本。

## 7. P17 交接审计

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 架构覆盖 | 记录模块关系、AppRoot/SceneContext、模式状态机、Hero 双驱动、命令队列、Save、Visibility/Fog 数据流 | 通过；见 `Architecture.md` 第 2～8 节 |
| 扩展入口 | 明确角色、非巨型 Weapon 的武器/技能、地图、命令与生产扩展步骤 | 通过；见 `Architecture.md` 第 9 节 |
| 需求映射 | 指导文档与用户补充需求均映射到实现或明确推迟 | 通过；见 `Architecture.md` 第 12 节及 `RequirementsAddendum.md` |
| 占位与生成资源 | 记录来源、替换方法；无来源不明的生成图片/音频 | 通过；Unity primitives、项目材质/Shader、地图定义、AudioMixer；无来源不明的位图或 AudioClip |
| 字体与许可 | CJK 字体运行时可用；正式发布前有本地来源/许可记录 | 运行时通过；Noto 已迁入 `_OriginCore` 并生成动态 TMP 资产，来源/许可记录仍待发布审计 |
| 序列化依赖 | `_OriginCore` 不混入未知第三方/旧正式内容 | 通过；外部组件全部解析到声明的 Unity 包；无 QuickOutline/旧正式内容引用 |
| TODO / 临时文件 | 无未解释 TODO/FIXME/HACK/NotImplemented 和 `.tmp/.bak/.orig/.rej/.old` 遗留 | 通过；命中仅为原子写入及测试临时路径逻辑，无实际遗留文件 |
| 孤立 Runtime 脚本 | 每个 Runtime 类型都有代码使用或序列化组件/资产引用 | 通过；名称扫描的 3 个候选均为被广泛调用的 extension container，无 orphan script |
| 最终结构/Console | Unity 导入文档 meta，Console 0 project Error/Warning | 历史 P16 Validator 已通过；当前维护只保留强制 refresh、编译和 Console 清洁检查 |

## 8. 当前维护与按需验收顺序

1. 保存所有场景并退出 Play Mode。
2. 等待 Unity 完成程序集编译，检查 Console 中的项目 Error/Warning，并修复到零。
3. 不再执行或维护 P01～P16 Apply/Validator/Build 菜单。
4. 只有用户明确要求时，才执行 EditMode/PlayMode Run All、受影响的手工/Profiler 链路或 Player 构建；历史通过结果保留在第 1～7 节，不作为每次修改的强制门槛。

## 8.1 Post-P17 Gameplay HUD 补充验收

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| UI 结构 | 小地图 1:1；选择栏底部居中；指令右下；资源右上单行；ACT 左上/FPS 左下图形条；所有外层面板对应边缘零偏移 | Unity 结构检查与 P06/P07/P08/P10/P13/P15 Validator 通过 |
| 提示精简 | 模式/F 键提示和常驻操作提示不可见；按钮无按键后缀 | 序列化结构检查通过；等待用户 Play Mode 目视确认 |
| 直控血条 | ACT/FPS 不显示数值占位，Health/Shield/Energy 图形条随当前 Pawn 实时变化 | 编译与引用检查通过；等待用户 Play Mode 目视确认 |
| RTS 世界血条 | 满血时可隐藏；首次掉血立即出现并按实际 Health/Shield 比例缩短 | 事件链与兜底同步实现、P09 Validator 通过；等待用户 Play Mode 伤害确认 |

本轮按既定约束不由 Codex 运行 Test Runner 或进入 Play Mode。用户只需在 `90_SystemTest` 手工切换 RTS/ACT/FPS，并让任一单位受到一次伤害完成上述补充验收。

## 8.2 Post-P17 独立小地图与 RTS 直移补充验收

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 小地图来源 | 移动、缩放或切换 RTS Camera 时底图、迷雾与实体标记不受 Camera 画面影响；项目不存在小地图 Camera、RenderTexture 或 FogOverlayPlane | `MinimapMapDefinition`、UI fog overlay 与 `EntityRegistry` 标记已落地；序列化结构通过，等待用户目视确认 |
| 信息边界 | 隐藏敌人仍不可见、不可选、不可锁定；建筑记忆、小地图与读档后的已探索区域保持原语义 | Runtime 逻辑未改变；等待用户 Play Mode 回归确认 |
| RTS 直移 | 边缘移动没有跟随旋转；滚轮不改变俯角；小地图点击立即跳转且无转向动画 | 固定 60°、全部 RTS 阻尼归零且 P07 Validator 通过；等待用户 Play Mode 手感确认 |

本轮不由 Codex 运行 Test Runner 或进入 Play Mode。用户只需在 `90_SystemTest` 的 RTS 模式移动友方单位、滚轮缩放并点击小地图，确认底图不随相机变化、UI 标记独立移动、三态迷雾和隐藏敌人正确，并观察相机是否完全无旋转追随。

## 8.3 Post-P17 黑色战争迷雾与场景边界补充验收

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 世界遮罩对齐 | RTS/ACT/FPS 移动或旋转相机时，黑区固定在同一世界位置，不出现体积雾漂移或视线方向偏移 | Shader 编译与材质绑定通过；等待 Play Mode 目视确认 |
| 地形深度 | AnimeGrass 自定义地形与标准 Lit 单位/建筑同时接受黑色遮罩；不能只把实体变黑而留下整个地面常亮 | 远角 Hidden 地面全黑；临时移入友方 VisionEmitter 并 Tick 后同一区域按范围重新显现，运行截图通过 |
| 地图外裁切 | Camera 看向玩法范围外或天空时不出现雾层；边界内无当前视野处为纯黑 | Shader bounds/depth 分支已落地；等待 Play Mode 目视确认 |
| 逻辑一致性 | Hidden/Explored 区域为黑，Visible 透明；敌人显隐、建筑记忆、小地图与存档继续读取同一 VisibilityGrid | Runtime 数据流未分叉；等待 Play Mode 回归确认 |
| 空气墙 | 每个 Gameplay 场景根有且只有一个 `WorldBoundaryWalls`，包含 West/East/South/North 四个启用、非 Trigger、无 Renderer 的 BoxCollider；边界内侧与 MapDefinition 对齐 | Unity 内存验证 4/4 通过；等待实际单位/飞行碰撞确认 |

## 8.4 Post-P17 指令/技能面板与路线补充验收

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 面板结构 | 右下角面板为 360×360 正方形 4×4 网格；可移动单位显示 Move，纯建筑/矿物选择隐藏 Move 且保留空槽，其他指令不重排 | 运行态检查：传输信标 `moveVisible=false`，首个动态槽保持第二行 `(12,-98)` |
| 面板内容 | 面板内不再显示 `COMMANDS/WAITING`、目标提示、队列摘要或反馈文本，全部区域用于指令槽 | `Prompt`、`Queue`、`Feedback` 已从 `P08_CommandPanel/Content` 删除；运行时字段和订阅已移除 |
| 面板表现 | 十六格完整描边；暗色底板、外框、阴影、分类强调色、高光和禁用态层级清晰 | Runtime 统一生成面板 chrome；Unity 编译与 Console 检查通过 |
| 面板避让 | 生产面板贴右边缘并紧邻正方形面板上方，不重叠 | 生产面板继续保持 Y=360；等待按需目视确认 |
| 路线颜色 | Move/集结点为白色虚线；Attack/AttackMove 的每个队列段为红色虚线 | 样式分类与分段渲染池保留；等待按需目视确认 |
| 技能路线 | 后续技能命令使用 `None` 样式且不生成路线 | 接口契约保留；当前无正式技能内容 |

本轮不运行 Test Runner 或进入 Play Mode。若用户后续要求目视验收，可在 `90_SystemTest` 检查 4×4 面板、生产面板避让，以及 Move、Attack/AttackMove、Shift 队列和集结点路线颜色。

## 8.5 Post-P17 资源栏、槽位边框与旧 UI 资源迁移

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 资源栏对齐 | `P10_ResourceHud` 保持右上角零偏移；`Resources` 文字 RectTransform 拉伸到容器并使用 MidlineRight、零 margin | 场景序列化检查通过；等待按需目视确认 |
| 指令槽边框 | 四个功能按钮与十二个预留槽均使用内部四边框，相邻列没有缺失边 | 16/16 槽位已改为 Inset Border；运行截图通过 |
| 旧指令图标 | Move、Attack、Stop 引用 `_OriginCore/Art/UI/Legacy/Commands` 的独立 GUID 副本 | 三个 `LegacyIcon` 已序列化，且不再引用旧 `Assets/Resources` GUID |
| 旧能力图标 | 四个图标复制到 `_OriginCore`，但没有按钮、场景引用或 Runtime 功能声明 | 四个 Sprite 导入成功；保持未引用状态 |

本轮仍只执行编译、序列化结构和 Console 清洁检查，不运行 Test Runner 或进入 Play Mode。

## 8.6 全局 UI 主题运行时检查（2026-08-19）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 主题单例 | 场景内始终只有一个 `OriginCoreUiTheme` | MainMenu 与 LegacyMap 均为 1 |
| 主菜单动态控件 | Options 的 5 Slider、2 Dropdown 与全部动态 Rebind Button 均有主题边框/状态 | 5/5、2/2、78/78 通过 |
| 页面切换 | Main、Play、Map、Story、MatchSetup/Loadout 切换后动态内容继续被主题覆盖 | 运行时切换与结构检查通过 |
| 游戏 HUD 与指令面板 | 普通 HUD 控件使用全局主题；16 个指令格保留 `CommandAccent` 分类样式 | 18/18 可见按钮有边框，2 个通用 Theme Edge、16 个 Command Accent |
| 暂停与设置 | Pause 按钮及其 Options 动态控件完整覆盖 | 24/24 按钮、5/5 Slider、2/2 Dropdown 通过 |
| 语义图形保护 | Health/Shield、小地图、准星、迷雾与图标不被重染 | 世界血条仍为绿色/蓝色；排除规则和运行时数据检查通过 |
| Console | 主题应用不得产生项目 Error/Warning | 0 Error；仅保留既有平坦 Visibility 兼容提示 |

## 8.7 RTS 基地初始镜头、静态建筑与炮台闭环（2026-08-19）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 初始镜头 | 进入地图后 Camera Rig 保持在友方主基地 XZ，默认指针值不触发边缘漂移 | 3 秒运行态检查：Rig `(-8.08,-5.55)` 与 `Z_TransportBeacon_Start` 一致，`edgeArmed=false` |
| 建筑槽位 | 建筑隐藏 Move，其他指令不补位 | 传输信标 Move 隐藏；Produce 保持首个动态槽 `(12,-98)` |
| 矿物静态语义 | 全部 Resource Node 为 Building role 且无队列、Agent、移动驱动 | LegacyMap 3/3 通过 |
| 炮台自动攻击 | Operational 炮台自动扫描并通过标准 AttackCapability 造成伤害 | 运行探针记录 58 次尝试、20 次成功攻击；Range 10 / Damage 10 / Cooldown 0.5s |
| Console | 本轮实现不产生项目 Error/Warning | 收尾清空后 0 Error / 0 Warning |

### 单位资产缓存回归（2026-08-19）

修复 `RuntimeStatBlock.OnValidate` 缓存失效与 `NavMeshMovementDriver` 的 EditMode 写回边界后，AssetDatabase 扫描的 18/18 个可移动 Prefab 均满足 RuntimeStat、NavMeshAgent 与 UnitDefinition 移速一致；原失败用例 `MovablePrefabsOwnOneCompleteCommandPipelineAndBuildingStaysImmobile` 已 1/1 通过。LegacyMap 运行时实例没有零速 Agent。

## 8.8 基地生产与集结点 Overlay（2026-08-19）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 标准比赛生产 | Bootstrap → Legacy match 中选择传输信标、点击 Produce 后正确扣费、推进十秒并生成一个 `z.w` | 运行探针通过：250→220 Crystal、0→1 Influence used、队列 1→0、Worker 0→1 |
| Scene 直启生产 | `SceneBootProxy` 晚于 `UnitSpawner.Awake` 创建 AppRoot 时，生成阶段仍能补绑 Catalog 并解析 `z.w` | 运行探针主动清空 Spawner Catalog 后入队并完成：Catalog 自动恢复、队列 1→0、Worker 0→1 |
| 集结点遮挡 | Rally 落点和白色虚线路线不被地形深度遮挡 | 标记已改用 queue 5000 / ZTest Always Overlay 材质，Renderer sorting order 96 |
| Console | 本轮实现不产生项目 Error/Warning | 强制导入、脚本编译与运行探针收尾后 0 Error / 0 Warning |

## 8.9 UI 像素对齐、中文指令与框选优先级（2026-08-19）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| Canvas 对齐 | 所有 Screen Space Canvas 启用 pixelPerfect | LegacyMap 运行态 3/3 通过 |
| 面板与槽位边框 | 贴边面板和 4×4 相邻槽的四边均在 RectTransform 内可见 | 已改为四条 Inset Image；运行截图四边完整 |
| 中文字体 | 中文技能名无“口口”，动态槽和静态按钮使用同一 CJK TMP 字体 | 指令面板 16/16 文本使用 `F_NotoSansSC_SDF`；“急袭斩/裂空斩/力量解放/闪现”正常显示 |
| 按钮可读性 | 按钮只显示短标题，最小字号不低于 11；长详情移出按钮 | 运行截图通过，详情显示于正下方 INFORMATION 框 |
| 信息框 | 默认显示实体信息；指令悬停/焦点显示目标、范围、冷却、成本与状态 | 运行探针调用动态槽后正确显示“急袭斩”详情 |
| 框选类别互斥 | 框内同时有单位和建筑时，预览与提交都只包含单位 | 全屏混合探针：Preview 1 Unit / 0 Building；Commit 1 Unit / 0 Building |
| Console | 本轮脚本不产生项目 Error | 强制刷新与编译后 0 Error；仅出现 MCP WebSocket 重连警告 |

### Slider 轨道端点补充检查

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 背景/填充范围 | Background 与 FillArea 使用相同水平起止位置，满值左端不露浅色块 | Runtime 结构由 300/284 修正为 284/284；MainMenu Options 截图确认白块消失 |
| 影响范围 | Brightness、Master Volume、Crosshair R/G/B 全部使用同一规则 | 全局 `OriginCoreUiTheme.StyleSlider` 统一处理 |

## 8.10 LegacyMap 手动选择（2026-08-19）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 初始地图 | 从 Map 入口打开配置页时仍可默认 System Test 0 | 保留 `preferredMapId=map.system-test-0` 的首次默认行为 |
| 用户选择 | 点击 Legacy Terrain 后不得被入口默认值覆盖 | 运行态调用实际 `Choice_1.onClick` 后保持 Legacy Terrain |
| 配置输出 | 当前配置输出 `map.legacy / legacy_map` | 运行结果 `map=map.legacy`、`scene=legacy_map`、`TryValidate=True` |

## 8.11 Hero Y 三把专武默认必带（2026-08-20）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 内容引用 | Hero Y 按顺序声明时隙之钥、时序之键、终焉之翼为 `RequiredActWeapons`；Catalog 验证数量、唯一性、注册、AllowedWeapons 与 ACT 支持 | AssetDatabase 读取 3 个稳定 ID，Catalog `TryValidate=True` |
| 配置兼容 | 缺失、错位或重复专武的配置在验证前归一化为前三槽必带，并按原顺序保留最多一个其他唯一武器 | `gun/final-wing/gun/sword` 已归一化为 `timeslot-key/sequence-key/final-wing/gun` |
| 配装 UI | ACT 第 1～3 槽各自显示 `REQUIRED`，只有对应专武且无 `EMPTY`；第 4 槽不提供任一专武 | MainMenu 运行态通过：前三槽各 1 个对应选项且 `empty=False`；第 4 槽 `requiredOffered=False` |
| 库存槽位 | 加载配置后时隙之钥、时序之键进入主手列表，终焉之翼进入独立 Auxiliary 槽 | `main=timeslot-key/sequence-key/gun`，`auxiliary=final-wing` |
| Console | 脚本与资产刷新后无项目 Error | 编译检查 0 Error |

## 8.12 Options 按键绑定可读性（2026-08-20）

| 检查项 | 通过标准 | 当前结果 |
|---|---|---|
| 动作行对比 | 动作名为深色字、行底为浅色，滚动列表内可直接阅读 | 运行截图通过：`#1A2945` 文字 / `#D1E3F5` 行底 |
| 按键名对比 | 按键名为粗体白字、Rebind Button 为深蓝底 | 运行截图通过，键盘与鼠标绑定均清晰 |
| 窄字形 | `WINDOW MODE`、`OPTIONS`、`INPUT BINDINGS` 的大写 `I` 不得消失 | 运行截图全部完整；Thin 字面已由全局屏幕 UI 合成 Bold 补强 |
| 动态刷新 | 绑定行由运行时主题识别，不依赖静态模板颜色 | 运行时生成的 UI/RTS/GLOBAL 行均应用专用主题 |
| Console | 本轮实现不产生项目 Error | 退出 Play 后最终复查 0 Error |
