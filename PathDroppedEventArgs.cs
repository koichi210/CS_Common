// ComboBoxEx([[_Common/ComboBoxEx.cs]])とTextBoxEx([[_Common/TextBoxEx.cs]])が共通で使う、
// ドラッグ&ドロップでパスが反映された時のイベント引数。
// 両方から参照されるため、どちらか一方だけを取り込むプロジェクトでもビルドできるよう
// 独立したファイルに分けてある。
using System;

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
}
