using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// StcFileInputOutput のキャラクタライゼーションテスト。
    /// ChangeStringCodeEUC2SJISは本番の17プロジェクトからは未使用で、既存のテストも
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

        // --- ChangeStringCodeEUC2SJIS -------------------------------------------------

        [TestMethod]
        public void ChangeStringCodeEUC2SJIS_EUCJPのファイルをShiftJISへ変換する()
        {
            string inPath = Path.Combine(tempDirectory, "in.txt");
            string outPath = Path.Combine(tempDirectory, "out.txt");
            string content = "こんにちは、世界！";

            File.WriteAllBytes(inPath, Encoding.GetEncoding("EUC-JP").GetBytes(content));

            bool result = fio.ChangeStringCodeEUC2SJIS(inPath, outPath);

            Assert.IsTrue(result);
            byte[] outBytes = File.ReadAllBytes(outPath);
            Assert.AreEqual(content, Encoding.GetEncoding("SHIFT-JIS").GetString(outBytes));
        }

        [TestMethod]
        public void ChangeStringCodeEUC2SJIS_入力ファイルが存在しなければfalseを返す()
        {
            string inPath = Path.Combine(tempDirectory, "nothing.txt");
            string outPath = Path.Combine(tempDirectory, "out.txt");

            bool result = fio.ChangeStringCodeEUC2SJIS(inPath, outPath);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ChangeStringCodeEUC2SJIS_英数字だけでも変換できる()
        {
            string inPath = Path.Combine(tempDirectory, "ascii_in.txt");
            string outPath = Path.Combine(tempDirectory, "ascii_out.txt");
            string content = "Hello, World! 12345";

            File.WriteAllBytes(inPath, Encoding.GetEncoding("EUC-JP").GetBytes(content));

            bool result = fio.ChangeStringCodeEUC2SJIS(inPath, outPath);

            Assert.IsTrue(result);
            byte[] outBytes = File.ReadAllBytes(outPath);
            Assert.AreEqual(content, Encoding.GetEncoding("SHIFT-JIS").GetString(outBytes));
        }
    }
}
