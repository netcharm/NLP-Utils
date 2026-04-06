using Sdcb.WordClouds;
using SkiaSharp;
using System.IO;
using System.Printing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WordCloud;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private byte[]? PngBytes { get; set; }

    public int CloudWidth { get;set; } = 1024;
    public int CloudHeight { get;set; } = 1024;
    public TextOrientations CloudOrientation { get;set; } = TextOrientations.PreferHorizontal;
    public string CloudFontFamily { get; set; } = "Consolas";

    #region Word Cloud Helper
    static IEnumerable<WordScore> MakeDemoScore()
    {
        string text = """
        459    cloud
        112    Retrieved
        88    form
        78    species
        74    meteorolog
        66    type
        62    ed.
        54    edit
        53    cumulus
        52    World
        50    into
        50    level
        49    Organization
        49    air
        48    International
        48    atmosphere
        48    troposphere
        45    Atlas
        45    genus
        44    cirrus
        43    convection
        42    vertical
        40    altitude
        40    stratocumulus
        38    high
        38    weather
        37    climate
        37    layer
        36    cumulonimbus
        35    appear
        35    variety
        34    more
        34    water
        33    altocumulus
        33    feature
        33    low
        31    formation
        31    other
        28    precipitation
        28    produce
        27    .mw-parser-output
        27    name
        27    surface
        27    very
        26    base
        25    April
        25    ISBN
        25    also
        25    origin
        24    cirrocumulus
        24    color
        24    cool
        24    stratiformis
        24    stratus
        23    Earth
        23    structure
        22    altostratus
        22    cumuliform
        22    doi
        22    genera
        22    physics
        22    result
        22    see
        22    stratiform
        22    usual
        21    general
        21    p.
        21    seen
        21    temperature
        21    than
        20    polar
        20    space
        20    top
        19    Bibcode
        19    National
        19    cirrostratus
        19    fog
        19    lift
        19    mostly
        19    over
        18    Archived
        18    Latin
        18    Universe
        18    group
        18    sometimes
        18    supplementary
        18    tower
        18    warm
        17    January
        17    change
        17    light
        17    main
        17    nimbostratus
        17    occur
        17    only
        17    stratocumuliform
        17    unstable
        17    wind
        16    associated
        16    cause
        16    global
        16    instability
        16    mid-level
        16    nasa
        16    new
        16    noctilucent
        16    range
        16    science
        16    show
        16    tend
        15    accessory
        15    any
        15    cirriform
        15    droplets
        15    during
        15    extent
        15    multi-level
        15    near
        15    one
        15    rain
        15    reflect
        15    stratosphere
        15    used
        14    J.
        14    October
        14    PDF
        14    airmass
        14    become
        14    common
        14    converge
        14    crystal
        14    cyclone
        14    front
        14    high-level
        14    observes
        14    stable
        14    system
        14    white
        13    all
        13    along
        13    because
        13    classify
        13    each
        13    fibratus
        13    higher
        13    identify
        13    increase
        13    large
        13    mesosphere
        13    sun
        12    castellanus
        12    classification
        12    compose
        12    condenses
        12    congestus
        12    effect
        12    fractus
        12    heavy
        12    include
        12    low-level
        12    moderate
        12    process
        12    radiation
        12    resemble
        12    solar
        12    term
        12    vapor
        12    zone
        11    August
        11    Ci
        11    February
        11    July
        11    M.
        11    March
        11    November
        11    b.
        11    conditions
        11    different
        11    ground
        11    known
        11    lead
        11    most
        11    occasional
        11    often
        11    perlucidus
        11    pressure
        11    research
        11    satellite
        11    saturated
        11    severe
        11    storm
        11    thunderstorm
        11    tornado
        11    visible
        """;

        return (MakeScore(text));
    }

    private static string PreprocessText(string text)
    {
        var result = string.Empty;
        var words = text + "\n";
#pragma warning disable IDE0079 // 请删除不必要的忽略
#pragma warning disable SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
        var results = new List<string>();
        var lines = Regex.Split(words, @"(\n\r|\r\n|\n|\r)", RegexOptions.IgnoreCase).Where(l => !string.IsNullOrEmpty(l.Trim()));
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^[\s\t]*?(\d{1,6})[\t\s]+(.+?)$", RegexOptions.IgnoreCase) && !Regex.IsMatch(line, @"^[\s\t]*?(\d{1,6})[\t\s]+(\d+)$", RegexOptions.IgnoreCase))
            {
                results.Add(Regex.Replace(line + "\n", @"(\d+)[\t\s]+(.+?)$", "$1\t$2\n", RegexOptions.IgnoreCase).Trim());
            }
            else if (Regex.IsMatch(line, @"^[\s\t]*?(.+?)[\t\s]+(\d{1,6})$", RegexOptions.IgnoreCase) && !Regex.IsMatch(line, @"^[\s\t]*?(\d+)[\t\s]+(\d{1,6})$", RegexOptions.IgnoreCase))
            {
                results.Add(Regex.Replace(line + "\n", @"(.+?)[\t\s]+(\d+)$", "$2\t$1\n", RegexOptions.IgnoreCase).Trim());
            }
        }
        result = string.Join("\n", results);
#pragma warning restore SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
        return (result.Trim());
#pragma warning restore IDE0079 // 请删除不必要的忽略
    }

    private static IEnumerable<WordScore> MakeScore(string? text = null)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text.Trim()))
        {
            return (MakeDemoScore());
        }
        else
        {
            var words = PreprocessText(text);
            var scores = words
                    .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.Trim().Split("\t")).Where(x => x.Length >= 2)
                    .Select(x => new WordScore(Score: int.Parse(x[0]), Word: x[1]))
                    .Where(x => !string.IsNullOrEmpty(x.Word.Trim()));
            return (scores);
        }
    }

    private void MakeWordsCloud()
    {
        try
        {
            Sdcb.WordClouds.WordCloud wc = Sdcb.WordClouds.WordCloud.Create(new WordCloudOptions(Math.Max(128, CloudWidth), Math.Max(128, CloudHeight), MakeScore(WordsTextBox.Text))
            {
                TextOrientation = CloudOrientation,
                FontManager = new FontManager([SKTypeface.FromFamilyName(CloudFontFamily)])
            });
            PngBytes = wc.ToSKBitmap().Encode(SKEncodedImageFormat.Png, 100).AsSpan().ToArray();
            WordCloudImage.Source = BitmapFrame.Create(new MemoryStream(PngBytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
    #endregion

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        #region allow drag and drop text
        WordsTextBox.AllowDrop = true;
        WordsTextBox.DragOver += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.OemText))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        };
        WordsTextBox.Drop += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.OemText))
            {
                try 
                { 
                    var text = (e.Data.GetData(DataFormats.Text) ?? e.Data.GetData(DataFormats.UnicodeText) ?? e.Data.GetData(DataFormats.OemText) ?? string.Empty) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        WordsTextBox.Text = text;
                    }
                    e.Handled = true;
                }
                catch { }
            }
        };
        #endregion

        #region bind cloud options to UI
        CloudFontValue.ItemsSource = Fonts.SystemFontFamilies;
        CloudFontValue.Text = CloudFontFamily;
        CloudFontValue.SelectedIndex = CloudFontValue.Items.IndexOf(CloudFontFamily);

        CloudOrientationValue.ItemsSource = Enum.GetValues(typeof(TextOrientations)).Cast<TextOrientations>();
        CloudOrientationValue.Text = CloudOrientationValue.Items.OfType<TextOrientations>().FirstOrDefault(o => o == CloudOrientation).ToString();
        CloudOrientationValue.SelectedIndex = CloudOrientationValue.Items.IndexOf(CloudOrientation);

        CloudWidthValue.SetBinding(TextBox.TextProperty, new Binding(nameof(CloudWidth)) { Source = this, Mode = BindingMode.TwoWay });
        CloudHeightValue.SetBinding(TextBox.TextProperty, new Binding(nameof(CloudHeight)) { Source = this, Mode = BindingMode.TwoWay });
        CloudFontValue.SetBinding(ComboBox.TextProperty, new Binding(nameof(CloudFontFamily)) { Source = this, Mode = BindingMode.TwoWay });
        CloudOrientationValue.SetBinding(ComboBox.TextProperty, new Binding(nameof(CloudOrientation)) { Source = this, Mode = BindingMode.TwoWay });
        #endregion

        WordsTextBox.Focusable = true;
        WordsTextBox.Focus();

        //MakeWordsCloud();
    }

    private void BuildWordsCloud_Click(object sender, RoutedEventArgs e)
    {
        MakeWordsCloud();
    }

    private void SaveWordsCloud_Click(object sender, RoutedEventArgs e)
    {
        if (PngBytes is not null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog()
            {
                AddToRecent = false,
                AddExtension = true,
                OverwritePrompt = true,
                CheckFileExists = false,
                Filter = "PNG Image|*.png",
                DefaultExt = "png",
                FileName = $"wordcloud-{DateTime.Now:yyyyMMddhhmmss}.png"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllBytes(dlg.FileName, PngBytes);
            }
        }
    }
}