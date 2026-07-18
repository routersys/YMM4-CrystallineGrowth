# v1.0.0 - 氷晶成長 for YMM4

YukkuriMovieMaker4向けの氷晶成長エフェクトプラグインの初回リリースです。
素材の輪郭を核形成点とみなし、Gravner–Griffeathの雪結晶成長モデルで氷晶を六方格子の上に成長させ、霜として描画します。
氷晶の形はシードから決定論的に決まり、凍結度のパラメータで素材が縁から凍りついていく遷移を表現できます。
計算はComputeSharpの計算シェーダーがDirect3D 12で実行し、YMM4のDirect3D 11側とは共有テクスチャおよび共有フェンスで接続します。
8言語のリソース構成のUIを備えます。

---

## 新機能

### 1. 氷晶成長の計算パイプライン

`CrystallineGrowthPipeline`は、シルエット、成長、描画の3段階の計算シェーダーを`ComputeContext`へ記録して実行します。成長格子のバッファーは計算領域の大きさと品質に応じて確保し、サイズが変わらないフレームでは再利用します。処理の流れは次のとおりです。

1. `SilhouetteShader`が、各格子セルに対応する画素のアルファ値を調べ、しきい値`0.05`を超えるセルを素材の輪郭として記録します。
2. `SeedInitShader`が、輪郭のうち6近傍に輪郭外のセルを持つ境界セルを核形成点として置き、それ以外のセルへ水蒸気の初期密度を設定します。
3. `JumpFloodSeedShader`と`JumpFloodPassShader`が、格子解像度のジャンプフラッドで各セルから最も近い核形成点を求め、`ReachMaskShader`が到達距離の内側のセルだけを付着の対象として記録します。
4. `DiffusionShader`と`GrowthUpdateShader`が、成長の1ステップを2回のディスパッチとして実行し、ステップ数の分だけ記録を繰り返します。
5. `RenderShader`が、画素ごとに近傍セルの結晶質量をカーネル補間で集計し、被覆、法線、屈折、白濁、光沢を計算して出力します。

| シェーダー | 役割 |
|---|---|
| `SilhouetteShader` | 画素のアルファ値から素材の輪郭を求める |
| `MaskHashShader` | 輪郭のハッシュを集計する |
| `SeedInitShader` | 輪郭の境界セルを核形成点として置く |
| `JumpFloodSeedShader` / `JumpFloodPassShader` | 最近傍の核形成点を求める |
| `ReachMaskShader` | 到達距離の内側のセルへ印を付ける |
| `DiffusionShader` | 水蒸気を拡散させる |
| `GrowthUpdateShader` | 凍結・付着・融解・ゆらぎを更新する |
| `RenderShader` | 氷を描画する |

### 2. Gravner–Griffeathモデルの成長ステップ

成長の計算は、Gravner and Griffeathの論文「Modeling Snow Crystal Growth II: A mesoscopic lattice map with plausible dynamics」（2007年）のセルオートマトンに基づきます。格子は各セルが6つの隣接セルを持つ三角格子で、行ごとに半セルずらしたオフセット座標で保持します。各セルは付着フラグ、境界質量、結晶質量、拡散質量の4つの状態を持ちます。

1ステップの更新は次の順で行います。

- 拡散: 結晶外のセルの拡散質量を、自セルと6近傍の一様な重み`1/7`で平均します。結晶セルと計算領域の外は反射境界です。
- 凍結: 結晶に隣接するセルで、拡散質量の割合`κ = 0.07`を結晶質量へ、残りを境界質量へ移します。
- 付着: 隣接する結晶セルの数で条件を分けます。1〜2個ではしきい値`β`、3個では境界質量`1`または近傍の拡散質量が`θ = 0.0205`未満のときのしきい値`α = 0.21`、4個以上では無条件で付着します。付着したセルには付着ステップを記録します。
- 融解: 境界質量の割合`μ = 0.015`と結晶質量の割合`γ = 0.00005`を拡散質量へ戻します。
- ゆらぎ: 拡散質量へ、セル番号とステップとシードのハッシュから決めた符号で`±σ`の変動を掛けます。

水蒸気の密度`ρ`は樹枝化のパラメータから0.45〜0.80、付着のしきい値`β`はファセットのパラメータから1.05〜2.60、ゆらぎの大きさ`σ`はゆらぎのパラメータから0〜0.002へ割り当てます。乱数は使用せず、すべての値が入力から決定論的に決まります。付着は到達距離の印を持つセルへ限定し、霜の広がりを縁からの距離で制限します。

拡散と更新のシェーダーは、拡散質量と付着フラグをそれぞれ2枚のバッファーで交互に読み書きし、全ステップを1つの`ComputeContext`へ記録して送出します。

### 3. 可視範囲への出力矩形の最小化

成長の完了後、付着ステップの記録をCPUへ読み戻します。読み戻す量は格子1面の整数だけで、画素は読み戻しません。付着ステップごとの境界を累積した配列を1回の走査で構築するため、凍結度が定める可視セルのバウンディングボックスはフレームごとに一定時間で求まります。

- 矩形にはカーネル補間の半径分の余白を加え、4画素境界へそろえます。
- 出力テクスチャは矩形の大きさで確保し、Direct2Dの`Crop`と`AffineTransform2D`で元の位置へ合成します。
- 矩形の外側は描画も合成も行わないため、到達距離を大きくしても処理量は氷晶が実在する範囲に比例します。

### 4. 構造キャッシュ

結晶の形を決める入力が変わらないフレームでは、成長の計算を再利用します。

- 素材の輪郭は、シルエットの計算後にセル位置のハッシュの総和とXORの2値へ集約し、8個の整数の読み戻しで前フレームと比較します。
- 輪郭のハッシュ、計算領域の大きさ、品質、シード、到達距離、樹枝化、ファセット、ゆらぎが一致する場合は、成長段階と記録の読み戻しを実行しません。
- 構造が同じで、凍結度、白濁、屈折、光沢、色、出力矩形も変わらないフレームでは、描画段階も実行せず、前フレームの出力テクスチャを使用します。

### 5. Direct3D 11・Direct3D 12相互運用

`CrystallineGrowthGpuInterop`は、YMM4のDirect3D 11・Direct2D側と、ComputeSharpのDirect3D 12側を接続します。ComputeSharpの`GraphicsDevice`は、YMM4が使うDXGIアダプターのLUIDと一致するものを選びます。

入力と出力は、ComputeSharpで確保した共有テクスチャをDirect3D 11のテクスチャとして開き、Direct2Dのビットマップとして扱います。入力のテクスチャは素材の大きさで確保し、出力のテクスチャは可視範囲の矩形を収める容量で確保して拡大時だけ作り直します。両デバイスの同期は、Direct3D 12のフェンスを共有フェンスとしてDirect3D 11側で開いて行います。

`BeginCompute`は、Direct3D 11のコマンドを送出したうえでDirect3D 12側を待機させ、`EndCompute`は、Direct3D 12側の完了をDirect3D 11側で待ちます。

Direct3D 12デバイスの取得や共有リソースの作成に失敗した場合は、`TryCreate`が`null`を返し、エフェクトを適用せず入力映像を表示します。

### 6. カスタムシェーダーによる合成

`CrystallineGrowthCustomEffect`は、`[CustomEffect(2)]`の2入力エフェクトです。入力0は元映像、入力1は描画した氷です。ピクセルシェーダー`CrystallineGrowth.hlsl`の`main`は、`amount`が0以下のとき元映像をそのまま返し、そうでないときは氷のRGBをアルファでクランプし、`ice + source * (1 - ice.a)`のアルファ合成で氷を元映像の上へ重ねます。屈折した背景は描画段階で氷の画素へ焼き込むため、合成は単純な重ね合わせです。

定数バッファーは`Amount`と3つの詰め物で16バイトです。`MapInputRectsToOutputRect`は2つの入力矩形の和集合を出力矩形とします。霜は素材の外側へ広がるため、出力範囲は素材より大きくなります。

シェーダーリソース: `pack://application:,,,/CrystallineGrowth;component/Shaders/CrystallineGrowth.cso`（ps_5_0、`ShaderResourceUri.Get`が生成）

### 7. エフェクト定義とパラメータ

`CrystallineGrowthEffect`は、YMM4の映像エフェクトとして宣言されます。

`[VideoEffect]`属性は以下のパラメーターで宣言されます。

- 表示名: `Texts.CrystallineGrowth`（ローカライズキー、日本語では「氷晶成長」）
- カテゴリー: `VideoEffectCategories.Decoration`・`VideoEffectCategories.Animation`
- 検索タグ: `TagIce`・`TagFrost`・`TagFreeze`
- `IsAviUtlSupported = false`によりAviUtl向けEXO出力は非対応
- `ResourceType = typeof(Texts)`でローカライズリソースを指定

公開プロパティは以下のとおりです。基本項目は「基本」グループ、成長項目は「成長」グループ、描画項目は「描画」グループに属します。

| プロパティ | 型 | デフォルト | 内部範囲 | アニメーション |
|---|---|---|---|---|
| `Amount` | `Animation` | 100 | 0〜100 | あり |
| `Freeze` | `Animation` | 100 | 0〜100 | あり |
| `Quality` | `CrystallineGrowthQuality` | `High` | — | なし |
| `Branching` | `Animation` | 60 | 0〜100 | あり |
| `Facet` | `Animation` | 40 | 0〜100 | あり |
| `Reach` | `Animation` | 25 | 1〜400 | あり |
| `Noise` | `Animation` | 25 | 0〜100 | あり |
| `Seed` | `int` | 0 | 0〜int.MaxValue | なし |
| `Frost` | `Animation` | 70 | 0〜100 | あり |
| `Refraction` | `Animation` | 40 | 0〜100 | あり |
| `Specular` | `Animation` | 50 | 0〜100 | あり |
| `IceColor` | `Color` | #FFCDE4FF | — | なし |

`GetAnimatables`は`Amount`・`Freeze`・`Branching`・`Facet`・`Reach`・`Noise`・`Frost`・`Refraction`・`Specular`を返します。`Seed`は負値を代入すると0へ丸めます。

`CreateExoVideoFilters`は空のシーケンスを返します（EXO非対応）。`CreateVideoEffect`は映像処理用のインスタンスを生成します。エフェクトを最初に生成したときに、更新確認を一度だけ開始します。

### 8. フレームごとの更新

各フレームでYMM4の`EffectDescription`からフレーム位置、アイテム長、FPSを取得し、アニメーション値を評価します。値をパイプラインが前提とする範囲へ制限してから転送します。

| パラメータ | 変換 |
|---|---|
| `Amount` | `value / 100` をカスタムシェーダーの`Amount`へ |
| `Freeze` | `value / 100` を0〜1へクランプ |
| `Branching` | `value / 100` を0〜1へクランプ |
| `Facet` | `value / 100` を0〜1へクランプ |
| `Reach` | `value / 100` を0.01〜4へクランプし、素材の長辺に掛けて画素数へ |
| `Noise` | `value / 100` を0〜1へクランプ |
| `Frost` | `value / 100` を0〜1へクランプ |
| `Refraction` | `value / 100` を0〜1へクランプし、最大8画素の参照ずれへ |
| `Specular` | `value / 100` を0〜1へクランプ |
| `IceColor` | RGB各成分を0〜1へ |
| `Seed` | 0以上へクランプ |

強さが0以下のとき、または凍結度が0以下のときは、氷を描画せず入力映像をそのまま出力します。入力の範囲が有限でない場合や、計算領域の余白を確保できない場合も、入力映像を表示します。

### 9. 品質設定

品質は、成長格子の解像度と成長ステップ数の上限をまとめて切り替えます。

| 品質 | 格子解像度 | 成長ステップ上限 |
|---|---:|---:|
| 標準 | 192 | 1200回 |
| 高品質 | 288 | 2048回 |
| 最高品質 | 384 | 3072回 |

格子解像度は計算領域の長辺のセル数です。短辺のセル数は計算領域の縦横比に合わせ、最小4セルとします。実際のステップ数は到達距離に応じたセル数から決まり、上限で打ち切ります。

### 10. ローカライズ

`Texts`クラスは`[AutoGenLocalizer]`属性を持つ`partial`クラスとして宣言されます。
`YukkuriMovieMaker.Generator`のソースジェネレーターが`Texts.csv`を処理し、各ロケールのリソースファイルを自動生成します。

対応リソース: 日本語（`ja-jp`）・英語（`en-us`）・中国語簡体字（`zh-cn`）・中国語繁体字（`zh-tw`）・韓国語（`ko-kr`）・スペイン語（`es-es`）・アラビア語（`ar-sa`）・インドネシア語（`id-id`）

主なローカライズキーは以下のとおりです。

| キー | ja-jp |
|---|---|
| `CrystallineGrowth` | 氷晶成長 |
| `BasicGroup` | 基本 |
| `GrowthGroup` | 成長 |
| `AppearanceGroup` | 描画 |
| `Amount` | 強さ |
| `Freeze` | 凍結度 |
| `Quality` | 品質 |
| `Branching` | 樹枝化 |
| `Facet` | ファセット |
| `Reach` | 到達距離 |
| `Noise` | ゆらぎ |
| `Seed` | シード |
| `Frost` | 白濁 |
| `Refraction` | 屈折 |
| `Specular` | 光沢 |
| `IceColor` | 色 |
| `QualityBalanced` | 標準 |
| `QualityHigh` | 高品質 |
| `QualityUltra` | 最高品質 |
| `TagIce` | 氷 |
| `TagFrost` | 霜 |
| `TagFreeze` | 凍結 |
| `UpdateAvailableMessage` | 新しいバージョン {0} が公開されています。 |
