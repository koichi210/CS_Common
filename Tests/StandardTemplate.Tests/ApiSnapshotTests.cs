using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// 共通クラスの public API が、承認済みスナップショットから 1 文字も動いていないことを検証する。
    ///
    /// リファクタ中の安全網その 1。このテストが緑なら、
    /// StandardTemplateClass.cs を参照している 17 プロジェクトの呼び出し側は影響を受けない。
    /// </summary>
    [TestClass]
    public class ApiSnapshotTests
    {
        private const string ApprovedFileName = "ApiSnapshot.approved.txt";
        private const string ReceivedFileName = "ApiSnapshot.received.txt";

        [TestMethod]
        public void PublicApiが承認済みスナップショットと一致すること()
        {
            string actual = ApiSurface.Dump();
            string approvedPath = Path.Combine(GetWritableDirectory(), ApprovedFileName);
            string receivedPath = Path.Combine(GetWritableDirectory(), ReceivedFileName);

            if (!File.Exists(approvedPath))
            {
                WriteUtf8(approvedPath, actual);
                Assert.Inconclusive(
                    "承認済みスナップショットが無かったので、現在の API を書き出した。" + Environment.NewLine +
                    "中身を確認してリポジトリにコミットしてから、もう一度実行して。" + Environment.NewLine +
                    approvedPath);
                return;
            }

            string approved = ApiSurface.Normalize(File.ReadAllText(approvedPath, Encoding.UTF8));
            if (actual == approved)
            {
                // 前回の失敗が残っていると紛らわしいので掃除する
                if (File.Exists(receivedPath)) File.Delete(receivedPath);
                return;
            }

            WriteUtf8(receivedPath, actual);
            Assert.Fail(
                "public API が変わっている。呼び出し側 17 プロジェクトに影響が出る可能性あり。" + Environment.NewLine +
                DescribeFirstDifference(approved, actual) + Environment.NewLine +
                "承認済み : " + approvedPath + Environment.NewLine +
                "今回の値 : " + receivedPath + Environment.NewLine +
                "意図した変更なら received を approved に上書きコピーして承認する。");
        }

        /// <summary>差分の全文は長いので、最初にズレた 1 行だけ抜き出して見せる。</summary>
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
        /// ソースが無いマシン（配布先など）で走らせた場合はテスト出力フォルダにフォールバックする。
        /// </summary>
        private static string GetWritableDirectory()
        {
            string sourceDir = Path.GetDirectoryName(GetThisFilePath());
            if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir))
            {
                return sourceDir;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetThisFilePath([CallerFilePath] string path = "")
        {
            return path;
        }

        private static void WriteUtf8(string path, string content)
        {
            // BOM 付き UTF-8。この環境は BOM 無しだと日本語が化けて見えるため
            // （読む側の File.ReadAllText は BOM を自動で読み飛ばすので比較には影響しない）
            File.WriteAllText(path, content, new UTF8Encoding(true));
        }
    }
}
