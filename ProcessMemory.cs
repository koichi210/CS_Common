// 「実行した操作を記憶しておき、あとから元に戻す(Undo)」ためのスタック管理。
// FFEdit(ファイル名変更・移動)とFileArranger(フォルダ振り分け)にまったく同じ実装が
// 別々に置かれていたため、_Commonへ集約した(bool/Booleanの表記ゆれ以外の差は無かった)。
//
// 使い方: 操作ごとにAddRestoreItemで「戻すための情報」を積み(現在の管理番号が付く)、
// 1回の操作が終わったらIncrementSerialNumberで次の番号へ進める。元に戻すときは
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

        private int _currentIdx = 0;
        private readonly List<RestoreStruct> _restoreList = new List<RestoreStruct>();

        public void IncrementSerialNumber()
        {
            _currentIdx++;
        }

        public Boolean DecrementSerialNumber()
        {
            if (_currentIdx == 0)
            {
                return false;
            }

            _currentIdx--;
            return true;
        }

        public void AddRestoreItem(String srcName, String destName)
        {
            _restoreList.Add(new RestoreStruct(_currentIdx, srcName, destName));
        }

        public void PopRestoreItem(ref String srcName, ref String destName)
        {
            int lastIdx = _restoreList.Count - 1;

            srcName = _restoreList[lastIdx].SrcName;
            destName = _restoreList[lastIdx].DestName;
            _restoreList.RemoveAt(lastIdx);
        }

        public Boolean HasRestoreItem()
        {
            return _restoreList.Count > 0 && _restoreList[_restoreList.Count - 1].SerialNumber == _currentIdx;
        }
    }
}
