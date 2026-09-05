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
        [TestMethod]
        public void PublicApiが承認済みスナップショットと一致すること()
        {
            SnapshotFile.Verify(
                ApiSurface.Dump(),
                "ApiSnapshot",
                "public API が変わっている。呼び出し側 17 プロジェクトに影響が出る可能性あり。");
        }
    }
}
