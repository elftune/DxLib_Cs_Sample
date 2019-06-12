using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DxLibDLL;

namespace CSbase
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 frm = new Form1();
            frm.Show();
            while(frm.Created)
            {
                long lNowTime = DX.GetNowHiPerformanceCount();
                long lNextTime = lNowTime + frm.INTERVAL_TIME;
                long lPrevTime = lNowTime;

                if (frm.BOOL_WAIT_VSYNC == true)
                {
                    while (frm.bOK == true)
                    {
                        lNowTime = DX.GetNowHiPerformanceCount();
                        if (frm.MainLoop((int)(lNowTime - lPrevTime)) == false)
                        {
                            frm.bOK = false;
                            goto EXIT_PRG;
                        }
                        lPrevTime = lNowTime;

                        Application.DoEvents();
                        if (DX.ProcessMessage() != 0)
                        {
                            frm.bOK = false;
                            goto EXIT_PRG;
                        }
                    }
                }
                else
                {
                    while (frm.bOK == true)
                    {
                        if (lNowTime >= lNextTime)
                        {
                            if (frm.MainLoop((int)(lNowTime - lPrevTime)) == false)
                            {
                                frm.bOK = false;
                                goto EXIT_PRG;
                            }
                            lPrevTime = lNowTime;
                            lNowTime = DX.GetNowHiPerformanceCount();
                            lNextTime = lNextTime + frm.INTERVAL_TIME;
                            if (lNowTime > lNextTime) lNextTime = lNowTime + frm.INTERVAL_TIME;
                        }

                        Application.DoEvents();
                        if (DX.ProcessMessage() != 0)
                        {
                            frm.bOK = false;
                            goto EXIT_PRG;
                        }
                        // Sleep(1) は実際には(1+α)msec停止するので、ある程度余裕のある時だけ使用するとよい
                        if (lNextTime - lNowTime > 2500) // ここでは2.5msecとした
                            System.Threading.Thread.Sleep(1);
                        lNowTime = DX.GetNowHiPerformanceCount();
                    }
                }
            }
EXIT_PRG:
            frm.Close();
        }
    }
}
