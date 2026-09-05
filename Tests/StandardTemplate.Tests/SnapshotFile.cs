using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// 「承認済みスナップショットと突き合わせる」仕組み。
    ///
    /// approved.txt が無ければ現在の値を書き出して Inconclusive（＝初回の焼き付け）。
    /// あれば1文字単位で比較し、違えば received.txt を残して失敗させる。
    /// </summary>
    internal static class SnapshotFile
    {
        public static void Verify(string actual, string baseName, string whatChanged,
                                  [CallerFilePath] string callerPath = "")
        {
            actual = Normalize(actual);

            string dir = GetWritableDirectory(callerPath);
            string approvedPath = Path.Combine(dir, baseName + ".approved.txt");
            string receivedPath = Path.Combine(dir, baseName + ".received.txt");

            if (!File.Exists(approvedPath))
            {
                WriteUtf8(approvedPath, actual);
                Assert.Inconclusive(
                    "承認済みスナップショットが無かったので、現在の値を書き出した。" + Environment.NewLine +
                    "中身を確認してリポジトリにコミットしてから、もう一度実行して。" + Environment.NewLine +
                    approvedPath);
                return;
            }

            string approved = Normalize(File.ReadAllText(approvedPath, Encoding.UTF8));
            if (actual == approved)
            {
                // 前回の失敗が残っていると紛らわしいので掃除する
                if (File.Exists(receivedPath)) File.Delete(receivedPath);
                return;
            }

            WriteUtf8(receivedPath, actual);
            Assert.Fail(
                whatChanged + Environment.NewLine +
                DescribeFirstDifference(approved, actual) + Environment.NewLine +
                "承認済み : " + approvedPath + Environment.NewLine +
                "今回の値 : " + receivedPath + Environment.NewLine +
                "意図した変更なら received を approved に上書きコピーして承認する。");
        }

        /// <summary>改行コードの違いで差分が出ないように正規化する。</summary>
        public static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
        }

        /// <summary>差分の全文は長いので、最初にズレた1行だけ抜き出して見せる。</summary>
        private static string DescribeFirstDifference(string approved, string actual)
        {
            string[] a = approved.Split('\n');
            string[] b = actual.Split('\n');

            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string left = i < a.Length ? a[i] : "(行なし)";
                string right = i < b.Length ? b[i] : "(行なし)";
                if (left != right)
                {
                    return string.Format(
                        "最初の差分 {0} 行目:{1}  承認済み : {2}{1}  今回の値 : {3}",
                        i + 1, Environment.NewLine, left, right);
                }
            }
            return "行単位の差分なし（末尾の空白か改行のみの違い）";
        }

        /// <summary>
        /// approved.txt はソースと一緒にコミットしたいのでソースフォルダに置く。
        /// ソースが無いマシンで走らせた場合はテスト出力フォルダにフォールバックする。
        /// </summary>
        private static string GetWritableDirectory(string callerPath)
        {
            string sourceDir = string.IsNullOrEmpty(callerPath) ? null : Path.GetDirectoryName(callerPath);
            if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir))
            {
                return sourceDir;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void WriteUtf8(string path, string content)
        {
            // BOM 付き UTF-8。この環境は BOM 無しだと日本語が化けて見えるため
            // （読む側の File.ReadAllText は BOM を自動で読み飛ばすので比較には影響しない）
            File.WriteAllText(path, content, new UTF8Encoding(true));
        }
    }
}
