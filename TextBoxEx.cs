// 標準のTextBoxに、ファイル/フォルダのドラッグ&ドロップ受け入れを追加しただけの拡張コントロール。
// EventRecorder以外のプロジェクトでも使い回せるよう_Commonに置いてある。
// ComboBoxEx([[_Common/ComboBoxEx.cs]])のTextBox版で、挙動・使い方は基本的に同じ。
//
// ■用途
// 「コピー/移動先フォルダ」欄のように、パスをテキストとして持つTextBoxへ、
// エクスプローラ等からファイル/フォルダをドラッグ&ドロップしてそのパスを設定できるようにする。
//
// ■挙動
// AllowDropは自動でtrueにしてあるので、追加設定なしでドロップを受け付ける。
// ドロップされた項目が複数ある場合は先頭の1件だけを使う。
// FolderPathOnly(既定true)がtrueの場合、ドロップされたのがファイルであれば
// そのファイルが入っているフォルダのパスに変換してからTextへ反映する。
// falseにすれば、ファイルならファイルパスそのまま、フォルダならフォルダパスそのままTextへ反映する。
//
// ■拡張ポイント
// PathDroppedイベントで、反映後のパスを受け取れる
// (反映そのものを止めたい場合はイベント内でCancelをtrueにする)。
//
// ■↑/↓キーでの数値インクリメント/デクリメント
// EnableUpDownIncrement(既定false)をtrueにすると、↑キーで1増加、↓キーで1減少する
// (EventRecorderのtextBox_Loopと同じ挙動)。値は1未満にはならない
// (ループ数0以下は意味を持たないユースケースを想定した下限)。既定はOFFなので、
// 既存のTextBoxEx利用箇所(パス入力欄等)には影響しない。
using System;
using System.IO;
using System.Windows.Forms;

namespace StandardTemplate
{
    public class TextBoxEx : TextBox
    {
        // trueの場合、ドロップされたのがファイルであれば、そのファイルが属するフォルダの
        // パスに変換してからTextへ反映する。既定はtrue(フォルダパス欄としての利用を想定)
        public Boolean FolderPathOnly { get; set; } = true;

        // trueの場合、↑/↓キーでテキストの数値を1ずつ増減できるようにする。既定はfalse
        public Boolean EnableUpDownIncrement { get; set; } = false;

        // ドロップによってTextへパスが反映された直後に発生する
        public event EventHandler<PathDroppedEventArgs> PathDropped;

        public TextBoxEx()
        {
            this.AllowDrop = true;
            this.DragEnter += TextBoxEx_DragEnter;
            this.DragDrop += TextBoxEx_DragDrop;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (EnableUpDownIncrement && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                int delta = (e.KeyCode == Keys.Up) ? 1 : -1;
                StepValue(delta);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        // 現在のTextを整数として1未満にならない範囲でdeltaだけ増減する
        private void StepValue(int delta)
        {
            int current;
            int.TryParse(this.Text, out current);
            int next = Math.Max(1, (current <= 0 ? 1 : current) + delta);

            this.Text = next.ToString();
            this.SelectionStart = this.Text.Length;
        }

        private void TextBoxEx_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void TextBoxEx_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            String[] paths = (String[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            String path = ResolvePath(paths[0]);

            PathDroppedEventArgs args = new PathDroppedEventArgs(path);
            PathDropped?.Invoke(this, args);
            if (args.Cancel)
            {
                return;
            }

            this.Text = path;
        }

        // FolderPathOnly指定に応じて、ドロップされたパスをそのまま使うか
        // 親フォルダのパスに変換するかを決める
        private String ResolvePath(String droppedPath)
        {
            if (!FolderPathOnly)
            {
                return droppedPath;
            }

            if (Directory.Exists(droppedPath))
            {
                return droppedPath;
            }

            if (File.Exists(droppedPath))
            {
                return Path.GetDirectoryName(droppedPath) ?? droppedPath;
            }

            return droppedPath;
        }
    }
}
