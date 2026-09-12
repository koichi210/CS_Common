// 標準のDataGridViewにCtrl+Z(元に戻す)/Ctrl+Y(やり直す)を追加しただけの拡張コントロール。
// EventRecorder以外のプロジェクトでも使い回せるよう_Commonに置いてある。
//
// ■対応範囲
// セルの値(Value)の変更を追跡する。手入力での編集はもちろん、コード側で
// Cell.Value = ... と直接代入した場合(貼り付け処理・Deleteキーでのクリア等)も、
// CellValueChangedイベント経由で拾えるので同じくUndo/Redo対象になる。
// 行の追加/削除そのものの取り消しには対応していない(対応する場合は別途拡張が必要)。
// 行が削除されるとインデックスがずれて古い履歴の指す位置が意味を失うため、
// そのタイミングでUndo/Redo履歴は安全のためクリアされる。
//
// ■複数セルをまとめて1回のUndoで戻したい場合
// 貼り付けや範囲クリアのように、1回の操作で複数セルが変わるケースをまとめて
// 1つのUndo単位にしたい時は、その処理の前後をBeginUndoBatch()/EndUndoBatch()で挟む。
// 挟まなければ、セル1つの変更ごとに別々のUndo単位になる。
//
// ■有効/無効
// EnableUndoRedoプロパティで切り替え可能(既定は true = 有効)。
//
// ■Ctrl+マウスホイールによる文字/チェックボックスの拡大縮小
// Ctrlキーを押しながらホイールを回すと、フォントサイズ・行の高さ・
// チェックボックスのグリフサイズがまとめて拡大縮小される。
// (Ctrlなしの通常のホイールは今まで通り行スクロールに使われる)
// ZoomFactorプロパティで現在の倍率を取得/設定でき、ResetZoom()で1.0倍に戻せる。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StandardTemplate
{
    public class DataGridViewEx : DataGridView
    {
        // Ctrl+Z/Ctrl+Yによる元に戻す/やり直すを有効にするかどうか。既定は有効
        public Boolean EnableUndoRedo { get; set; } = true;

        // Ctrl+ホイールでの拡大縮小の下限/上限、および1ノッチあたりの増減量
        private const float MinZoomFactor = 0.5f;
        private const float MaxZoomFactor = 3.0f;
        private const float ZoomStep = 0.1f;

        // チェックボックスのグリフサイズの基準値(等倍時、96DPI相当のピクセル数)
        private const int BaseCheckBoxSize = 13;

        private float zoomFactor = 1.0f;

        // 拡大縮小の基準となる、コントロール生成直後(デザイナ設定反映後)の元サイズ
        private Font baseFont;
        private int baseRowHeight;
        private int baseColumnHeadersHeight;

        // 現在の拡大率(0.5〜3.0)。Ctrl+ホイールで変化する
        public float ZoomFactor
        {
            get { return zoomFactor; }
            set
            {
                float clamped = Math.Max(MinZoomFactor, Math.Min(MaxZoomFactor, value));
                if (Math.Abs(clamped - zoomFactor) < 0.0001f)
                {
                    return;
                }

                zoomFactor = clamped;
                ApplyZoom();
            }
        }

        // 拡大率を等倍(1.0倍)に戻す
        public void ResetZoom()
        {
            ZoomFactor = 1.0f;
        }

        // 1件のセル変更を表す(元の値・変更後の値のペア)
        private class CellChange
        {
            public int RowIndex;
            public int ColumnIndex;
            public Object OldValue;
            public Object NewValue;
        }

        // Undo/Redoの履歴。1エントリ=1回のUndo/Redo単位(複数セルをまとめたものも含む)
        private readonly Stack<List<CellChange>> undoStack = new Stack<List<CellChange>>();
        private readonly Stack<List<CellChange>> redoStack = new Stack<List<CellChange>>();

        // 各セルの「現在の値」のキャッシュ。CellValueChangedが飛んできた時点では
        // 新しい値しか分からないため、直前の値をここに覚えておいて差分を取る。
        // キーは (行番号 << 32 | 列番号) にエンコードしたlong
        private readonly Dictionary<long, Object> valueCache = new Dictionary<long, Object>();

        // BeginUndoBatch()〜EndUndoBatch()の間に集めた変更を溜めておくバッファ(null=バッチ中でない)
        private List<CellChange> pendingBatch;

        // Undo/Redoを適用している最中は、その変更自体を新たな履歴として拾わないようにするフラグ
        private Boolean isApplyingHistory;

        public DataGridViewEx()
        {
            this.CellValueChanged += DataGridViewEx_CellValueChanged;
            this.RowsAdded += DataGridViewEx_RowsAdded;
            this.RowsRemoved += DataGridViewEx_RowsRemoved;
            this.CellPainting += DataGridViewEx_CellPainting;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // デザイナで設定されたFont/行の高さ等がすべて反映された直後の値を、
            // 拡大縮小の基準サイズ(=1.0倍時のサイズ)として覚えておく
            CaptureBaseMetricsIfNeeded();
        }

        private void CaptureBaseMetricsIfNeeded()
        {
            if (baseFont != null)
            {
                return;
            }

            baseFont = this.Font;
            baseRowHeight = this.RowTemplate.Height;
            baseColumnHeadersHeight = this.ColumnHeadersHeight;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                int steps = e.Delta / SystemInformation.MouseWheelScrollDelta;
                if (steps != 0)
                {
                    ZoomFactor = zoomFactor + steps * ZoomStep;
                }
                // Ctrl+ホイールは拡大縮小専用にし、行スクロールへは渡さない
                return;
            }

            base.OnMouseWheel(e);
        }

        // フォントサイズ・行の高さ・ヘッダーの高さを現在のZoomFactorに合わせて更新する
        private void ApplyZoom()
        {
            CaptureBaseMetricsIfNeeded();
            if (baseFont == null)
            {
                return;
            }

            float newSize = Math.Max(1f, baseFont.Size * zoomFactor);
            Font oldFont = this.Font;
            Font newFont = new Font(baseFont.FontFamily, newSize, baseFont.Style);

            this.Font = newFont;
            this.DefaultCellStyle.Font = newFont;
            this.ColumnHeadersDefaultCellStyle.Font = newFont;
            this.RowHeadersDefaultCellStyle.Font = newFont;

            if (oldFont != baseFont)
            {
                oldFont.Dispose();
            }

            int newRowHeight = Math.Max(this.RowTemplate.MinimumHeight, (int)Math.Round(baseRowHeight * zoomFactor));
            this.RowTemplate.Height = newRowHeight;
            foreach (DataGridViewRow row in this.Rows)
            {
                row.Height = newRowHeight;
            }

            this.ColumnHeadersHeight = Math.Max(4, (int)Math.Round(baseColumnHeadersHeight * zoomFactor));

            this.Invalidate();
        }

        // チェックボックス列のセルは、標準描画のままだとグリフが常に固定サイズなので、
        // 背景/枠線だけ標準描画に任せてグリフ部分だけZoomFactorに応じたサイズで描き直す
        private void DataGridViewEx_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!(this.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn))
            {
                return;
            }

            if (Math.Abs(zoomFactor - 1.0f) < 0.0001f)
            {
                // 等倍時は標準描画のほうがOSのテーマに沿った見た目になるので任せる
                return;
            }

            e.PaintBackground(e.ClipBounds, true);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            Boolean isChecked = ToCheckBoxChecked(e.Value);
            int size = Math.Max(4, (int)Math.Round(BaseCheckBoxSize * zoomFactor));
            Rectangle box = new Rectangle(
                e.CellBounds.X + (e.CellBounds.Width - size) / 2,
                e.CellBounds.Y + (e.CellBounds.Height - size) / 2,
                size,
                size);

            ButtonState state = isChecked ? ButtonState.Checked : ButtonState.Normal;
            if (this.Columns[e.ColumnIndex].ReadOnly || this.Rows[e.RowIndex].ReadOnly)
            {
                state |= ButtonState.Inactive;
            }

            ControlPaint.DrawCheckBox(e.Graphics, box, state);
            e.Handled = true;
        }

        private static Boolean ToCheckBoxChecked(Object cellValue)
        {
            if (cellValue is Boolean)
            {
                return (Boolean)cellValue;
            }

            try
            {
                return Convert.ToBoolean(cellValue);
            }
            catch
            {
                return false;
            }
        }

        // 複数セルへの変更をまとめて1つのUndo単位にしたい処理の前後で呼ぶ
        public void BeginUndoBatch()
        {
            pendingBatch = new List<CellChange>();
        }

        public void EndUndoBatch()
        {
            if (pendingBatch == null)
            {
                return;
            }

            List<CellChange> batch = pendingBatch;
            pendingBatch = null;

            if (batch.Count > 0)
            {
                PushUndo(batch);
            }
        }

        // Undo/Redo履歴を空にする(ファイルの読込直後など、それ以前の編集を
        // 誤って元に戻せてしまわないようにしたい場面で呼ぶ)
        public void ClearUndoHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
            RebuildValueCache();
        }

        public Boolean CanUndo
        {
            get { return undoStack.Count > 0; }
        }

        public Boolean CanRedo
        {
            get { return redoStack.Count > 0; }
        }

        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            List<CellChange> changes = undoStack.Pop();
            ApplyChanges(changes, useOldValue: true);
            redoStack.Push(changes);
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                return;
            }

            List<CellChange> changes = redoStack.Pop();
            ApplyChanges(changes, useOldValue: false);
            undoStack.Push(changes);
        }

        protected override Boolean ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // セル編集中(テキストボックス等で入力中)のCtrl+Zは、そのエディタ自身の
            // 1文字戻す挙動に任せたいので、編集中でない時だけグリッド側のUndo/Redoを割り込ませる
            if (EnableUndoRedo && !this.IsCurrentCellInEditMode)
            {
                if (keyData == (Keys.Control | Keys.Z))
                {
                    Undo();
                    return true;
                }

                if (keyData == (Keys.Control | Keys.Y))
                {
                    Redo();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ApplyChanges(List<CellChange> changes, Boolean useOldValue)
        {
            isApplyingHistory = true;
            try
            {
                foreach (CellChange change in changes)
                {
                    if (change.RowIndex < 0 || change.RowIndex >= this.Rows.Count
                        || change.ColumnIndex < 0 || change.ColumnIndex >= this.Columns.Count)
                    {
                        continue;
                    }

                    Object value = useOldValue ? change.OldValue : change.NewValue;
                    this.Rows[change.RowIndex].Cells[change.ColumnIndex].Value = value;
                    valueCache[MakeKey(change.RowIndex, change.ColumnIndex)] = value;
                }
            }
            finally
            {
                isApplyingHistory = false;
            }
        }

        private void PushUndo(List<CellChange> changes)
        {
            undoStack.Push(changes);
            // 新しい変更が入ったら、それより後のRedo履歴は辻褄が合わなくなるので破棄する
            redoStack.Clear();
        }

        private void DataGridViewEx_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!EnableUndoRedo || isApplyingHistory)
            {
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            long key = MakeKey(e.RowIndex, e.ColumnIndex);
            Object oldValue;
            valueCache.TryGetValue(key, out oldValue);
            Object newValue = this.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

            valueCache[key] = newValue;

            if (Object.Equals(oldValue, newValue))
            {
                return;
            }

            CellChange change = new CellChange
            {
                RowIndex = e.RowIndex,
                ColumnIndex = e.ColumnIndex,
                OldValue = oldValue,
                NewValue = newValue
            };

            if (pendingBatch != null)
            {
                pendingBatch.Add(change);
            }
            else
            {
                PushUndo(new List<CellChange> { change });
            }
        }

        private void DataGridViewEx_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            // 新しい行のセルは初期値nullとしてキャッシュしておく(差分検出の基準点として必要)
            for (int r = e.RowIndex; r < e.RowIndex + e.RowCount; r++)
            {
                for (int c = 0; c < this.Columns.Count; c++)
                {
                    valueCache[MakeKey(r, c)] = null;
                }
            }
        }

        private void DataGridViewEx_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            // 行が消えると後続行のインデックスが詰まり、キャッシュも既存のUndo/Redo履歴が
            // 指すインデックスも意味を失う。安全のため両方作り直す(履歴はクリアのみ)
            RebuildValueCache();
            undoStack.Clear();
            redoStack.Clear();
        }

        private void RebuildValueCache()
        {
            valueCache.Clear();
            for (int r = 0; r < this.Rows.Count; r++)
            {
                if (this.Rows[r].IsNewRow)
                {
                    continue;
                }

                for (int c = 0; c < this.Columns.Count; c++)
                {
                    valueCache[MakeKey(r, c)] = this.Rows[r].Cells[c].Value;
                }
            }
        }

        private static long MakeKey(int rowIndex, int columnIndex)
        {
            return ((long)rowIndex << 32) | (uint)columnIndex;
        }
    }
}
