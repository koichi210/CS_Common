// プロファイル(設定ファイル)の一覧表示・保存ダイアログのWPF版。
// StcUtils.UpdateProfileList / JsonSaveRestore.SaveProfileWithDialog([[_Common/JsonSaveRestore.cs]])と同じ挙動。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace StandardTemplate.Wpf
{
    public static class WpfProfile
    {
        // DirectoryPath配下(サブフォルダ含む)のプロファイルを、DirectoryPathからの相対パスで一覧にする
        public static void UpdateProfileList(ComboBox ComboCtrl, String[] FileExtensions, String DefaultProfileName, String DirectoryPath)
        {
            List<String> names = ListProfiles(FileExtensions, DirectoryPath);
            if (names == null)
            {
                return;
            }

            ComboCtrl.Items.Clear();
            foreach (String name in names)
            {
                ComboCtrl.Items.Add(name);
            }

            // WinForms版と同じく、選択が変わったこと(SelectionChanged)でそのプロファイルが読み込まれる
            int index = names.IndexOf(DefaultProfileName);
            ComboCtrl.SelectedIndex = (index >= 0) ? index : (names.Count > 0 ? 0 : -1);
        }

        // internal: 画面を使わずにテストできるよう、一覧の作成だけを分けてある。フォルダが無い・読めない場合はnull
        internal static List<String> ListProfiles(String[] FileExtensions, String DirectoryPath)
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return null;
            }

            List<String> names = new List<String>();
            try
            {
                foreach (String extension in FileExtensions)
                {
                    foreach (String file in Directory.GetFiles(DirectoryPath, extension, SearchOption.AllDirectories))
                    {
                        String name = file.Substring(DirectoryPath.TrimEnd('\\').Length + 1);
                        if (name != String.Empty && !names.Contains(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
            return names;
        }

        // 「保存先を選ぶ→保存する→プロファイル一覧を更新する→結果を知らせる」。
        // 戻り値は保存したファイルのパス。キャンセル時と失敗時は空文字を返す。
        // プロファイル未選択(Textが空)のときはDefaultFileNameを現在のファイル名として扱う
        public static String SaveProfileWithDialog(StcFileInputOutput FileIO, ComboBox ProfileCtrl, String[] ProfileExtensions,
                                                   Func<String, Boolean> Save, String InitialDirectory,
                                                   String DefaultFileName = "",
                                                   String SuccessMessage = "設定値を保存しました♪")
        {
            String OriginalFileName = String.IsNullOrEmpty(ProfileCtrl.Text) ? DefaultFileName : ProfileCtrl.Text;

            String SaveFileName = FileIO.SelectSaveFileName(OriginalFileName, InitialDirectory);
            if (String.IsNullOrEmpty(SaveFileName))
            {
                return "";
            }

            if (!Save(SaveFileName))
            {
                MessageBox.Show("設定の保存に失敗しました" + Environment.NewLine + SaveFileName,
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return "";
            }

            JsonSaveRestore.DeleteMigratedXml(OriginalFileName, SaveFileName, InitialDirectory);

            UpdateProfileList(ProfileCtrl, ProfileExtensions, Path.GetFileName(SaveFileName), InitialDirectory);
            MessageBox.Show(SuccessMessage + Environment.NewLine + SaveFileName);
            return SaveFileName;
        }
    }
}
