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
            String exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            String pointerPath = Path.Combine(exeDir, PointerFileName);

            String dataFolder = TryReadPointerFile(pointerPath);

            if (dataFolder == null)
            {
                dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    appName);

                Directory.CreateDirectory(dataFolder);
                WritePointerFile(pointerPath, dataFolder, appName);
            }

            Directory.CreateDirectory(dataFolder);
            return dataFolder;
        }

        // ポインタファイルの1行目(実データフォルダのパス)を読み取る。
        // ファイルが無い/1行目が空/そのフォルダが実在しない場合はnullを返す
        private static String TryReadPointerFile(String pointerPath)
        {
            if (!File.Exists(pointerPath))
            {
                return null;
            }

            String[] lines = File.ReadAllLines(pointerPath);
            if (lines.Length == 0)
            {
                return null;
            }

            String candidate = lines[0].Trim();
            if (String.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
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
