using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HanLP_Utils
{
    public partial class MainForm : Form
    {
        internal bool _KeepNumber = true;
        internal bool _FilterHtmlTag = true;
        internal bool _FilterLrcTag = true;
        internal bool _FilterAssTag = true;

        private string replaceCharEntity( string htmlstr )
        {
            string result = htmlstr;
            Dictionary<string, string> CHAR_ENTITIES = new Dictionary<string, string>()
            {
                { "&nbsp", " " },
                { "&160", " " },
                { "&lt", "<" },
                { "&60", "<" },
                { "&gt", ">" },
                { "&62", ">" },
                { "&amp", "&" },
                { "&38", "&" },
                { "&quot", "\"" },
                { "&34", "\"" }
            };

            foreach ( var k in CHAR_ENTITIES )
            {
                result = result.Replace( k.Key, k.Value );
            }
            return ( result );
        }

        internal string filterHtmlTag( string text )
        {
            string result = text;

            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml( text );
            var scripts = doc.DocumentNode.SelectNodes( "//script" );
            var styles = doc.DocumentNode.SelectNodes( "//style" );
            var forms = doc.DocumentNode.SelectNodes( "//form" );
            var links = doc.DocumentNode.SelectNodes( "//a" );
            var comments = doc.DocumentNode.SelectNodes( "//comment()" );
            if ( scripts != null ) foreach ( var node in scripts ) { node.Remove(); }
            if ( styles != null ) foreach ( var node in styles ) { node.Remove(); }
            if ( forms != null ) foreach ( var node in forms ) { node.Remove(); }
            if ( links != null ) foreach ( var node in links ) { node.Remove(); }
            if ( comments != null ) foreach ( var node in comments ) { node.Remove(); }

            result = doc.DocumentNode.SelectSingleNode( "//body" ).InnerText.Trim();

            result = Regex.Replace( result, @"((\r\n)|(\n\r)|(\r)|(\n)){2,}", "", RegexOptions.IgnoreCase | RegexOptions.Multiline );
            result = Regex.Replace( result, @"(</.*?>)", "", RegexOptions.IgnoreCase | RegexOptions.Multiline );
            //result = Regex.Replace( result, @"[(</.*?>)|(&gt;)|(&lt;)|(&amp;)]{1,}", " ", RegexOptions.IgnoreCase | RegexOptions.Multiline );

            return ( result.Trim() );
        }

        internal string filterASS( string text )
        {
            string result = text;

            //string pat_ass_header = @"(\[Script Info\])(((\n)|(\r)|(\n\r)|(\r\n)).*?$)*?(((\n)|(\r)|(\n\r)|(\r\n))\[Events\]$)";
            //string pat_ass_header = @"(\[Script Info\])(((\n)|(\r)|(\n\r)|(\r\n)).*?)*?(\[Events\]$)";
            //string pat_ass_head = @"(^\[Script Info\](([(\r)|(\n)|(\r\n)].*?)*?)^\[Events\][(\r)|(\n)|(\r\n)].*?Text)";
            string pat_ass_diag = @"(^Format:.*?Text)|(^Dialogue:.*?,.*?,.*?,.*?,.*?,.*?,.*?,.*?,.*?,)|(\\N)|(\{\\kf.*?\})|(\{\\f.*?\})|(\\f.*?%)|(\{\\(3){0,1}c&H.*?&\})|(\\(3){0,1}c&H.*?&)|(\{\\a\d+\})|(\{\\.*?\})";

            int ass_s = result.IndexOf( "[Script Info]" );
            int ass_e = result.IndexOf( "[Events]" );
            if ( ass_s >= 0 && ass_e > ass_s )
                result = result.Remove( ass_s, ass_e - ass_s + 8 );

            //result = Regex.Replace( result, pat_ass_header, "", RegexOptions.IgnoreCase | RegexOptions.Multiline );
            //result = Regex.Replace( result, pat_ass_head, "", RegexOptions.IgnoreCase | RegexOptions.Multiline );
            result = Regex.Replace( result, pat_ass_diag, "", RegexOptions.IgnoreCase | RegexOptions.Multiline );

            return ( result.Trim() );
        }

        internal string filterLrc( string text )
        {
            string result = text;

            string pat_lyric = @"(\[id:.*?\])|(\[al:.*?\])|(\[ar:.*?\])|(\[ti:.*?\])|(\[by:.*?\])|(\[la:.*?\])|(\[lg:.*?\])|(\[offset:.*?\])|(\[\d+:\d+(\.\d+){0,1}\])";
            result = Regex.Replace( result, pat_lyric, "", RegexOptions.IgnoreCase | RegexOptions.Multiline );

            return ( result.Trim() );
        }

        internal string filterMisc( string text )
        {
            string result = text;

            string pat_misc = @"(&#\d+;)|([\u0000-\u0009,\u000B-\u000C,\u000E-\u001F,\u0021-\u0040,\u005B-\u0060,\u007B-\u00FF,\u2000-\u206F,\u2190-\u2426,\u3000-\u303F,\u31C0-\u31E3,\uFE10-\uFE4F])|([\.|·|　|…])";
            result = Regex.Replace( result, pat_misc, "", RegexOptions.IgnoreCase | RegexOptions.Multiline );

            if ( !_KeepNumber )
                result = Regex.Replace( result, @"\d+", "", RegexOptions.IgnoreCase | RegexOptions.Multiline );

            return ( result.Trim() );
        }

        internal List<CUE> loadLrc( string lrcfile )
        {
            List<CUE> cues = new List<CUE>();

            if ( File.Exists( lrcfile ) )
            {
                var lines = File.ReadAllLines(lrcfile);
                foreach ( string line in lines )
                {

                }
            }

            return ( cues );
        }

        public class CUE
        {
            public DateTime Start;
            public DateTime End;
            public TimeSpan Duration;
            public string Text;
        }

        #region Screen Snapshot routine
        // P/Invoke declarations
        [DllImport( "gdi32.dll" )]
        static extern bool BitBlt( IntPtr hdcDest,
            int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSource,
            int xSrc, int ySrc,
            CopyPixelOperation rop );
        [DllImport( "user32.dll" )]
        static extern bool ReleaseDC( IntPtr hWnd, IntPtr hDc );
        [DllImport( "gdi32.dll" )]
        static extern IntPtr DeleteDC( IntPtr hDc );
        [DllImport( "gdi32.dll" )]
        static extern IntPtr DeleteObject( IntPtr hDc );
        [DllImport( "gdi32.dll" )]
        static extern IntPtr CreateCompatibleBitmap( IntPtr hdc, int nWidth, int nHeight );
        [DllImport( "gdi32.dll" )]
        static extern IntPtr CreateCompatibleDC( IntPtr hdc );
        [DllImport( "gdi32.dll" )]
        static extern IntPtr SelectObject( IntPtr hdc, IntPtr bmp );
        [DllImport( "user32.dll" )]
        public static extern IntPtr GetDesktopWindow();
        [DllImport( "user32.dll" )]
        public static extern IntPtr GetWindowDC( IntPtr ptr );
        [DllImport( "user32.dll" )]
        public static extern bool SetProcessDPIAware();
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal Bitmap ScreenShot()
        {
            if ( Environment.OSVersion.Version.Major >= 6 ) SetProcessDPIAware();

            //using Screen.AllScreens to fetch all screens
            Size sz = Screen.PrimaryScreen.Bounds.Size;
            IntPtr hDesk = GetDesktopWindow();
            IntPtr hSrce = GetWindowDC(hDesk);
            IntPtr hDest = CreateCompatibleDC(hSrce);
            IntPtr hBmp = CreateCompatibleBitmap(hSrce, sz.Width, sz.Height);
            IntPtr hOldBmp = SelectObject(hDest, hBmp);
            bool b = BitBlt(hDest, 0, 0, sz.Width, sz.Height, hSrce, 0, 0, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
            Bitmap bmp = Bitmap.FromHbitmap(hBmp);
            SelectObject( hDest, hOldBmp );
            DeleteObject( hBmp );
            DeleteDC( hDest );
            ReleaseDC( hDesk, hSrce );
            //bmp.Save( @"c:\temp\test.png" );
            //bmp.Dispose();
            return ( bmp );
        }
        #endregion

        #region OCR with microsoft cognitive api
        internal static ImageCodecInfo GetEncoder( ImageFormat format )
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach ( ImageCodecInfo codec in codecs )
            {
                if ( codec.FormatID == format.Guid )
                {
                    return codec;
                }
            }
            return null;
        }

        internal string ocr_ms( Bitmap src, string lang = "unk", string apikey = "" )
        {
            string result = "";

            var uri = @"https://westus.api.cognitive.microsoft.com/vision/v1.0/ocr?language="+ lang +"&detectOrientation=true";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            //request.Timeout = 10000;
            request.UserAgent = @"Mozilla/5.0 (Windows NT 6.1; WOW64; rv:37.0) Gecko/20190101 Firefox/87.0";
            request.Referer = @"https://westus.api.cognitive.microsoft.com";
            request.ContentType = "application/octet-stream";
            request.Headers["Ocp-Apim-Subscription-Key"] = apikey;

            using ( Stream png = new MemoryStream() )
            {
                src.Save( png, ImageFormat.Png );
                byte[] buffer = ((MemoryStream)png).ToArray();
                string buf = "data:image/png; base64," + Convert.ToBase64String( buffer );
                request.ContentLength = buf.Length;
                Stream requestStream = request.GetRequestStream();
                requestStream.Write( Encoding.ASCII.GetBytes( buf ), 0, buf.Length );
                requestStream.Close();
                //using ( Stream requestStream = request.GetRequestStream() )
                //{
                //    //png.CopyTo( requestStream );
                //    requestStream.Write( Encoding.ASCII.GetBytes( buf ), 0, buf.Length );
                //    //requestStream.Flush();
                //}
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }

            return ( result );
        }
        #endregion

    }
}
