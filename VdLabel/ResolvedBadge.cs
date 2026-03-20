using System.Drawing;

namespace VdLabel;

/// <summary>
/// ランタイムで解決されたバッジ値（コマンド出力または静的設定から取得）を表す。
/// カタログおよびオーバーレイでの表示に使用される。
/// </summary>
public record ResolvedBadge(string Label, Color Color);
