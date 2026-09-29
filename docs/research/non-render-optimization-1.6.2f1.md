# 描画以外の軽量化の調査（ゲーム版 1.6.2f1）

調査日: 2026-09-29。対象: ユーザー提供の参照DLL（`Game.dll` ほか）を ILSpy で逆コンパイル・IL化して確認した実コード。
**ゲーム内での計測は行っていない。** ここに書く効果はコード上の仕組みから推定したもので、数値の裏付けはない。

## 結論

1. **シミュレーション結果を変えずにMODで削れるCPU処理は、見つかった範囲ではほとんどない。** 描画以外の主な負荷はシミュレーション（経路探索、市民・車両・動物のAI）で、量は都市の内容（人口・交通・動物の数）で決まる。量を減らせば、そのぶんゲームの結果や見た目が変わる。
2. **効果が見込めて、MODが不要なもの**: ゲーム自身の設定 `performancePreference`（FrameRate / Balanced / SimulationSpeed）と自動保存の設定。まずこれを試すのが安全。
3. **MODで実装できるが、ゲーム内容を変えるもの**: 野生動物・家畜の出現上限の引き下げ、市民・車両の描画距離の短縮（動く物体の補間処理が減る）。現行の設計方針（シミュレーションと木・市民・車両は変更しない）の外なので、**実装は判断待ち**とした。
4. **推奨しないもの**: 経路探索スレッド数の変更、配置済みオブジェクトへの `Updated` 付与、GC設定の変更（理由は下表）。

## 候補一覧

| # | 候補 | 仕組み（コード上の根拠） | 期待効果 | 変わるもの・リスク | 判断 |
|---|---|---|---|---|---|
| 1 | パフォーマンス優先を FrameRate に | `SimulationSystem.OnUpdate`: 1描画フレームあたりのシミュレーション歩数を `min(8, round(速度×2))` まで回す。`SimulationSpeed` 以外では、`(EndFrameBarrier.lastElapsedTime − currentElapsedTime) ÷ 前回の1歩あたり所要時間` で上限を掛け、`FrameRate` は切り捨て、`Balanced` は切り上げ（最低1歩）。既定値は `GeneralSettings.performancePreference`（未設定時 Balanced） | 描画が重い場面でシミュレーション歩数が減り、フレームレートと1% lowが安定しやすい | シミュレーション（ゲーム内時間の進み）が遅くなる。結果そのものは変わらない | **推奨（ゲーム設定、MOD不要）**。まず計測対象に加える |
| 2 | 自動保存の間隔・有無 | `GeneralSettings.autoSave` / `autoSaveInterval` / `autoSaveCount`（`AutoSaveSystem`） | 保存時の一時的な停止の頻度を減らせる | 保存間隔が空く | ゲーム設定で調整（MOD不要） |
| 3 | 野生動物・家畜の出現上限 | `CreatureSpawnerSystem`（512フレームごと）: 出現地点ごとに「現在の群れ数 ≥ random(0..`CreatureSpawnData.m_MaxGroupCount`)」なら出さない。群れの頭数は `WildlifeData` / `DomesticatedData.m_GroupMemberCount` | 動物のAI・移動・経路（`WildlifeAISystem`、`AnimalMoveSystem`、`AnimalNavigationSystem`）と描画の対象数が減る | 景観（農場の家畜、野生動物）が減る。既存の個体は消えず、新規出現だけが減る。Prefab値の変更なので、本MODの `LodLedger` と同じ方式で元に戻せる | 実装可能。**方針外（見た目・内容が変わる）なので判断待ち**。都市ごとの動物数が不明で、効果の大きさは計測が必要 |
| 4 | 市民・車両の描画距離 | `ObjectInterpolateSystem` の各ジョブは culling データを使い、表示中の動く物体だけを毎フレーム補間する。表示数を減らせばCPUの補間処理も減る。動く物体の `CullingInfo.m_MinLod` は Prefab から、更新のたびに入る（`PreCullingSystem`） | 描画とCPU（補間）の両方が減る | 遠くの市民・車両が早く消える。Cs2Saver のコメントでは、全個体を毎フレーム走査する方式は CPU 5 ms の損失だった。Prefab 方式なら走査は不要 | 実装可能。**現仕様（市民・車両は変更しない）の外なので判断待ち** |
| 5 | ペット | `HouseholdPetSpawnSystem`（16フレームごと）が、飼い主の移動に合わせてペットを出現させる。ペットは世帯に紐づく（`HouseholdPetInitializeSystem`） | 動物の数とAIが減る | 世帯データ（シミュレーション）に関わる | **非推奨**（シミュレーション変更。安全に戻す手段が不明） |
| 6 | 経路探索スレッド数 | `PathfindQueueSystem.OnCreate`: `m_MaxThreadCount = max(1, JobWorkerCount / 2)`。private で、生成時にリストの容量も決まる | 不明。9700X（16スレッド）では既に約7スレッド | private フィールドの書き換え。ほかのジョブとの取り合い | **非推奨** |
| 7 | 配置済み小物の再登録 | 静的物体の検索ツリーは `Updated`/`Deleted` またはロード時にだけ更新される（`Game.Objects.SearchSystem`） | 小物設定の即時反映 | `Updated` は多数の無関係なシステムを起動する | **非推奨**（再ロードで反映する現行方式を維持） |
| 8 | GC調整 | ゲームコード中に `GarbageCollector` / `GCSettings` の利用はない（`Resources.UnloadUnusedAssets` のみ） | 不明 | 実行環境全体に影響 | **非推奨**（根拠なし） |

## 計測の提案

描画側の計測（`docs/benchmarks/1.6.2f1.md`）と同じ手順で、次を追加するのが最小の検証になる。

- `performancePreference`: Balanced（既定）と FrameRate を、速度1と速度3で比較。平均FPS・1% low に加え、ゲーム内時間の進み（同じ実時間で何日進んだか）を記録する。
- 動物の多いマップで、動物の数（おおよそ）とCPUフレーム時間を記録し、#3 を実装する価値があるか判断する。

## 次の判断事項（ユーザー）

- #3（動物の出現上限）と #4（市民・車両の描画距離）を、既定オフの任意機能として追加するか。どちらも現行仕様の「変更しない」範囲を広げる変更になる。
