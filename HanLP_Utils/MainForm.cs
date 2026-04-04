using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using com.hankcs.hanlp;
using com.hankcs.hanlp.dictionary;
using com.hankcs.hanlp.dictionary.py;
using com.hankcs.hanlp.dictionary.stopword;
using com.hankcs.hanlp.seg;
using com.hankcs.hanlp.seg.common;
using com.hankcs.hanlp.seg.CRF;
using com.hankcs.hanlp.seg.Dijkstra;
using com.hankcs.hanlp.seg.NShort;
using com.hankcs.hanlp.tokenizer;
using HtmlAgilityPack;

namespace HanLP_Utils
{
    public partial class MainForm : Form
    {
        private string AppPath = Path.GetDirectoryName(Application.ExecutablePath);
        private string CWD = Directory.GetCurrentDirectory();

        private string ROOT = Path.GetDirectoryName(Application.ExecutablePath);
        private string CoreDictionaryPath = $"data/dictionary/CoreNatureDictionary.txt";
        private string BiGramDictionaryPath = $"data/dictionary/CoreNatureDictionary.ngram.txt";
        private string CoreStopWordDictionaryPath = $"data/dictionary/stopwords.txt";
        private string CoreSynonymDictionaryDictionaryPath = $"data/dictionary/synonym/CoreSynonym.txt";
        private string PersonDictionaryPath = $"data/dictionary/person/nr.txt";
        private string PersonDictionaryTrPath = $"data/dictionary/person/nr.tr.txt";
        private string TraditionalChineseDictionaryPath = $"data/dictionary/tc/TraditionalChinese.txt";
        private string CustomDictionaryPath = $"data/dictionary/custom/CustomDictionary.txt; 现代汉语补充词库.txt; 全国地名大全.txt ns; 人名词典.txt; 机构名词典.txt; 上海地名.txt ns; netcharm.txt nrf; 战舰少女N.txt nrf; data/dictionary/person/nrf.txt nrf";
        private string CRFSegmentModelPath = $"data/model/segment/CRFSegmentModel.txt";
        private string HMMSegmentModelPath = $"data/model/segment/HMMSegmentModel.bin";
        private bool ShowTermNature = true;

        private List<string> CustomDictionaryList = new List<string>();
        private List<string> CustomStopList = new List<string>();

        private HanLP_Result HR = new HanLP_Result();

        private Encoding ASCII = Encoding.ASCII;
        private Encoding CJK = Encoding.GetEncoding("GBK");
        private Font DefaultOutputFont { get; set; } = null;
        private double DefaultFontSize { get; set; } = 9;

        public MainForm()
        {
            InitializeComponent();
        }

        private void LoadConfig()
        {
            #region Load Application config
            Configuration appCfg = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            AppSettingsSection appSection = appCfg.AppSettings;
            try
            {
                if (appSection.Settings.AllKeys.Contains("Font"))
                {
                    var font_str = appSection.Settings["Font"].Value;
                    var cvt = new FontConverter();
                    DefaultOutputFont = cvt.ConvertFromInvariantString(font_str) as Font;
                    DefaultOutputFont = new Font(DefaultOutputFont.FontFamily, DefaultOutputFont.Size, DefaultOutputFont.Style, GraphicsUnit.Pixel);
                    DefaultFontSize = DefaultOutputFont.Size;
                    edSrc.Font = DefaultOutputFont;
                    edDst.Font = DefaultOutputFont;
                }
                else
                {
                    var cvt = new FontConverter();
                    DefaultOutputFont = new Font(DefaultOutputFont.FontFamily, DefaultOutputFont.Size, DefaultOutputFont.Style, GraphicsUnit.Pixel);
                    appSection.Settings.Add("Font", cvt.ConvertToInvariantString(DefaultOutputFont));
                    appCfg.Save();
                }
            }
            catch { }
            #endregion

            #region Load "hanlp.properties"
            var config = Path.Combine(AppPath, "hanlp.properties");
            if (File.Exists(config))
            {
                var lines = File.ReadAllLines(config);
                foreach (string line in lines)
                {
                    if (line.StartsWith("#")) continue;
                    var kv = line.Split('=');
                    if (kv.Length == 2)
                    {
                        var k = kv[0].Trim();
                        var v = kv[1].Trim();

                        if (k.Equals("root", StringComparison.CurrentCultureIgnoreCase))
                        {
                            ROOT = v;
                        }
                        else if (k.Equals("CoreDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            CoreDictionaryPath = v;
                        }
                        else if (k.Equals("BiGramDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            BiGramDictionaryPath = v;
                        }
                        else if (k.Equals("CoreStopWordDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            CoreStopWordDictionaryPath = v;
                        }
                        else if (k.Equals("CoreSynonymDictionaryDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            CoreSynonymDictionaryDictionaryPath = v;
                        }
                        else if (k.Equals("PersonDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            PersonDictionaryPath = v;
                        }
                        else if (k.Equals("PersonDictionaryTrPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            PersonDictionaryTrPath = v;
                        }
                        else if (k.Equals("TraditionalChineseDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            TraditionalChineseDictionaryPath = v;
                        }
                        else if (k.Equals("CustomDictionaryPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            CustomDictionaryPath = v;
                        }
                        else if (k.Equals("CRFSegmentModelPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            CRFSegmentModelPath = v;
                        }
                        else if (k.Equals("HMMSegmentModelPath", StringComparison.CurrentCultureIgnoreCase))
                        {
                            HMMSegmentModelPath = v;
                        }
                        else if (k.Equals("ShowTermNature", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (bool.TryParse(v, out ShowTermNature))
                            {
                                //HanLP.Config.ShowTermNature = ShowTermNature;
                            }
                        }
                    }
                }
            }
            java.lang.System.getProperties().setProperty("java.class.path", $"{ROOT};.");
            HanLP.Config.ShowTermNature = ShowTermNature;
            chkTermNature.Checked = HanLP.Config.ShowTermNature;
            #endregion

            #region Load custom dictionary list
            var custom_dicts_file = Path.Combine(ROOT, "custom_dicts.txt");
            if (File.Exists(custom_dicts_file))
            {
                var custom_dicts = File.ReadAllLines(custom_dicts_file);
                foreach (var custom_dict in custom_dicts)
                {
                    var dict = custom_dict.Trim();
                    if (dict.StartsWith("#") || string.IsNullOrEmpty(dict)) continue;
                    var fullpath = Path.IsPathRooted(dict) ? dict : Path.GetFullPath(Path.Combine(ROOT, dict));
                    CustomDictionaryList.Add(fullpath);
                }
            }
            #endregion
        }

        private void AddCustomDict()
        {
            var sw = Stopwatch.StartNew();

            List<string> filelist = new List<string>();
            filelist.AddRange(CustomDictionary.path);
            var CustomDict = CustomDictionaryPath.Split(';');
            if (CustomDict.Length > filelist.Count)
            {
                filelist.Clear();
                string lastfolder = "";
                for (int i = 0; i < CustomDict.Length; i++)
                {
                    if (string.IsNullOrEmpty(Path.GetDirectoryName(CustomDict[i].Trim())))
                    {
                        var path = Path.Combine(ROOT, lastfolder, CustomDict[i].Trim()).Replace("\\", "/");
                        if (!filelist.Contains(path)) filelist.Add(path);
                    }
                    else
                    {
                        var path = Path.Combine(ROOT, CustomDict[i].Trim()).Replace("\\", "/");
                        if (!filelist.Contains(path)) filelist.Add(path);
                        lastfolder = Path.GetDirectoryName(CustomDict[i].Trim());
                    }
                }
            }

            List<string> userdict = new List<string>() {
                    Path.Combine( ROOT, "data", "dictionary", "userdict.txt" ).Replace("\\", "/"),
                    Path.Combine( AppPath, "userdict.txt" ).Replace("\\", "/"),
                }.ToList();
            if (!CWD.Equals(AppPath, StringComparison.CurrentCultureIgnoreCase))
                userdict.Add(Path.Combine(CWD, "userdict.txt").Replace("\\", "/"));

            foreach (var f in userdict)
            {
                if (File.Exists(f)) { filelist.Add(f); }
            }

            filelist.AddRange(CustomDictionaryList);

            var ROOT_EXISTS = Directory.Exists(ROOT);
            StringBuilder sb = new StringBuilder();
            List<string> ss = new List<string>();
            foreach (string file in filelist)
            {
                try
                {
                    var fn = Path.Combine(Path.GetDirectoryName(file), $"{Path.GetFileNameWithoutExtension(file)}.txt");
                    if (!ROOT_EXISTS) fn = fn.Replace(Path.GetFullPath(ROOT), AppPath + Path.DirectorySeparatorChar);
                    var nt = Path.GetExtension(file).Split();
                    if (File.Exists(fn))
                    {
                        var lines = File.ReadAllLines(fn);
                        if (nt.Length > 1)
                        {
                            var nu = string.Join(" ", nt.Skip(1));
                            if (nu.Equals("w", StringComparison.CurrentCultureIgnoreCase) || nu.Equals("sym", StringComparison.CurrentCultureIgnoreCase))
                            {
                                foreach (var sym in lines)
                                {
                                    if (sym.Trim().Length > 0 &&
                                        !CustomDictionary.contains(sym.Trim()) &&
                                        !CoreStopWordDictionary.contains(sym))
                                        CoreStopWordDictionary.add(sym);
                                }
                            }
                            else ss.AddRange(lines.Where(s => !CoreStopWordDictionary.contains(s)).Select(s => $"{s} {nu} 1").ToList());
                        }
                        else ss.AddRange(lines);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }

            var err_count = 0;
            foreach (var w in ss)
            {
                try
                {
                    if (string.IsNullOrEmpty(w)) continue;
                    var ws = w.Split();
                    var uw = ws[0].Trim();
                    if (ws.Length == 1)
                        CustomDictionary.add(uw);
                    else if (ws.Length >= 2)
                    {
                        var nf = string.Join(" ", ws.Skip(1));
                        CustomDictionary.add(uw, nf.Trim());
                    }
                    if (CoreStopWordDictionary.contains(uw))
                    {
                        CoreStopWordDictionary.remove(uw);
                    }
                }
                catch (Exception ex)
                {
                    if (err_count < 3)
                    {
                        MessageBox.Show(ex.ToString());
                        err_count++;
                    }
                }
            }

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void AddStopWords()
        {
            var err_count = 0;
            try
            {
                List<string> stopwords = new List<string>();
                stopwords.AddRange(new string[] { "。", "、", "，", "　", "　　", "□", "□□", "一", "一一" });

                List<string> stopfile = new List<string>() {
                    Path.Combine(ROOT, "data", "dictionary", "stopwords.txt").Replace("\\", "/"),
                    Path.Combine(AppPath, "stopwords.txt").Replace("\\", "/"),
                };
                if (!CWD.Equals(AppPath, StringComparison.CurrentCultureIgnoreCase))
                    stopfile.Add(Path.Combine(CWD, "stopwords.txt"));

                foreach (var file in stopfile)
                {
                    if (File.Exists(file))
                    {
                        var lines = File.ReadAllLines(file);
                        stopwords.AddRange(lines.Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w)));
                    }
                }

                foreach (var w in stopwords)
                {
                    if (CustomDictionary.contains(w)) CustomDictionary.remove(w);
                    if (!CoreStopWordDictionary.contains(w)) CoreStopWordDictionary.add(w);
                }
            }
            catch (Exception ex)
            {
                if (err_count < 3)
                {
                    MessageBox.Show(ex.ToString());
                    err_count++;
                }
            }
        }

        private string ReadUrl(string url)
        {
            string result = string.Empty;

            //HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(URL);
            //myRequest.Method = "GET";
            //WebResponse myResponse = myRequest.GetResponse();
            //StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
            //result = sr.ReadToEnd();
            //sr.Close();
            //myResponse.Close();

            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);
            var scripts = doc.DocumentNode.SelectNodes( "//script" );
            var styles = doc.DocumentNode.SelectNodes( "//style" );
            var links = doc.DocumentNode.SelectNodes( "//a" );
            var comments = doc.DocumentNode.SelectNodes( "//comment()" );
            foreach (var node in scripts) { node.Remove(); }
            foreach (var node in styles) { node.Remove(); }
            foreach (var node in links) { node.Remove(); }
            foreach (var node in comments) { node.Remove(); }

            result = doc.DocumentNode.SelectSingleNode("//body").InnerText.Trim();

            result = Regex.Replace(result, @"[ |(\r\n)|(\t)]{2,}", ", ", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"[(&gt;)|(&lt;)|(&amp;)]{1,}", " ", RegexOptions.IgnoreCase);

            return (result);
        }

        private string[] GetLinks(string html)
        {
            List<string> links = new List<string>();

            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument(); //web.Load(html);
            doc.LoadHtml(html);
            var alist = doc.DocumentNode.SelectNodes( "//a" );
            foreach (var a in alist)
            {
                string href = a.GetAttributeValue( "href", "" );
                links.Add(href);
            }

            return (links.ToArray());
        }

        private Segment GetSegment()
        {
            #region Create Segment
            Segment segment = HanLP.newSegment();

            if (cmiCutMethodDefault.Checked)
            {
            }
            else if (cmiCutMethodStandard.Checked)
            {
                segment = StandardTokenizer.SEGMENT;
            }
            else if (cmiCutMethodNLP.Checked)
            {
                segment = NLPTokenizer.SEGMENT;
            }
            else if (cmiCutMethodIndex.Checked)
            {
                segment = IndexTokenizer.SEGMENT;
            }
            else if (cmiCutMethodNShort.Checked)
            {
                segment = new NShortSegment();
            }
            else if (cmiCutMethodShortest.Checked)
            {
                segment = new DijkstraSegment();
            }
            else if (cmiCutMethodCRF.Checked)
            {
                segment = new CRFSegment();
            }
            else if (cmiCutMethodHighSpeed.Checked)
            {
                segment = SpeedTokenizer.SEGMENT;
            }

            segment.enableCustomDictionary(true);
            segment.enableMultithreading(true);
            segment.enablePartOfSpeechTagging(true);
            segment.enableNameRecognize(cmiCutRecognizeChineseName.Checked);
            segment.enableTranslatedNameRecognize(cmiCutRecognizeTranslatedName.Checked);
            segment.enableJapaneseNameRecognize(cmiCutRecognizeJapaneseName.Checked);
            segment.enablePlaceRecognize(cmiCutRecognizePlace.Checked);
            segment.enableOrganizationRecognize(cmiCutRecognizeOrganization.Checked);
            #endregion

            return (segment);
        }

        private int HalfWidthLength(string text)
        {
            return (string.IsNullOrEmpty(text) ? 0 : CJK.GetByteCount(text));

            //var width = string.IsNullOrEmpty(text) ? 0 : text.Length;
            ////var minValue = UnicodeRanges.CjkUnifiedIdeographs.FirstCodePoint;
            ////var maxValue = minValue + UnicodeRanges.CjkUnifiedIdeographs.Length;
            ////var cjkCharRegex = new Regex(@"\p{IsCJKUnifiedIdeographs}");
            //var wd = 0;
            //foreach (var c in text)
            //{
            //    //var wd0 = wd + (cjkCharRegex.IsMatch($"{c}") ? 2 : 1);
            //    wd = wd + (0x4E00 <= c && c <= 0x2FA1F ? 2 : 1);
            //}
            //return (wd > width ? wd : width);
        }

        private int FullWidthCount(string text)
        {
            return (string.IsNullOrEmpty(text) ? 0 : CJK.GetByteCount(text) - text.Length);
        }

        private void FontSizeChange(object sender, int action, int max = 48, int min = 9)
        {
            if (sender is TextBox)
            {
                var edBox = sender as TextBox;
                var font_old = edBox.Font;
                double font_size = font_old.Size; //font_old.SizeInPoints;
                if (action < 0)
                {
                    font_size -= 1;
                    font_size = font_size < min ? min : font_size;
                }
                else if (action == 0)
                {
                    font_size = DefaultFontSize;
                }
                else if (action > 0)
                {
                    font_size += 1;
                    font_size = font_size > max ? max : font_size;
                }
                if (font_size != font_old.Size)
                {
                    var font_new = new Font(font_old.FontFamily, (float)font_size, font_old.Style, GraphicsUnit.Pixel);
                    edBox.Font = font_new;
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            try
            {
                if (DefaultOutputFont == null) DefaultOutputFont = DefaultFont;
                DefaultFontSize = DefaultFont.Size;

                LoadConfig();
                AddStopWords();
                AddCustomDict();

                edSrc.MouseWheel += edBox_MouseWheel;
                edSrc.MouseMove += edBox_MouseMove;
                edSrc.KeyDown += edBox_KeyDown;
                edSrc.KeyUp += edBox_KeyUp;

                edDst.MouseWheel += edBox_MouseWheel;
                edDst.MouseMove += edBox_MouseMove;
                edDst.KeyDown += edBox_KeyDown;
                edDst.KeyUp += edBox_KeyUp;

                btnOCR.Visible = false;
                btnOCR.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MainForm_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dragFiles = (string [])e.Data.GetData(DataFormats.FileDrop, true);
                if (dragFiles.Length > 0)
                {
                    e.Effect = DragDropEffects.Copy;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effect = DragDropEffects.Copy;
            }
            return;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            // Determine whether string data exists in the drop data. If not, then 
            // the drop effect reflects that the drop cannot occur. 
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //e.Effect = DragDropEffects.Copy;
                try
                {
                    string[] dragFiles = (string [])e.Data.GetData(DataFormats.FileDrop, true);
                    if (dragFiles.Length > 0)
                    {
                        string dragFileName = dragFiles[0].ToString();
                        string ext = Path.GetExtension(dragFileName).ToLower();
                        string[] text = { ".txt", ".text"};
                        string[] html = { ".htm", ".html", ".xml"};

                        if (dragFileName.EndsWith(".url", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var content = File.ReadAllLines( dragFileName );
                            foreach (var line in content)
                            {

                            }
                        }
                        else if (text.Contains(ext))
                        {
                            edSrc.Text = File.ReadAllText(dragFileName);
                        }
                        else if (html.Contains(ext))
                        {
                            edSrc.Text = ReadUrl(dragFileName);
                        }
                    }
                }
                catch
                {

                }
            }
            //else if ( e.Data.GetDataPresent( DataFormats.Html ) )
            //{
            //    var content = e.Data.GetData( DataFormats.Html, true ).ToString();
            //    edSrc.Text = string.Join("\n", GetLinks( content ));
            //}
            else if (e.Data.GetDataPresent(DataFormats.Text) ||
                      e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                var content = e.Data.GetData( "System.String", true ).ToString();
                if (content.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase) ||
                     content.StartsWith("https://", StringComparison.CurrentCultureIgnoreCase) ||
                     content.StartsWith("ftp://", StringComparison.CurrentCultureIgnoreCase) ||
                     content.StartsWith("file://", StringComparison.CurrentCultureIgnoreCase))
                {
                    edSrc.Text = ReadUrl(content);
                }
                else
                {
                    edSrc.Text = content;
                }
            }
            return;
        }

        private void cmiCutMethod_Click(object sender, EventArgs e)
        {
            foreach (var cmi in cmActions.Items)
            {
                if (cmi.GetType() == typeof(ToolStripMenuItem))
                {
                    var mi = cmi as ToolStripMenuItem;
                    if (mi.CheckOnClick && mi != sender && mi.Name.StartsWith("cmiCutMethod", StringComparison.CurrentCultureIgnoreCase))
                    {
                        mi.Checked = !(sender as ToolStripMenuItem).Checked;
                    }
                }
            }
        }

        private void cmiFilterText_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var txt = edSrc.Text;
                var mi = sender as ToolStripMenuItem;
                if (mi.Name == cmiActionFilterSub.Name)
                {
                    txt = filterASS(txt);
                }
                else if (mi.Name == cmiActionFilterLrc.Name)
                {
                    txt = filterLrc(txt);
                }
                else if (mi.Name == cmiActionFilterMlTag.Name)
                {
                    txt = filterHtmlTag(txt);
                }
                edSrc.Text = filterMisc(txt);
            }
            catch { }

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void cmiCustomDictReload_Click(object sender, EventArgs e)
        {
            try
            {
                AddStopWords();
                AddCustomDict();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void chkTermNature_CheckedChanged(object sender, EventArgs e)
        {
            HanLP.Config.ShowTermNature = chkTermNature.Checked;
        }

        private void edBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox)
            {
                var delta = 0;
                if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
                {
                    delta = -1;
                }
                else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
                {
                    delta = +1;
                }
                else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
                {
                    delta = 0;
                }

                if (e.KeyCode != Keys.ControlKey)
                {
                    new Action(() => { FontSizeChange(sender, delta); }).Invoke();
                }
            }
        }

        private void edBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (sender is TextBox)
            {
                var edBox = sender as TextBox;
                if (e.Control && e.KeyCode == Keys.A)
                {
                    edBox.SelectAll();
                }
            }
        }

        private void edBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is TextBox)
            {
                var edBox = sender as TextBox;
                if (ModifierKeys == Keys.Control && e.Button == MouseButtons.Left && edBox.SelectionLength > 0)
                {
                    edBox.DoDragDrop(edBox.SelectedText, DragDropEffects.Copy);
                }
            }
        }

        private void edBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                new Action(() => { FontSizeChange(sender, e.Delta); }).Invoke();
            }
        }

        private void btnSegment_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            Segment segment = GetSegment();

            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                var t = line.Trim().Replace("　", " ").Replace("□", " ");
                var result = segment.seg(t);
                foreach (Term r in result.toArray())
                {
                    if (CoreStopWordDictionary.contains(r.word)) result.remove(r);
                }
                var text = result.toArray();
                if (text.Length <= 0)
                    sb.AppendLine();
                else
                    sb.AppendLine(string.Join(", ", text).Trim());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnTokenizer_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            Segment segment = GetSegment();
            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                var result = segment.seg(line.Trim().Replace("　", " ").Replace("□", " ") );
                foreach (Term r in result.toArray())
                {
                    if (CoreStopWordDictionary.contains(r.word)) result.remove(r);
                }
                var text = result.toArray();
                if (text.Length <= 0) continue;
                sb.AppendLine(string.Join(", ", text).Trim());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnKeyword_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            var text = HanLP.extractKeyword( edSrc.Text, 25 ).toArray();
            if (text.Length <= 0) return;
            edDst.Text = string.Join(", ", text);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnSummary_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            var text = HanLP.extractSummary( edSrc.Text, 15 ).toArray();
            if (text.Length <= 0) return;
            var ro = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            edDst.Text = Regex.Replace(string.Join(", ", text), @"[　| ]{2,}", " ", ro);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnPhrase_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                var text = HanLP.extractPhrase( line.Trim().Replace("　", " ").Replace("□", " "), 10 ).toArray();
                if (text.Length <= 0) continue;
                sb.AppendLine(string.Join(", ", text).Trim());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnWordFreq_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Segment segment = GetSegment();
                Dictionary<string, int> freq = new Dictionary<string, int>( );
                foreach (string line in edSrc.Lines)
                {
                    var text = segment.seg( line.Trim().Replace("　", " ").Replace("□", " ") ).toArray();
                    if (text.Length <= 0) continue;
                    foreach (Term t in text)
                    {
                        var word = t.ToString().Trim();
                        var wt = t.word.Trim();
                        if (string.IsNullOrEmpty(wt) || wt.Length <= 1) continue;
                        if (CoreStopWordDictionary.contains(wt)) continue;

                        if (freq.ContainsKey(word))
                            freq[word]++;
                        else
                            freq.Add(word, 1);
                    }
                }

                var sortedword = freq.ToList();
                sortedword.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));

                var width_k = sortedword.Max(w => HalfWidthLength(w.Key)) + 1;
                var width_v = sortedword.Max(w => w.Value).ToString().Length + 1;
                var sb = sortedword.Select(w => {
                    var k = w.Key.PadRight(width_k - FullWidthCount(w.Key), ' ');
                    var v = w.Value.ToString().PadLeft(width_v, ' ');
                    return($"{k}{v}".Replace("/", " "));
                }).ToList();
                edDst.Text = string.Join(Environment.NewLine, sb);

                //StringBuilder sb = new StringBuilder();
                //foreach (var w in sortedword)
                //{
                //    var k = w.Key.PadRight(width_k - FullWidthCount(w.Key), ' ');
                //    var v = w.Value.ToString().PadLeft(width_v, ' ');
                //    sb.AppendLine($"{k}{v}".Replace("/", " "));
                //}
                //edDst.Text = string.Join("\n", sb);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnSC2TC_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                sb.AppendLine(HanLP.convertToTraditionalChinese(line).ToString());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnTC2SC_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                sb.AppendLine(HanLP.convertToSimplifiedChinese(line).ToString());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnSrc2Py_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();

            var mode = Convert.ToInt32( ( sender as Button ).Tag );
            StringBuilder sb = new StringBuilder();
            foreach (string line in edSrc.Lines)
            {
                List<string> text = new List<string>();
                string lt = line.Trim().Replace( "　", " " ).Replace( "□", " ");
                int idx = -1;
                foreach (Pinyin py in HanLP.convertToPinyinList(lt).toArray())
                {
                    idx++;
                    if (py.getPinyinWithoutTone().ToString().Equals("none", StringComparison.CurrentCultureIgnoreCase))
                        text.Add(lt[idx].ToString());
                    else if (mode == 0)
                        text.Add(py.getPinyinWithoutTone().ToString());
                    else if (mode == 1)
                        text.Add(py.ToString());
                    else if (mode == 2)
                        text.Add(py.getPinyinWithToneMark().ToString());
                }
                if (text.Count <= 0) { sb.AppendLine(); continue; }
                var conn = ", ";
                if (!cmiPySeprateCommas.Checked) conn = " ";
                //if( cmiPyShowPunctuation.Checked )
                sb.AppendLine(string.Join(conn, text).Trim());
            }
            edDst.Text = string.Join("\n", sb);

            sw.Stop();
            lblInfo.Text = $"{sw.Elapsed}s";
        }

        private void btnOCR_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                try
                {
                    Bitmap src = (Bitmap)Clipboard.GetImage();
                    ocr_ms(src);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }
    }
}
