// <root><Setting attribute="キー">値</Setting>...</root> という形のXML設定ファイルを、
// キーと値の対応として読み書きする。
//
// CaptureWindow / PictTriming / PictMerge / PictMerge2 の4プロジェクトが、
// それぞれ同じ形のXMLを手書きで組み立て、読み込みは属性名のif/else連打で
// 取り出していたため集約した。ファイル形式は従来とまったく同じなので、
// これまでの設定ファイルはそのまま読める。
//
// StcSaveRestore(コントロールを直接登録して一括で保存/復元する仕組み)との違いは、
// こちらが「コントロールに依存しないただのキーと値」である点。WPFのように
// StcSaveRestoreが使えない画面でも利用できる。
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace StandardTemplate
{
    class StcSimpleSettings
    {
        private const String RootName = "root";
        private const String ElementName = "Setting";
        private const String AttributeName = "attribute";

        private readonly Dictionary<String, String> Values = new Dictionary<String, String>();

        public void Set(String Key, String Value)
        {
            Values[Key] = Value;
        }

        // 設定に無いキーはDefaultValueを返す
        public String Get(String Key, String DefaultValue = "")
        {
            String value;
            return Values.TryGetValue(Key, out value) ? value : DefaultValue;
        }

        public Boolean IsExist(String Key)
        {
            return Values.ContainsKey(Key);
        }

        public Boolean Save(String FilePath)
        {
            XmlDocument document = new XmlDocument();
            document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));

            XmlElement root = document.CreateElement(RootName);
            document.AppendChild(root);

            foreach (KeyValuePair<String, String> pair in Values)
            {
                XmlElement element = document.CreateElement(ElementName);
                element.SetAttribute(AttributeName, pair.Key);
                element.InnerText = pair.Value ?? "";
                root.AppendChild(element);
            }

            try
            {
                AtomicFile.Write(FilePath, document.Save);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
            return true;
        }

        // ファイルが無い・読めない・壊れている場合はnullを返す(呼び出し側は既定値で動かす)
        public static StcSimpleSettings Load(String FilePath)
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            XmlDocument document = new XmlDocument();
            try
            {
                document.Load(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException)
            {
                return null;
            }

            if (document.DocumentElement == null)
            {
                return null;
            }

            StcSimpleSettings settings = new StcSimpleSettings();
            foreach (XmlNode node in document.DocumentElement)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                {
                    continue;
                }

                String key = element.GetAttribute(AttributeName);
                if (key != String.Empty)
                {
                    settings.Set(key, element.InnerText);
                }
            }

            return settings;
        }
    }
}
