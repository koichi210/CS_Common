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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern Boolean AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, String lpNewItem);

        private const uint MF_SEPARATOR = 0x800;
        private const uint MF_STRING = 0x0;
        private const int WM_SYSCOMMAND = 0x112;

        // システムコマンドのIDは下位4bitをWindowsが予約しているため、16の倍数かつ
        // 0xF000未満にする必要がある(MSDN既定のルール)
        private const int _sysMenuIdChangeDataFolder = 0x1000;

        // OnHandleCreated(ウィンドウハンドル生成後)から呼ぶこと
        public static void AppendToSystemMenu(Form targetForm)
        {
            IntPtr systemMenu = GetSystemMenu(targetForm.Handle, false);
            AppendMenu(systemMenu, MF_SEPARATOR, 0, String.Empty);
            AppendMenu(systemMenu, MF_STRING, _sysMenuIdChangeDataFolder, "データ保存先を変更(&D)...");
        }

        // アプリ独自の項目を足したい場合に使う(IDは16の倍数かつ0xF000未満、0x1000は予約済み)
        public static void AppendMenuItem(Form targetForm, int sysMenuId, String text)
        {
            AppendMenu(GetSystemMenu(targetForm.Handle, false), MF_STRING, (uint)sysMenuId, text);
        }

        public static Boolean IsChangeDataFolderCommand(Message m)
        {
            return IsSysCommand(m, _sysMenuIdChangeDataFolder);
        }

        public static Boolean IsSysCommand(Message m, int sysMenuId)
        {
            return m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == sysMenuId;
        }

        // 保存先を選び直し、exe直下のポインタファイル(DataFolder.txt)を書き換える。
        // onFolderChangedを渡すと、完了ダイアログをOKで閉じた後に新しい保存先を引数に呼ばれるので、
        // そこで_userDataFolderを差し替えてプロファイルを読み直す(再起動不要)。
        // 渡さない場合は従来通り、変更は次回起動時から反映する
        public static void ChangeDataFolder(String appName, String currentFolder, Action<String, String> migrateFiles,
            Action<String> onFolderChanged = null)
        {
            // フォルダ選択ダイアログの実装は[[_Common/DataFolderChooser.cs]]に集約してある
            String selectedFolder = DataFolderChooser.ChooseFolder(
                "プロファイルの保存先フォルダを選んでください", currentFolder);

            if (String.IsNullOrEmpty(selectedFolder))
            {
                return;
            }

            if (String.Equals(
                Path.GetFullPath(selectedFolder).TrimEnd('\\'),
                Path.GetFullPath(currentFolder).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (migrateFiles != null)
            {
                migrateFiles(currentFolder, selectedFolder);
            }

            try
            {
                UserDataLocation.SetUserDataFolder(appName, selectedFolder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // exeが書き込み禁止の場所にあるなどで案内板を書き換えられなかった時は、
                // 移したファイルを元の保存先へ戻す(次回起動でプロファイルが見えなくなるのを防ぐ)
                if (migrateFiles != null)
                {
                    migrateFiles(selectedFolder, currentFolder);
                }

                MessageBox.Show(
                    "保存先を変更できなかったよ" + Environment.NewLine + ex.Message,
                    appName + " - データ保存先の変更",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            String notice = (onFolderChanged != null)
                ? "OKを押すと、新しい保存先のプロファイルを読み込むよ"
                : "今のセッションはこれまで通り" + Environment.NewLine + currentFolder + Environment.NewLine
                    + "を使うよ。新しい保存先は次回起動時から反映されるよ";

            MessageBox.Show(
                "保存先を変更したよ" + Environment.NewLine + selectedFolder + Environment.NewLine + Environment.NewLine
                    + notice,
                appName + " - データ保存先の変更",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (onFolderChanged != null)
            {
                onFolderChanged(selectedFolder);
            }
        }

        // 旧フォルダのプロファイル(*.xml / *.json)を新フォルダへ移す。
        // 移動するかは利用者に確認し、同名ファイルがある分はスキップする。
        // IsExcludeにtrueを返させると、そのファイルは移動対象から外れる(プロファイル以外の設定ファイル等)
        public static void MoveProfiles(String oldFolder, String newFolder, String appName, Func<String, Boolean> isExclude = null)
        {
            DialogResult moveResult = MessageBox.Show(
                "既存のプロファイルを新しい保存先に移動しますか？" + Environment.NewLine + Environment.NewLine
                    + "移動元: " + oldFolder + Environment.NewLine
                    + "移動先: " + newFolder,
                appName + " - プロファイルの引っ越し",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (moveResult != DialogResult.Yes)
            {
                return;
            }

            List<String> movedFiles = new List<String>();
            List<String> skippedFiles = new List<String>();

            foreach (String sourcePath in ListProfiles(oldFolder, isExclude))
            {
                // サブフォルダに保存したプロファイルは、サブフォルダの構造を保ったまま移す
                String fileName = GetRelativePath(oldFolder, sourcePath);
                String destPath = Path.Combine(newFolder, fileName);

                if (File.Exists(destPath))
                {
                    skippedFiles.Add(fileName);
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
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
                appName + " - プロファイルの引っ越し",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static List<String> ListProfiles(String folder, Func<String, Boolean> isExclude)
        {
            // プロファイル一覧(StcSaveRestore側・WpfProfile)と同じく、サブフォルダの中も対象にする。
            // 検索パターン"*.xml"は拡張子が3文字のため".xmlx"などにも一致する旧仕様があるので、拡張子は自分で見る
            List<String> files = new List<String>();
            foreach (String path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                String extension = Path.GetExtension(path);
                Boolean isProfile = extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
                if (isProfile && (isExclude == null || !isExclude(path)))
                {
                    files.Add(path);
                }
            }
            return files;
        }

        // folderからpathまでの相対パスを返す(pathはfolderの配下にある前提)
        private static String GetRelativePath(String folder, String path)
        {
            String prefix = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
            String fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(prefix.Length)
                : Path.GetFileName(path);
        }
    }
}
