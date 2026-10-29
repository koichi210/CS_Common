using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// DataGridViewEx(Undo/Redo)・DataFolderMenu(プロファイルの引っ越し)・
    /// JsonSaveRestore.LoadWithMigration(JSON破損時)の回帰テスト。
    /// </summary>
    [TestClass]
    public class CommonControlTests
    {
        private sealed class TestProfile : StcSaveRestore
        {
        }

        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "CommonControlTests_" + Guid.NewGuid().ToString("N"));
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

        // 1列3行(a,b,c)のグリッド。値は編集として入れて、差分検出用のキャッシュに載せておく
        private static DataGridViewEx CreateGrid()
        {
            DataGridViewEx grid = new DataGridViewEx();
            grid.AllowUserToAddRows = false;
            grid.Columns.Add("c0", "c0");
            grid.Rows.Add(3);
            grid[0, 0].Value = "a";
            grid[0, 1].Value = "b";
            grid[0, 2].Value = "c";
            return grid;
        }

        [TestMethod]
        public void DataGridViewEx_行を途中に挿入したあとのUndoは正しい行に戻る()
        {
            using (DataGridViewEx grid = CreateGrid())
            {
                grid.Rows.Insert(0, 1);

                // 挿入前の行cは3行目に押し出されている。以前はその行のキャッシュがずれて、Undoで空欄になった
                grid[0, 3].Value = "X";
                grid.Undo();

                Assert.AreEqual("c", grid[0, 3].Value);
            }
        }

        [TestMethod]
        public void DataGridViewEx_並べ替えのあとのUndoは正しい行に戻る()
        {
            using (DataGridViewEx grid = CreateGrid())
            {
                grid.Sort(grid.Columns[0], ListSortDirection.Descending);
                Assert.AreEqual("c", grid[0, 0].Value);

                // 以前は並べ替え前の行(a)を覚えたままで、Undoで別の行の値が書き戻された
                grid[0, 0].Value = "z";
                grid.Undo();

                Assert.AreEqual("c", grid[0, 0].Value);
            }
        }

        [TestMethod]
        public void DataFolderMenu_ListProfilesはサブフォルダも対象にして拡張子を正確に見る()
        {
            Directory.CreateDirectory(Path.Combine(tempDirectory, "sub"));
            File.WriteAllText(Path.Combine(tempDirectory, "a.json"), "{}");
            File.WriteAllText(Path.Combine(tempDirectory, "b.xml"), "<r/>");
            File.WriteAllText(Path.Combine(tempDirectory, "sub", "c.json"), "{}");
            File.WriteAllText(Path.Combine(tempDirectory, "d.xmlx"), "x");
            File.WriteAllText(Path.Combine(tempDirectory, "e.txt"), "x");
            File.WriteAllText(Path.Combine(tempDirectory, "skip.xml"), "<r/>");

            var names = DataFolderMenu.ListProfiles(tempDirectory, p => Path.GetFileName(p) == "skip.xml")
                .Select(p => DataFolderMenu.GetRelativePath(tempDirectory, p))
                .OrderBy(p => p)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "a.json", "b.xml", Path.Combine("sub", "c.json") }, names);
        }

        [TestMethod]
        public void LoadWithMigration_JSONが壊れていても画面を既定値に戻す()
        {
            TextBox textBox = new TextBox();
            try
            {
                TestProfile profile = new TestProfile();
                profile.RegisterCtrl("Name", "Key", textBox, "既定値");
                string jsonPath = Path.Combine(tempDirectory, "p.json");
                string xmlPath = Path.Combine(tempDirectory, "p.xml");
                File.WriteAllText(jsonPath, "{ これはJSONではない");
                textBox.Text = "前の値";

                Boolean result = JsonSaveRestore.LoadWithMigration(profile, jsonPath, xmlPath, p => profile.LoadXmlFile(p));

                Assert.IsFalse(result);
                Assert.AreEqual("既定値", textBox.Text, "以前は前の値のまま残っていた");
            }
            finally
            {
                textBox.Dispose();
            }
        }
    }
}
