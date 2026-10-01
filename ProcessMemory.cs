// 「実行した操作を記憶しておき、あとから元に戻す(Undo)」ためのスタック管理。
// FFEdit(ファイル名変更・移動)とFileArranger(フォルダ振り分け)にまったく同じ実装が
// 別々に置かれていたため、_Commonへ集約した(bool/Booleanの表記ゆれ以外の差は無かった)。
//
// 使い方: 1回の操作を始めるときにIncrementSerialNumberで番号を進め、
// 操作ごとにAddRestoreItemで「戻すための情報」を積む。元に戻すときは
// DecrementSerialNumberで番号を戻し、HasRestoreItemがtrueの間
// PopRestoreItemで取り出す(同じ番号の分だけ取り出せる)。
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

            public RestoreStruct(int serialNumber, string srcName, string destName)
            {
                SerialNumber = serialNumber;
                SrcName = srcName;
                DestName = destName;
            }
        }

        private int CurrentIdx = 0;
        private List<RestoreStruct> RestoreList = new List<RestoreStruct>();

        public void IncrementSerialNumber()
        {
            CurrentIdx++;
        }

        public Boolean DecrementSerialNumber()
        {
            if (CurrentIdx == 0)
            {
                return false;
            }

            CurrentIdx--;
            return true;
        }

        public void AddRestoreItem(String srcName, String destName)
        {
            RestoreList.Add(new RestoreStruct(CurrentIdx, srcName, destName));
        }

        public void PopRestoreItem(ref String srcName, ref String destName)
        {
            int lastIdx = RestoreList.Count - 1;

            srcName = RestoreList[lastIdx].SrcName;
            destName = RestoreList[lastIdx].DestName;
            RestoreList.RemoveAt(lastIdx);
        }

        public Boolean HasRestoreItem()
        {
            if (RestoreList.Count == 0)
            {
                return false;
            }

            int listEndIdx = RestoreList.Count - 1;
            return RestoreList[listEndIdx].SerialNumber == CurrentIdx;
        }
    }
}
