using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DxLibDLL;

namespace CSbase
{
    class TickerElement
    {
        public long lStartTime; // 表示開始した時刻
        public int xe; // ここまで動かす
        public int x; // 今のX
        public int y; // 今のY
        public string sMessage; // 表示する文字列
        public uint uiColor; // 色
        public int nDirection; // In(-1) Out(+1) Hold(0)
    }

    public class Ticker
    {
        static Object lockObj = new object();
        int nFontHandle = -1;
        int xInitPos, yInitPos;
        List<TickerElement> list = null;
        int nExpireTime = 5 * 1000 * 1000; // 5秒で消える設定。お好みで
        float fSpeed = 12.0F ; // 1フレーム当たり12dot
        int nXOffset = 8;
        int nFontSize = 10;

        public Ticker(int xInitPos, int yInitPos, int nFontHandle)
        {
            this.xInitPos = xInitPos;
            this.yInitPos = yInitPos;
            this.nFontHandle = nFontHandle;
            list = new List<TickerElement>();
            nFontSize = DX.GetFontSizeToHandle(this.nFontHandle);
        }

        public bool Add(string sMessage, uint uiColor)
        {
            lock (lockObj)
            {
                TickerElement te = new TickerElement();
                te.x = xInitPos;
                te.y = yInitPos;
                te.sMessage = sMessage;
                te.uiColor = uiColor;
                te.nDirection = -1;
                te.lStartTime = -1;
                te.xe = -999;

                list.Add(te);
                for (int j = 0; j < list.Count - 1; j++)
                {
                    TickerElement te2 = (TickerElement)list[j];
                    te2.y -= nFontSize;
                }
            }

            return true;
        }

        public bool Update(int nDeltaTime) // usec
        {
            lock (lockObj)
            {
                long lTime = DX.GetNowHiPerformanceCount();
                int fontsize = DX.GetFontSizeToHandle(nFontHandle);
                for (int i = list.Count-1; i >= 0; i--)
                {
                    TickerElement te = (TickerElement)list[i];

                    // 新規追加か？
                    if (te.lStartTime < 0)
                    {
                        te.lStartTime = lTime;
                        te.xe = xInitPos - DX.GetDrawStringWidthToHandle(te.sMessage, te.sMessage.Length, nFontHandle) - nXOffset;
                    }

                    float fDelta = (float)nDeltaTime / 16666.6F;
                    DX.DrawStringToHandle(te.x, te.y, te.sMessage, te.uiColor, nFontHandle);
                    switch (te.nDirection)
                    {
                        case -1:
                            if (te.x > te.xe)
                            {
                                te.x -= (int)(fDelta * fSpeed);
                                if (te.x <= te.xe)
                                {
                                    te.x = te.xe;
                                    te.nDirection = 0;
                                }
                            }
                            if (te.y + fontsize < 0)
                                te.nDirection = 1;
                            break;

                        case 0:
                            if ((lTime - te.lStartTime > nExpireTime) || (te.y + fontsize < 0))
                                te.nDirection = 1;
                            break;

                        case 1:
                            if (te.x < xInitPos)
                            {
                                te.x += (int)(fDelta * fSpeed) * 2;
                            }
                            if ((te.x >= xInitPos) || (te.y + fontsize < 0))
                                list.RemoveAt(i);
                            break;
                    }
                }
            }

            return true;
        }

        public void DeleteAll(bool bDeleteJustNow = false)
        {
            lock (lockObj)
            {
                if (bDeleteJustNow == true)
                {
                    list.Clear();
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        TickerElement te = (TickerElement)list[i];
                        te.nDirection = 1;
                    }
                }
            }
        }
    }
}
