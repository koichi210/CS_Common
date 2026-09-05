using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// StcUtils のキャラクタライゼーションテスト（安全網その 2）。
    ///
    /// ここに書く期待値は「あるべき仕様」ではなく「リファクタ前の実装が実際に返す値」。
    /// 目的は正しさの証明ではなく、リファクタで挙動が動いたことを検知すること。
    /// だから今の実装が微妙な結果を返していても、そのまま固定してよい。
    /// </summary>
    [TestClass]
    public class StcUtilsTests
    {
        private StcUtils util;

        [TestInitialize]
        public void SetUp()
        {
            util = new StcUtils();
        }

        // --- GetFilePathType -------------------------------------------------
        // 判定は上から順の if で、":" を含むかどうかが最優先という実装。

        [TestMethod]
        public void GetFilePathType_コロンを含めばWindowsフルパス扱い()
        {
            Assert.AreEqual(StcUtils.FILE_PATH_TYPE.WINDOWS_FULLPATH, util.GetFilePathType(@"C:\work\a.txt"));
        }

        [TestMethod]
        public void GetFilePathType_スラッシュ2連はPerforceパス扱い()
        {
            Assert.AreEqual(StcUtils.FILE_PATH_TYPE.PERFORCE_PATH, util.GetFilePathType("//depot/main/a.txt"));
        }

        [TestMethod]
        public void GetFilePathType_スラッシュ区切りはLinuxパス扱い()
        {
            Assert.AreEqual(StcUtils.FILE_PATH_TYPE.LINUX_PATH, util.GetFilePathType("home/ogu/a.txt"));
        }

        [TestMethod]
        public void GetFilePathType_円マーク区切りはWindowsパス扱い()
        {
            Assert.AreEqual(StcUtils.FILE_PATH_TYPE.WINDOWS_PATH, util.GetFilePathType(@"work\a.txt"));
        }

        [TestMethod]
        public void GetFilePathType_区切り文字が無ければOTHER()
        {
            Assert.AreEqual(StcUtils.FILE_PATH_TYPE.OTHER, util.GetFilePathType("a.txt"));
        }

        // --- 性質ベースのテスト ---------------------------------------------
        // 入出力を1件ずつ並べたものは BehaviorSnapshot に任せてある。
        // ここには「毎回同じ値にならないので焼き付けられないもの」と、
        // 「入力が変わっても常に成り立ってほしい関係」を書く。

        [TestMethod]
        public void AssortList_順番は変わっても要素の集合は変わらない()
        {
            // 実装が乱数（Guid）で並べ替えるため、結果を固定値と比較できない。
            // 「何が返るか」ではなく「何が保たれるか」を確かめる。
            string[] source = { "a", "b", "c", "d", "e" };

            string[] actual = util.AssortList(source);

            Assert.AreEqual(source.Length, actual.Length, "件数が変わってはいけない");
            CollectionAssert.AreEquivalent(source, actual, "順序を無視すれば中身は同じはず");
        }

        [TestMethod]
        public void AssortList_空配列を渡しても落ちない()
        {
            Assert.AreEqual(0, util.AssortList(new string[0]).Length);
        }

        [TestMethod]
        public void TrimDuplication_二度かけても結果が変わらない()
        {
            // 重複除去は一度やれば十分で、繰り返しても安定していてほしい
            string[] once = util.TrimDuplication(new[] { "a", "b", "a", "c", "b" });
            string[] twice = util.TrimDuplication(once);

            CollectionAssert.AreEqual(once, twice);
        }

        [TestMethod]
        public void パス区切り変換_LinuxにしてからWindowsに戻すと元に戻る()
        {
            // ただし元の文字列に "/" が混ざっている場合は戻らない（区別が付かなくなるため）。
            // ここでは "\" だけで構成されたパスに限って成り立つことを確認する。
            string windowsPath = @"C:\work\sub\a.txt";

            string roundTrip = util.ChangeLinuxPath2WindowsPath(util.ChangeWindowsPath2LinuxPath(windowsPath));

            Assert.AreEqual(windowsPath, roundTrip);
        }

        [TestMethod]
        public void 配列とリストを相互変換しても中身が変わらない()
        {
            string[] source = { "a", "b", "c" };

            CollectionAssert.AreEqual(source, util.List2Array(util.Array2List(source)));
        }

        [TestMethod]
        public void 文字列と配列を相互変換しても中身が変わらない()
        {
            // 区切り文字を含まない要素だけなら、連結して分割し直せば元に戻る
            string[] source = { "a", "b", "c" };

            string linear = util.ChangeStrArray2Linear(source, ",");
            string[] roundTrip = util.ChangeStrLinear2Array(linear, ",");

            CollectionAssert.AreEqual(source, roundTrip);
        }
    }
}
