# CS2 Optimization

Cities: Skylines II 用の、導入するだけで控えめな描画軽量化を自動適用するコードMODです。
ゲームのシミュレーション、セーブデータ、`Settings.coc` は変更しません。

> **状態: ゲーム内未検証。** ビルドと自動テスト（ゲームDLLを使わない純粋ロジック）は通っていますが、
> 実ゲームでの動作確認と比較計測はまだです。FPSの改善幅は保証しません。
> 結果は [docs/benchmarks/1.6.0f1.md](docs/benchmarks/1.6.0f1.md) に記録します。

## 対応版

- ゲーム版 **1.6.0f1** のみ。それ以外の版では描画を一切変更せず、設定画面とログに理由を出します。
- 判定は実行時の `Application.version` と、書き換えるフィールドの型の一致で行います。

## 動作

設定の「モード」で選びます。新規導入時は `Auto` です。保存済みの設定は起動時に上書きしません。

| 状況 | 影の描画距離 | 小物のLOD下限 | 木・市民・車両・建物 |
|---|---:|---:|---|
| `Auto`、Write Everywhere なし | 300 m | 元値 +6（描画距離およそ半分） | 変更しない |
| `Auto`、Write Everywhere あり | 300 m | 変更しない | 変更しない |
| `Off` | 変更しない | 変更しない | 変更しない |
| `Custom` | 無変更 / 100 / 175 / 300 / 500 m | 無変更 / +6 / +12 | 変更しない |

- **影**: ゲーム設定で影が有効なときだけ、影を描く最大距離を制限します。影を有効化したり、太陽光・解像度・カスケード数を変更したりはしません。ゲームが既に要求値以下の距離にしている場合は延ばしません。MOD専用のHDRP Volumeを1つ作り、`maxShadowDistance` だけを上書きします。ゲーム側のVolumeは書き換えません。
- **小物**: `StaticObjectData` または `NetObjectData` を持ち、`BuildingData`・`TreeData`・`PlantData` を持たないPrefabの `ObjectGeometryData.m_MinLod` を元値から上げます（+6ごとに描画距離が半分）。同じ設定を何度適用しても積み上がりません。変更時は既存オブジェクトの描画下限も一度だけ更新するので、`Off` はその場で反映されます。
- `Custom` に切り替えると、直前のモードの値をコピーしてから個別に変更できます。
- `Off`、都市を離れるとき、MODの破棄時には、保持している元値へ同期的に戻します。変更値は保存しないので、次回起動時は常にゲームの元の値から始まります。

### Write Everywhere との関係

Write Everywhere（アセンブリ `BelzontWE`）を検出すると、`Auto` では小物を変更しません。看板や文字が早く消えるのを避けるためです。`Custom` では小物の変更を選べますが、文字が早く消える可能性があります。

### 他の描画MODとの競合

- **Cs2Saver**（アセンブリ `Cs2Saver`）が読み込まれている場合、このMODは描画処理をすべて停止し、設定画面とログに理由を出します。どちらか一方をオフにしてください。
- その他の描画MODは網羅的に判定できません。重なる場合は `Custom` で影・小物を個別に「無変更」に戻してください。
- 競合の判定は初回登録時と都市（またはメニュー）ロード完了時に行います。

## ビルド

.NET 8 SDK と、ゲーム本体のインストールが必要です。HDRPの
`Unity.RenderPipelines.HighDefinition.Runtime.dll` と `Unity.RenderPipelines.Core.Runtime.dll`
はゲーム本体の `Cities2_Data\Managed` から参照します（ゲームDLLは出力にもZIPにも含めません。
含まれるとビルドが失敗します）。

```powershell
# 純粋ロジックのテスト（ゲーム不要）
dotnet test tests/CS2Optimization.Core.Tests/CS2Optimization.Core.Tests.csproj

# MODのビルド（Steamの既定パス以外なら GameManagedDir を指定）
dotnet build src/CS2Optimization/CS2Optimization.csproj -c Release `
  -p:GameManagedDir="C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed"

# 配布ZIPの作成と内容検査 → artifacts/CS2Optimization-<version>.zip
dotnet build src/CS2Optimization/CS2Optimization.csproj -c Release -t:Package -p:GameManagedDir="..."
```

## 導入と解除

1. `CS2Optimization.dll` をローカルMODフォルダの `CS2Optimization` フォルダへ置きます。
   参照元の Cs2Saver インストーラーは
   `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\.cache\Mods\local\CS2Optimization\`
   を使っています（ゲームの `.cache\Mods\mod_directory.json` の記載に従う）。
2. ゲームを起動し、MOD一覧で有効化します。設定は「オプション → CS2 Optimization」。
3. 解除はフォルダを削除するだけです。変更値は保存されないため、ゲーム側に残るものはありません。

ログ: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\CS2Optimization.log`

## 検証の手順（未実施）

同じセーブ・カメラ・時刻・速度1で、ベースライン / `Auto` / `Off` / 影のみ / 小物のみ を各3回、
順序を入れ替えて、ウォームアップを除き60秒以上計測します。平均FPSだけでなくCPU/GPUフレーム時間と
1% lowを記録し、同条件のスクリーンショットで看板・文字・樹木・近景を比較します。
計測中は Auditor の Deep Capture や大きなJSON出力を重ねません。
改善が再現しない、1% lowが一貫して悪化する、看板・文字が予期せず消える場合は既定の対象を減らします。

ゲーム内で確認する項目: 都市A→B→Aの切替、設定変更、`Off` 直後の復元、MOD無効化、WE併用、
ゲーム設定の影オフ、未対応版、`Cs2Saver` 併用、設定保存→再起動。

## ライセンス

GPL-3.0（[LICENSE](LICENSE)）。Cs2Saver（CS2PerformancePatcher）由来のコードを含みます。
詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
