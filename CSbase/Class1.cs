using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using DxLibDLL;


namespace CSbase
{
    public partial class Form1 : Form
    {
        // タイトル
        private bool Proc_Title(int nDeltaTime)
        {
            DX.ClearDrawScreen();
            DX.DrawString(0, 0, "FPS=" + DX.GetFPS().ToString("0.00"), DXLIB_COLOR_WHITE);
            return true;
        }

        // ゲーム中
        private bool Proc_Game(int nDeltaTime)
        {
            return true;
        }

        // ゲームオーバー
        private bool Proc_Result(int nDeltaTime)
        {
            return true;
        }
    }
}
