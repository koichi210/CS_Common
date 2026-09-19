// ウィンドウのシステムメニュー(タイトルバー右クリック)に「データ保存先を変更」を足すための共通処理。
// Cheetos / EventRecorder / FFEdit / FileArranger に同じコードが別々に置かれていたため集約した。
//
// 使い方(Form側):
//   protected override void OnHandleCreated(EventArgs e)
//   {
//       base.OnHandleCreated(e);
//       DataFolderMenu.AppendToSystemMenu(this);
//   }
//
//   protected override void WndProc(ref Message m)
//   {
//       if (DataFolderMenu.IsChangeDataFolderCommand(m))
//       {
//           DataFolderMenu.ChangeDataFolder("アプリ名", userDataFolder, MoveExistingProfiles);
//           return;
//       }
//       base.WndProc(ref m);
//   }
//
// 「何を引っ越すか」はアプリごとに違う(全プロファイルを移す/設定ファイル1つだけ移す等)ため、
// 移動処理は呼び出し側から渡す。全プロファイルを移すだけならMoveProfilesをそのまま渡せばよい。
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StandardTemplate
{
    static class DataFolderMenu
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, Boolean bRevert);

        [DllImport("user32.dll")]
        private static extern Boolean AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, String lpNewItem);

        private const uint MF_SEPARATOR = 0x800;
        private const uint MF_STRING = 0x0;
        private const int WM_SYSCOMMAND = 0x112;

        // システムコマンドのIDは下位4bitをWindowsが予約しているため、16の倍数かつ
        // 0xF000未満にする必要がある(MSDN既定のルール)
        private const int SysMenuId_ChangeDataFolder = 0x1000;

        // OnHandleCreated(ウィンドウハンドル生成後)から呼ぶこと
        public static void AppendToSystemMenu(Form TargetForm)
        {
            IntPtr systemMenu = GetSystemMenu(TargetForm.Handle, false);
            AppendMenu(systemMenu, MF_SEPARATOR, 0, String.Empty);
            AppendMenu(systemMenu, MF_STRING, SysMenuId_ChangeDataFolder, "データ保存先を変更(&D)...");
        }

        // アプリ独自の項目を足したい場合に使う(IDは16の倍数かつ0xF000未満、0x1000は予約済み)
        public static void AppendMenuItem(Form TargetForm, int SysMenuId, String Text)
        {
            AppendMenu(GetSystemMenu(TargetForm.Handle, false), MF_STRING, (uint)SysMenuId, Text);
        }

        public static Boolean IsChangeDataFolderCommand(Message m)
        {
            return IsSysCommand(m, SysMenuId_ChangeDataFolder);
        }

        public static Boolean IsSysCommand(Message m, int SysMenuId)
        {
            return m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == SysMenuId;
        }

        // 保存先を選び直し、exe直下のポインタファイル(DataFolder.txt)を書き換える。
        // 実行中のCurrentFolderはその場では切り替えず、変更は次回起動時から反映する
        public static void ChangeDataFolder(String AppName, String CurrentFolder, Action<String, String> MigrateFiles)
        {
            // フォルダ選択ダイアログの実装は[[_Common/DataFolderChooser.cs]]に集約してある
            String selectedFolder = DataFolderChooser.ChooseFolder(
                "プロファイルの保存先フォルダを選んでください", CurrentFolder);

            if (String.IsNullOrEmpty(selectedFolder))
            {
                return;
            }

            if (String.Equals(
                Path.GetFullPath(selectedFolder).TrimEnd('\\'),
                Path.GetFullPath(CurrentFolder).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (MigrateFiles != null)
            {
                MigrateFiles(CurrentFolder, selectedFolder);
            }

            UserDataLocation.SetUserDataFolder(AppName, selectedFolder);

            MessageBox.Show(
                "保存先を変更したよ" + Environment.NewLine + selectedFolder + Environment.NewLine + Environment.NewLine
                    + "今のセッションはこれまで通り" + Environment.NewLine + CurrentFolder + Environment.NewLine
                    + "を使うよ。新しい保存先は次回起動時から反映されるよ",
                AppName + " - データ保存先の変更",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // 旧フォルダのプロファイル(*.xml / *.json)を新フォルダへ移す。
        // 移動するかは利用者に確認し、同名ファイルがある分はスキップする。
        // IsExcludeにtrueを返させると、そのファイルは移動対象から外れる(プロファイル以外の設定ファイル等)
        public static void MoveProfiles(String OldFolder, String NewFolder, String AppName, Func<String, Boolean> IsExclude = null)
        {
            DialogResult moveResult = MessageBox.Show(
                "既存のプロファイルを新しい保存先に移動しますか？" + Environment.NewLine + Environment.NewLine
                    + "移動元: " + OldFolder + Environment.NewLine
                    + "移動先: " + NewFolder,
                AppName + " - プロファイルの引っ越し",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (moveResult != DialogResult.Yes)
            {
                return;
            }

            List<String> movedFiles = new List<String>();
            List<String> skippedFiles = new List<String>();

            foreach (String sourcePath in ListProfiles(OldFolder, IsExclude))
            {
                String fileName = Path.GetFileName(sourcePath);
                String destPath = Path.Combine(NewFolder, fileName);

                if (File.Exists(destPath))
                {
                    skippedFiles.Add(fileName);
                    continue;
                }

                try
                {
                    File.Move(sourcePath, destPath);
                    movedFiles.Add(fileName);
                }
                catch (Exception)
                {
                    skippedFiles.Add(fileName);
                }
            }

            String message = movedFiles.Count + "件のプロファイルを移動したよ";
            if (skippedFiles.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine
                    + skippedFiles.Count + "件は移動先に同名ファイルが既にあった(または移動に失敗した)ためスキップしたよ:"
                    + Environment.NewLine + String.Join(Environment.NewLine, skippedFiles.ToArray());
            }

            MessageBox.Show(
                message,
                AppName + " - プロファイルの引っ越し",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static List<String> ListProfiles(String Folder, Func<String, Boolean> IsExclude)
        {
            List<String> files = new List<String>();
            files.AddRange(Directory.GetFiles(Folder, "*.xml"));
            foreach (String path in Directory.GetFiles(Folder, "*.json"))
            {
                if (IsExclude == null || !IsExclude(path))
                {
                    files.Add(path);
                }
            }
            return files;
        }
    }
}
