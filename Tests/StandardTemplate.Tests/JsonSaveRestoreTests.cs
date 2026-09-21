using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// JsonSaveRestore.DeleteMigratedXml(「現在のファイルに保存しますか？」で「はい」を選び、
    /// xml→jsonへ切り替わって保存された後に、旧xmlを削除するかどうかの判定)のテスト。
    /// </summary>
    [TestClass]
    public class JsonSaveRestoreTests
    {
        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "JsonSaveRestoreTests_" + Guid.NewGuid().ToString("N"));
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
            }
        }

        [TestMethod]
        public void xmlからjsonへ切り替わったら同名の旧xmlを削除する()
        {
            string xmlPath = Path.Combine(tempDirectory, "profile.xml");
            string jsonPath = Path.Combine(tempDirectory, "profile.json");
            File.WriteAllText(xmlPath, "<root></root>");

            JsonSaveRestore.DeleteMigratedXml("profile.xml", jsonPath, tempDirectory);

            Assert.IsFalse(File.Exists(xmlPath), "移行後は旧xmlが削除されること");
        }

        [TestMethod]
        public void 拡張子が変わっていなければ削除しない()
        {
            string xmlPath = Path.Combine(tempDirectory, "profile.xml");
            File.WriteAllText(xmlPath, "<root></root>");

            // 保存先もxmlのまま(「はい」を選んだが元々jsonだった、等)の場合は何もしない
            JsonSaveRestore.DeleteMigratedXml("profile.xml", xmlPath, tempDirectory);

            Assert.IsTrue(File.Exists(xmlPath), "拡張子が変わっていなければ削除対象にならないこと");
        }

        [TestMethod]
        public void ファイル名が違えば削除しない()
        {
            string xmlPath = Path.Combine(tempDirectory, "profileA.xml");
            string otherJsonPath = Path.Combine(tempDirectory, "profileB.json");
            File.WriteAllText(xmlPath, "<root></root>");

            // 別名で保存した場合は、元のxmlを勝手に消さない
            JsonSaveRestore.DeleteMigratedXml("profileA.xml", otherJsonPath, tempDirectory);

            Assert.IsTrue(File.Exists(xmlPath), "別名で保存した場合は元のxmlを消さないこと");
        }

        [TestMethod]
        public void 旧xmlが存在しなくても例外にならない()
        {
            string jsonPath = Path.Combine(tempDirectory, "profile.json");

            JsonSaveRestore.DeleteMigratedXml("profile.xml", jsonPath, tempDirectory);
            // 例外が飛ばなければOK
        }
    }
}
