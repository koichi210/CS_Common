using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    [TestClass]
    public class NaturalStringComparerTests
    {
        [TestMethod]
        public void 数字部分は桁数ではなく数値として比較される()
        {
            String[] input = { "HOGE_100", "HOGE_2", "HOGE_0", "HOGE_10", "HOGE_1" };
            String[] expected = { "HOGE_0", "HOGE_1", "HOGE_2", "HOGE_10", "HOGE_100" };

            Array.Sort(input, NaturalStringComparer.Instance);

            CollectionAssert.AreEqual(expected, input);
        }

        [TestMethod]
        public void 数字を含まない文字列同士は通常の文字列比較になる()
        {
            String[] input = { "banana", "apple", "cherry" };
            String[] expected = { "apple", "banana", "cherry" };

            Array.Sort(input, NaturalStringComparer.Instance);

            CollectionAssert.AreEqual(expected, input);
        }

        [TestMethod]
        public void 先頭の0は数値としては無視される()
        {
            String[] input = { "IMG_010", "IMG_2", "IMG_1" };
            String[] expected = { "IMG_1", "IMG_2", "IMG_010" };

            Array.Sort(input, NaturalStringComparer.Instance);

            CollectionAssert.AreEqual(expected, input);
        }

        [TestMethod]
        public void 全角数字は数字として扱わず通常の文字として比較される()
        {
            // 以前は全角数字も数字として扱いつつ、先頭の0の除去は半角の0しか見ないため、
            // 「第０１話」が「第２話」より後ろに並んでいた
            Assert.IsTrue(NaturalStringComparer.Instance.Compare("第０１話", "第２話") < 0);
            Assert.IsTrue(NaturalStringComparer.Instance.Compare("第2話", "第10話") < 0, "半角はこれまで通り数値として比較する");
        }

        [TestMethod]
        public void nullは前方に並ぶ()
        {
            Assert.IsTrue(NaturalStringComparer.Instance.Compare(null, "a") < 0);
            Assert.IsTrue(NaturalStringComparer.Instance.Compare("a", null) > 0);
            Assert.AreEqual(0, NaturalStringComparer.Instance.Compare(null, null));
        }
    }
}
