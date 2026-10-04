using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// コピペされていた処理を1か所に集約した共通ヘルパーのテスト。
    /// AtomicFile.TryDelete(旧XML削除) / GenericProfile.MakeKey(JSONの保存キー) /
    /// PathDropResolver.Resolve(ドロップされたパスの解決)。
    /// </summary>
    [TestClass]
    public class CommonHelperTests
    {
        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "CommonHelperTests_" + Guid.NewGuid().ToString("N"));
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

        // --- AtomicFile.TryDelete -------------------------------------------------

        [TestMethod]
        public void TryDelete_存在するファイルは削除する()
        {
            string path = Path.Combine(tempDirectory, "a.xml");
            File.WriteAllText(path, "x");

            AtomicFile.TryDelete(path);

            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void TryDelete_存在しないファイルやフォルダでも例外にならない()
        {
            AtomicFile.TryDelete(Path.Combine(tempDirectory, "nothing.xml"));
            AtomicFile.TryDelete(Path.Combine(tempDirectory, "no_dir", "nothing.xml"));
        }

        // --- GenericProfile.MakeKey -------------------------------------------------

        [TestMethod]
        public void MakeKey_AttrNameとAttrValueを縦棒でつなぐ()
        {
            // WinForms版/WPF版で互いのプロファイルを読めるよう、既存のJSONファイルと同じ形式であること
            Assert.AreEqual("Name|Value", GenericProfile.MakeKey("Name", "Value"));
        }

        // --- PathDropResolver.Resolve -------------------------------------------------

        [TestMethod]
        public void Resolve_FolderPathOnlyならファイルは親フォルダに変換する()
        {
            string file = Path.Combine(tempDirectory, "a.txt");
            File.WriteAllText(file, "x");

            Assert.AreEqual(tempDirectory, PathDropResolver.Resolve(file, true));
        }

        [TestMethod]
        public void Resolve_FolderPathOnlyでもフォルダはそのまま()
        {
            Assert.AreEqual(tempDirectory, PathDropResolver.Resolve(tempDirectory, true));
        }

        [TestMethod]
        public void Resolve_FolderPathOnlyでなければファイルもそのまま()
        {
            string file = Path.Combine(tempDirectory, "a.txt");
            File.WriteAllText(file, "x");

            Assert.AreEqual(file, PathDropResolver.Resolve(file, false));
        }

        [TestMethod]
        public void Resolve_存在しないパスはそのまま()
        {
            string missing = Path.Combine(tempDirectory, "missing.txt");

            Assert.AreEqual(missing, PathDropResolver.Resolve(missing, true));
        }
    }
}
