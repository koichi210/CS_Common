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
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StandardTemplate
{
    public class DataGridViewEx : DataGridView
    {
        // Ctrl+Z/Ctrl+Yによる元に戻す/やり直すを有効にするかどうか。既定は有効
        public Boolean EnableUndoRedo { get; set; } = true;

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
