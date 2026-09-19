// 「実行した操作を記憶しておき、あとから元に戻す(Undo)」ためのスタック管理。
// FFEdit(ファイル名変更・移動)とFileArranger(フォルダ振り分け)にまったく同じ実装が
// 別々に置かれていたため、_Commonへ集約した(bool/Booleanの表記ゆれ以外の差は無かった)。
//
// 使い方: 1回の操作を始めるときにIncrementRegistNumberで番号を進め、
// 操作ごとにSetRestoreListで「戻すための情報」を積む。元に戻すときは
// DecrementRegistNumberで番号を戻し、IsExistRestoreListがtrueの間
// GetRestoreListで取り出す(同じ番号の分だけ取り出せる)。
using System;
using System.Collections.Generic;

namespace StandardTemplate
{
    class StcProcessMemory
    {
        public struct RestoreStruct
        {
            public int SerialNumber;    // 管理番号
            public String SrcName;      // 記憶するデータ
            public String DestName;     // 記憶するデータ

            public RestoreStruct(int serial_number, string src_name, string dest_name)
            {
                SerialNumber = serial_number;
                SrcName = src_name;
                DestName = dest_name;
            }
        }

        private int CurrentIdx = 0;
        private List<RestoreStruct> RestoreList = new List<RestoreStruct>();

        public void IncrementRegistNumber()
        {
            CurrentIdx++;
        }

        public Boolean DecrementRegistNumber()
        {
            if (CurrentIdx == 0)
            {
                return false;
            }

            CurrentIdx--;
            return true;
        }

        public void SetRestoreList(String SrcName, String DestName)
        {
            RestoreList.Add(new RestoreStruct(CurrentIdx, SrcName, DestName));
        }

        public void GetRestoreList(ref String SrcName, ref String DestName)
        {
            int LastIdx = RestoreList.Count - 1;

            SrcName = RestoreList[LastIdx].SrcName;
            DestName = RestoreList[LastIdx].DestName;
            RestoreList.RemoveAt(LastIdx);
        }

        public Boolean IsExistRestoreList()
        {
            if (RestoreList.Count == 0)
            {
                return false;
            }

            int ListEndIdx = RestoreList.Count - 1;
            return RestoreList[ListEndIdx].SerialNumber == CurrentIdx;
        }
    }
}
