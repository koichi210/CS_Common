// プロファイル(設定ファイル)の一覧表示・保存ダイアログのWPF版。
// StcUtils.UpdateProfileList / JsonSaveRestore.SaveProfileWithDialog([[_Common/JsonSaveRestore.cs]])と同じ挙動。
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace StandardTemplate.Wpf
{
    public static class WpfProfile
    {
        // DirectoryPath配下(サブフォルダ含む)のプロファイルを、DirectoryPathからの相対パスで一覧にする
        public static void UpdateProfileList(ComboBox comboCtrl, String[] fileExtensions, String defaultProfileName, String directoryPath)
        {
            List<String> names = ListProfiles(fileExtensions, directoryPath);
            if (names == null)
            {
                return;
            }

            comboCtrl.Items.Clear();
            foreach (String name in names)
            {
                comboCtrl.Items.Add(name);
            }

            // WinForms版と同じく、選択が変わったこと(SelectionChanged)でそのプロファイルが読み込まれる
            int index = names.IndexOf(defaultProfileName);
            comboCtrl.SelectedIndex = (index >= 0) ? index : (names.Count > 0 ? 0 : -1);
        }

        // internal: 画面を使わずにテストできるよう、一覧の作成だけを分けてある。フォルダが無い・読めない場合はnull
        internal static List<String> ListProfiles(String[] fileExtensions, String directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }

            List<String> names = new List<String>();
            try
            {
                foreach (String extension in fileExtensions)
                {
                    foreach (String file in Directory.GetFiles(directoryPath, extension, SearchOption.AllDirectories))
                    {
                        String name = file.Substring(directoryPath.TrimEnd('\\').Length + 1);
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
        public static String SaveProfileWithDialog(StcFileInputOutput fileIO, ComboBox profileCtrl, String[] profileExtensions,
                                                   Func<String, Boolean> save, String initialDirectory,
                                                   String defaultFileName = "",
                                                   String successMessage = "設定値を保存しました♪")
        {
            String originalFileName = String.IsNullOrEmpty(profileCtrl.Text) ? defaultFileName : profileCtrl.Text;

            String saveFileName = fileIO.SelectSaveFileName(originalFileName, initialDirectory);
            if (String.IsNullOrEmpty(saveFileName))
            {
                return "";
            }

            if (!save(saveFileName))
            {
                MessageBox.Show("設定の保存に失敗しました" + Environment.NewLine + saveFileName,
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return "";
            }

            JsonSaveRestore.DeleteMigratedXml(originalFileName, saveFileName, initialDirectory);

            UpdateProfileList(profileCtrl, profileExtensions, Path.GetFileName(saveFileName), initialDirectory);
            MessageBox.Show(successMessage + Environment.NewLine + saveFileName);
            return saveFileName;
        }
    }
}
