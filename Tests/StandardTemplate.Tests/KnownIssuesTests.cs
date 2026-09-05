using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// リファクタ前の調査で見つかった「明らかに意図とズレている挙動」を記録するテスト。
    ///
    /// public 凍結の方針なので今は直さない。代わりに「現状こうなっている」を固定しておく。
    /// ここが赤くなったら、それは誰かが直したということ。直すと決めたらテストの方を書き換える。
    ///
    /// 直すかどうかを判断するために、呼び出し元があるかも調べて各テストに書いてある。
    /// （2026-09-05 時点、CS_Form リポジトリ全体を grep して確認）
    /// </summary>
    [TestClass]
    public class KnownIssuesTests
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
        public void 既知の問題_AdjustDirectoryNameはドライブ直下でコロンが二重になる()
        {
            // 意図: "C:" のようにドライブレターだけなら終端に "\" を足したい（コメントにそう書いてある）
            // 実際: DestDirName += @":\" となっていて、コロンまで足してしまう
            Assert.AreEqual(@"C::\", util.AdjustDirectoryName(@"C:\"), "ドライブ直下の指定が壊れたパスになる");
            Assert.AreEqual(@"C::\", util.AdjustDirectoryName("C:"));

            // 通常のフォルダ指定は正しく動くので、壊れるのはドライブ直下だけ
            Assert.AreEqual(@"C:\work", util.AdjustDirectoryName(@"C:\work\"));

            // 呼び出し元: 無し（未使用メソッド）。そのため実害は出ていない。
        }

        [TestMethod]
        public void 既知の問題_AdjustDirectoryNameは相対パスだと末尾の区切りを落とさない()
        {
            // ":" を含まない場合、いったん TrimEnd した結果を捨てて入力をそのまま返している
            Assert.AreEqual(@"work\", util.AdjustDirectoryName(@"work\"), "相対パスでは TrimEnd の結果が捨てられる");

            // 呼び出し元: 無し（未使用メソッド）
        }

        [TestMethod]
        public void 既知の問題_ChangeCygwinPath2WindowsPathはほぼ変換していない()
        {
            // 意図: "/cygdrive/c/work" → "C:\work"（コメントにそう書いてある）
            // 実際: NewPath.Insert(1, ":") の戻り値を捨てているのでコロンが入らない。
            //       区切り文字を "\" に直す処理も無い。
            Assert.AreEqual("c/work/a.txt", util.ChangeCygwinPath2WindowsPath("/cygdrive/c/work/a.txt"),
                            "コロンも区切り文字の変換も行われていない");

            // 呼び出し元: 無し。Cheetos は同名メソッドを自前で持っており、そちらを使っている
            // （Cheetos\Utils.cs）。共通クラスのこれは誰も呼んでいない。
        }

        [TestMethod]
        public void 既知の問題_AppendLinuxPathNameは常に区切りを足すのでスラッシュが重複する()
        {
            // 意図: 片方に "/" があれば足さない
            // 実際: Substring(Path1.Length, 0) と Substring(0, 0) はどちらも常に空文字を返すため、
            //       条件が常に成立して必ず "/" を足してしまう
            Assert.AreEqual("a/b", util.AppendLinuxPathName("a", "b"), "両方に区切りが無いケースだけは正しい");
            Assert.AreEqual("a//b", util.AppendLinuxPathName("a/", "b"), "Path1 の末尾に区切りがあっても足す");
            Assert.AreEqual("a//b", util.AppendLinuxPathName("a", "/b"), "Path2 の先頭に区切りがあっても足す");
            Assert.AreEqual("a///b", util.AppendLinuxPathName("a/", "/b"), "両方にあると3連になる");

            // 呼び出し元: 無し（未使用メソッド）
        }

        [TestMethod]
        public void 既知の問題_StcSecureの単純版は日本語を往復できない()
        {
            // 暗号化した結果のバイト列を Encoding.Unicode.GetString() で文字列にしているのが原因。
            // 暗号文は任意のバイト列なので、有効な UTF-16 とは限らない。
            // 不正な並びは置換文字に潰され、そこで情報が失われて元に戻らなくなる。
            Assert.AreEqual("hello", secure.Decode(secure.Encode("hello")), "たまたま往復できるものもある");
            Assert.AreEqual("0123456789", secure.Decode(secure.Encode("0123456789")));

            Assert.AreNotEqual("パスワード", secure.Decode(secure.Encode("パスワード")),
                               "日本語を入れると往復できずデータが壊れる");

            // 呼び出し元: 無し。設定ファイルの保存・復元に使われているのは鍵つきの
            // Encode(str, out ...) / Decode(str, key, iv, cryptData) の方で、そちらは
            // 暗号文を byte[] のまま扱うのでこの問題を踏まない（実運用は無傷）。
        }

        [TestMethod]
        public void 既知の問題_RemoveStringArrayは空文字を指定すると落ちる()
        {
            // String.Replace("", ...) が ArgumentException を投げる
            Assert.ThrowsException<ArgumentException>(() => util.RemoveStringArray(new[] { "abc" }, ""));

            // 呼び出し元: StcUtils.GetSubDirFileList が RemoveStringArray(FileList, TopDirPath) を
            // 呼んでいる。TopDirPath に空文字が来ると落ちるため、ここだけは実際に踏む可能性がある。
        }

        [TestMethod]
        public void 既知の問題_改行コード変換は二度かけると壊れる()
        {
            // LF2CRLF は "\n" を無条件に "\r\n" へ置換するので、すでに CRLF のものに掛けると
            // CR が増えてしまう。ChangeNewLineCode が先に CRLF2LF で正規化してから
            // 変換しているのは、この性質を避けるためと思われる。
            Assert.AreEqual("a\r\r\nb", util.ChangeNewLineCodeLF2CRLF("a\r\nb"), "CRLF に再度かけると CR が増える");

            // 正規化してから掛ける ChangeNewLineCode は何度呼んでも安定している
            string once = util.ChangeNewLineCode(StcFileInputOutput.ENCORD_TYPE.SHIFT_JIS, "a\nb");
            string twice = util.ChangeNewLineCode(StcFileInputOutput.ENCORD_TYPE.SHIFT_JIS, once);
            Assert.AreEqual(once, twice, "ChangeNewLineCode は何度かけても結果が変わらない");
        }
    }
}
