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

        // TODO: 疎通確認が済んだら、以下を実測ベースで追加していく。
        //   - GetNumber / GetNumberFromRear（範囲外 StartIdx、Length=0、ゼロ埋め、既定値 "01"）
        //   - パス変換系（Windows <-> Cygwin <-> Linux）
        //   - 文字列配列系（TrimDuplication / AssortList / RemoveStringArray）
        //   - 改行コード変換（LF <-> CRLF）
    }
}
