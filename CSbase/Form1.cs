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

/*
 *  C# + Form でDxLibアプリを作成するための初期化テンプル (bPcsでも似たようなことをやっている）
 *  64bit, Unicode, .NET Framework 4.7.1
 *  
 *  バックバッファは1920x1080だが、最初は1280x720で起動し、F4を押すと1920x1080に切り替える
 *  1920x1080モニターが前提。4Kとか知らん
 *  わりと 1366x768 環境の人は多いと思われるので、最初は1280x720にしているのだ。
 *  
 *  1280x720の場合はオフスクリーンを作ってそこに描画し、最後に表画面に転送する
 *  C#（というかフォーム）の場合、ちょっと回りくどいことをしないと「もう一段階」縮小されて汚い画面に
 *  なってしまう。まぁこんなもんだと思って我慢しよう。
 *  
 *  なお、マルチスレッド仕様ではない。DxLibではグラフィック系データが含まれるファイルをスレッド中で
 *  ロードすると高確率でデッドロックする。Async読み込みで似たようなことはできるが、あれはメインスレッドを
 *  回さないとロードしない。つまり、グラフィックデータのAsync読み込み中にタイトルバーをつかむと止まるのだ。
 **/

namespace CSbase
{
    public partial class Form1 : Form
    {
        public const string APP_TITLE = "C# BaseCode for DxLib [x64][Unicode] Ver. 0.2019.06.12.01";
        bool BOOL_WAIT_VSYNC = false;

        const int SIZE_WIDTH = 1280, SIZE_HEIGHT = 720; // 初期表示はHalf HD
        const int SIZE_X_BACKBUFFER = 1920, SIZE_Y_BACKBUFFER = 1080; // バックバッファはFull HD
        const int INTERVAL_TIME = 16666; // 60fps
        bool bOK = false;
        long lNowTime, lNextTime;
        int nPosOld_X, nPosOld_Y;
        int nGRAPH_OFFSCREEN_1920x1080, nGRAPH_OFFSCREEN_1280x720;
        bool bVFullScreen = false; // 仮想フルスクリーンかどうか
        Ticker ticker = null; // ティッカー
        int nFontTicker = -1, nFontSizeTicker = 14;

        enum GameState { GS_TITLE, GS_GAME, GS_RESULT }; // 状態遷移
        GameState gamestate = GameState.GS_TITLE;

        uint DXLIB_COLOR_WHITE = DX.GetColor(255, 255, 255); // 色
        uint DXLIB_COLOR_BLACK = DX.GetColor(0, 0, 0);
        uint DXLIB_COLOR_RED = DX.GetColor(255, 0, 0);
        uint DXLIB_COLOR_YELLOW = DX.GetColor(255, 255, 0);

        public Form1()
        {
            InitializeComponent();
            this.Text = APP_TITLE;

            // 最初にこうしておかないとうまくいかない
            FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER);

            DX.SetUserWindow(this.Handle);
            DX.ChangeWindowMode(DX.TRUE);
            DX.SetFullSceneAntiAliasingMode(4, 2); // AA
            DX.SetDrawValidMultiSample(4, 2);
            DX.SetMultiThreadFlag(DX.TRUE);
            DX.SetGraphMode(SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER, 32);
            DX.SetZBufferBitDepth(24); // デフォルト(16)ではモアレ等が多い
            DX.SetCreateDrawValidGraphZBufferBitDepth(24); // MakeScreen用 
            DX.SetWindowSizeChangeEnableFlag(DX.TRUE, DX.FALSE);

            DX.SetAlwaysRunFlag(DX.TRUE);
            DX.SetWaitVSyncFlag(BOOL_WAIT_VSYNC == true ? DX.TRUE : DX.FALSE); // VSyncを待つとAsync読み込みが重いので自前で60fpsを維持しよう

            DX.SetFontCacheCharNum(4096); // フォントキャッシュ。デフォルトだと表示文字種類が多いと極端に重くなる。まぁ2048くらいでいいかも

            bOK = (DX.DxLib_Init() == 0); // もしエラーがあっても終了はForm_Loadで処理するように
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (bOK == false)
            {
                MessageBox.Show("ＤＸライブラリの初期化においてエラーが発生しました。終了します。", APP_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            else
            {
                // DX.SetWindowStyleMode(0); // これは不要かな？
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.ClientSize = new Size(SIZE_WIDTH, SIZE_HEIGHT);
                Point pt = Cursor.Position;
                int x = (Screen.GetBounds(pt).Width - Size.Width) / 2;
                int y = (Screen.GetBounds(pt).Height - Size.Height) / 2;
                Location = new Point(x, y);

                DX.SetDrawMode(DX.DX_DRAWMODE_BILINEAR); // DxLib_Init()の後にやらないと意味がない
                DX.SetCreateSoundIgnoreLoopAreaInfo(DX.TRUE); // WAVのウr-プ情報を無視
                DX.SetDrawScreen(DX.DX_SCREEN_BACK);

                // C#(というかフォームアプリで)1280x720表示するための細工
                nGRAPH_OFFSCREEN_1920x1080 = DX.MakeScreen(SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER);
                nGRAPH_OFFSCREEN_1280x720 = DX.MakeScreen(SIZE_WIDTH, SIZE_HEIGHT);
                if (nGRAPH_OFFSCREEN_1280x720 < 0 || nGRAPH_OFFSCREEN_1920x1080 < 0) Close();

                nFontTicker = DX.CreateFontToHandle("", nFontSizeTicker, -1, DX.DX_FONTTYPE_NORMAL);
                ticker = new Ticker(SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER - nFontSizeTicker, nFontTicker);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.F4) // 画面モード切替
            {
                if (bVFullScreen == false)
                {
                    // 仮想フルスクリーン？モード
                    nPosOld_X = Location.X;
                    nPosOld_Y = Location.Y;
                    Point pt = Cursor.Position;
                    Location = new Point(Screen.GetBounds(pt).X, Screen.GetBounds(pt).Y);
                    FormBorderStyle = FormBorderStyle.None;
                    ClientSize = new Size(Screen.GetBounds(pt).Width, Screen.GetBounds(pt).Height);
                    bVFullScreen = true;
                    ticker.Add("V-Fullscreen Mode", DXLIB_COLOR_RED);
                }
                else
                {
                    // ウィンドウモード
                    Location = new Point(nPosOld_X, nPosOld_Y);
                    FormBorderStyle = FormBorderStyle.FixedSingle;
                    ClientSize = new Size(SIZE_WIDTH, SIZE_HEIGHT);
                    bVFullScreen = false;
                    ticker.Add("Window Mode", DXLIB_COLOR_BLACK);
                }
            }
        }

        private void Form1_Shown(object sender, EventArgs e) // ゲームループ用に使用
        {
            if (bOK == false) Close();

            lNowTime = DX.GetNowHiPerformanceCount();
            lNextTime = lNowTime + INTERVAL_TIME;
            long lPrevTime = lNowTime;

            if (BOOL_WAIT_VSYNC == true)
            {
                while (bOK == true)
                {
                    lNowTime = DX.GetNowHiPerformanceCount();
                    if (MainLoop((int)(lNowTime - lPrevTime)) == false)
                    {
                        bOK = false;
                        Close();
                    }
                    lPrevTime = lNowTime;

                    Application.DoEvents();
                    if (DX.ProcessMessage() != 0)
                    {
                        bOK = false;
                        Close();
                    }
                }
            }
            else
            {
                while (bOK == true)
                {
                    if (lNowTime >= lNextTime)
                    {
                        if (MainLoop((int)(lNowTime - lPrevTime)) == false)
                        {
                            bOK = false;
                            Close();
                        }
                        lPrevTime = lNowTime;
                        lNowTime = DX.GetNowHiPerformanceCount();
                        lNextTime = lNextTime + INTERVAL_TIME;
                        if (lNowTime > lNextTime) lNextTime = lNowTime + INTERVAL_TIME;
                    }

                    Application.DoEvents();
                    if (DX.ProcessMessage() != 0)
                    {
                        bOK = false;
                        Close();
                    }
                    // Sleep(1) は実際には(1+α)msec停止するので、ある程度余裕のある時だけ使用するとよい
                    if (lNextTime - lNowTime > 2500) // ここでは2.5msecとした
                        System.Threading.Thread.Sleep(1);
                    lNowTime = DX.GetNowHiPerformanceCount();
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) // 終了前
        {
            bOK = false;

            if (ticker != null)
            {
                ticker.DeleteAll(true);
                ticker = null;
            }

            DX.MV1InitModel();
            DX.InitSoundMem();
            DX.InitGraph();
            DX.InitFontToHandle();
            DX.DxLib_End();
        }

        // ちらつき対策 (特に起動時。あえて何もしないことでちらつきを防ぐ)
        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        private bool MainLoop(int nDeltaTime) // 振り分け・画面更新
        {
            if (bOK == false) return true;
            if (bVFullScreen == true)
            {
                DX.SetDrawScreen(DX.DX_SCREEN_BACK);
            }
            else
            {
                DX.SetDrawScreen(nGRAPH_OFFSCREEN_1920x1080);
            }
            DX.SetDrawMode(DX.DX_DRAWMODE_BILINEAR);

            bool bResult = true;
            switch (gamestate)
            {
                case GameState.GS_TITLE:
                    bResult = Proc_Title(nDeltaTime);
                    break;
                case GameState.GS_GAME:
                    bResult = Proc_Game(nDeltaTime);
                    break;
                case GameState.GS_RESULT:
                    bResult = Proc_Result(nDeltaTime);
                    break;
            }

            if (bVFullScreen == false)
            {
                DX.SetDrawScreen(nGRAPH_OFFSCREEN_1280x720);
                DX.SetDrawMode(DX.DX_DRAWMODE_BILINEAR);
                DX.DrawRectExtendGraph(0, 0, SIZE_WIDTH, SIZE_HEIGHT, 0, 0, SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER, nGRAPH_OFFSCREEN_1920x1080, DX.FALSE);

                DX.SetDrawScreen(DX.DX_SCREEN_BACK);
                DX.SetDrawMode(DX.DX_DRAWMODE_NEAREST);
                DX.DrawRectExtendGraph(0, 0, SIZE_X_BACKBUFFER, SIZE_Y_BACKBUFFER, 0, 0, SIZE_WIDTH, SIZE_HEIGHT, nGRAPH_OFFSCREEN_1280x720, DX.FALSE);
            }

            ticker.Update(nDeltaTime);
            return ((DX.ScreenFlip() == 0) && (bResult == true));
        }
    }
}
