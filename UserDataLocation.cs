// 「ビルド出力フォルダ(bin/Debug、bin/Release)を誤って丸ごと削除すると、そこに置いていた
// ユーザーデータ(設定ファイル・マクロ等)まで巻き込まれて復元不能になる」という事故を防ぐための、
// 2段構成のユーザーデータ置き場管理クラス。
//
// ①ポインタファイル: exeと同じフォルダに置く小さなテキストファイル。中身は実データフォルダの
//    パスが1行書いてあるだけ。ビルド出力フォルダの掃除等で誤って消えても、実データには
//    一切影響しない(ポインタファイルは次回起動時に自動で作り直される)。
// ②実データフォルダ: 既定では%LOCALAPPDATA%\<アプリ名>\ (Windows標準のユーザーデータ置き場で、
//    リポジトリともビルド出力とも完全に別の場所にあるため、git操作やビルドのクリーンでは
//    絶対に触れない)。①のポインタファイルを書き換えれば、任意の場所に変更することもできる。
//
// 起動時の挙動: ①を読んで②の場所を知り、そこからユーザーデータを読み込む。
// ①が存在しない/中身が不正な場合は、既定の②(%LOCALAPPDATA%配下)を使い、①を新規作成する。
using System;
using System.IO;

namespace StandardTemplate
{
    public static class UserDataLocation
    {
        // exeと同じフォルダに置く、ポインタファイルの名前
        private const String PointerFileName = "DataFolder.txt";

        // appNameごとのユーザーデータフォルダを取得する(無ければ作成する)。
        // ポインタファイルが無い/中身が無効な場合は、既定の場所(%LOCALAPPDATA%\<appName>\)を使い、
        // ポインタファイルを自動的に作り直す(=ポインタファイルだけ消えても次回起動で自己修復する)
        public static String GetUserDataFolder(String appName)
        {
            String exeDir = Path.GetDirectoryName(GetHostAssembly().Location);
            String pointerPath = Path.Combine(exeDir, PointerFileName);

            String dataFolder = TryReadPointerFile(pointerPath);

            if (dataFolder == null)
            {
                dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    appName);

                Directory.CreateDirectory(dataFolder);

                // exeが書き込み禁止の場所(Program Files等)にあっても起動できるよう、案内板の作成失敗は無視する
                try
                {
                    WritePointerFile(pointerPath, dataFolder, appName);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            Directory.CreateDirectory(dataFolder);
            return dataFolder;
        }

        // ①のポインタファイルを書き換えて、実データフォルダの場所を任意のパスに変更する。
        // GUI(システムメニュー等)から「保存先フォルダを変更する」機能を作る時に使う想定。
        // 呼び出し側の責務: 変更は次回起動時から反映される(実行中のuserDataFolderは
        // readonlyで既に確定しているため、この呼び出しだけでは今のセッションには反映されない)
        public static void SetUserDataFolder(String appName, String newDataFolder)
        {
            String exeDir = Path.GetDirectoryName(GetHostAssembly().Location);
            String pointerPath = Path.Combine(exeDir, PointerFileName);

            Directory.CreateDirectory(newDataFolder);
            WritePointerFile(pointerPath, newDataFolder, appName);
        }

        // ポインタファイルを置く基準アセンブリ。通常はexe自身(GetEntryAssembly)だが、
        // MSTest(vstest.console)経由でForm1のコンストラクタが呼ばれるテストではホストの
        // 都合でGetEntryAssemblyがnullを返すことがあるため、その場合は今このコードを
        // 実行しているアセンブリ(テスト実行時はテストDLL自身)にフォールバックする
        private static System.Reflection.Assembly GetHostAssembly()
        {
            return System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
        }

        // ポインタファイルの1行目(実データフォルダのパス)を読み取る。
        // ファイルが無い/1行目が空/そのフォルダが実在しない場合はnullを返す
        private static String TryReadPointerFile(String pointerPath)
        {
            if (!File.Exists(pointerPath))
            {
                return null;
            }

            String[] lines;
            try
            {
                lines = File.ReadAllLines(pointerPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
            if (lines.Length == 0)
            {
                return null;
            }

            String candidate = lines[0].Trim();
            if (String.IsNullOrEmpty(candidate) || candidate.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Directory.Exists(candidate))
            {
                return null;
            }

            return candidate;
        }

        private static void WritePointerFile(String pointerPath, String dataFolder, String appName)
        {
            String content =
                dataFolder + Environment.NewLine
                + "# このファイルは" + appName + "のユーザーデータフォルダの場所を示す案内板です。" + Environment.NewLine
                + "# 削除しても実データ(1行目のフォルダ)は消えません。次回起動時にこのファイルだけ自動的に作り直されます。" + Environment.NewLine
                + "# データフォルダを別の場所に変更したい場合は、1行目のパスを書き換えてください。";

            File.WriteAllText(pointerPath, content);
        }
    }
}
