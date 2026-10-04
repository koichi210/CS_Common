// ComboBoxEx([[_Common/ComboBoxEx.cs]])とTextBoxEx([[_Common/TextBoxEx.cs]])、およびそのWPF版
// ([[_Common/Wpf/WpfPathDropControls.cs]])が共通で使う、ドラッグ&ドロップでパスが反映された時の
// イベント引数と、ドロップされたパスの解決処理。
// 複数のファイルから参照されるため、どれか一方だけを取り込むプロジェクトでもビルドできるよう
// 独立したファイルに分けてある。
using System;
using System.IO;

namespace StandardTemplate
{
    // PathDroppedイベントの引数。反映されるパスと、反映を取りやめるためのCancelを持つ
    public class PathDroppedEventArgs : EventArgs
    {
        public String Path { get; }
        public Boolean Cancel { get; set; }

        public PathDroppedEventArgs(String path)
        {
            Path = path;
        }
    }

    // FolderPathOnly指定に応じて、ドロップされたパスをそのまま使うか親フォルダのパスに
    // 変換するかを決める。WinForms版TextBoxEx/ComboBoxExとWPF版で同じ処理が3か所に
    // コピーされていたため集約した
    internal static class PathDropResolver
    {
        public static String Resolve(String droppedPath, Boolean folderPathOnly)
        {
            if (folderPathOnly && !Directory.Exists(droppedPath) && File.Exists(droppedPath))
            {
                return Path.GetDirectoryName(droppedPath) ?? droppedPath;
            }
            return droppedPath;
        }
    }
}
