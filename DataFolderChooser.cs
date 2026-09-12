// 「データ保存先を変更」機能([[_Common/UserDataLocation.cs]])で使う、フォルダ選択ダイアログの
// 共通実装。標準のFolderBrowserDialog(SHBrowseForFolder)は見た目が古いツリー表示のダイアログに
// なってしまうため、WindowsAPICodePack-Shell(NuGet)のCommonOpenFileDialogを使う。
// SaveFileDialog等と同じ新しいコモンダイアログ(パンくず・検索窓付き)の見た目で、
// かつIsFolderPicker=trueで素直にフォルダだけを選ばせられる。
//
// 元々EventRecorderのForm1.csに直接書かれていたprivateメソッドだったが、Cheetos/FFEdit/
// FileArrangerにも同じ「データ保存先を変更」機能を追加する際、各プロジェクトが別々に
// (しかも一部は見た目の異なる標準FolderBrowserDialogで)実装してしまうと、同じ機能なのに
// 見た目や挙動がプロジェクトごとにバラつく。ここに1つだけ実装を置き、「データ保存先を変更」
// 機能を持つプロジェクト全部(EventRecorder/Cheetos/FFEdit/FileArranger)で共有することで
// 見た目・挙動を統一する。
//
// JsonFileStorage.cs([[_Common/JsonFileStorage.cs]])と同じ理由で、この機能を使わない他の
// プロジェクトにNuGet依存(WindowsAPICodePack-Shell)を強要しないよう、StandardTemplateClass.cs
// 本体ではなく独立したこのファイルに分離してある。使うプロジェクトはcsprojに
// このファイルのCompile Includeと、<PackageReference Include="WindowsAPICodePack-Shell" .../>
// の追加が必要
using System;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace StandardTemplate
{
    public static class DataFolderChooser
    {
        // CommonOpenFileDialog(WindowsAPICodePack-Shell)でフォルダを選ばせ、選ばれたフォルダの
        // フルパスを返す(キャンセル時はnull)
        public static String ChooseFolder(String title, String initialDirectory)
        {
            using (CommonOpenFileDialog dlg = new CommonOpenFileDialog())
            {
                dlg.Title = title;
                dlg.InitialDirectory = initialDirectory;
                dlg.IsFolderPicker = true;
                dlg.RestoreDirectory = true;

                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return null;
                }

                return String.IsNullOrEmpty(dlg.FileName) ? null : dlg.FileName;
            }
        }
    }
}
