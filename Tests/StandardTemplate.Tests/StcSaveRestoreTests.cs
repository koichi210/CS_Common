using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// StcSaveRestore（設定の保存・復元）のテスト。
    ///
    /// このクラスは internal だが、Link のソース参照なので各プロジェクトから普通に使えてしまい、
    /// 実際 10 プロジェクトが継承している（Cheetos / FFEdit / FileArranger / ImageViewer /
    /// Mailer / PerforceWrapper / StaticAnalysisViewer / TrimFileData / TrimHtmlData /
    /// VisualStudioBuilder）。壊すと影響が広いので、ここを厚く固めておく。
    ///
    /// 中身を1行ずつ検証するのではなく、「保存して読み直したら元に戻る」という
    /// 使う側から見た結果で確認する。内部をどう書き換えてもこの性質は変わらないはずなので、
    /// リファクタの安全網として一番効く。
    /// </summary>
    [TestClass]
    public class StcSaveRestoreTests
    {
        /// <summary>各プロジェクトの SaveRestore クラスと同じ使い方をするための継承。</summary>
        private sealed class TestProfile : StcSaveRestore
        {
        }

        private readonly List<Control> controls = new List<Control>();
        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "StcTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
        }

        [TestCleanup]
        public void TearDown()
        {
            foreach (Control c in controls)
            {
                c.Dispose();
            }
            controls.Clear();

            try
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
            catch (IOException)
            {
                // 後片付けに失敗してもテストの成否には関係ないので黙って流す
            }
        }

        private T Track<T>(T control) where T : Control
        {
            controls.Add(control);
            return control;
        }

        private string PathFor(string name)
        {
            return Path.Combine(tempDirectory, name + ".xml");
        }

        // ------------------------------------------------------------------

        [TestMethod]
        public void テキストボックスの値が保存して読み直すと戻る()
        {
            TextBox textBox = Track(new TextBox());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "TextBox1", textBox, "既定値");

            textBox.Text = "保存したい値";
            string path = PathFor("text");
            Assert.IsTrue(profile.SaveXmlFile(path), "保存に成功するはず");

            textBox.Text = "書き換えてしまった値";
            Assert.IsTrue(profile.LoadXmlFile(path), "読み込みに成功するはず");

            Assert.AreEqual("保存したい値", textBox.Text);
        }

        [TestMethod]
        public void チェックボックスとラジオボタンの状態が戻る()
        {
            CheckBox checkBox = Track(new CheckBox());
            RadioButton radio = Track(new RadioButton());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "Check1", checkBox);
            profile.RegistCtrl("Name", "Radio1", radio);

            checkBox.Checked = true;
            radio.Checked = false;
            string path = PathFor("check");
            profile.SaveXmlFile(path);

            checkBox.Checked = false;
            radio.Checked = true;
            profile.LoadXmlFile(path);

            Assert.IsTrue(checkBox.Checked, "チェック状態が戻るはず");
            Assert.IsFalse(radio.Checked, "ラジオの状態が戻るはず");
        }

        [TestMethod]
        public void コンボボックスの入力値が戻る()
        {
            ComboBox combo = Track(new ComboBox());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "Combo1", combo);

            combo.Text = "入力した文字列";
            string path = PathFor("combo");
            profile.SaveXmlFile(path);

            combo.Text = "別の文字列";
            profile.LoadXmlFile(path);

            Assert.AreEqual("入力した文字列", combo.Text);
        }

        [TestMethod]
        public void コンボボックスの履歴一覧が戻る()
        {
            ComboBox combo = Track(new ComboBox());
            var profile = new TestProfile();
            profile.RegistCtrlList("List", "ComboList1", combo);

            combo.Items.Add("一件目");
            combo.Items.Add("二件目");
            string path = PathFor("combolist");
            profile.SaveXmlFile(path);

            combo.Items.Clear();
            profile.LoadXmlFile(path);

            Assert.AreEqual(2, combo.Items.Count, "件数が戻るはず");
            Assert.AreEqual("一件目", combo.Items[0]);
            Assert.AreEqual("二件目", combo.Items[1]);
        }

        [TestMethod]
        public void チェック付きリストの項目と状態が戻る()
        {
            CheckedListBox listBox = Track(new CheckedListBox());
            var profile = new TestProfile();
            profile.RegistCtrlList("List", "CheckList1", listBox);

            listBox.Items.Add("チェックする項目");
            listBox.Items.Add("チェックしない項目");
            listBox.SetItemChecked(0, true);
            listBox.SetItemChecked(1, false);

            string path = PathFor("checkedlist");
            profile.SaveXmlFile(path);

            listBox.Items.Clear();
            profile.LoadXmlFile(path);

            Assert.AreEqual(2, listBox.Items.Count, "件数が戻るはず");
            Assert.AreEqual("チェックする項目", listBox.Items[0]);
            Assert.IsTrue(listBox.GetItemChecked(0), "チェック状態も戻るはず");
            Assert.IsFalse(listBox.GetItemChecked(1));
        }

        [TestMethod]
        public void スクロールバーの位置が戻る()
        {
            HScrollBar bar = Track(new HScrollBar());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "Scroll1", bar, 10);

            bar.Value = 42;
            string path = PathFor("scroll");
            profile.SaveXmlFile(path);

            bar.Value = 7;
            profile.LoadXmlFile(path);

            Assert.AreEqual(42, bar.Value);
        }

        [TestMethod]
        public void 複数の種類のコントロールをまとめて保存して戻せる()
        {
            TextBox textBox = Track(new TextBox());
            CheckBox checkBox = Track(new CheckBox());
            ComboBox combo = Track(new ComboBox());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "Text", textBox);
            profile.RegistCtrl("Name", "Check", checkBox);
            profile.RegistCtrl("Name", "Combo", combo);

            textBox.Text = "文字";
            checkBox.Checked = true;
            combo.Text = "選択";
            string path = PathFor("mixed");
            profile.SaveXmlFile(path);

            textBox.Text = "";
            checkBox.Checked = false;
            combo.Text = "";
            profile.LoadXmlFile(path);

            Assert.AreEqual("文字", textBox.Text);
            Assert.IsTrue(checkBox.Checked);
            Assert.AreEqual("選択", combo.Text);
        }

        [TestMethod]
        public void 読み込み時にファイルに無い項目は既定値に戻る()
        {
            // LoadXmlFile は最初に SetDefaultParam を呼んで全コントロールを既定値に戻し、
            // そのうえでファイルにある項目だけ上書きする。
            TextBox saved = Track(new TextBox());
            var writer = new TestProfile();
            writer.RegistCtrl("Name", "Saved", saved);
            saved.Text = "ファイルに入る値";
            string path = PathFor("default");
            writer.SaveXmlFile(path);

            // 保存したときには存在しなかったコントロールを足して読み込む
            TextBox notSaved = Track(new TextBox());
            var reader = new TestProfile();
            reader.RegistCtrl("Name", "Saved", saved);
            reader.RegistCtrl("Name", "NotSaved", notSaved, "こちらが既定値");

            saved.Text = "書き換え";
            notSaved.Text = "書き換え";
            reader.LoadXmlFile(path);

            Assert.AreEqual("ファイルに入る値", saved.Text, "ファイルにある項目は復元される");
            Assert.AreEqual("こちらが既定値", notSaved.Text, "ファイルに無い項目は既定値になる");
        }

        [TestMethod]
        public void 保存したファイルにバージョンが入る()
        {
            TextBox textBox = Track(new TextBox());
            var profile = new TestProfile();
            profile.RegistCtrl("Name", "Text", textBox);

            string path = PathFor("version");
            profile.SaveXmlFile(path, "3");

            Assert.AreEqual(3, profile.LoadXmlVersion(path));
        }

        [TestMethod]
        public void 存在しないファイルを読んでも落ちずに失敗を返す()
        {
            var profile = new TestProfile();

            Assert.IsFalse(profile.LoadXmlFile(PathFor("なにもない")), "存在しないファイルは false");
            Assert.IsFalse(profile.LoadXmlFile(""), "ファイル名が空でも false");
        }

        [TestMethod]
        public void 要素名を変えても保存して読み直せる()
        {
            TextBox textBox = Track(new TextBox());
            var profile = new TestProfile();
            profile.SetElement("MyParam");
            profile.RegistCtrl("Name", "Text", textBox);

            textBox.Text = "要素名を変えた";
            string path = PathFor("element");
            profile.SaveXmlFile(path);

            textBox.Text = "";
            profile.LoadXmlFile(path);

            Assert.AreEqual("要素名を変えた", textBox.Text);
        }
    }
}
