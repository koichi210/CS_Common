// StcSaveRestore(RegistCtrlで登録したコントロールを一括で保存/復元する仕組み)を、
// XMLではなくJSONで読み書きするためのヘルパー。
//
// 同じ内容がCheetos/FileArrangerのSaveRestore.csに個別に書かれていたため集約した。
// StandardTemplateClass.cs本体に置かないのは、Newtonsoft.Jsonへの依存を
// 「JSON対応したプロジェクトだけ」に閉じ込めるため(_Common/JsonFileStorage.cs と同じ方針)。
//
// 旧XMLからの移行:
//   LoadWithMigration(sr, jsonPath, xmlPath, LoadXml) を起動時に呼ぶと、
//   JSONがあればJSONを読み、無ければ旧XMLを読んでJSONで保存し直し、旧XMLを削除する。
//   XMLの読み込み方(LoadProcの引数)はプロジェクトごとに違うため、呼び出し側から渡す。
using System;
using System.IO;
using System.Windows.Forms;

namespace StandardTemplate
{
    static class JsonSaveRestore
    {
        // 「保存先を選ぶ→保存する→プロファイル一覧を更新する→結果を知らせる」という
        // プロファイル保存ボタンの中身が7プロジェクトで同じだったため集約した。
        // Save は拡張子で振り分ける各プロジェクトの SaveProfile を渡す(旧XMLも保存できる)。
        // 戻り値は保存したファイル名。キャンセル時と失敗時は空文字を返す
        public static String SaveProfileWithDialog(StcUtils Utils, StcFileInputOutput FileIO,
                                                   ComboBox ProfileCtrl, String[] ProfileExtensions,
                                                   Func<String, Boolean> Save,
                                                   String InitialDirectory = "",
                                                   String SuccessMessage = "設定値を保存しました♪")
        {
            // 削除対象の判定に使うため、SelectSaveFileNameを呼ぶ前の選択内容を覚えておく
            String OriginalFileName = ProfileCtrl.Text;

            String SaveFileName = FileIO.SelectSaveFileName(OriginalFileName, InitialDirectory);
            if (String.IsNullOrEmpty(SaveFileName))
            {
                // ダイアログでキャンセルされた
                return "";
            }

            if (!Save(SaveFileName))
            {
                MessageBox.Show("設定の保存に失敗しました" + Environment.NewLine + SaveFileName,
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return "";
            }

            // xml撲滅方針: 「現在のファイルに保存しますか？」で「はい」を選んだ結果、
            // 拡張子がxml->jsonへ切り替わっていたら、保存が成功した後で旧xmlを削除する
            DeleteMigratedXml(OriginalFileName, SaveFileName, InitialDirectory);

            Utils.UpdateProfileList(ref ProfileCtrl, ProfileExtensions, Path.GetFileName(SaveFileName), InitialDirectory);
            MessageBox.Show(SuccessMessage + Environment.NewLine + SaveFileName);
            return SaveFileName;
        }

        // internal: テスト(StandardTemplate.Tests)から直接呼べるようにするため
        internal static void DeleteMigratedXml(String OriginalFileName, String SaveFileName, String InitialDirectory)
        {
            if (!OriginalFileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                !SaveFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            String oldPath = (InitialDirectory != String.Empty) ? Path.Combine(InitialDirectory, OriginalFileName) : OriginalFileName;

            // 念のため、拡張子違いの同名ファイルであることを確認してから消す
            if (!String.Equals(Path.GetFileNameWithoutExtension(oldPath), Path.GetFileNameWithoutExtension(SaveFileName), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 消せなくても保存自体は成功しているので実害は無い
            }
        }

        public static Boolean Save(StcSaveRestore SaveRestore, String FilePath)
        {
            try
            {
                JsonFileStorage.Save(FilePath, SaveRestore.BuildGenericProfile());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static Boolean Load(StcSaveRestore SaveRestore, String FilePath)
        {
            GenericProfile profile = JsonFileStorage.Load<GenericProfile>(FilePath);
            if (profile == null)
            {
                return false;
            }

            SaveRestore.ApplyGenericProfile(profile);
            return true;
        }

        // JSONがあればJSONを読む。無ければ旧XMLを読み、JSONで保存し直してから旧XMLを削除する
        public static Boolean LoadWithMigration(StcSaveRestore SaveRestore, String JsonPath, String XmlPath, Func<String, Boolean> LoadXml)
        {
            if (File.Exists(JsonPath))
            {
                return Load(SaveRestore, JsonPath);
            }

            // 旧XMLが無くてもLoadXmlは呼ぶ。StcSaveRestore.LoadXmlFileはファイルの有無に関わらず
            // 最初に登録済みコントロールの既定値(RegistCtrlのElementValue)を適用するため、
            // ここで省くと設定ファイルが1つも無い初回起動で既定値が入らなくなる
            if (!LoadXml(XmlPath))
            {
                return false;
            }

            // JSONで保存できたときだけ旧XMLを消す(消してから保存に失敗して設定を失うことがないように)
            if (Save(SaveRestore, JsonPath))
            {
                TryDeleteFile(XmlPath);
            }
            return true;
        }

        private static void TryDeleteFile(String FilePath)
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 消せなくても実害は無い(以降はJSONが優先して読まれる)
            }
        }
    }
}
