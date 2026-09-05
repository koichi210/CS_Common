using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// かつて不具合があった箇所の回帰テスト（2026-09-05 に修正）。
    ///
    /// リファクタ前の調査で見つかった不具合を直したので、同じ壊れ方を二度としないよう
    /// 修正後の正しい挙動を固定しておく。
    /// もとは KnownIssuesTests として「壊れている現状」を記録していたテストを、
    /// 修正に合わせて「あるべき挙動」の検証に書き換えたもの。
    /// </summary>
    [TestClass]
    public class FixedBugRegressionTests
    {
        private StcUtils util;
        private StcSecure secure;

        [TestInitialize]
        public void SetUp()
        {
            util = new StcUtils();
            secure = new StcSecure();
        }

        [TestMethod]
        public void AdjustDirectoryName_ドライブ直下は区切り付きで返る()
        {
            // 以前は @":\" を足していたため "C::\" とコロンが二重になっていた
            Assert.AreEqual(@"C:\", util.AdjustDirectoryName(@"C:\"));
            Assert.AreEqual(@"C:\", util.AdjustDirectoryName("C:"));
        }

        [TestMethod]
        public void AdjustDirectoryName_フォルダ指定は終端の区切りが落ちる()
        {
            Assert.AreEqual(@"C:\work", util.AdjustDirectoryName(@"C:\work\"));
            Assert.AreEqual(@"C:\work", util.AdjustDirectoryName(@"C:\work"));
        }

        [TestMethod]
        public void AdjustDirectoryName_相対パスでも終端の区切りが落ちる()
        {
            // 以前は else 節で入力をそのまま返しており、TrimEnd の結果が捨てられていた
            Assert.AreEqual("work", util.AdjustDirectoryName(@"work\"));
            Assert.AreEqual("work", util.AdjustDirectoryName("work"));
        }

        [TestMethod]
        public void ChangeCygwinPath2WindowsPath_ドライブレターと区切りが変換される()
        {
            // 以前は String.Insert の戻り値を捨てていたためコロンが入らず、
            // 区切り文字を変換する処理も無かった
            Assert.AreEqual(@"c:\work\a.txt", util.ChangeCygwinPath2WindowsPath("/cygdrive/c/work/a.txt"));
            Assert.AreEqual("c:", util.ChangeCygwinPath2WindowsPath("/cygdrive/c"));
        }

        [TestMethod]
        public void ChangeCygwinPath2WindowsPath_対象外のパスはそのまま返る()
        {
            Assert.AreEqual(@"C:\work", util.ChangeCygwinPath2WindowsPath(@"C:\work"), "すでに Windows 形式ならそのまま");
            Assert.AreEqual("/home/user", util.ChangeCygwinPath2WindowsPath("/home/user"), "cygdrive でなければそのまま");
            Assert.AreEqual("", util.ChangeCygwinPath2WindowsPath(""));
        }

        [TestMethod]
        public void パス変換_CygwinとWindowsを往復すると元に戻る()
        {
            string windowsPath = @"C:\work\sub\a.txt";

            string cygwin = util.ChangeWindowsPath2CygwinPath(windowsPath);
            Assert.AreEqual("/cygdrive/C/work/sub/a.txt", cygwin);

            Assert.AreEqual(windowsPath, util.ChangeCygwinPath2WindowsPath(cygwin), "往復して元に戻るはず");
        }

        [TestMethod]
        public void AppendLinuxPathName_区切りが重複も欠落もしない()
        {
            // 以前は Substring(n, 0) が常に空文字を返すせいで条件が必ず成立し、
            // 区切りを足し続けてスラッシュが重複していた
            Assert.AreEqual("a/b", util.AppendLinuxPathName("a", "b"), "無ければ足す");
            Assert.AreEqual("a/b", util.AppendLinuxPathName("a/", "b"), "前にあれば足さない");
            Assert.AreEqual("a/b", util.AppendLinuxPathName("a", "/b"), "後にあれば足さない");
            Assert.AreEqual("a/b", util.AppendLinuxPathName("a/", "/b"), "両方にあれば片方を落とす");
            Assert.AreEqual("/home/user/work", util.AppendLinuxPathName("/home/user", "work"));
        }

        [TestMethod]
        public void StcSecure_日本語を含めても往復できる()
        {
            // 以前は暗号文を Encoding.Unicode で文字列化していたため、有効な UTF-16 に
            // ならない並びが置換文字に潰れて復号できなくなっていた。Base64 で受け渡す。
            foreach (string word in new[] { "hello", "パスワード", "0123456789", "", "記号!\"#$%&'()", "混在Abc漢字123" })
            {
                Assert.AreEqual(word, secure.Decode(secure.Encode(word)), "往復できるはず: " + word);
            }
        }

        [TestMethod]
        public void StcSecure_暗号化結果はBase64になる()
        {
            string encoded = secure.Encode("hello");

            // 例外が出なければ Base64 として妥当
            byte[] bytes = Convert.FromBase64String(encoded);

            Assert.IsTrue(bytes.Length > 0);
            Assert.AreNotEqual("hello", encoded, "平文がそのまま出てはいけない");
        }

        [TestMethod]
        public void RemoveStringArray_空文字を指定しても落ちない()
        {
            // String.Replace は第1引数が空文字だと ArgumentException を投げる。
            // 「何も削除しない」とみなしてそのまま返すようにした。
            CollectionAssert.AreEqual(new[] { "abc" }, util.RemoveStringArray(new[] { "abc" }, ""));

            // 通常の削除はこれまでどおり
            CollectionAssert.AreEqual(new[] { "ac", "cd" }, util.RemoveStringArray(new[] { "abc", "bcd" }, "b"));
        }

        [TestMethod]
        public void 改行コード変換_二度かけても壊れない()
        {
            // 以前は "\n" を無条件に "\r\n" へ置換していたため、すでに CRLF の箇所で
            // CR が増えていた。HTML 取得処理から実際に呼ばれており実害があった。
            Assert.AreEqual("a\r\nb", util.ChangeNewLineCodeLF2CRLF("a\r\nb"), "CRLF に再度かけても増えない");
            Assert.AreEqual("a\r\nb", util.ChangeNewLineCodeLF2CRLF("a\nb"), "LF は CRLF になる");

            // 混在していても揃う
            Assert.AreEqual("a\r\nb\r\nc", util.ChangeNewLineCodeLF2CRLF("a\r\nb\nc"));

            // 何度かけても結果が変わらない
            string once = util.ChangeNewLineCodeLF2CRLF("a\nb\r\nc");
            Assert.AreEqual(once, util.ChangeNewLineCodeLF2CRLF(once));
        }
    }
}
