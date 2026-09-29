# CS2 Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This plan assumes Native execution by the Work session; no task subagents are required.

**Goal:** Cities: Skylines II 1.6.0f1 用の、導入後に控えめな描画軽量化を自動適用し、その場で無効化できる独立したコードMODを作る。

**Architecture:** 純粋な設定方針、PrefabのLOD元値を保持する制御器、HDRP影距離の制御器、ゲームの登録・設定UIを分ける。WE併用を検知した自動設定では小物を変更せず、影のみ制限する。未知のゲーム版や `Cs2Saver` 併用時は描画変更を止める。

**Tech Stack:** C# / `netstandard2.1`（CoreとMOD）、.NET 8 / xUnit（純粋ロジックのテスト）、Cities: Skylines II `Game.dll`、Unity Entities、HDRP、ゲーム内ModSetting。

**Spec:** `docs/superpowers/specs/2026-09-29-cs2-optimization-design.md`

## Global Constraints

- 実装先は `pengin0503/CS2-Optimization`。初回は `main` から作業ブランチを作る。ゲーム版 `1.6.0f1` を初期対応範囲とし、未知の版で描画変更を行わない。
- `CS2PerformancePatcher` commit `69015dd90cfc667c100beb16fd158a43a9e03a23` の描画距離・影距離の実装を参考にする。使用したコードにはGPL-3.0と著作権表示を残す。`CS2-WriteEverywhere` のコードは取り込まない。
- 初期値は `Auto`: 影300 m、小物のLOD下限はWE未導入時だけ元値+6、木・車両・市民・建物は無変更。影がゲーム設定で無効なら有効化しない。
- `Custom` の選択肢は影が無変更/100/175/300/500 m、小物が無変更/+6/+12。`Off` と破棄では元値に戻す。
- `Settings.coc`、セーブデータ、ゲームDLLを書き換えたり配布ZIPにゲームDLLを同梱したりしない。フレームごとに市民・車両の全件処理をしない。
- 添付の参照ZIPだけではHDRPの `Unity.RenderPipelines.HighDefinition.Runtime.dll` と `Unity.RenderPipelines.Core.Runtime.dll` が不足する。完全ビルドはゲーム本体の `Cities2_Data/Managed` を参照する。

## Review Focus

1. WEあり/なしで自動小物調整の有無が切り替わる: Task 1 の方針テスト。
2. 既存設定と手動の `Custom` が再起動時に初期値へ戻らない: Task 5 の保存・再読込テストとゲーム内確認。
3. 同一Prefabに再適用してLOD値が積み上がる、別都市で古いEntity IDを再利用する: Task 2 の連続適用・都市切替テスト。
4. ゲーム内でOffにした直後やMOD破棄時に描画が戻らない: Task 3/4 の復元テストとゲーム内確認。
5. 影オフ、別の描画MOD、未知のゲーム版のときに無言で上書きする: Task 1/4/5 の停止理由・影状態テスト。

## File Map

| Path | 責務 |
|---|---|
| `src/CS2Optimization.Core/CS2Optimization.Core.csproj` | ゲーム参照なしの方針・元値管理 |
| `src/CS2Optimization/CS2Optimization.csproj` | Core参照、ゲームインストールからの非コピー参照とビルド |
| `src/CS2Optimization.Core/EffectiveProfilePolicy.cs` | 設定から実効プロファイル・停止理由を決定 |
| `src/CS2Optimization.Core/LodLedger.cs` | 都市単位の元値記録、目標値、復元候補 |
| `src/CS2Optimization.Core/PrefabCategoryPolicy.cs` | 小物分類の純粋な判定 |
| `src/CS2Optimization.Core/ShadowOverrideState.cs` | 影が有効なときだけ要求距離を返す |
| `src/CS2Optimization/Rendering/PrefabLodSystem.cs` | ECSのPrefab選択・反映・同期復元 |
| `src/CS2Optimization/Rendering/ShadowDistanceSystem.cs` | 自前HDRP Volumeの生成・無効化・破棄 |
| `src/CS2Optimization/Mod.cs`, `Settings.cs`, `LocaleJA.cs`, `LocaleEN.cs` | 登録、版判定、競合検出、設定UI |
| `tests/CS2Optimization.Core.Tests/` | 方針、元値管理、影の状態遷移の自動テスト |
| `README.md`, `LICENSE`, `THIRD_PARTY_NOTICES.md` | インストール・対応版・検証法・帰属 |

### Task 1: 実効設定の純粋ロジックと基礎プロジェクト

**Files:** `src/CS2Optimization.Core/EffectiveProfilePolicy.cs`, `src/CS2Optimization.Core/CS2Optimization.Core.csproj`, `src/CS2Optimization/CS2Optimization.csproj`, `tests/CS2Optimization.Core.Tests/EffectiveProfilePolicyTests.cs`, テスト用 `.csproj`、`LICENSE`。

**Interfaces:** `EffectiveProfilePolicy.Resolve(Mode mode, int customShadowMetres, int customPropLodOffset, bool writeEverywhereLoaded, bool cs2SaverLoaded, bool supportedGameVersion, bool gameShadowsEnabled) -> EffectiveProfile { ShadowMetres, PropLodOffset, StopReason }`。`ShadowMetres=0` は無変更。`StopReason` は `None` / `Cs2SaverConflict` / `UnsupportedGame` / `InvalidSettings`。`EffectiveProfilePolicy.ToCustom(EffectiveProfile current) -> (int shadowMetres, int propLodOffset)`。

- [ ] `Auto`（WEなし: 300/+6、WEあり: 300/0）、`Off`（0/0）、`Custom`、影オフ（0）、競合・未知版（停止理由付き0/0）、無効値、`ToCustom`の値コピーをテストに書く。
- [ ] `dotnet test tests/CS2Optimization.Core.Tests/CS2Optimization.Core.Tests.csproj` で未実装による失敗を確認する。
- [ ] 列挙型と `Resolve` を最小実装し、入力値は列挙済みの影距離と0/6/12だけ受け入れる。無効値は変更なしで停止理由を示す。
- [ ] 同じテストを成功させる。MOD側の `netstandard2.1` プロジェクトにゲームDLL参照を `<Private>false</Private>` で設定し、ゲームDLLを出力へコピーしないことをビルド設定で確認する。CoreとそのテストはゲームDLLなしで動かす。
- [ ] この単位をコミットする。

### Task 2: 小物LODの元値管理

**Files:** `src/CS2Optimization.Core/LodLedger.cs`, `tests/CS2Optimization.Core.Tests/LodLedgerTests.cs`。

**Interfaces:** `LodLedger<TId>.GetOrCapture(TId id, int current) -> int`; `Target(int original, int offset) -> int` は0..255へ収める; `Entries` は復元用のIDと元値; `Clear()` は都市終了時に呼ぶ。

- [ ] 元値120に+6を繰り返しても126、+12なら132、+0で120、元値254+12で255になるテストを書く。同じIDを新都市で再利用すると `Clear()` 後には新しい元値を保持するテストを加える。
- [ ] `dotnet test ... --filter FullyQualifiedName~LodLedgerTests` が失敗することを確認する。
- [ ] 記録と計算だけを実装する。元値は変更前に一度だけ保存し、書き込み対象の列挙は呼び出し側に委ねる。
- [ ] 対象のテストが成功することを確認してコミットする。

### Task 3: ECS Prefabへの反映と確実な復元

**Files:** `src/CS2Optimization/Rendering/PrefabLodSystem.cs`, `src/CS2Optimization.Core/PrefabCategoryPolicy.cs`, `tests/CS2Optimization.Core.Tests/PrefabSelectionTests.cs`。

**Interfaces:** `SetOffset(int offset)`, `RestoreNow()`, `ResetForNewCity()`。Task 2 の `LodLedger<Entity>` を使用。`PrefabCategoryPolicy.IsEligible(bool hasStatic, bool hasNet, bool hasBuilding, bool hasTree, bool hasPlant) -> bool`。`ObjectGeometryData` を持つPrefabのうち `StaticObjectData` または `NetObjectData` を持ち、`BuildingData`/`TreeData`/`PlantData`を持たないものだけ変更。

- [ ] 建物・木・植物の除外、対象小物、設定反転後の元値復元、消滅したEntityへの書き戻し回避をテストに書く。
- [ ] テスト失敗を確認する。
- [ ] `ObjectInitializeSystem` の後に実行されるゲームシステムを実装する。設定変更とPrefab追加時だけ更新し、EntityManagerの有効性を確かめて `RestoreNow()` で同期的に戻す。都市終了では復元後 `Clear()` する。
- [ ] 純粋テストとゲームDLL参照のビルドを通す。ゲーム内で都市A→B→A、Off切替、MOD破棄時の小物表示を確認する。
- [ ] この単位をコミットする。

### Task 4: 影距離の独立制御

**Files:** `src/CS2Optimization/Rendering/ShadowDistanceSystem.cs`, `src/CS2Optimization.Core/ShadowOverrideState.cs`, `tests/CS2Optimization.Core.Tests/ShadowOverrideStateTests.cs`。

**Interfaces:** `ShadowOverrideState.Resolve(int requestedMetres, bool gameShadowsEnabled) -> float?`。`null` はHDRP override無効。ゲーム実装は自分の `Volume` に `HDShadowSettings.maxShadowDistance` だけ設定し、`SetDistance(float?)` と `RestoreNow()` を公開。

- [ ] 影オフ・要求0ではoverrideなし、影オン・300で300 m、Offへ戻した時点で無効になるテストを書く。
- [ ] テスト失敗を確認する。
- [ ] 自前Volumeだけを制御する。太陽光 `Light`、cascade、解像度、ゲーム側Volumeに書かない。終了時はVolumeを破棄し、ロード時とゲーム設定変更を拾うため120フレームごとに影の状態を再評価する。
- [ ] テストとHDRP参照を含むビルドを確認する。ゲーム内の影オン/オフ切替で表示とログが整合することを確認する。
- [ ] この単位をコミットする。

### Task 5: 自動適用、設定保存、競合・版ガード

**Files:** `src/CS2Optimization/Mod.cs`, `Settings.cs`, `LocaleJA.cs`, `LocaleEN.cs`, `tests/CS2Optimization.Core.Tests/StartupPolicyTests.cs`。

**Interfaces:** `Settings` は `Mode`, `CustomShadowMetres`, `CustomPropLodOffset` を保存。`Mode=Custom` の切替時に直前の実効値をコピーする。`Mod.OnLoad(UpdateSystem)` は設定読み込み後にTask 1の方針を二制御器へ反映。`OnDispose()` はTask 3/4の同期復元を呼ぶ。

- [ ] 初回Auto、保存済みOff/Custom、WE検出、`Cs2Saver`競合、未知ゲーム版、ゲームの影オフ、Custom切替時の実効値コピーをテストに書く。
- [ ] テスト失敗を確認する。
- [ ] `Application.version` と読み込まれたアセンブリ名（WE=`BelzontWE`、競合=`Cs2Saver`）でガードし、`Game.Settings.ModSetting` に日本語・英語の状態表示を追加する。初回登録時と都市ロード時に競合を再判定する。各機能の失敗は片方へ波及させず、同一警告を繰り返さない。
- [ ] テスト・MODビルドを通す。ゲーム内で設定保存→再起動、WEあり/なし、Cs2Saverあり、Off直後の復元を確認する。
- [ ] この単位をコミットする。

### Task 6: 配布物と比較計測

**Files:** `README.md`, `THIRD_PARTY_NOTICES.md`, ビルド・ZIP生成スクリプト、検証結果を記す `docs/benchmarks/1.6.0f1.md`。

- [ ] コピーしたCs2Saver由来の部分を著作権表示とGPL-3.0に照らして点検し、WEコードとゲームDLLがZIPにないことをパッケージ検査で確認する。
- [ ] ゲーム版、ビルド方法、導入・解除、Auto/Off/Custom、WEとの関係、既存描画MODとの競合時の扱いをREADMEへ記載する。
- [ ] 同じセーブ・カメラ・時刻・速度1でベースライン/Auto/Off/影のみ/小物のみを各3回、ウォームアップを除き60秒以上計測する。CPU/GPUフレーム時間、平均FPS、1% low、MOD自身の処理時間、スクリーンショットを記録する。計測中にAuditorのDeep CaptureやJSON出力はしない。
- [ ] 既定の変更で改善を再現できない、1% lowが一貫して悪化する、看板・文字が予期せず消える場合は既定の対象を減らして再測定する。実ゲーム検証なしに「改善を確認」と表記しない。
- [ ] `dotnet test`、参照DLLを指定した `dotnet build -c Release`、ZIP内容の検査を実行し、結果を記録してコミットする。

## 完了時の引き渡し

ゲーム内の比較測定にはWindows上のゲーム実行が必要。このWork環境で実ゲーム検証ができない場合、ビルド・自動テストと未実施のゲーム内項目を明確に分けて報告する。初回リリースの判断は対応版でのゲーム内動作・復元・比較測定の結果に基づく。
