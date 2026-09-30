// TextBoxEx/ComboBoxEx([[_Common/TextBoxEx.cs]] / [[_Common/ComboBoxEx.cs]])のWPF版。
// ファイル/フォルダのドラッグ&ドロップでパスを入力できる。挙動はWinForms版と同じ
// (先頭の1件だけ使う・FolderPathOnlyならファイルは親フォルダに変換・PathDroppedでキャンセル可)。
//
// WPFのTextBoxは自前でDragOverを処理してファイルのドロップを拒否してしまうため、
// バブルより先に届くPreviewDragOver/PreviewDropで横取りしている。
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StandardTemplate.Wpf
{
    internal static class PathDrop
    {
        public static void Attach(Control Ctrl, Func<Boolean> FolderPathOnly, Action<String> Apply,
                                  Func<String, PathDroppedEventArgs> RaiseDropped)
        {
            Ctrl.AllowDrop = true;
            Ctrl.PreviewDragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            Ctrl.PreviewDragEnter += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            Ctrl.PreviewDrop += (s, e) =>
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    return;
                }
                e.Handled = true;

                String[] paths = e.Data.GetData(DataFormats.FileDrop) as String[];
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                String path = ResolvePath(paths[0], FolderPathOnly());
                PathDroppedEventArgs args = RaiseDropped(path);
                if (args.Cancel)
                {
                    return;
                }
                Apply(path);
            };
        }

        public static String ResolvePath(String DroppedPath, Boolean FolderPathOnly)
        {
            if (FolderPathOnly && !Directory.Exists(DroppedPath) && File.Exists(DroppedPath))
            {
                return Path.GetDirectoryName(DroppedPath) ?? DroppedPath;
            }
            return DroppedPath;
        }
    }

    public class TextBoxEx : TextBox
    {
        public Boolean FolderPathOnly { get; set; } = true;

        // trueの場合、↑/↓キーでテキストの数値を1ずつ増減できる(1未満にはならない)
        public Boolean EnableUpDownIncrement { get; set; } = false;

        public event EventHandler<PathDroppedEventArgs> PathDropped;

        public TextBoxEx()
        {
            PathDrop.Attach(this, () => FolderPathOnly, path => Text = path, path =>
            {
                PathDroppedEventArgs args = new PathDroppedEventArgs(path);
                PathDropped?.Invoke(this, args);
                return args;
            });
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (EnableUpDownIncrement && (e.Key == Key.Up || e.Key == Key.Down))
            {
                int current;
                int.TryParse(Text, out current);
                int delta = (e.Key == Key.Up) ? 1 : -1;
                Text = Math.Max(1, (current <= 0 ? 1 : current) + delta).ToString();
                CaretIndex = Text.Length;
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }
    }

    // WinForms版のComboBox(DropDownStyle=DropDown)と同じく、既定で入力可能(IsEditable=true)にしてある
    public class ComboBoxEx : ComboBox
    {
        public Boolean FolderPathOnly { get; set; } = true;

        public event EventHandler<PathDroppedEventArgs> PathDropped;

        public ComboBoxEx()
        {
            IsEditable = true;
            PathDrop.Attach(this, () => FolderPathOnly, path => Text = path, path =>
            {
                PathDroppedEventArgs args = new PathDroppedEventArgs(path);
                PathDropped?.Invoke(this, args);
                return args;
            });
        }
    }
}
