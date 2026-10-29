using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// StcFileInputOutput のキャラクタライゼーションテスト。
    /// ChangeStringCodeEuc2Sjisは本番の17プロジェクトからは未使用で、既存のテストも
    /// 無かった。簡略化する前に、現状の挙動を固定する特性化テストとして追加した。
    /// </summary>
    [TestClass]
    public class StcFileInputOutputTests
    {
        private StcFileInputOutput fio;
        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            fio = new StcFileInputOutput();
            tempDirectory = Path.Combine(Path.GetTempPath(), "StcFileInputOutputTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
        }

        [TestCleanup]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
            catch (IOException)
            {
                // 後片付けの失敗はテストの成否に関係ないので黙って流す
            }
        }

        // --- ChangeStringCodeEuc2Sjis -------------------------------------------------

        [TestMethod]
        public void ChangeStringCodeEuc2Sjis_EUCJPのファイルをShiftJISへ変換する()
        {
            string inPath = Path.Combine(tempDirectory, "in.txt");
            string outPath = Path.Combine(tempDirectory, "out.txt");
            string content = "こんにちは、世界！";

            File.WriteAllBytes(inPath, Encoding.GetEncoding("EUC-JP").GetBytes(content));

            bool result = fio.ChangeStringCodeEuc2Sjis(inPath, outPath);

            Assert.IsTrue(result);
            byte[] outBytes = File.ReadAllBytes(outPath);
            Assert.AreEqual(content, Encoding.GetEncoding("SHIFT-JIS").GetString(outBytes));
        }

        [TestMethod]
        public void ChangeStringCodeEuc2Sjis_入力ファイルが存在しなければfalseを返す()
        {
            string inPath = Path.Combine(tempDirectory, "nothing.txt");
            string outPath = Path.Combine(tempDirectory, "out.txt");

            bool result = fio.ChangeStringCodeEuc2Sjis(inPath, outPath);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ChangeStringCodeEuc2Sjis_英数字だけでも変換できる()
        {
            string inPath = Path.Combine(tempDirectory, "ascii_in.txt");
            string outPath = Path.Combine(tempDirectory, "ascii_out.txt");
            string content = "Hello, World! 12345";

            File.WriteAllBytes(inPath, Encoding.GetEncoding("EUC-JP").GetBytes(content));

            bool result = fio.ChangeStringCodeEuc2Sjis(inPath, outPath);

            Assert.IsTrue(result);
            byte[] outBytes = File.ReadAllBytes(outPath);
            Assert.AreEqual(content, Encoding.GetEncoding("SHIFT-JIS").GetString(outBytes));
        }

        [TestMethod]
        public void ChangeStringCodeUtf2Sjis_入力ファイルが存在しなければfalseで出力先を作らない()
        {
            string outPath = Path.Combine(tempDirectory, "out.txt");

            Assert.IsFalse(fio.ChangeStringCodeUtf2Sjis(Path.Combine(tempDirectory, "none.txt"), outPath));
            Assert.IsFalse(File.Exists(outPath), "以前は空ファイルで出力先を上書き・作成していた");
        }

        [TestMethod]
        public void DeleteAnyFile_ワイルドカードの拡張子が3文字でも長い拡張子のファイルは消さない()
        {
            string xls = Path.Combine(tempDirectory, "a.xls");
            string xlsx = Path.Combine(tempDirectory, "b.xlsx");
            File.WriteAllText(xls, "x");
            File.WriteAllText(xlsx, "x");

            Assert.IsTrue(fio.DeleteAnyFile(Path.Combine(tempDirectory, "*.xls")));

            Assert.IsFalse(File.Exists(xls));
            Assert.IsTrue(File.Exists(xlsx), "以前は.xlsxも一緒に消えていた(3文字拡張子の旧仕様)");
        }

        [TestMethod]
        public void CreateTempFile_指定した拡張子のファイルができる()
        {
            string path = fio.CreateTempFile("bat");
            try
            {
                Assert.AreEqual(".bat", Path.GetExtension(path));
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
